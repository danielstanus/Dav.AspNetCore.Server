using System.Xml.Linq;
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

    [Fact]
    public async Task SaveChangesAsync_ShorterRewrite_KeepsTheFileReadable()
    {
        // arrange
        var options = new XmlFilePropertyStoreOptions { RootPath = rootPath, AcceptCustomProperties = true };
        var item = new TestStoreItem(UriHelper.CreateUri("/doc.txt"));
        var propertyName = XName.Get("tag", "urn:test");

        var store = new XmlFilePropertyStore(options);
        Assert.True(await store.SetPropertyAsync(item, propertyName, PropertyMetadata.Default, false, new string('a', 4096)));
        await store.SaveChangesAsync();

        // act: a shorter rewrite used to leave the trailing bytes of the previous document behind.
        Assert.True(await store.SetPropertyAsync(item, propertyName, PropertyMetadata.Default, false, "short"));
        await store.SaveChangesAsync();

        // assert: a fresh instance forces a read from disk.
        var reader = new XmlFilePropertyStore(new XmlFilePropertyStoreOptions { RootPath = rootPath });
        var properties = await reader.GetPropertiesAsync(item);

        var property = Assert.Single(properties);
        Assert.Equal(propertyName, property.Name);
        Assert.Equal("short", property.CurrentValue);
    }

    [Fact]
    public async Task SaveChangesAsync_PropertyWithoutValue_CanBeReadBack()
    {
        // arrange
        var options = new XmlFilePropertyStoreOptions { RootPath = rootPath, AcceptCustomProperties = true };
        var item = new TestStoreItem(UriHelper.CreateUri("/empty.txt"));
        var propertyName = XName.Get("tag", "urn:test");

        var store = new XmlFilePropertyStore(options);
        Assert.True(await store.SetPropertyAsync(item, propertyName, PropertyMetadata.Default, false, null));

        // act
        await store.SaveChangesAsync();

        // assert
        var reader = new XmlFilePropertyStore(new XmlFilePropertyStoreOptions { RootPath = rootPath });
        var properties = await reader.GetPropertiesAsync(item);

        var property = Assert.Single(properties);
        Assert.Equal(propertyName, property.Name);
        Assert.Null(property.CurrentValue);
    }

    [Fact]
    public async Task GetPropertiesAsync_CorruptFile_ReturnsEmptyWithoutRewritingIt()
    {
        // arrange
        var item = new TestStoreItem(UriHelper.CreateUri("/corrupt.txt"));
        var filePath = Path.Combine(rootPath, "corrupt.txt.xml");
        await File.WriteAllTextAsync(filePath, "<PropertyStore>not closed");

        var store = new XmlFilePropertyStore(new XmlFilePropertyStoreOptions { RootPath = rootPath });

        // act
        var properties = await store.GetPropertiesAsync(item);

        // assert
        Assert.Empty(properties);
        Assert.Equal("<PropertyStore>not closed", await File.ReadAllTextAsync(filePath));
    }
}
