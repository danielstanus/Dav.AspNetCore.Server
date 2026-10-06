using Microsoft.AspNetCore.Authentication;

namespace Dav.AspNetCore.Server.Authentication;

public class DigestAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// Gets or sets the digest authentication events.
    /// </summary>
    public new DigestAuthenticationEvents Events { get; } = new();
    
    /// <summary>
    /// Gets or sets the realm.
    /// </summary>
    public string? Realm { get; set; }

    /// <summary>
    /// Gets or sets the digest algorithm. Supported values are <c>MD5</c> (default, required by
    /// many legacy clients such as Office) and <c>SHA-256</c>.
    /// </summary>
    public string Algorithm { get; set; } = "MD5";
}