using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dav.AspNetCore.Server.Locks;

public static class WebDavOptionsBuilderExtensions
{
    /// <summary>
    /// Adds in memory locking.
    /// </summary>
    /// <param name="builder">The web dav options builder.</param>
    /// <param name="maxLocks">The maximum number of locks that may be held at once.</param>
    /// <returns>The web dav options builder.</returns>
    public static WebDavOptionsBuilder AddInMemoryLocks(this WebDavOptionsBuilder builder, int maxLocks = 10_000)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        
        builder.Services.Replace(ServiceDescriptor.Singleton<ILockManager>(new InMemoryLockManager(Array.Empty<ResourceLock>(), maxLocks)));

        return builder;
    }

    /// <summary>
    /// Adds a lock manager.
    /// </summary>
    /// <param name="builder">The web dav options builder.</param>
    /// <typeparam name="T">The lock manager type.</typeparam>
    /// <returns>The web dav options builder.</returns>
    public static WebDavOptionsBuilder AddLocks<T>(this WebDavOptionsBuilder builder) where T : class, ILockManager
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        builder.Services.Replace(ServiceDescriptor.Scoped<ILockManager, T>());
        
        return builder;
    }
}