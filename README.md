[![CI](https://github.com/danielstanus/Dav.AspNetCore.Server/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danielstanus/Dav.AspNetCore.Server/actions/workflows/ci.yml)
[![MIT License](https://img.shields.io/static/v1?label=License&message=MIT&color=success)](https://github.com/danielstanus/Dav.AspNetCore.Server/blob/main/LICENSE)
[![Nuget](https://img.shields.io/nuget/v/DanielStanus.WebDav.AspNetCore.Server)](https://www.nuget.org/packages/DanielStanus.WebDav.AspNetCore.Server/)

# WebDAV for ASP.NET Core

This package (`DanielStanus.WebDav.AspNetCore.Server`) is a fork of [Dav.AspNetCore.Server](https://github.com/ThuCommix/Dav.AspNetCore.Server), a WebDAV implementation based on <a href="http://www.webdav.org/specs/rfc4918.html">RFC 4918</a>.
It allows you to easily integrate DAV functionality into your ASP.NET Core application.

## Features
- RFC 4918 compliant
- Supports any registered authentication, but also ships with Basic and Digest authentication
- Extensible infrastructure which lets you design your own store or locking providers

## Requirements

This library targets **.NET 10** (`net10.0`).

> **Migration note:** the project was upgraded from **.NET 7** to **.NET 10**.
> All projects and the CI/CD workflows now target .NET 10, and the NuGet dependencies
> (`Microsoft.Data.Sqlite`, `Microsoft.Data.SqlClient`, `Npgsql`,
> `Microsoft.Extensions.*`, `xunit`, etc.) were updated to their .NET 10 compatible versions.

## Installation

Install DanielStanus.WebDav.AspNetCore.Server via dotnet cli or through the package manager provided by your favorite IDE.

```cmd
> dotnet add package DanielStanus.WebDav.AspNetCore.Server
```
## Getting started

In order to enable WebDAV in your project you need to add the following service registrations and middlewares:

```csharp
using Dav.AspNetCore.Server;
using Dav.AspNetCore.Server.Store;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWebDav(davBuilder =>
{
    // add the local files store with a mount point
    davBuilder.AddLocalFiles(options =>
    {
        options.RootPath = "/tmp/";
    });
});

var app = builder.Build();

app.Map("/dav", davApp =>
{
    davApp.UseWebDav();
});

app.Run();
```

## Add locking support

There are different types of locking implementations available. 
If you need something simple you can start out with the in memory lock implementation:

```csharp
builder.Services.AddWebDav(davBuilder =>
{
    [...]
    davBuilder.AddInMemoryLocks();
});
```

In case you need something more distributed you can check out the other sql based implementations:
- [Sqlite](src/Dav.AspNetCore.Server.Extensions.Sqlite/README.md)
- [SqlServer](src/Dav.AspNetCore.Server.Extensions.SqlServer/README.md)
- [PostgreSQL](src/Dav.AspNetCore.Server.Extensions.Npgsql/README.md)

## Accepting properties

Storing (custom) properties is a crucial part of DAV. To start accepting properties you need to configure
a property store. Like previously mentioned in the locking section there are different implementations available:

```csharp
builder.Services.AddWebDav(davBuilder =>
{
    [...]
    
    // there will be a xml file containing properties for each resource made available
    // it's important to not expose this folder
    
    davBuilder.AddXmlFilePropertyStore(options =>
    {
        options.AcceptCustomProperties = true;
        options.RootPath = "/tmp_meta/";
    });
});
```

You may ask: what exactly is a "custom" property; A custom property is a property not made available by the
dav resource itself, it can be arbitrary data. Since this example uses the local file store, all properties
are computed and thus can't be changed which only leaves us with adding additional properties. On different
dav resources with normal properties (not protected and not calculated) you can change them without having
`AcceptCustomProperties = true`.

Different sql based implementations are available here:
- [Sqlite](src/Dav.AspNetCore.Server.Extensions.Sqlite/README.md)
- [SqlServer](src/Dav.AspNetCore.Server.Extensions.SqlServer/README.md)
- [PostgreSQL](src/Dav.AspNetCore.Server.Extensions.Npgsql/README.md)

## Contributing
Feel free to open issues or submit pullrequests.

## Fork origin

This repository is a fork of [ThuCommix/Dav.AspNetCore.Server](https://github.com/ThuCommix/Dav.AspNetCore.Server),
originally created by **Kevin Scholz** and licensed under **MIT**. This fork upgrades the project from .NET 7 to .NET 10
and is published on NuGet as **`DanielStanus.WebDav.AspNetCore.Server`** (and its `...Extensions.*` packages).

The original copyright and MIT license are preserved. This fork is not affiliated with or endorsed by the original author.

### What this fork adds

- Migration to **.NET 10**.
- Fixes for running on **Windows**: platform-independent `Uri` handling (previously every WebDAV request except `OPTIONS` failed with HTTP 500).
- `Lock-Token` response header on `LOCK` (RFC 4918) so Office/Word can edit.
- `PUT` now truncates the file (previously left trailing bytes when overwriting with shorter content).
- `MS-Author-Via: DAV` header on `OPTIONS` for Office compatibility.