using System.Collections.Concurrent;
using System.Xml.Linq;
using Dav.AspNetCore.Server.Store;

namespace Dav.AspNetCore.Server.Locks;

public sealed class InMemoryLockManager : ILockManager
{
    private readonly ConcurrentDictionary<Uri, ResourceLock> locks = new();
    private readonly int maxLocks;
    
    private static readonly ValueTask<IReadOnlyCollection<LockType>> SupportedLocks = new(new List<LockType>
    {
        LockType.Exclusive,
        LockType.Shared
    });

    /// <summary>
    /// Initializes a new <see cref="InMemoryLockManager"/> class.
    /// </summary>
    /// <param name="locks">The pre population locks.</param>
    /// <param name="maxLocks">The maximum number of locks that may be held at once.</param>
    public InMemoryLockManager(IEnumerable<ResourceLock> locks, int maxLocks = 10_000)
    {
        ArgumentNullException.ThrowIfNull(locks, nameof(locks));
        if (maxLocks <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxLocks), "The maximum number of locks must be greater than zero.");

        this.maxLocks = maxLocks;
        foreach (var resourceLock in locks)
        {
            this.locks[resourceLock.Id] = resourceLock;
        }
    }

    /// <summary>
    /// Gets all hold locks.
    /// </summary>
    public IReadOnlyCollection<ResourceLock> Locks => locks.Values.ToList().AsReadOnly();

    /// <summary>
    /// Locks the resource async.
    /// </summary>
    /// <param name="uri">The uri.</param>
    /// <param name="lockType">The lock type.</param>
    /// <param name="owner">The lock owner.</param>
    /// <param name="recursive">A value indicating whether the lock will be recursive.</param>
    /// <param name="timeout">The lock timeout.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The lock result.</returns>
    public async ValueTask<LockResult> LockAsync(
        Uri uri, 
        LockType lockType, 
        XElement owner, 
        bool recursive, 
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri, nameof(uri));
        ArgumentNullException.ThrowIfNull(owner, nameof(owner));
        
        var activeLocks = await GetLocksAsync(uri, cancellationToken);
        if ((activeLocks.All(x => x.LockType == LockType.Shared) &&
             lockType == LockType.Shared) ||
            activeLocks.Count == 0)
        {
            // Drop expired locks first: they no longer protect anything, but they used to count
            // towards the limit (blocking every new lock with 507 once the cap was reached) and
            // kept their owner payload in memory until the process restarted.
            RemoveExpiredLocks();

            // Bound the total number of locks so a client cannot exhaust memory by creating locks forever.
            if (locks.Count >= maxLocks)
                return new LockResult(DavStatusCode.InsufficientStorage);

            var newLock = new ResourceLock(
                new Uri($"urn:uuid:{Guid.NewGuid():D}"),
                uri,
                lockType,
                owner,
                recursive,
                timeout,
                DateTime.UtcNow);
                
            locks.TryAdd(newLock.Id, newLock);
            return new LockResult(DavStatusCode.Ok, newLock);
        }
        
        return new LockResult(DavStatusCode.Locked);
    }

    /// <summary>
    /// Refreshes the resource lock async.
    /// </summary>
    /// <param name="uri">The uri.</param>
    /// <param name="token">The lock token.</param>
    /// <param name="timeout">The lock timeout.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The lock result.</returns>
    public ValueTask<LockResult> RefreshLockAsync(
        Uri uri, 
        Uri token, 
        TimeSpan timeout, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri, nameof(uri));
        ArgumentNullException.ThrowIfNull(token, nameof(token));
        
        var activeLock = locks.Values.FirstOrDefault(x => IsSameUri(x.Uri, uri) && x.Id == token && x.IsActive);
        if (activeLock == null)
            return new ValueTask<LockResult>(new LockResult(DavStatusCode.PreconditionFailed));

        var refreshLock = activeLock with
        {
            Timeout = timeout,
            IssueDate = DateTime.UtcNow
        };

        if (!locks.TryRemove(activeLock.Id, out _))
            return new ValueTask<LockResult>(new LockResult(DavStatusCode.PreconditionFailed));

        return !locks.TryAdd(refreshLock.Id, refreshLock) 
            ? new ValueTask<LockResult>(new LockResult(DavStatusCode.PreconditionFailed)) 
            : new ValueTask<LockResult>(new LockResult(DavStatusCode.Ok, refreshLock));
    }

    /// <summary>
    /// Unlocks the resource async.
    /// </summary>
    /// <param name="uri">The uri.</param>
    /// <param name="token">The lock token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The status code.</returns>
    public ValueTask<DavStatusCode> UnlockAsync(
        Uri uri, 
        Uri token, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri, nameof(uri));
        ArgumentNullException.ThrowIfNull(token, nameof(token));
        
        var activeLock = locks.Values.FirstOrDefault(x => IsSameUri(x.Uri, uri) && x.Id == token && x.IsActive);
        if (activeLock == null)
            return new ValueTask<DavStatusCode>(DavStatusCode.Conflict);

        return locks.TryRemove(activeLock.Id, out _) 
            ? new ValueTask<DavStatusCode>(DavStatusCode.NoContent) 
            : new ValueTask<DavStatusCode>(DavStatusCode.Conflict);
    }

    /// <summary>
    /// Gets all active resource locks async.
    /// </summary>
    /// <param name="uri">The uri.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of all active resource locks for the given store item.</returns>
    public ValueTask<IReadOnlyCollection<ResourceLock>> GetLocksAsync(
        Uri uri, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri, nameof(uri));
        
        var targetPath = NormalizePath(uri.LocalPath);

        // Build the ancestor paths of the target, including the target itself
        // ("/a/b" -> "/", "/a", "/a/b"). A recursive lock on any ancestor applies to the target.
        var ancestors = new List<string> { "/" };
        if (targetPath != "/")
        {
            var index = 1;
            while (index <= targetPath.Length)
            {
                var next = targetPath.IndexOf('/', index);
                if (next < 0)
                {
                    ancestors.Add(targetPath);
                    break;
                }

                ancestors.Add(targetPath.Substring(0, next));
                index = next + 1;
            }
        }

        var allActiveLocks = new List<ResourceLock>();
        foreach (var resourceLock in locks.Values)
        {
            if (!resourceLock.IsActive)
                continue;

            var lockPath = NormalizePath(resourceLock.Uri.LocalPath);
            foreach (var ancestor in ancestors)
            {
                if (!string.Equals(lockPath, ancestor, PathComparison))
                    continue;

                // A recursive lock applies to the whole subtree; a non-recursive one only to itself.
                if (resourceLock.Recursive || string.Equals(ancestor, targetPath, PathComparison))
                    allActiveLocks.Add(resourceLock);

                break;
            }
        }

        return ValueTask.FromResult<IReadOnlyCollection<ResourceLock>>(allActiveLocks);
    }

    /// <summary>
    /// Removes all expired locks. The conditional removal makes sure a lock that was refreshed
    /// concurrently (same id, new instance) is not dropped by mistake.
    /// </summary>
    private void RemoveExpiredLocks()
    {
        foreach (var pair in locks)
        {
            if (pair.Value.IsActive)
                continue;

            locks.TryRemove(new KeyValuePair<Uri, ResourceLock>(pair.Key, pair.Value));
        }
    }

    /// <summary>
    /// The path comparison to use. Windows file paths are case-insensitive, so a lock created for
    /// "/File.txt" must also protect "/file.txt".
    /// </summary>
    private static StringComparison PathComparison
        => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// Normalizes a store path so "/a/b/", "a/b" and "/a/b" compare equal, and the root is "/".
    /// </summary>
    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return "/";

        if (!path.StartsWith('/'))
            path = $"/{path}";

        path = path.TrimEnd('/');
        return path.Length == 0 ? "/" : path;
    }

    /// <summary>
    /// Compares two store uris using the platform path comparison (case-insensitive on Windows).
    /// </summary>
    private static bool IsSameUri(Uri left, Uri right)
    {
        ArgumentNullException.ThrowIfNull(left, nameof(left));
        ArgumentNullException.ThrowIfNull(right, nameof(right));

        return string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(NormalizePath(left.LocalPath), NormalizePath(right.LocalPath), PathComparison);
    }

    /// <summary>
    /// Gets the supported locks async.
    /// </summary>
    /// <param name="item">The store item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of available lock types for the given resource.</returns>
    public ValueTask<IReadOnlyCollection<LockType>> GetSupportedLocksAsync(
        IStoreItem item,
        CancellationToken cancellationToken = default)
        => SupportedLocks;
}