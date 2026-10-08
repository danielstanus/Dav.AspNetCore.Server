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

        // Windows rejects characters such as '*' or '?' in file names. Path.GetFullPath throws an
        // ArgumentException for them, which used to escape as an HTTP 500: reject them explicitly
        // so a malformed request gets a clean 403 on every platform that would fail later on.
        if (OperatingSystem.IsWindows() && relative.IndexOfAny(InvalidWindowsFileNameChars) >= 0)
            throw new UnauthorizedAccessException("The requested path is outside of the store root.");

        if (Path.IsPathRooted(relative))
            throw new UnauthorizedAccessException("The requested path is outside of the store root.");

        string path;
        try
        {
            path = Path.GetFullPath(Path.Combine(root, relative));
        }
        catch (Exception exception) when (exception is ArgumentException or PathTooLongException or NotSupportedException)
        {
            // Very long paths and any other path form the file system rejects must not surface as a 500.
            throw new UnauthorizedAccessException("The requested path is outside of the store root.", exception);
        }

        if (!IsInside(root, rootWithSeparator, path))
            throw new UnauthorizedAccessException("The requested path is outside of the store root.");

        EnsureLinksStayInsideRoot(root, rootWithSeparator, path);

        return path;
    }

    /// <summary>
    /// Resolves symlinks, junctions and other reparse points along the path and rejects the request
    /// when any of them points outside of the root. The lexical containment check cannot see them:
    /// a link stored inside the root would otherwise let a request read or write outside of it (CWE-59).
    /// </summary>
    /// <param name="root">The canonical root path.</param>
    /// <param name="rootWithSeparator">The canonical root path with a trailing separator.</param>
    /// <param name="path">The lexically contained path.</param>
    private static void EnsureLinksStayInsideRoot(string root, string rootWithSeparator, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        if (relative.Length == 0 || relative == ".")
            return;

        var current = root;
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (segment.Length == 0 || segment is "." or "..")
                continue;

            current = Path.Combine(current, segment);

            var info = GetFileSystemInfo(current);
            if (info == null)
                return; // the remaining path does not exist yet (for example a PUT of a new file)

            if ((info.Attributes & FileAttributes.ReparsePoint) == 0)
                continue;

            var target = info.ResolveLinkTarget(returnFinalTarget: true);

            // Not every reparse point is a link (for example cloud storage placeholders):
            // only links that resolve to another location are checked.
            if (target == null)
                continue;

            current = target.FullName;

            if (!IsInside(root, rootWithSeparator, current))
                throw new UnauthorizedAccessException("The requested path is outside of the store root.");
        }
    }

    private static FileSystemInfo? GetFileSystemInfo(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.Directory) != 0
                ? new DirectoryInfo(path)
                : new FileInfo(path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or IOException)
        {
            return null; // the path does not exist (or cannot be inspected): nothing to resolve
        }
    }

    /// <summary>
    /// The characters Windows does not accept in a file name, except the path separators (and the
    /// colon, which is rejected separately because it denotes a drive or an alternate data stream).
    /// </summary>
    private static readonly char[] InvalidWindowsFileNameChars = Path.GetInvalidFileNameChars()
        .Where(character => character is not ('\\' or '/' or ':'))
        .ToArray();

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
