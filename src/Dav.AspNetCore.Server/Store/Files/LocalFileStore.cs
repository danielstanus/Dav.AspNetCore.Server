namespace Dav.AspNetCore.Server.Store.Files;

public class LocalFileStore : FileStore
{
    private readonly LocalFileStoreOptions options;

    /// <summary>
    /// Initializes a new <see cref="LocalFileStore"/> class.
    /// </summary>
    /// <param name="options">The local file store options.</param>
    public LocalFileStore(LocalFileStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options, nameof(options));
        if (string.IsNullOrWhiteSpace(options.RootPath))
            throw new InvalidOperationException(
                "The local file store root path must be configured via LocalFileStoreOptions.RootPath.");
        this.options = options;
    }

    public override ValueTask<bool> DirectoryExistsAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        var path = StorePath.Resolve(options.RootPath, uri.LocalPath);
        return ValueTask.FromResult(System.IO.Directory.Exists(path));
    }

    public override ValueTask<bool> FileExistsAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        var path = StorePath.Resolve(options.RootPath, uri.LocalPath);
        return ValueTask.FromResult(System.IO.File.Exists(path));
    }

    public override ValueTask DeleteDirectoryAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        var path = StorePath.Resolve(options.RootPath, uri.LocalPath);
        System.IO.Directory.Delete(path);
        
        return ValueTask.CompletedTask;
    }

    public override ValueTask DeleteFileAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        var path = StorePath.Resolve(options.RootPath, uri.LocalPath);
        System.IO.File.Delete(path);
        
        return ValueTask.CompletedTask;
    }

    public override ValueTask<DirectoryProperties> GetDirectoryPropertiesAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        var path = StorePath.Resolve(options.RootPath, uri.LocalPath);
        var directoryInfo = new DirectoryInfo(path);
        var directoryProperties = new DirectoryProperties(
            uri,
            directoryInfo.Name,
            directoryInfo.CreationTimeUtc,
            directoryInfo.LastWriteTimeUtc);

        return ValueTask.FromResult(directoryProperties);
    }

    public override ValueTask<FileProperties> GetFilePropertiesAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        var path = StorePath.Resolve(options.RootPath, uri.LocalPath);
        var fileInfo = new FileInfo(path);
        var fileProperties = new FileProperties(
            uri,
            fileInfo.Name,
            fileInfo.CreationTimeUtc,
            fileInfo.LastWriteTimeUtc,
            fileInfo.Length);

        return ValueTask.FromResult(fileProperties);
    }

    public override ValueTask<Stream> OpenFileStreamAsync(Uri uri, OpenFileMode mode, CancellationToken cancellationToken = default)
    {
        var path = StorePath.Resolve(options.RootPath, uri.LocalPath);
        // FileMode.Create truncates an existing file, so a shorter PUT does not leave
        // trailing bytes of the previous content behind (File.OpenWrite does not truncate).
        return ValueTask.FromResult<Stream>(mode == OpenFileMode.Read 
            ? System.IO.File.OpenRead(path) 
            : new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None));
    }

    public override ValueTask CreateDirectoryAsync(Uri uri, CancellationToken cancellationToken)
    {
        var path = StorePath.Resolve(options.RootPath, uri.LocalPath);
        System.IO.Directory.CreateDirectory(path);
        
        return ValueTask.CompletedTask;
    }

    public override ValueTask<Uri[]> GetFilesAsync(Uri uri, CancellationToken cancellationToken)
    {
        var path = StorePath.Resolve(options.RootPath, uri.LocalPath);
        return ValueTask.FromResult(System.IO.Directory.GetFiles(path)
            // Links are resolved (and contained) by StorePath on access; hiding them in listings avoids
            // walking through a link that could point outside of the root (CWE-59).
            .Where(x => !IsReparsePoint(x))
            .Select(x =>
            {
                // Path.GetRelativePath returns backslashes on windows, uris always use forward slashes.
                var relativePath = $"/{Path.GetRelativePath(options.RootPath, x)}".Replace('\\', '/');
                return UriHelper.CreateUri(relativePath);
            }).ToArray());
    }

    public override ValueTask<Uri[]> GetDirectoriesAsync(Uri uri, CancellationToken cancellationToken)
    {
        var path = StorePath.Resolve(options.RootPath, uri.LocalPath);
        return ValueTask.FromResult(System.IO.Directory.GetDirectories(path)
            .Where(x => !IsReparsePoint(x))
            .Select(x =>
            {
                // Path.GetRelativePath returns backslashes on windows, uris always use forward slashes.
                var relativePath = $"/{Path.GetRelativePath(options.RootPath, x)}".Replace('\\', '/');
                return UriHelper.CreateUri(relativePath);
            }).ToArray());
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (System.IO.File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}