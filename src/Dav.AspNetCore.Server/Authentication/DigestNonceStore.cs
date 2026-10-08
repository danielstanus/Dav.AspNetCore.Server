using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Dav.AspNetCore.Server.Authentication;

/// <summary>
/// Stores the digest nonces handed out to clients so they can be validated (and replayed
/// nonce-counts rejected) when the client responds.
/// </summary>
/// <remarks>
/// The previous implementation kept opaque values in an unbounded, static, non thread-safe list and
/// never validated the nonce at all, which allowed unlimited replays and a memory exhaustion DoS.
/// This cache is bounded, thread-safe, entries expire and every entry tracks the highest nonce-count
/// seen so a captured Authorization header cannot be reused.
/// </remarks>
internal static class DigestNonceStore
{
    private sealed record NonceEntry(string Opaque, DateTimeOffset ExpiresAt, long NonceCount);

    private static readonly ConcurrentDictionary<string, NonceEntry> Entries = new(StringComparer.Ordinal);
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private const int MaxEntries = 10_000;
    private static long lastCleanupTicks;

    /// <summary>
    /// Creates a new nonce/opaque pair and remembers it until it expires.
    /// </summary>
    public static (string Nonce, string Opaque) Create()
    {
        CleanupIfDue();

        if (Entries.Count >= MaxEntries)
            EvictOldest();

        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var opaque = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        Entries[nonce] = new NonceEntry(opaque, DateTimeOffset.UtcNow + Lifetime, 0);

        return (nonce, opaque);
    }

    /// <summary>
    /// Validates a returned nonce/opaque pair and rejects replayed nonce-counts.
    /// </summary>
    /// <param name="nonce">The nonce sent by the client.</param>
    /// <param name="opaque">The opaque value sent by the client.</param>
    /// <param name="nonceCount">The parsed nonce-count (qop=auth always requires one).</param>
    /// <param name="error">The failure reason, if any.</param>
    /// <returns>True when the nonce is valid and not replayed.</returns>
    public static bool TryValidate(
        string nonce,
        string opaque,
        long nonceCount,
        out string? error)
    {
        error = null;

        if (string.IsNullOrEmpty(nonce) || !Entries.TryGetValue(nonce, out var entry))
        {
            error = "Stale or unknown nonce.";
            return false;
        }

        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            Entries.TryRemove(nonce, out _);
            error = "Stale nonce.";
            return false;
        }

        if (!FixedTimeEquals(entry.Opaque, opaque))
        {
            error = "Invalid opaque value.";
            return false;
        }

        while (true)
        {
            if (nonceCount <= entry.NonceCount)
            {
                error = "Replayed nonce count.";
                return false;
            }

            var updated = entry with { NonceCount = nonceCount };
            if (Entries.TryUpdate(nonce, updated, entry))
                break;

            if (!Entries.TryGetValue(nonce, out entry))
            {
                error = "Stale nonce.";
                return false;
            }
        }

        return true;
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.ASCII.GetBytes(left);
        var rightBytes = Encoding.ASCII.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static void CleanupIfDue()
    {
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref lastCleanupTicks);
        if (now - last < 30_000)
            return;

        if (Interlocked.CompareExchange(ref lastCleanupTicks, now, last) != last)
            return;

        RemoveExpired();
    }

    private static void RemoveExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in Entries)
        {
            if (pair.Value.ExpiresAt <= now)
                Entries.TryRemove(pair.Key, out _);
        }
    }

    private static void EvictOldest()
    {
        RemoveExpired();
        if (Entries.Count < MaxEntries)
            return;

        foreach (var pair in Entries.OrderBy(x => x.Value.ExpiresAt).Take(Entries.Count - MaxEntries + 1))
            Entries.TryRemove(pair.Key, out _);
    }
}
