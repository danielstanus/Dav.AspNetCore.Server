using System;

namespace Dav.AspNetCore.Server.Store;

public class StoreOptions
{
    /// <summary>
    /// Validates the options. Called when the store is registered so misconfiguration fails fast.
    /// </summary>
    internal virtual void Validate()
    {
    }
}
