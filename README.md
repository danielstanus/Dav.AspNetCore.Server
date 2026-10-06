<div align="center">

# DCS.WebDav.AspNetCore.Server

![.NET 10](https://img.shields.io/badge/.NET%2010-512BD4?style=for-the-badge&logo=.net&logoColor=white)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![WebDAV](https://img.shields.io/badge/WebDAV-RFC%204918-0A7EA4?style=for-the-badge)
[![NuGet](https://img.shields.io/nuget/v/DCS.WebDav.AspNetCore.Server?style=for-the-badge&logo=nuget&logoColor=white)](https://www.nuget.org/packages/DCS.WebDav.AspNetCore.Server)
![License](https://img.shields.io/badge/License-MIT-success?style=for-the-badge)
[![CI](https://img.shields.io/github/actions/workflow/status/danielstanus/Dav.AspNetCore.Server/ci.yml?branch=main&style=for-the-badge&label=CI)](https://github.com/danielstanus/Dav.AspNetCore.Server/actions/workflows/ci.yml)

**WebDAV (RFC 4918) server for ASP.NET Core. Add DAV endpoints to your app to list, read, write, move, copy and lock resources — and let Microsoft Office open and save documents directly.**

[Installation](#installation) •
[Getting started](#getting-started) •
[Locking](#locking) •
[Properties](#properties) •
[Authentication](#authentication) •
[Configuration](#configuration) •
[Office integration](#office-integration) •
[Hosting on IIS](#hosting-on-iis) •
[Extensions](#extensions) •
[Breaking changes](#breaking-changes) •
[Changelog](CHANGELOG.md) •
[Security](SECURITY.md) •
[Contributing](#contributing) •
[Releases](#releases) •
[Fork origin](#fork-origin)

</div>

---

## Features

- RFC 4918 compliant WebDAV server (`OPTIONS`, `GET`, `HEAD`, `PUT`, `DELETE`, `MKCOL`, `PROPFIND`, `PROPPATCH`, `COPY`, `MOVE`, `LOCK`, `UNLOCK`).
- Works with any registered authentication, and ships with **Basic** and **Digest**.
- Extensible: bring your own **store**, **lock manager** or **property store**.
- Office friendly: emits `MS-Author-Via: DAV` and a proper `Lock-Token`, so **Word / Excel / PowerPoint** can open and save documents over WebDAV.
- Targets **.NET 10** and runs on **Windows**, Linux and macOS.

## Requirements

- **.NET 10** (`net10.0`) or later.

## Installation

Install via the .NET CLI or your IDE's package manager:

```cmd
dotnet add package DCS.WebDav.AspNetCore.Server
```

The optional SQL providers are separate packages (see [Extensions](#extensions)).

## Getting started

Register the WebDAV services and add the middleware:

```csharp
using Dav.AspNetCore.Server;
using Dav.AspNetCore.Server.Store;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWebDav(davBuilder =>
{
    // expose a local folder
    davBuilder.AddLocalFiles(options =>
    {
        options.RootPath = @"C:\WebDavRoot\";
    });
});

var app = builder.Build();

// mount WebDAV under /dav
app.Map("/dav", davApp =>
{
    davApp.UseWebDav();
});

app.Run();
```

> `RootPath` is **required**: the store fails at startup if it is not set. WebDAV also caps uploads at
> **500 MB** and XML request bodies at **1 MB**. See [Configuration](#configuration) for every option and its
> default, and [Breaking changes](#breaking-changes) when upgrading.

## Locking

Office requires `LOCK`/`UNLOCK`. Start with the in-memory manager, or use a SQL one for multiple instances:

```csharp
builder.Services.AddWebDav(davBuilder =>
{
    davBuilder.AddInMemoryLocks();
    // or: AddSqliteLocks / AddSqlLocks / AddNpgsqlLocks
});
```

## Properties

Storing properties is a core part of DAV and is **required for Office** (it sends `PROPPATCH` with `Win32*` properties). Configure a property store:

```csharp
builder.Services.AddWebDav(davBuilder =>
{
    // one XML file per resource; do NOT expose this folder
    davBuilder.AddXmlFilePropertyStore(options =>
    {
        options.AcceptCustomProperties = true;
        options.RootPath = @"C:\WebDavMeta\";
    });
    // or: AddSqlitePropertyStore / AddSqlPropertyStore / AddNpgsqlPropertyStore
});
```

A "custom" property is one not computed by the DAV resource itself. With the local file store all properties are computed, so you can only **add** custom ones — which is exactly what `AcceptCustomProperties = true` enables.

## Authentication

```csharp
using Dav.AspNetCore.Server.Authentication;

builder.Services.AddAuthentication().AddBasic(options =>
{
    options.Realm = "My WebDAV";

    options.Events.OnAuthenticating = (context, cancellationToken) =>
    {
        // validate context.UserName / context.Password against your own store/API
        var isValid = context.UserName == "user" && context.Password == "secret";
        return Task.FromResult(isValid);
    };
});

builder.Services.AddWebDav(davBuilder =>
{
    davBuilder.RequiresAuthentication = true;
});

app.Map("/dav", davApp =>
{
    davApp.UseAuthentication(); // must run before UseWebDav
    davApp.UseWebDav();
});
```

> Always combine Basic/Digest with **HTTPS**.

## Configuration

Everything is configured through `WebDavOptions` (passed to `AddWebDav`) plus the per-store options.
These are the defaults and what you normally want to change.

### `WebDavOptions`

| Option | Default | Description |
|--------|---------|-------------|
| `RequiresAuthentication` | `false` | When `false` the endpoint is **open**. Set it to `true` and register an authentication scheme (`UseAuthentication` must run before `UseWebDav`). |
| `MaxResourceSizeBytes` | `500 MB` | Maximum size of a single uploaded resource (`PUT`); larger uploads return `413`. `null` disables the limit. |
| `MaxXmlRequestBodyBytes` | `1 MB` | Maximum size of an XML request body (`PROPFIND`, `PROPPATCH`, `LOCK`). Larger bodies are rejected. |
| `DisallowInfinityDepth` | `false` | Reject `PROPFIND` with `Depth: infinity`. Recommended for large trees. |
| `MaxLockTimeout` | `null` | Maximum lock timeout. `null` allows non-expiring (`Infinite`) locks; set e.g. `TimeSpan.FromHours(1)`. |
| `ServerName` / `DisableServerName` | `null` / `false` | `Server` response header. Set `DisableServerName = true` to hide the server name and version. |

### Local file store (`AddLocalFiles`)

| Option | Default | Description |
|--------|---------|-------------|
| `RootPath` | *(required)* | Absolute path exposed as the DAV root. **There is no default** — the app fails at startup if it is not set. |

### XML file property store (`AddXmlFilePropertyStore`)

| Option | Default | Description |
|--------|---------|-------------|
| `RootPath` | *(required)* | Folder for the `*.xml` sidecar property files. **Do not expose it** through the DAV root or the web. |
| `AcceptCustomProperties` | `false` | Allow clients to add custom (non-computed) properties, required by Office. |

### Authentication

| Option | Default | Description |
|--------|---------|-------------|
| `RequiresAuthentication` | `false` | See `WebDavOptions` above. |
| `BasicAuthenticationSchemeOptions.Realm` | `null` | Realm advertised in the `WWW-Authenticate` challenge. |
| `DigestAuthenticationSchemeOptions.Realm` | `null` | Falls back to the request host when not set. |
| `DigestAuthenticationSchemeOptions.Algorithm` | `"MD5"` | `MD5` (widest client support, e.g. Office) or `SHA-256`. |

> Always put WebDAV behind **HTTPS** and set `RequiresAuthentication = true`; Basic/Digest without TLS exposes credentials.

### Sizing uploads (Kestrel + WebDAV)

The library caps uploads at `MaxResourceSizeBytes` (500 MB). Kestrel has its own
`Limits.MaxRequestBodySize` (30 MB by default). To accept files larger than 500 MB, raise **both**:

```csharp
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = null); // or a concrete size
builder.Services.AddWebDav(dav => dav.MaxResourceSizeBytes = 2L * 1024 * 1024 * 1024); // 2 GB
```

> The effective limits are written to the log once at startup (`WebDAV configured: MaxResourceSizeBytes=...`),
> so you can always see whether uploads are capped, and at which size. The upload is streamed to disk, so
> this is a size limit, not memory usage.

### Complete example

```csharp
builder.Services.AddWebDav(dav =>
{
    dav.RequiresAuthentication = true;
    dav.DisallowInfinityDepth = true;
    dav.MaxLockTimeout = TimeSpan.FromHours(1);
    dav.MaxResourceSizeBytes = 2L * 1024 * 1024 * 1024; // 2 GB
    dav.MaxXmlRequestBodyBytes = 4 * 1024 * 1024;       // 4 MB
    dav.DisableServerName = true;

    dav.AddLocalFiles(o => o.RootPath = @"C:\WebDavRoot\");
    dav.AddInMemoryLocks();
    dav.AddXmlFilePropertyStore(o =>
    {
        o.RootPath = @"C:\WebDavMeta\";
        o.AcceptCustomProperties = true;
    });
});
```

## Office integration

The server is ready for Office: `OPTIONS` advertises `MS-Author-Via: DAV`, `LOCK` returns a `Lock-Token`, and `PUT` replaces the file. From a web page you can hand a document to desktop Word/Excel with the Office URI scheme:

```html
<a href="ms-word:ofe|u|https://your-server/dav/report.docx">Open in Word</a>
```

Notes:

- The document URL must be `http`/`https` (prefer **https**).
- Office may open WebDAV documents in **Protected View** (read-only). For a smooth experience, on the client machines add the host to **Trusted Sites** and enable **"Open documents read-write while browsing"** (deploy via GPO/Intune). See the [documentation](https://learn.microsoft.com/en-us/office/client-developer/office-uri-schemes) for the Office URI scheme.
- Without a property store, Office `PROPPATCH` of its `Win32*` properties returns `404` — configure one (see [Properties](#properties)).

## Hosting on IIS

IIS ships its **own WebDAV module** that intercepts DAV verbs (`PROPFIND`, `PROPPATCH`, `LOCK`, `UNLOCK`, `MKCOL`, `COPY`, `MOVE`, ...), so a WebDAV app hosted on IIS will return `404`/`405` or misbehave unless that module is removed. You must also explicitly allow the `OPTIONS` verb in Request Filtering.

Add this to the app's `web.config` (or configure the equivalents in IIS Manager):

```xml
<system.webServer>
  <security>
    <requestFiltering>
      <verbs>
        <remove verb="OPTIONS" />
        <add verb="OPTIONS" allowed="true" />
      </verbs>
    </requestFiltering>
  </security>
  <modules>
    <!-- Remove the IIS built-in WebDAV so it does not intercept DAV verbs -->
    <remove name="WebDAVModule" />
  </modules>
  <handlers>
    <remove name="WebDAV" />
    <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
  </handlers>
</system.webServer>
```

Equivalent in IIS Manager:

- **Modules** → remove `WebDAVModule`.
- **Handler Mappings** → remove `WebDAV`.
- **Request Filtering → HTTP Verbs** → add `OPTIONS` as allowed.
- The Application Pool should be **No Managed Code** (Integrated pipeline), as usual for ASP.NET Core.
- The ASP.NET Core handler must use `verb="*"` so every WebDAV verb reaches your app.

If you host the app under a virtual application/subpath, wrap the block in `<location path="." inheritInChildApplications="false">`.

> Symptom → cause: `405 Method Not Allowed` on `PROPFIND`/`LOCK` is almost always the IIS `WebDAVModule` not being removed; `OPTIONS` returning `404`/`403` is usually Request Filtering.

## Extensions

Distributed locks and properties on SQL databases (each has its own README and schema):

![SQLite](https://img.shields.io/badge/SQLite-003B57?style=for-the-badge&logo=sqlite&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL%20Server-CC2927?style=for-the-badge&logo=microsoftsqlserver&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4169E1?style=for-the-badge&logo=postgresql&logoColor=white)

- [Sqlite](https://www.nuget.org/packages/DCS.WebDav.AspNetCore.Server.Extensions.Sqlite) — `DCS.WebDav.AspNetCore.Server.Extensions.Sqlite`
- [SqlServer](https://www.nuget.org/packages/DCS.WebDav.AspNetCore.Server.Extensions.SqlServer) — `DCS.WebDav.AspNetCore.Server.Extensions.SqlServer`
- [PostgreSQL](https://www.nuget.org/packages/DCS.WebDav.AspNetCore.Server.Extensions.Npgsql) — `DCS.WebDav.AspNetCore.Server.Extensions.Npgsql`

## Breaking changes

Review these before upgrading from an earlier version of this fork.

### `RootPath` is now required (security fix)

`LocalFileStoreOptions.RootPath` and `XmlFilePropertyStoreOptions.RootPath` no longer default to the root
of the first fixed drive. You **must** set them explicitly, otherwise the application fails at startup with an
`InvalidOperationException`. This fixes a path-traversal issue where leaving `RootPath` unset exposed the
entire file system.

```csharp
davBuilder.AddLocalFiles(o => o.RootPath = @"C:\WebDavRoot\");
davBuilder.AddXmlFilePropertyStore(o => o.RootPath = @"C:\WebDavMeta\");
```

### Upload and XML size limits

- `PUT` is capped at **500 MB** (`WebDavOptions.MaxResourceSizeBytes`); larger uploads return `413`.
  Raise the value or set it to `null` to restore the previous unlimited behaviour.
- XML request bodies are capped at **1 MB** (`WebDavOptions.MaxXmlRequestBodyBytes`). Larger bodies are
  rejected (`PROPFIND` falls back to `allprop`; `PROPPATCH`/`LOCK` return `400`).

### Paths outside the store root are rejected

Requests whose path resolves outside `RootPath` (for example `/C:/...` or `/server/share/...`) now return
`403` instead of accessing the file system.

### `Range` requests

Unsatisfiable ranges now return `416` with `Content-Range: bytes */<length>`, and the number of bytes
returned is inclusive as required by RFC 7233 (`bytes=0-9` returns 10 bytes). Previously, a range larger
than the file could hang the request.

### Digest authentication is stricter

`Digest` now validates the `nonce`, `opaque` and nonce-count (`nc`) and rejects replayed requests, and it
requires `nc`/`cnonce` when `qop=auth`. Clients that do not send these will no longer authenticate. The
default algorithm remains `MD5` for compatibility; set `options.Algorithm = "SHA-256"` to require SHA-256.

## Releases

You can also consume the library **without NuGet.org**: every version is published as a GitHub Release with the `.nupkg` files attached.

- Releases: https://github.com/danielstanus/Dav.AspNetCore.Server/releases

Download the `.nupkg` files and use them as a local feed:

```cmd
:: put the .nupkg files in a folder, e.g. C:\Feeds\dav
dotnet nuget add source "C:\Feeds\dav" -n dav-release
dotnet add package DCS.WebDav.AspNetCore.Server --version 1.1.0
```

## Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) first. By participating you agree to
the [Code of Conduct](CODE_OF_CONDUCT.md). For security issues, see [SECURITY.md](SECURITY.md).

## Fork origin

This repository is a fork of [ThuCommix/Dav.AspNetCore.Server](https://github.com/ThuCommix/Dav.AspNetCore.Server), originally created by **Kevin Scholz** and licensed under **MIT**. This fork upgrades the project from .NET 7 to .NET 10 and is published on NuGet as **`DCS.WebDav.AspNetCore.Server`** (and its `...Extensions.*` packages).

The original copyright and MIT license are preserved. This fork is not affiliated with or endorsed by the original author.

### What this fork adds

- Migration to **.NET 10**.
- Fixes for running on **Windows**: platform-independent `Uri` handling (previously every WebDAV request except `OPTIONS` failed with HTTP 500).
- `Lock-Token` response header on `LOCK` (RFC 4918) so Office/Word can edit.
- `PUT` now truncates the file (previously left trailing bytes when overwriting with shorter content).
- `MS-Author-Via: DAV` header on `OPTIONS` for Office compatibility.
- **Security hardening (1.1.0)**: path-traversal fix in the local file and XML property stores, `RootPath` now required, Digest anti-replay (nonce/`nc`/opaque validation), `Range` and XML-body DoS fixes, `403` for paths outside the store root, and configurable upload/XML size limits. See [Breaking changes](#breaking-changes).
