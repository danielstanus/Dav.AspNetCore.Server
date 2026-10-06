# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.1.0] - 2026-10-06

### Security

- **Path traversal / root escape fixed** in `LocalFileStore` and `XmlFilePropertyStore`: every path is now
  resolved through a `StorePath` guard that rejects drive-letter, UNC and alternate-data-stream paths and
  verifies the result stays inside `RootPath`. Requests such as `/C:/...` or `//server/share/...` now return
  `403` instead of reading, writing or deleting files outside the root.
- **Digest authentication hardened**: nonces are stored with an expiry and validated, `opaque` and the client
  response are compared in constant time, the nonce-count (`nc`) is enforced (replayed requests are rejected),
  and `realm`/`uri` are validated. Added `DigestAuthenticationSchemeOptions.Algorithm` (`MD5` by default,
  `SHA-256` supported).
- **`Range` request denial-of-service fixed**: ranges are clamped to the resource length and an unsatisfiable
  range returns `416` instead of looping forever.
- **XML request bodies are bounded** by `WebDavOptions.MaxXmlRequestBodyBytes` (1 MB default) and parsed with
  DTD prohibited and no external resolver.
- **Upload size is capped** by `WebDavOptions.MaxResourceSizeBytes` (500 MB default, `null` to disable);
  oversized `PUT` requests return `413` and the partially written file is removed.

### Changed

- `LocalFileStoreOptions.RootPath` and `XmlFilePropertyStoreOptions.RootPath` are now **required**. The
  application fails at startup when they are not configured (previously they defaulted to the root of the
  first fixed drive).
- Malformed `Authorization` headers (Basic/Digest) no longer produce an HTTP 500.
- The default `Server` header now reports `1.1.0`.
- The effective upload/XML limits are logged once at startup.

### Added

- `WebDavOptions.MaxResourceSizeBytes` and `WebDavOptions.MaxXmlRequestBodyBytes`.
- `DigestAuthenticationSchemeOptions.Algorithm`.
- `DavStatusCode.RequestEntityTooLarge` (413) and `DavStatusCode.RequestedRangeNotSatisfiable` (416).

### Breaking changes

See the *Breaking changes* section of the README. The user-visible ones are: `RootPath` required, upload/XML
size limits, `403` for paths outside the store root, and the stricter Digest validation.

## [1.0.0] - 2026-10-05

First packaged release of this fork (migration to .NET 10).

- RFC 4918 WebDAV verbs: `OPTIONS`, `GET`, `HEAD`, `PUT`, `DELETE`, `MKCOL`, `PROPFIND`, `PROPPATCH`,
  `COPY`, `MOVE`, `LOCK`, `UNLOCK`.
- Basic and Digest authentication.
- Local file store, in-memory and SQL lock managers, XML and SQL property stores.

[Unreleased]: https://github.com/danielstanus/Dav.AspNetCore.Server/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/danielstanus/Dav.AspNetCore.Server/releases/tag/v1.1.0
[1.0.0]: https://github.com/danielstanus/Dav.AspNetCore.Server/releases/tag/v1.0.0
