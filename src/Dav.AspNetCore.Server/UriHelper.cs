namespace Dav.AspNetCore.Server;

internal static class UriHelper
{
    /// <summary>
    /// Creates an absolute uri from an uri or an absolute path.
    /// </summary>
    /// <remarks>
    /// The <see cref="Uri"/> class interprets unix style absolute paths (e.g. <c>/dav/file.txt</c>) as file
    /// uris, but throws a <see cref="UriFormatException"/> when running on Windows. The conversion is therefore
    /// done explicitly so a path resolves to the very same uri on every platform.
    /// </remarks>
    /// <param name="value">The uri or absolute path.</param>
    /// <returns>The absolute uri.</returns>
    public static Uri CreateUri(string value)
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        // Values that already are absolute uris (http://host/path, urn:uuid:..., file:///path) are kept as is.
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
            return absolute;

        if (string.IsNullOrWhiteSpace(value))
            value = "/";
        else if (!value.StartsWith('/'))
            value = $"/{value}";

        try
        {
            return new Uri($"file://{value}");
        }
        catch (UriFormatException)
        {
            // Some values (e.g. a colon in the first segment: "file:///a:b") are rejected by Uri as a
            // non-rooted DOS path. Percent-encode the offending characters and retry so a malformed
            // path cannot turn into an unhandled exception.
            var escaped = value.Replace(":", "%3A").Replace("|", "%7C");
            if (Uri.TryCreate($"file://{escaped}", UriKind.Absolute, out var fallback))
                return fallback;

            // Last resort: a path that cannot exist, so callers get a clean 404 instead of an exception.
            return new Uri($"file:///{Guid.NewGuid():N}");
        }
    }

    public static Uri GetParent(this Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri, nameof(uri));

        if (uri.Segments.Length == 1)
            return CreateUri(uri.Segments[0]);

        var uriString = string.Empty;
        for (var i = 0; i < uri.Segments.Length - 1; i++)
        {
            uriString += Uri.UnescapeDataString(uri.Segments[i]);
        }

        return CreateUri(uriString);
    }

    public static Uri Combine(Uri uri, string path)
    {
        var localPath = uri.LocalPath;
        if (!localPath.EndsWith("/"))
            localPath += "/";

        return CreateUri($"{localPath}{path.TrimStart('/')}");
    }

    public static Uri GetRelativeUri(this Uri relativeTo, Uri uri)
    {
        ArgumentNullException.ThrowIfNull(relativeTo, nameof(relativeTo));
        ArgumentNullException.ThrowIfNull(uri, nameof(uri));
        
        if (uri.Segments.Length > relativeTo.Segments.Length)
            return uri;
        
        // validate root
        for (var i = 0; i < uri.Segments.Length; i++)
        {
            if (relativeTo.Segments[i].Trim('/') != uri.Segments[i].Trim('/'))
                return uri;
        }

        var relativePath = string.Join("", relativeTo.Segments.Select(s => Uri.UnescapeDataString(s)).Skip(uri.Segments.Length));
        if (!relativePath.StartsWith("/"))
            relativePath = $"/{relativePath}";

        if (relativePath.EndsWith("/"))
            relativePath = relativePath.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(relativePath))
            return CreateUri("/");
        
        return CreateUri(relativePath);
    }
}
