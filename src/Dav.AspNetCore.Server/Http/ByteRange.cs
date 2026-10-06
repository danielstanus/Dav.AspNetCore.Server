namespace Dav.AspNetCore.Server.Http;

/// <summary>
/// Resolves a single byte range against the total resource length, clamping the requested end to
/// the last byte so a range request can never read past the end of the stream.
/// </summary>
internal static class ByteRange
{
    /// <summary>
    /// Tries to resolve the inclusive byte range <paramref name="start"/>-<paramref name="end"/>.
    /// </summary>
    /// <param name="length">The total length of the resource.</param>
    /// <param name="from">The requested first byte, or null for a suffix range.</param>
    /// <param name="to">The requested last byte, or null to read until the end.</param>
    /// <param name="start">The resolved first byte.</param>
    /// <param name="end">The resolved last byte (inclusive).</param>
    /// <returns>True when the range is satisfiable, otherwise false (respond with 416).</returns>
    public static bool TryResolve(long length, long? from, long? to, out long start, out long end)
    {
        start = 0;
        end = 0;

        if (length <= 0 || (from == null && to == null))
            return false;

        if (from == null)
        {
            // Suffix range: the last N bytes. "bytes=-0" is unsatisfiable.
            var suffix = to!.Value;
            if (suffix <= 0)
                return false;

            start = suffix >= length ? 0 : length - suffix;
            end = length - 1;
            return true;
        }

        start = from.Value;
        end = to == null ? length - 1 : Math.Min(to.Value, length - 1);

        return start >= 0 && start < length && end >= start;
    }
}
