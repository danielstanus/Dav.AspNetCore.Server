namespace Dav.AspNetCore.Server.Locks;

/// <summary>
/// Resolves the effective lock timeout from the values requested by the client and the configured maximum.
/// </summary>
internal static class LockTimeout
{
    /// <summary>
    /// Resolves the effective timeout.
    /// </summary>
    /// <param name="requested">The timeouts requested by the client (TimeSpan.Zero means "Infinite").</param>
    /// <param name="maxTimeout">The configured maximum, or null to allow non-expiring locks.</param>
    /// <returns>The timeout to grant.</returns>
    public static TimeSpan Resolve(IReadOnlyCollection<TimeSpan> requested, TimeSpan? maxTimeout)
    {
        ArgumentNullException.ThrowIfNull(requested);

        if (requested.Count == 0)
            return maxTimeout ?? TimeSpan.Zero;

        if (maxTimeout is TimeSpan max)
        {
            // A configured maximum wins: "Infinite" (TimeSpan.Zero) and larger requests are clamped to it,
            // so a client can never create a lock that outlives the configured maximum.
            var finite = requested.Where(timeout => timeout > TimeSpan.Zero).ToArray();
            var timeout = finite.Length == 0 ? max : finite.Max();
            return timeout > max ? max : timeout;
        }

        // No maximum configured: honour "Infinite" when requested, otherwise the largest value.
        return requested.Any(timeout => timeout == TimeSpan.Zero)
            ? TimeSpan.Zero
            : requested.Max();
    }
}
