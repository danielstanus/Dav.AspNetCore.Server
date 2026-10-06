using Dav.AspNetCore.Server;
using Dav.AspNetCore.Server.Authentication;
using Dav.AspNetCore.Server.Locks;
using Dav.AspNetCore.Server.Store;

var builder = WebApplication.CreateBuilder(args);

// Optional: allow uploads larger than the Kestrel default (30 MB).
// WebDAV still enforces WebDavOptions.MaxResourceSizeBytes (500 MB by default).
// builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = null);

// --- Optional authentication (uncomment and set RequiresAuthentication = true) ---
// builder.Services.AddAuthentication().AddBasic(options =>
// {
//     options.Realm = "My WebDAV";
//     options.Events.OnAuthenticating = (context, cancellationToken) =>
//     {
//         var isValid = context.UserName == "Demo" && context.Password == "password";
//         return Task.FromResult(isValid);
//     };
// });
//
// builder.Services.AddAuthentication().AddDigest(options =>
// {
//     options.Realm = "My WebDAV";
//     options.Events.OnPasswordRequested = (context, cancellationToken) =>
//         Task.FromResult<string?>(context.UserName == "Demo" ? "password" : null);
// });

// Folders exposed over DAV and used for the sidecar property files.
var rootPath = Path.Combine(builder.Environment.ContentRootPath, "davdata");
var metaPath = Path.Combine(builder.Environment.ContentRootPath, "davmeta");
Directory.CreateDirectory(rootPath);
Directory.CreateDirectory(metaPath);

builder.Services.AddWebDav(davBuilder =>
{
    // davBuilder.RequiresAuthentication = true;

    // The root path is required (the store fails at startup when it is not set).
    davBuilder.AddLocalFiles(options =>
    {
        options.RootPath = rootPath;
    });

    // Required by Office (LOCK/UNLOCK). Use a SQL lock manager for multiple instances.
    davBuilder.AddInMemoryLocks();

    // Required by Office (PROPPATCH of Win32* properties). Do NOT expose this folder.
    davBuilder.AddXmlFilePropertyStore(options =>
    {
        options.RootPath = metaPath;
        options.AcceptCustomProperties = true;
    });

    // Uploads are capped at 500 MB by default. Raise it or set it to null for no limit.
    // davBuilder.MaxResourceSizeBytes = 2L * 1024 * 1024 * 1024; // 2 GB
});

var app = builder.Build();

// mount WebDAV under /dav
app.Map("/dav", davApp =>
{
    // davApp.UseAuthentication(); // must run before UseWebDav
    davApp.UseWebDav();
});

app.Run();
