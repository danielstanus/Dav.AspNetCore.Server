using System.Xml.Linq;
using Dav.AspNetCore.Server.Http;
using Dav.AspNetCore.Server.Store;
using Microsoft.AspNetCore.Http;

namespace Dav.AspNetCore.Server.Handlers;

internal class GetHandler : RequestHandler
{
    /// <summary>
    /// Handles the web dav request async.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    protected override async Task HandleRequestAsync(CancellationToken cancellationToken = default)
    {
        if (Item == null)
        {
            Context.SetResult(DavStatusCode.NotFound);
            return;
        }

        var contentType = await GetNonExpensivePropertyAsync(Item, XmlNames.GetContentType, cancellationToken);
        if (!string.IsNullOrWhiteSpace(contentType))
            Context.Response.Headers["Content-Type"] = contentType;
        
        var contentLanguage = await GetNonExpensivePropertyAsync(Item, XmlNames.GetContentLanguage, cancellationToken);
        if (!string.IsNullOrWhiteSpace(contentLanguage))
            Context.Response.Headers["Content-Language"] = contentLanguage;
        
        var lastModified = await GetNonExpensivePropertyAsync(Item, XmlNames.GetLastModified, cancellationToken);
        if (!string.IsNullOrWhiteSpace(lastModified))
            Context.Response.Headers["Last-Modified"] = lastModified;
        
        // The ETag is a content hash (marked as expensive), so it is fetched explicitly here and
        // quoted as required by RFC 7232. Conditional requests use the same cached value.
        var etagResult = await PropertyManager.GetPropertyAsync(Item, XmlNames.GetEtag, cancellationToken);
        var etag = etagResult.Value as string;
        if (!string.IsNullOrWhiteSpace(etag))
            Context.Response.Headers["ETag"] = $"\"{etag}\"";
        
        var contentLength = await GetNonExpensivePropertyAsync(Item, XmlNames.GetContentLength, cancellationToken);
        if (!string.IsNullOrWhiteSpace(contentLength))
            Context.Response.Headers["Content-Length"] = contentLength;

        await using var readableStream = await Item.GetReadableStreamAsync(cancellationToken);
        if (readableStream.CanSeek)
        {
            Context.Response.Headers["Accept-Ranges"] = "bytes";
            Context.Response.ContentLength = readableStream.Length;
        }
        else
        {
            Context.Response.Headers["Accept-Ranges"] = "none";
        }

        if (Context.Request.Method == WebDavMethods.Head)
        {
            Context.SetResult(DavStatusCode.Ok);
            return;
        }
        
        var disableRanges = false;
        
        var requestHeaders = Context.Request.GetTypedHeaders();
        if (requestHeaders.IfRange != null)
        {
            if (requestHeaders.IfRange.EntityTag != null &&
                !string.IsNullOrWhiteSpace(etag) &&
                requestHeaders.IfRange.EntityTag.Tag != etag)
            {
                disableRanges = true;
            }

            if (requestHeaders.IfRange.LastModified != null &&
                !string.IsNullOrWhiteSpace(lastModified) &&
                requestHeaders.IfRange.LastModified != DateTimeOffset.Parse(lastModified))
            {
                disableRanges = true;
            }
        }

        await SendDataAsync(Context, readableStream, disableRanges, cancellationToken);
    }

    private async Task<string?> GetNonExpensivePropertyAsync(
        IStoreItem item,
        XName propertyName,
        CancellationToken cancellationToken = default)
    {
        var metadata = PropertyManager.GetPropertyMetadata(item, propertyName);
        if (metadata == null || metadata.Expensive)
            return null;

        var result = await PropertyManager.GetPropertyAsync(item, propertyName, cancellationToken);
        return (string?)result.Value;
    }
    
    private static async Task SendDataAsync(
        HttpContext context,
        Stream stream,
        bool disableRanges,
        CancellationToken cancellationToken = default)
    {
        var requestHeaders = context.Request.GetTypedHeaders();

        if (!disableRanges && 
            requestHeaders.Range != null &&
            requestHeaders.Range.Unit.Equals("bytes") &&
            requestHeaders.Range.Ranges.Count == 1 &&
            stream.CanSeek)
        {
            var range = requestHeaders.Range.Ranges.First();

            // Resolve and clamp the range. An unsatisfiable range must not fall through to the
            // loop below, and the end must never exceed the resource length.
            if (!ByteRange.TryResolve(stream.Length, range.From, range.To, out var start, out var end))
            {
                // A 416 response has no body, so the previously set Content-Length must be cleared.
                context.Response.ContentLength = null;
                context.Response.Headers.Remove("Content-Length");
                context.Response.Headers["Content-Range"] = $"bytes */{stream.Length}";
                context.SetResult(DavStatusCode.RequestedRangeNotSatisfiable);
                return;
            }

            var bytesToRead = end - start + 1;
            stream.Seek(start, SeekOrigin.Begin);
            
            context.SetResult(DavStatusCode.PartialContent);
            
            context.Response.ContentLength = bytesToRead;
            context.Response.Headers["Content-Range"] = $"bytes {start}-{end}/{stream.Length}";

            var buffer = new byte[64 * 1024];
            while (bytesToRead > 0)
            {
                var toRead = (int)Math.Min(bytesToRead, buffer.Length);
                var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken);

                // Reaching the end of the stream early must terminate the loop instead of
                // spinning forever when the requested range was larger than the content.
                if (bytesRead <= 0)
                    break;

                await context.Response.Body.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                
                bytesToRead -= bytesRead;
            }

            return;
        }
        
        context.SetResult(DavStatusCode.Ok);
        await stream.CopyToAsync(context.Response.Body, cancellationToken);
    }
}