using System.Text;
using System.Xml;
using System.Xml.Linq;
using Dav.AspNetCore.Server.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Dav.AspNetCore.Server;

internal static class HttpContextExtensions
{
    private static readonly UTF8Encoding Utf8Encoding = new(false);  
    
    public static void SetResult(
        this HttpContext context,
        DavStatusCode statusCode)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));
        context.Response.StatusCode = (int)statusCode;
    }

    public static async Task WriteDocumentAsync(
        this HttpContext context, 
        DavStatusCode statusCode, 
        XDocument document, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));
        ArgumentNullException.ThrowIfNull(document, nameof(document));

        using var memoryStream = new MemoryStream();

        var xmlWriter = XmlWriter.Create(memoryStream, new XmlWriterSettings
        {
            Encoding = Utf8Encoding, 
            Indent = false,
            OmitXmlDeclaration = false,
            NamespaceHandling = NamespaceHandling.OmitDuplicates,
            Async = true
        });
        
        await document.WriteToAsync(xmlWriter, cancellationToken);
        await xmlWriter.DisposeAsync();
        
        context.SetResult(statusCode);

        context.Response.ContentType = "application/xml; charset=\"utf-8\"";
        context.Response.ContentLength = memoryStream.Length;

        memoryStream.Seek(0, SeekOrigin.Begin);
        await memoryStream.CopyToAsync(context.Response.Body, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<XDocument?> ReadDocumentAsync(
        this HttpContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));
        
        if (context.Request.Body == Stream.Null)
            return null;

        if (context.Request.ContentLength == 0)
            return null;

        if (context.Request.ContentType == null)
            return null;

        if (!context.Request.ContentType.Contains("application/xml") &&
            !context.Request.ContentType.Contains("text/xml"))
            return null;

        var options = context.RequestServices?.GetService<WebDavOptions>();
        var maxBytes = options?.MaxXmlRequestBodyBytes ?? WebDavOptions.DefaultMaxXmlRequestBodyBytes;
        if (maxBytes <= 0)
            maxBytes = WebDavOptions.DefaultMaxXmlRequestBodyBytes;

        if (context.Request.ContentLength is long contentLength && contentLength > maxBytes)
            return null;

        // Buffer the body with a hard cap so a chunked request cannot exhaust memory either.
        using var buffer = new MemoryStream();
        if (!await TryCopyToAsync(context.Request.Body, buffer, maxBytes, cancellationToken))
            return null;

        buffer.Seek(0, SeekOrigin.Begin);

        // Explicitly harden the parser: no DTD, no external resolver and hard character limits.
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = maxBytes,
            MaxCharactersFromEntities = maxBytes,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            Async = true
        };

        try
        {
            using var reader = XmlReader.Create(buffer, settings);
            return await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static async Task<bool> TryCopyToAsync(
        Stream source,
        Stream destination,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        long total = 0;

        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            total += bytesRead;
            if (total > maxBytes)
                return false;

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        return true;
    }
    
    public static Task SendLockedAsync(
        this HttpContext context, 
        Uri uri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        var href = new XElement(XmlNames.Href, $"{context.Request.PathBase}{uri.AbsolutePath}");
        var lockTokenSubmitted = new XElement(XmlNames.LockTokenSubmitted, href);
        var error = new XElement(XmlNames.Error, lockTokenSubmitted);
        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            error);

        return context.WriteDocumentAsync(DavStatusCode.Locked, document, cancellationToken);
    }

    /// <summary>
    /// Gets the typed web dav headers.
    /// </summary>
    /// <param name="request">The http request.</param>
    /// <returns>The typed web dav headers.</returns>
    public static WebDavRequestHeaders GetTypedWebDavHeaders(this HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        IfHeaderValue.TryParse(request.Headers["If"], out var ifHeaderValue);
        DepthHeaderValue.TryParse(request.Headers["Depth"], out var depthHeaderValue);
        DestinationHeaderValue.TryParse(request.Headers["Destination"], out var destinationHeaderValue);
        OverwriteHeaderValue.TryParse(request.Headers["Overwrite"], out var overwriteHeaderValue);
        LockTokenHeaderValue.TryParse(request.Headers["Lock-Token"], out var lockTokenHeaderValue);
        TimeoutHeaderValue.TryParse(request.Headers["Timeout"], out var timeoutHeaderValue);
        
        return new WebDavRequestHeaders
        {
            If = ifHeaderValue?.Conditions ?? Array.Empty<IfHeaderValueCondition>(),
            Depth = depthHeaderValue?.Depth,
            Destination = destinationHeaderValue?.Destination,
            Overwrite = overwriteHeaderValue?.Overwrite,
            LockTokenUri = lockTokenHeaderValue?.Uri,
            Timeouts = timeoutHeaderValue?.Timeouts ?? Array.Empty<TimeSpan>()
        };
    }
}