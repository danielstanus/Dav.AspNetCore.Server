using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dav.AspNetCore.Server;

/// <summary>
/// Validates the WebDAV security configuration at startup so an open or misconfigured endpoint fails
/// fast instead of silently exposing the store.
/// </summary>
/// <remarks>
/// Rules:
/// <list type="bullet">
/// <item><description><see cref="WebDavOptions.RequiresAuthentication"/> and
/// <see cref="WebDavOptions.AllowAnonymousAccess"/> cannot both be enabled.</description></item>
/// <item><description>When authentication is required, at least one scheme must be registered.</description></item>
/// <item><description>Anonymous access is only allowed automatically in the Development environment;
/// anywhere else it must be opted into explicitly.</description></item>
/// </list>
/// </remarks>
internal sealed class WebDavStartupValidation : IHostedService
{
    private readonly IServiceProvider services;
    private readonly WebDavOptions options;
    private readonly ILogger<WebDavStartupValidation> logger;

    /// <summary>
    /// Initializes a new <see cref="WebDavStartupValidation"/> class.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <param name="options">The web dav options.</param>
    /// <param name="logger">The logger.</param>
    public WebDavStartupValidation(
        IServiceProvider services,
        WebDavOptions options,
        ILogger<WebDavStartupValidation> logger)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(options, nameof(options));
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));

        this.services = services;
        this.options = options;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var environment = services.GetService<IWebHostEnvironment>();
        var isDevelopment = environment?.IsDevelopment() ?? false;

        if (options.RequiresAuthentication && options.AllowAnonymousAccess)
        {
            throw new InvalidOperationException(
                "WebDavOptions.RequiresAuthentication and WebDavOptions.AllowAnonymousAccess cannot both be enabled. " +
                "Enable authentication, or explicitly allow anonymous access, but not both.");
        }

        if (options.RequiresAuthentication)
        {
            var schemeProvider = services.GetService<IAuthenticationSchemeProvider>();
            var schemes = schemeProvider == null
                ? Array.Empty<AuthenticationScheme>()
                : (await schemeProvider.GetAllSchemesAsync().WaitAsync(cancellationToken).ConfigureAwait(false)).ToArray();

            if (schemes.Length == 0)
            {
                throw new InvalidOperationException(
                    "WebDavOptions.RequiresAuthentication is true but no authentication scheme is registered. " +
                    "Register one, for example: builder.Services.AddAuthentication().AddBasic(options => { ... });");
            }

            logger.LogInformation(
                "WebDAV authentication is required. Registered schemes: {Schemes}.",
                string.Join(", ", schemes.Select(scheme => scheme.Name)));
            return;
        }

        if (options.AllowAnonymousAccess)
        {
            logger.LogWarning(
                "WebDAV anonymous access is explicitly enabled (WebDavOptions.AllowAnonymousAccess = true); the endpoint is open.");
            return;
        }

        if (isDevelopment)
        {
            logger.LogWarning(
                "WebDAV authentication is disabled. Anonymous access is only allowed automatically in the Development environment.");
            return;
        }

        throw new InvalidOperationException(
            "WebDAV authentication is disabled. Set WebDavOptions.RequiresAuthentication = true and register an authentication scheme, " +
            "or set WebDavOptions.AllowAnonymousAccess = true to explicitly allow anonymous access. " +
            "Anonymous access is only allowed automatically in the Development environment.");
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
