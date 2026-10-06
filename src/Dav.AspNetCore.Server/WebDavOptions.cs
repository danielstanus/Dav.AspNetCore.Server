namespace Dav.AspNetCore.Server;

public class WebDavOptions
{
    /// <summary>
    /// The default maximum size (1 MB) of an XML request body such as PROPFIND, PROPPATCH or LOCK.
    /// </summary>
    public const long DefaultMaxXmlRequestBodyBytes = 1024 * 1024;

    /// <summary>
    /// The default maximum size (500 MB) of a single uploaded resource.
    /// </summary>
    public const long DefaultMaxResourceSizeBytes = 500L * 1024 * 1024;

    /// <summary>
    /// Disallows propfind requests with depth set to infinity.
    /// </summary>
    public bool DisallowInfinityDepth { get; set; }

    /// <summary>
    /// Gets or sets the maximum size in bytes of an XML request body (PROPFIND, PROPPATCH, LOCK).
    /// </summary>
    public long MaxXmlRequestBodyBytes { get; set; } = DefaultMaxXmlRequestBodyBytes;

    /// <summary>
    /// Gets or sets the maximum size in bytes of a resource that can be uploaded with PUT.
    /// A value of <see langword="null"/> disables the limit.
    /// </summary>
    public long? MaxResourceSizeBytes { get; set; } = DefaultMaxResourceSizeBytes;

    /// <summary>
    /// Gets or sets the server name.
    /// </summary>
    public string? ServerName { get; set; }
    
    /// <summary>
    /// A value indicating whether the server header is disabled.
    /// </summary>
    public bool DisableServerName { get; set; }
    
    /// <summary>
    /// Gets or sets the maximum lock timeout.
    /// </summary>
    public TimeSpan? MaxLockTimeout { get; set; } = null;
    
    /// <summary>
    /// A value indicating whether web dav requires authentication.
    /// </summary>
    public bool RequiresAuthentication { get; set; }
}