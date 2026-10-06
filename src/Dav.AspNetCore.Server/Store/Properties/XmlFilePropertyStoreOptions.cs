namespace Dav.AspNetCore.Server.Store.Properties;

public class XmlFilePropertyStoreOptions : PropertyStoreOptions
{
    /// <summary>
    /// Gets or sets the root path where the properties will be shared.
    /// </summary>
    /// <remarks>
    /// This value is required. It previously defaulted to the root directory of the first fixed
    /// drive, which exposed the whole file system when it was left unconfigured.
    /// </remarks>
    public string RootPath { get; set; } = string.Empty;

    internal override void Validate()
    {
        if (string.IsNullOrWhiteSpace(RootPath))
            throw new InvalidOperationException(
                "The xml file property store root path must be configured via XmlFilePropertyStoreOptions.RootPath.");
    }
}