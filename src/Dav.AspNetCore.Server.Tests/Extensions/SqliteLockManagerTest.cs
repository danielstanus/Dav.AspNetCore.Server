using System.Xml.Linq;
using Dav.AspNetCore.Server.Extensions;
using Dav.AspNetCore.Server.Extensions.Sqlite;
using Dav.AspNetCore.Server.Locks;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dav.AspNetCore.Server.Tests.Extensions;

public class SqliteLockManagerTest : IDisposable
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"dav-lock-{Guid.NewGuid():N}.db");
    private readonly SqliteLockManager manager;

    public SqliteLockManagerTest()
    {
        using (var connection = new SqliteConnection($"Data Source={databasePath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "CREATE TABLE dav_aspnetcore_server_resource_lock (" +
                "Id TEXT NOT NULL, Uri TEXT NOT NULL, LockType INTEGER NOT NULL, Owner TEXT NOT NULL, " +
                "Recursive INTEGER NOT NULL, Timeout INTEGER NOT NULL, Issued INTEGER NOT NULL, Depth INTEGER NOT NULL);";
            command.ExecuteNonQuery();
        }

        manager = new SqliteLockManager(new SqlLockOptions { ConnectionString = $"Data Source={databasePath}" });
    }

    public void Dispose()
    {
        manager.Dispose();

        try
        {
            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task GetLocksAsync_ReturnsTheResourceUri_NotTheLockId()
    {
        // arrange
        var uri = UriHelper.CreateUri("/dir/file.txt");
        var created = await manager.LockAsync(
            uri,
            LockType.Exclusive,
            new XElement("owner", "xUnit"),
            recursive: false,
            TimeSpan.FromMinutes(5));

        Assert.Equal(DavStatusCode.Ok, created.StatusCode);

        // act
        var locks = await manager.GetLocksAsync(uri);

        // assert: the previous implementation returned the lock id (urn:uuid:...) as resource uri.
        var resourceLock = Assert.Single(locks);
        Assert.Equal(uri, resourceLock.Uri);
        Assert.Equal(created.ResourceLock!.Id, resourceLock.Id);
        Assert.NotEqual(resourceLock.Id, resourceLock.Uri);
    }

    [Fact]
    public async Task RefreshLockAsync_ReturnsTheRefreshedLock()
    {
        // arrange
        var uri = UriHelper.CreateUri("/dir/file.txt");
        var created = await manager.LockAsync(
            uri,
            LockType.Exclusive,
            new XElement("owner", "xUnit"),
            recursive: true,
            TimeSpan.FromMinutes(5));

        Assert.Equal(DavStatusCode.Ok, created.StatusCode);

        // act
        var refreshed = await manager.RefreshLockAsync(uri, created.ResourceLock!.Id, TimeSpan.FromMinutes(10));

        // assert: a refresh used to return an empty Ok result, so callers could not build lockdiscovery.
        Assert.Equal(DavStatusCode.Ok, refreshed.StatusCode);
        Assert.NotNull(refreshed.ResourceLock);
        Assert.Equal(uri, refreshed.ResourceLock!.Uri);
        Assert.Equal(TimeSpan.FromMinutes(10), refreshed.ResourceLock.Timeout);
        Assert.True(refreshed.ResourceLock.Recursive);
    }

    [Fact]
    public async Task UnlockAsync_RemovesTheLock()
    {
        // arrange
        var uri = UriHelper.CreateUri("/dir/file.txt");
        var created = await manager.LockAsync(
            uri,
            LockType.Exclusive,
            new XElement("owner", "xUnit"),
            recursive: false,
            TimeSpan.FromMinutes(5));

        // act
        var status = await manager.UnlockAsync(uri, created.ResourceLock!.Id);

        // assert
        Assert.Equal(DavStatusCode.NoContent, status);
        Assert.Empty(await manager.GetLocksAsync(uri));
    }
}
