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

    /// <summary>
    /// Removes the request path base from a destination path so it maps to the store uri.
    /// </summary>
    /// <remarks>
    /// RFC 4918 allows an absolute destination; when it points outside of this WebDAV mount it is a
    /// foreign destination and must be rejected (502 Bad Gateway), never executed against the store.
    /// The old implementation stripped the base length blindly, which threw an
    /// <see cref="ArgumentOutOfRangeException"/> for shorter paths and silently mapped destinations
    /// of other prefixes into the store.
    /// </remarks>
    /// <param name="pathBase">The request path base (may be empty or null).</param>
    /// <param name="destination">The parsed destination uri.</param>
    /// <param name="result">The destination uri without the path base.</param>
    /// <returns>True when the destination belongs to the path base, otherwise false.</returns>
    public static bool TryRemovePathBase(string? pathBase, Uri destination, out Uri result)
    {
        ArgumentNullException.ThrowIfNull(destination);

        result = destination;

        if (string.IsNullOrEmpty(pathBase))
            return true;

        var normalizedBase = pathBase.Replace('\\', '/').TrimEnd('/');
        if (normalizedBase.Length == 0)
            return true;

        var localPath = destination.LocalPath.Replace('\\', '/');

        if (!localPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
            return false;

        // The base must match a whole path segment: "/dav" matches "/dav/x" and "/dav",
        // but not a sibling such as "/davx/y".
        if (localPath.Length > normalizedBase.Length && localPath[normalizedBase.Length] != '/')
            return false;

        result = CreateUri(localPath.Substring(normalizedBase.Length));
        return true;
    }

    /// <summary>
    /// Returns a canonical uri for lock lookups: the local path without a trailing separator
    /// (except the root).
    /// </summary>
    /// <remarks>
    /// Collection uris built with <see cref="GetParent"/> keep the trailing slash ("/dir/"), but
    /// the ADO lock managers compare the raw local path stored for the lock ("/dir"), so a
    /// non-normalized collection uri would never match a lock on the collection. The in-memory
    /// manager normalizes internally; normalizing here keeps every manager consistent.
    /// </remarks>
    /// <param name="uri">The uri to normalize.</param>
    /// <returns>The uri without a trailing path separator.</returns>
    public static Uri NormalizeLockUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        var localPath = uri.LocalPath.Replace('\\', '/');
        if (localPath.Length <= 1 || !localPath.EndsWith('/'))
            return uri;

        return CreateUri(localPath.TrimEnd('/'));
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
