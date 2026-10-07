using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;

namespace Dav.AspNetCore.Server.Http.Headers;

public class DestinationHeaderValue
{
    /// <summary>
    /// Initializes a new <see cref="DestinationHeaderValue"/> class.
    /// </summary>
    /// <param name="destination">The destination.</param>
    public DestinationHeaderValue(Uri destination)
    {
        ArgumentNullException.ThrowIfNull(destination, nameof(destination));
        Destination = destination;
    }
    
    /// <summary>
    /// Gets the destination uri.
    /// </summary>
    public Uri Destination { get; }
    
    /// <summary>
    /// Parses the input.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <returns>The destination header value.</returns>
    /// <exception cref="FormatException"></exception>
    public static DestinationHeaderValue Parse(string input)
    {
        if (TryParse(input, out var parsedValue))
            return parsedValue;

        throw new FormatException("The Destination-Header could not be parsed.");
    }

    /// <summary>
    /// Try to parse the input.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <param name="parsedValue">The destination header value.</param>
    /// <returns>True on success, otherwise false.</returns>
    public static bool TryParse(string? input, [NotNullWhen(true)] out DestinationHeaderValue? parsedValue)
    {
        parsedValue = null;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        if (Uri.TryCreate(input, UriKind.Absolute, out var absoluteUri))
        {
            // Absolute uri, keep the path only so it matches the store resource uris.
            input = absoluteUri.LocalPath;
        }

        // Ensure the value is rooted before it reaches PathString (some absolute uris such as
        // "urn:uuid:..." have a LocalPath that does not start with '/' and would otherwise throw).
        if (!input.StartsWith("/", StringComparison.Ordinal))
            input = $"/{input}";

        var path = new PathString(input);
        if (!path.HasValue)
            return false;

        parsedValue = new DestinationHeaderValue(path.ToUri());
        return true;
    }
}