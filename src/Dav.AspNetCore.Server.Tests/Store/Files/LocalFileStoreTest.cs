using Dav.AspNetCore.Server.Store;
using Dav.AspNetCore.Server.Store.Files;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dav.AspNetCore.Server.Tests.Store.Files;

public class LocalFileStoreTest : IDisposable
{
    private readonly string rootPath = Path.Combine(Path.GetTempPath(), $"dav-local-store-{Guid.NewGuid():N}");

    public LocalFileStoreTest()
    {
        System.IO.Directory.CreateDirectory(rootPath);
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(rootPath))
            System.IO.Directory.Delete(rootPath, recursive: true);
    }

    [Fact]
    public async Task OpenFileStreamAsync_Write_TruncatesExistingContent()
    {
        // arrange
        var store = new LocalFileStore(new LocalFileStoreOptions { RootPath = rootPath });
        var uri = UriHelper.CreateUri("/test.txt");
        var filePath = Path.Combine(rootPath, "test.txt");

        // act - write long content first, then a shorter one
        await using (var first = await store.OpenFileStreamAsync(uri, OpenFileMode.Write))
            await first.WriteAsync("contenido largo original"u8.ToArray());

        await using (var second = await store.OpenFileStreamAsync(uri, OpenFileMode.Write))
            await second.WriteAsync("corto"u8.ToArray());

        // assert - the file must contain only the last written content
        Assert.Equal("corto", await System.IO.File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public void Constructor_WithoutRootPath_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new LocalFileStore(new LocalFileStoreOptions()));
    }

    [Fact]
    public async Task FileExistsAsync_RootedPath_ThrowsOnWindows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var store = new LocalFileStore(new LocalFileStoreOptions { RootPath = rootPath });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await store.FileExistsAsync(UriHelper.CreateUri("/C:/Windows/win.ini")));
    }

    [Fact]
    public async Task OpenFileStreamAsync_UncPath_Throws()
    {
        var store = new LocalFileStore(new LocalFileStoreOptions { RootPath = rootPath });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await store.OpenFileStreamAsync(UriHelper.CreateUri("//server/share/evil.txt"), OpenFileMode.Write));
    }

    [Fact]
    public void AddLocalFiles_WithoutRootPath_ThrowsAtRegistration()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddWebDav(builder => builder.AddLocalFiles()));
    }

    [Fact]
    public async Task GetDirectoriesAsync_SkipsLinks()
    {
        // arrange
        var store = new LocalFileStore(new LocalFileStoreOptions { RootPath = rootPath });
        System.IO.Directory.CreateDirectory(Path.Combine(rootPath, "real"));
        var target = Path.Combine(Path.GetTempPath(), $"dav-link-target-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(target);

        try
        {
            var linkPath = Path.Combine(rootPath, "link");
            if (!TryCreateDirectoryLink(linkPath, target))
                return;

            // act
            var directories = await store.GetDirectoriesAsync(UriHelper.CreateUri("/"), CancellationToken.None);

            // assert
            var directory = Assert.Single(directories);
            Assert.Equal("/real", directory.LocalPath.TrimEnd('/'));

            try
            {
                System.IO.Directory.Delete(linkPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
        finally
        {
            try
            {
                if (System.IO.Directory.Exists(target))
                    System.IO.Directory.Delete(target, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private static bool TryCreateDirectoryLink(string linkPath, string targetPath)
        => TestDirectoryLink.TryCreate(linkPath, targetPath);
}
