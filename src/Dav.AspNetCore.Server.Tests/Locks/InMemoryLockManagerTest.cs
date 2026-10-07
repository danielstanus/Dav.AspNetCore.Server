using System.Xml.Linq;
using Dav.AspNetCore.Server.Locks;
using Xunit;

namespace Dav.AspNetCore.Server.Tests.Locks;

public class InMemoryLockManagerTest
{
    [Theory]
    [InlineData(LockType.Exclusive, true, 0)]
    [InlineData(LockType.Exclusive, false, 60)]
    [InlineData(LockType.Shared, true, 0)]
    [InlineData(LockType.Shared, false, 60)]
    public async Task LockAsync(LockType lockType, bool recursive, int timeout)
    {
        // arrange
        var memoryLockManager = new InMemoryLockManager(Array.Empty<ResourceLock>());
        var uri = UriHelper.CreateUri("/test.txt");

        // act
        var result = await memoryLockManager.LockAsync(
            uri, 
            lockType, 
            new XElement("href", "xUnit"), 
            recursive, 
            TimeSpan.FromSeconds(timeout));

        // assert
        Assert.Equal(DavStatusCode.Ok, result.StatusCode);
        Assert.NotNull(result.ResourceLock);
        Assert.Equal(uri, result.ResourceLock.Uri);
        Assert.Equal(lockType, result.ResourceLock.LockType);
        Assert.Equal(recursive, result.ResourceLock.Recursive);
        Assert.Equal(TimeSpan.FromSeconds(timeout), result.ResourceLock.Timeout);
    }

    [Fact]
    public async Task LockAsync_ParentUriLocked_Fails()
    {
        // arrange
        var resourceLock = new ResourceLock(
            UriHelper.CreateUri($"urn:uuid:{Guid.NewGuid():D}"),
            UriHelper.CreateUri("/"),
            LockType.Exclusive,
            new XElement("href", "xUnit"),
            true,
            TimeSpan.Zero,
            DateTime.Now);
        
        var memoryLockManager = new InMemoryLockManager(new[] { resourceLock });

        // act
        var result = await memoryLockManager.LockAsync(
            UriHelper.CreateUri("/test.txt"), 
            LockType.Exclusive, 
            new XElement("href", "xUnit"), 
            true, 
            TimeSpan.Zero);

        // assert
        Assert.Equal(DavStatusCode.Locked, result.StatusCode);
        Assert.Null(result.ResourceLock);
    }

    [Fact]
    public async Task UnlockAsync()
    {
        // arrange
        var resourceLock = new ResourceLock(
            UriHelper.CreateUri($"urn:uuid:{Guid.NewGuid():D}"),
            UriHelper.CreateUri("/test.txt"),
            LockType.Exclusive,
            new XElement("href", "xUnit"),
            true,
            TimeSpan.Zero,
            DateTime.Now);
        
        var memoryLockManager = new InMemoryLockManager(new[] { resourceLock });

        // act
        var statusCode = await memoryLockManager.UnlockAsync(resourceLock.Uri, resourceLock.Id);

        // assert
        Assert.Equal(DavStatusCode.NoContent, statusCode);
    }
    
    [Fact]
    public async Task UnlockAsync_UriNotLocked_Fails()
    {
        // arrange
        var memoryLockManager = new InMemoryLockManager(Array.Empty<ResourceLock>());

        // act
        var statusCode = await memoryLockManager.UnlockAsync(UriHelper.CreateUri("/test.txt"), UriHelper.CreateUri($"urn:uuid:{Guid.NewGuid():D}"));

        // assert
        Assert.Equal(DavStatusCode.Conflict, statusCode);
    }

    [Fact]
    public async Task LockAsync_SameUriLocked_Fails()
    {
        // arrange
        var resourceLock = new ResourceLock(
            UriHelper.CreateUri($"urn:uuid:{Guid.NewGuid():D}"),
            UriHelper.CreateUri("/test.txt"),
            LockType.Exclusive,
            new XElement("href", "xUnit"),
            true,
            TimeSpan.Zero,
            DateTime.Now);
        
        var memoryLockManager = new InMemoryLockManager(new[] { resourceLock });
        
        // act
        var result = await memoryLockManager.LockAsync(
            resourceLock.Uri, 
            LockType.Exclusive, 
            new XElement("href", "xUnit"), 
            true, 
            TimeSpan.Zero);

        // assert
        Assert.Equal(DavStatusCode.Locked, result.StatusCode);
        Assert.Null(result.ResourceLock);
    }

    [Fact]
    public async Task RefreshLockAsync()
    {
        // arrange
        var resourceLock = new ResourceLock(
            UriHelper.CreateUri($"urn:uuid:{Guid.NewGuid():D}"),
            UriHelper.CreateUri("/test.txt"),
            LockType.Exclusive,
            new XElement("href", "xUnit"),
            true,
            TimeSpan.Zero,
            DateTime.Now);

        var memoryLockManager = new InMemoryLockManager(new[] { resourceLock });

        // act
        var result = await memoryLockManager.RefreshLockAsync(
            resourceLock.Uri, 
            resourceLock.Id,
            TimeSpan.Zero);

        // assert
        Assert.Equal(DavStatusCode.Ok, result.StatusCode);
        Assert.NotNull(result.ResourceLock);
        Assert.Equal(resourceLock.Uri, result.ResourceLock.Uri);
        Assert.Equal(resourceLock.LockType, result.ResourceLock.LockType);
        Assert.Equal(resourceLock.Recursive, result.ResourceLock.Recursive);
        Assert.Equal(resourceLock.Timeout, result.ResourceLock.Timeout);
    }
    
    [Fact]
    public async Task RefreshLockAsync_LockDoesNotExist_Fails()
    {
        // arrange
        var memoryLockManager = new InMemoryLockManager(Array.Empty<ResourceLock>());

        // act
        var result = await memoryLockManager.RefreshLockAsync(
            UriHelper.CreateUri("/test.txt"), 
            UriHelper.CreateUri($"urn:uuid:{Guid.NewGuid():D}"),
            TimeSpan.Zero);

        // assert
        Assert.Equal(DavStatusCode.PreconditionFailed, result.StatusCode);
        Assert.Null(result.ResourceLock);
    }

    [Fact]
    public async Task LockAsync_SharedLockOnSharedUri_Works()
    {
        // arrange
        var resourceLock = new ResourceLock(
            UriHelper.CreateUri($"urn:uuid:{Guid.NewGuid():D}"),
            UriHelper.CreateUri("/test.txt"),
            LockType.Shared,
            new XElement("href", "xUnit"),
            true,
            TimeSpan.Zero,
            DateTime.Now);

        var memoryLockManager = new InMemoryLockManager(new[] { resourceLock });

        // act
        var result = await memoryLockManager.LockAsync(
            resourceLock.Uri, 
            LockType.Shared, 
            new XElement("href", "xUnit"), 
            true, 
            TimeSpan.Zero);

        // assert
        Assert.Equal(DavStatusCode.Ok, result.StatusCode);
        Assert.NotNull(result.ResourceLock);
        Assert.Equal(resourceLock.Uri, result.ResourceLock.Uri);
        Assert.Equal(resourceLock.LockType, result.ResourceLock.LockType);
        Assert.Equal(resourceLock.Recursive, result.ResourceLock.Recursive);
        Assert.Equal(resourceLock.Timeout, result.ResourceLock.Timeout);
    }
    
    [Fact]
    public async Task LockAsync_SharedLockOnSharedParentUri_Works()
    {
        // arrange
        var resourceLock = new ResourceLock(
            UriHelper.CreateUri($"urn:uuid:{Guid.NewGuid():D}"),
            UriHelper.CreateUri("/"),
            LockType.Shared,
            new XElement("href", "xUnit"),
            true,
            TimeSpan.Zero,
            DateTime.Now);

        var memoryLockManager = new InMemoryLockManager(new[] { resourceLock });
        var uri = UriHelper.CreateUri("/test.txt");

        // act
        var result = await memoryLockManager.LockAsync(
            uri,
            LockType.Shared, 
            new XElement("href", "xUnit"), 
            true, 
            TimeSpan.Zero);

        // assert
        Assert.Equal(DavStatusCode.Ok, result.StatusCode);
        Assert.NotNull(result.ResourceLock);
        Assert.Equal(uri, result.ResourceLock.Uri);
        Assert.Equal(resourceLock.LockType, result.ResourceLock.LockType);
        Assert.Equal(resourceLock.Recursive, result.ResourceLock.Recursive);
        Assert.Equal(resourceLock.Timeout, result.ResourceLock.Timeout);
    }
    
    [Fact]
    public async Task LockAsync_SharedLockOnExclusiveUri_Fails()
    {
        // arrange
        var resourceLock = new ResourceLock(
            UriHelper.CreateUri($"urn:uuid:{Guid.NewGuid():D}"),
            UriHelper.CreateUri("/test.txt"),
            LockType.Exclusive,
            new XElement("href", "xUnit"),
            true,
            TimeSpan.Zero,
            DateTime.Now);

        var memoryLockManager = new InMemoryLockManager(new[] { resourceLock });

        // act
        var result = await memoryLockManager.LockAsync(
            resourceLock.Uri, 
            LockType.Shared, 
            new XElement("href", "xUnit"), 
            true, 
            TimeSpan.Zero);

        // assert
        Assert.Equal(DavStatusCode.Locked, result.StatusCode);
        Assert.Null(result.ResourceLock);
    }
    
    [Fact]
    public async Task LockAsync_SharedLockOnExclusiveParentUri_Fails()
    {
        // arrange
        var resourceLock = new ResourceLock(
            UriHelper.CreateUri($"urn:uuid:{Guid.NewGuid():D}"),
            UriHelper.CreateUri("/"),
            LockType.Exclusive,
            new XElement("href", "xUnit"),
            true,
            TimeSpan.Zero,
            DateTime.Now);

        var memoryLockManager = new InMemoryLockManager(new[] { resourceLock });

        // act
        var result = await memoryLockManager.LockAsync(
            UriHelper.CreateUri("/test.txt"), 
            LockType.Shared, 
            new XElement("href", "xUnit"), 
            true, 
            TimeSpan.Zero);

        // assert
        Assert.Equal(DavStatusCode.Locked, result.StatusCode);
        Assert.Null(result.ResourceLock);
    }

    [Fact]
    public async Task GetSupportedLocksAsync()
    {
        // arrange
        var memoryLockManager = new InMemoryLockManager(Array.Empty<ResourceLock>());
        var item = new TestStoreItem(UriHelper.CreateUri("/test.txt"));

        // act
        var result = await memoryLockManager.GetSupportedLocksAsync(item);

        // assert
        Assert.Equal(2, result.Count);
        Assert.Contains(LockType.Exclusive, result);
        Assert.Contains(LockType.Shared, result);
    }

    [Fact]
    public async Task LockAsync_MaxLocksReached_ReturnsInsufficientStorage()
    {
        // arrange
        var memoryLockManager = new InMemoryLockManager(Array.Empty<ResourceLock>(), maxLocks: 1);

        var first = await memoryLockManager.LockAsync(
            UriHelper.CreateUri("/one.txt"),
            LockType.Exclusive,
            new XElement("href", "xUnit"),
            false,
            TimeSpan.FromMinutes(5));
        Assert.Equal(DavStatusCode.Ok, first.StatusCode);

        // act
        var second = await memoryLockManager.LockAsync(
            UriHelper.CreateUri("/two.txt"),
            LockType.Exclusive,
            new XElement("href", "xUnit"),
            false,
            TimeSpan.FromMinutes(5));

        // assert
        Assert.Equal(DavStatusCode.InsufficientStorage, second.StatusCode);
        Assert.Null(second.ResourceLock);
    }

    [Fact]
    public void Constructor_InvalidMaxLocks_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new InMemoryLockManager(Array.Empty<ResourceLock>(), maxLocks: 0));
    }

    [Fact]
    public async Task GetLocksAsync_RecursiveParentLock_IsReturned()
    {
        var rootLock = CreateLock("/", recursive: true);
        var memoryLockManager = new InMemoryLockManager(new[] { rootLock });

        var result = await memoryLockManager.GetLocksAsync(UriHelper.CreateUri("/a/b.txt"));

        Assert.Single(result);
        Assert.Contains(rootLock, result);
    }

    [Fact]
    public async Task GetLocksAsync_RecursiveParentLock_IsNotDuplicated()
    {
        var rootLock = CreateLock("/", recursive: true);
        var memoryLockManager = new InMemoryLockManager(new[] { rootLock });

        var result = await memoryLockManager.GetLocksAsync(UriHelper.CreateUri("/a/b.txt"));

        Assert.Single(result);
    }

    [Fact]
    public async Task GetLocksAsync_NonRecursiveParentLock_IsNotReturned()
    {
        var rootLock = CreateLock("/", recursive: false);
        var memoryLockManager = new InMemoryLockManager(new[] { rootLock });

        var result = await memoryLockManager.GetLocksAsync(UriHelper.CreateUri("/a/b.txt"));

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLocksAsync_ExactLock_IsReturned()
    {
        var itemLock = CreateLock("/a/b.txt", recursive: false);
        var memoryLockManager = new InMemoryLockManager(new[] { itemLock });

        var result = await memoryLockManager.GetLocksAsync(UriHelper.CreateUri("/a/b.txt"));

        Assert.Single(result);
    }

    [Fact]
    public async Task GetLocksAsync_RecursiveLock_IsNotReturnedForSibling()
    {
        var itemLock = CreateLock("/a", recursive: true);
        var memoryLockManager = new InMemoryLockManager(new[] { itemLock });

        var result = await memoryLockManager.GetLocksAsync(UriHelper.CreateUri("/b"));

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLocksAsync_TrailingSlash_IsNormalized()
    {
        var itemLock = CreateLock("/a/b", recursive: false);
        var memoryLockManager = new InMemoryLockManager(new[] { itemLock });

        var result = await memoryLockManager.GetLocksAsync(UriHelper.CreateUri("/a/b/"));

        Assert.Single(result);
    }

    [Fact]
    public async Task GetLocksAsync_ExpiredLock_IsNotReturned()
    {
        var expiredLock = new ResourceLock(
            UriHelper.CreateUri($"urn:uuid:{Guid.NewGuid():D}"),
            UriHelper.CreateUri("/a"),
            LockType.Exclusive,
            new XElement("href", "xUnit"),
            false,
            TimeSpan.FromMinutes(5),
            DateTime.UtcNow - TimeSpan.FromMinutes(10));
        var memoryLockManager = new InMemoryLockManager(new[] { expiredLock });

        var result = await memoryLockManager.GetLocksAsync(UriHelper.CreateUri("/a"));

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLocksAsync_IsCaseInsensitiveOnWindows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var itemLock = CreateLock("/File.txt", recursive: false);
        var memoryLockManager = new InMemoryLockManager(new[] { itemLock });

        var result = await memoryLockManager.GetLocksAsync(UriHelper.CreateUri("/file.txt"));

        Assert.Single(result);
    }

    private static ResourceLock CreateLock(string path, bool recursive)
        => new(
            UriHelper.CreateUri($"urn:uuid:{Guid.NewGuid():D}"),
            UriHelper.CreateUri(path),
            LockType.Exclusive,
            new XElement("href", "xUnit"),
            recursive,
            TimeSpan.FromMinutes(5),
            DateTime.UtcNow);
}