using Dav.AspNetCore.Server.Http;
using Dav.AspNetCore.Server.Store;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Dav.AspNetCore.Server;

internal class WebDavMiddleware
{
    private readonly WebDavOptions webDavOptions;
    private readonly ILogger<WebDavMiddleware> logger;

    // The default name intentionally omits the assembly version so the response cannot be used
    // to fingerprint the exact build (CWE-200). Set DisableServerName = true to remove the header.
    private static readonly string DefaultServerName = "Dav.AspNetCore.Server";

    /// <summary>
    /// Initializes a new <see cref="WebDavMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next request delegate.</param>
    /// <param name="webDavOptions">The web dav options.</param>
    /// <param name="logger">The logger.</param>
    public WebDavMiddleware(
        RequestDelegate next,
        WebDavOptions webDavOptions,
        ILogger<WebDavMiddleware> logger)
    {
        this.webDavOptions = webDavOptions;
        this.logger = logger;

        // Announce the effective security-relevant limits once at startup so operators are aware
        // of the defaults (uploads are capped at 500 MB and XML bodies at 1 MB unless changed).
        // The authentication state is validated and logged by WebDavStartupValidation.
        logger.LogInformation(
            "WebDAV configured: MaxResourceSizeBytes={MaxResourceSize}, MaxXmlRequestBodyBytes={MaxXmlRequestBodyBytes}, RequiresAuthentication={RequiresAuthentication}.",
            webDavOptions.MaxResourceSizeBytes is long limit ? $"{limit} bytes" : "unlimited",
            webDavOptions.MaxXmlRequestBodyBytes,
            webDavOptions.RequiresAuthentication);

        if (webDavOptions.MaxResourceSizeBytes is null)
            logger.LogWarning(
                "WebDAV upload size limit is disabled (WebDavOptions.MaxResourceSizeBytes = null); uploads are unbounded.");
    }

    /// <summary>
    /// Invokes the middleware async.
    /// </summary>
    /// <param name="context">The http context.</param>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task InvokeAsync(HttpContext context)
    {
        var middlewareStart = DateTime.UtcNow;

        // The method and path are attacker-controlled: strip control characters so they cannot be
        // used to forge log lines (CWE-117).
        var method = SanitizeForLog(context.Request.Method);
        var path = SanitizeForLog(context.Request.Path.Value);

        // Uploaded content is served with its own content type. Forbid MIME sniffing and sandbox the
        // response so a malicious HTML/XML upload cannot script the application origin (CWE-79).
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (!string.IsNullOrEmpty(webDavOptions.ContentSecurityPolicy))
            context.Response.Headers["Content-Security-Policy"] = webDavOptions.ContentSecurityPolicy;

        if (webDavOptions.RequiresAuthentication &&
            context.Request.Method != WebDavMethods.Options &&
            context.User.Identity?.IsAuthenticated != true)
        {
            await context.ChallengeAsync();
            return;
        }

        var resourceStore = context.RequestServices.GetService<IStore>();
        if (resourceStore == null)
            throw new InvalidOperationException("MapWebDav was used but it was never added. Use AddWebDav during service configuration.");
        
        if (!webDavOptions.DisableServerName)
            context.Response.Headers["Server"] = string.IsNullOrWhiteSpace(webDavOptions.ServerName)
                ? DefaultServerName
                : webDavOptions.ServerName;
        
        if (!RequestHandlerFactory.TryGetRequestHandler(context.Request.Method, out var handler))
        {
            logger.LogInformation("Request {Method} is not implemented.", method);
            context.Response.StatusCode = StatusCodes.Status501NotImplemented;
            return;
        }
        
        logger.LogInformation("Request starting {Method} {Path}", method, path);

        try
        {
            await handler.HandleRequestAsync(context, resourceStore, context.RequestAborted).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            // The store rejected the path because it resolves outside of the configured root.
            logger.LogWarning("Forbidden request {Method} {Path}", method, path);
            if (!context.Response.HasStarted)
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unexpected error while handling request {Method} {Path} {ElapsedMs}ms", method, path, (DateTime.UtcNow - middlewareStart).TotalMilliseconds);
            
            if (!context.Response.HasStarted)
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        }
        
        logger.LogInformation("Request finished {Method} {Path} {StatusCode} {ElapsedMs}ms", method, path, context.Response.StatusCode, (DateTime.UtcNow - middlewareStart).TotalMilliseconds);
    }

    internal static string SanitizeForLog(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
            builder.Append(char.IsControl(character) ? '?' : character);

        return builder.ToString();
    }
}