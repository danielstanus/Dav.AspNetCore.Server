using Dav.AspNetCore.Server.Http;

namespace Dav.AspNetCore.Server.Handlers;

internal class PutHandler : RequestHandler
{
    /// <summary>
    /// Handles the web dav request async.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    protected override async Task HandleRequestAsync(CancellationToken cancellationToken = default)
    {
        var requestUri = Context.Request.Path.ToUri();
        var itemName = requestUri.GetRelativeUri(Collection.Uri).LocalPath.Trim('/');

        var maxSize = Options.MaxResourceSizeBytes;

        // Reject early when the declared size already exceeds the configured maximum so no
        // empty or partial resource is created.
        if (maxSize is long declaredLimit &&
            Context.Request.ContentLength is long contentLength &&
            contentLength > declaredLimit)
        {
            Context.SetResult(DavStatusCode.RequestEntityTooLarge);
            return;
        }

        var result = await Collection.CreateItemAsync(itemName, cancellationToken);
        if (result.Item == null)
        {
            Context.SetResult(result.StatusCode);
            return;
        }

        try
        {
            Stream body = Context.Request.Body;

            // A chunked upload has no Content-Length, so cap the stream itself.
            if (maxSize is long bodyLimit)
                body = new LimitedReadStream(body, bodyLimit);

            await result.Item.WriteDataAsync(body, cancellationToken);
        }
        catch (InvalidDataException)
        {
            // The upload exceeded the configured maximum: remove the partially written resource.
            await Collection.DeleteItemAsync(itemName, cancellationToken);
            Context.SetResult(DavStatusCode.RequestEntityTooLarge);
        }
    }
}
