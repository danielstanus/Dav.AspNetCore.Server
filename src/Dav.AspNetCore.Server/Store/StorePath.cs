namespace Dav.AspNetCore.Server.Store;

/// <summary>
/// Resolves store item uris to absolute file system paths and guarantees that the
/// resolved path stays inside the configured store root.
/// </summary>
/// <remarks>
/// <see cref="System.IO.Path.Combine(string, string)"/> discards the first argument when the
/// second one is rooted. Because item uris are derived from untrusted request paths, a value such
/// as <c>/C:/Windows/win.ini</c> or <c>//server/share</c> can otherwise escape the store root and
/// read, write or delete arbitrary files. This helper rejects rooted paths and verifies containment.
/// </remarks>
internal static class StorePath
{
    /// <summary>
    /// Resolves the given store relative path against the configured root.
    /// </summary>
    /// <param name="rootPath">The configured store root path.</param>
    /// <param name="relativePath">The store relative path (usually the local path of an item uri).</param>
    /// <returns>The absolute path inside the root.</returns>
    /// <exception cref="InvalidOperationException">The root path is not configured.</exception>
    /// <exception cref="UnauthorizedAccessException">The path resolves outside of the root.</exception>
    public static string Resolve(string rootPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new InvalidOperationException("The store root path was not configured.");

        var root = Path.GetFullPath(rootPath);
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        var raw = relativePath ?? string.Empty;

        // A UNC path ("\\server\share" or "//server/share") never belongs to a store root.
        if (raw.StartsWith(@"\\", StringComparison.Ordinal) || raw.StartsWith("//", StringComparison.Ordinal))
            throw new UnauthorizedAccessException("The requested path is outside of the store root.");

        // Normalize both separators to the platform separator and drop the leading separators
        // that originate from the uri root ("/a/b" -> "a/b", "\a\b" -> "a\b").
        var relative = raw
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        // On Windows a colon denotes a drive or an alternate data stream, never a valid file name.
        if (OperatingSystem.IsWindows() && relative.Contains(':'))
            throw new UnauthorizedAccessException("The requested path is outside of the store root.");

        if (Path.IsPathRooted(relative))
            throw new UnauthorizedAccessException("The requested path is outside of the store root.");

        var path = Path.GetFullPath(Path.Combine(root, relative));

        if (!IsInside(root, rootWithSeparator, path))
            throw new UnauthorizedAccessException("The requested path is outside of the store root.");

        return path;
    }

    private static bool IsInside(string root, string rootWithSeparator, string path)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (string.Equals(path, root, comparison))
            return true;

        return path.StartsWith(rootWithSeparator, comparison);
    }
}
