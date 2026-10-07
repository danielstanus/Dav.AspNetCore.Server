using Dav.AspNetCore.Server;
using Dav.AspNetCore.Server.Authentication;
using Dav.AspNetCore.Server.Locks;
using Dav.AspNetCore.Server.Store;

var builder = WebApplication.CreateBuilder(args);

// Optional: allow uploads larger than the Kestrel default (30 MB).
// WebDAV still enforces WebDavOptions.MaxResourceSizeBytes (500 MB by default).
// builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = null);

// ---------------------------------------------------------------------------
// Optional authentication.
// Uncomment this block, then set davBuilder.RequiresAuthentication = true and
// uncomment davApp.UseAuthentication() below.
// ---------------------------------------------------------------------------
// builder.Services.AddAuthentication(options =>
// {
//     // The WebDAV middleware challenges the default scheme, so it must be configured.
//     options.DefaultScheme = BasicAuthenticationDefaults.AuthenticationScheme;
//     options.DefaultChallengeScheme = BasicAuthenticationDefaults.AuthenticationScheme;
// }).AddBasic(options =>
// {
//     options.Realm = "My WebDAV";
//     options.Events.OnAuthenticating = (context, cancellationToken) =>
//     {
//         var isValid = context.UserName == "Demo" && context.Password == "password";
//         return Task.FromResult(isValid);
//     };
// });
//
// // Or Digest (add SHA-256 with options.Algorithm = "SHA-256"):
// builder.Services.AddAuthentication(options =>
// {
//     options.DefaultScheme = DigestAuthenticationDefaults.AuthenticationScheme;
//     options.DefaultChallengeScheme = DigestAuthenticationDefaults.AuthenticationScheme;
// }).AddDigest(options =>
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
    // Enable authentication (see the commented AddAuthentication block above).
    // davBuilder.RequiresAuthentication = true;

    // The sample is intentionally open. Outside Development the host fails to start unless anonymous
    // access is explicitly allowed here (or authentication is enabled above). When you enable
    // authentication, remove this line: RequiresAuthentication and AllowAnonymousAccess are exclusive.
    davBuilder.AllowAnonymousAccess = true;

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

    // Recommended hardening:
    davBuilder.DisallowInfinityDepth = true; // reject the expensive PROPFIND with Depth: infinity
    // davBuilder.MaxLockTimeout = TimeSpan.FromHours(1); // default: locks expire after one hour
    // davBuilder.DisableServerName = true;              // do not send the Server header
});

var app = builder.Build();

// mount WebDAV under /dav
app.Map("/dav", davApp =>
{
    // Must run before UseWebDav when authentication is enabled.
    // davApp.UseAuthentication();
    davApp.UseWebDav();
});

app.Run();
