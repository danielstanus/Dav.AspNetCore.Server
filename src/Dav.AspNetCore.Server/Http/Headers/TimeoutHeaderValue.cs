using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Dav.AspNetCore.Server.Http.Headers;

public class TimeoutHeaderValue
{
    /// <summary>
    /// The largest timeout accepted, in seconds (100 years). Larger values would overflow
    /// <see cref="TimeSpan.FromSeconds(double)"/> and turn a malformed header into a 500.
    /// </summary>
    private const long MaxTimeoutSeconds = 100L * 365 * 24 * 60 * 60;

    /// <summary>
    /// Initializes a new <see cref="TimeoutHeaderValue"/> class.
    /// </summary>
    /// <param name="timeouts">The timeouts.</param>
    public TimeoutHeaderValue(params TimeSpan[] timeouts)
    {
        Timeouts = timeouts;
    }
    
    /// <summary>
    /// Gets the timeouts.
    /// </summary>
    public TimeSpan[] Timeouts { get; }

    /// <summary>
    /// Parses the input.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <returns>The timeout header value.</returns>
    /// <exception cref="FormatException"></exception>
    public static TimeoutHeaderValue Parse(string input)
    {
        if (TryParse(input, out var parsedValue))
            return parsedValue;

        throw new FormatException("The Timeout-Header could not be parsed.");
    }

    /// <summary>
    /// Try to parse the input.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <param name="parsedValue">The timeout header value.</param>
    /// <returns>True on success, otherwise false.</returns>
    public static bool TryParse(string? input, [NotNullWhen(true)] out TimeoutHeaderValue? parsedValue)
    {
        parsedValue = null;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        var timeouts = new List<TimeSpan>();
        var values = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var value in values)
        {
            if (value.Equals("Infinite", StringComparison.OrdinalIgnoreCase))
            {
                timeouts.Add(TimeSpan.Zero);
                continue;
            }

            if (value.StartsWith("Second-", StringComparison.OrdinalIgnoreCase))
            {
                // Reject negative and oversized values instead of throwing (they used to surface as HTTP 500).
                if (long.TryParse(
                        value.Substring("Second-".Length),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var seconds) &&
                    seconds >= 0 &&
                    seconds <= MaxTimeoutSeconds)
                {
                    timeouts.Add(TimeSpan.FromSeconds(seconds));
                }
            }
        }

        if (timeouts.Count > 0)
        {
            parsedValue = new TimeoutHeaderValue(timeouts.ToArray());
            return true;
        }

        return false;
    }
}