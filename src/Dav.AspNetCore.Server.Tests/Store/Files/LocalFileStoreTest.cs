using Dav.AspNetCore.Server.Store.Files;
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
}
