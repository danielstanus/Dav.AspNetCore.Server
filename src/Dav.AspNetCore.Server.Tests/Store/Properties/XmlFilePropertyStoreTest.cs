using Dav.AspNetCore.Server.Store;
using Dav.AspNetCore.Server.Store.Properties;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dav.AspNetCore.Server.Tests.Store.Properties;

public class XmlFilePropertyStoreTest : IDisposable
{
    private readonly string rootPath = Path.Combine(Path.GetTempPath(), $"dav-xml-props-{Guid.NewGuid():N}");

    public XmlFilePropertyStoreTest()
    {
        System.IO.Directory.CreateDirectory(rootPath);
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(rootPath))
            System.IO.Directory.Delete(rootPath, recursive: true);
    }

    [Fact]
    public void Constructor_WithoutRootPath_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new XmlFilePropertyStore(new XmlFilePropertyStoreOptions()));
    }

    [Fact]
    public async Task GetPropertiesAsync_RootedPath_ThrowsOnWindows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var store = new XmlFilePropertyStore(new XmlFilePropertyStoreOptions { RootPath = rootPath });
        var item = new TestStoreItem(UriHelper.CreateUri("/C:/Windows/win.ini"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await store.GetPropertiesAsync(item));
    }

    [Fact]
    public void AddXmlFilePropertyStore_WithoutRootPath_ThrowsAtRegistration()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(
            () => services.AddWebDav(builder => builder.AddXmlFilePropertyStore()));
    }
}
