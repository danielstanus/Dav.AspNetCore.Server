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
[Office integration](#office-integration) •
[Hosting on IIS](#hosting-on-iis) •
[Extensions](#extensions) •
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

> Set `builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = null);` to allow uploading large files.

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

## Releases

You can also consume the library **without NuGet.org**: every version is published as a GitHub Release with the `.nupkg` files attached.

- Releases: https://github.com/danielstanus/Dav.AspNetCore.Server/releases

Download the `.nupkg` files and use them as a local feed:

```cmd
:: put the .nupkg files in a folder, e.g. C:\Feeds\dav
dotnet nuget add source "C:\Feeds\dav" -n dav-release
dotnet add package DCS.WebDav.AspNetCore.Server --version 1.0.0
```

## Contributing

Feel free to open issues or submit pull requests.

## Fork origin

This repository is a fork of [ThuCommix/Dav.AspNetCore.Server](https://github.com/ThuCommix/Dav.AspNetCore.Server), originally created by **Kevin Scholz** and licensed under **MIT**. This fork upgrades the project from .NET 7 to .NET 10 and is published on NuGet as **`DCS.WebDav.AspNetCore.Server`** (and its `...Extensions.*` packages).

The original copyright and MIT license are preserved. This fork is not affiliated with or endorsed by the original author.

### What this fork adds

- Migration to **.NET 10**.
- Fixes for running on **Windows**: platform-independent `Uri` handling (previously every WebDAV request except `OPTIONS` failed with HTTP 500).
- `Lock-Token` response header on `LOCK` (RFC 4918) so Office/Word can edit.
- `PUT` now truncates the file (previously left trailing bytes when overwriting with shorter content).
- `MS-Author-Via: DAV` header on `OPTIONS` for Office compatibility.
