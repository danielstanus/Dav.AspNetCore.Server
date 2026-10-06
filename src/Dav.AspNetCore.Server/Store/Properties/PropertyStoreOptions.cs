namespace Dav.AspNetCore.Server.Store.Properties;

public class PropertyStoreOptions
{
    /// <summary>
    /// A value indicating whether the property store will accept custom properties.
    /// </summary>
    public bool AcceptCustomProperties { get; set; }

    /// <summary>
    /// Validates the options. Called when the property store is registered so misconfiguration fails fast.
    /// </summary>
    internal virtual void Validate()
    {
    }
}