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
    /// Gets or sets the maximum lock timeout. Requests for "Infinite" or for a longer timeout are
    /// clamped to this value. Defaults to one hour so locks cannot block a resource forever.
    /// Set to <see langword="null"/> to allow non-expiring locks again.
    /// </summary>
    public TimeSpan? MaxLockTimeout { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the Content-Security-Policy header sent with every response. Defaults to
    /// <c>sandbox</c>, which stops an uploaded HTML file from running scripts or reaching the origin
    /// of the application (stored XSS mitigation). Set to <see langword="null"/> or an empty string
    /// to remove the header.
    /// </summary>
    public string? ContentSecurityPolicy { get; set; } = "sandbox";
    
    /// <summary>
    /// A value indicating whether web dav requires authentication.
    /// </summary>
    /// <remarks>
    /// When <see langword="true"/> at least one authentication scheme must be registered, otherwise
    /// the host fails to start. When <see langword="false"/> the endpoint is open: that is only allowed
    /// automatically in the Development environment, or in any environment when
    /// <see cref="AllowAnonymousAccess"/> is explicitly set to <see langword="true"/>.
    /// </remarks>
    public bool RequiresAuthentication { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether anonymous (unauthenticated) access is explicitly allowed.
    /// </summary>
    /// <remarks>
    /// Anonymous access is always allowed in the Development environment (a warning is logged). In any
    /// other environment the host fails to start unless this is set to <see langword="true"/>, so an open
    /// endpoint must be an explicit decision and not an accidental default.
    /// </remarks>
    public bool AllowAnonymousAccess { get; set; }
}