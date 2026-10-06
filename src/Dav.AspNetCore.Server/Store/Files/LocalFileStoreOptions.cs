namespace Dav.AspNetCore.Server.Store.Files;

public class LocalFileStoreOptions : StoreOptions
{
    /// <summary>
    /// Gets or sets the root path.
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
                "The local file store root path must be configured via LocalFileStoreOptions.RootPath.");
    }
}