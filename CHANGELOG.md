# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.2.0] - 2026-10-07

### Security

- **Authentication is no longer silently optional outside Development (M-01)**: the host now fails to start
  when the WebDAV endpoint is open in a non-Development environment unless
  `WebDavOptions.AllowAnonymousAccess` is explicitly set to `true`. When `RequiresAuthentication = true`,
  at least one authentication scheme must be registered. In Development the endpoint may stay open over HTTP
  (a warning is logged).
- **Malformed headers can no longer cause an HTTP 500 (M-02)**: negative or oversized `Timeout` values are
  rejected with `400`, `Destination` values such as `urn:...` and paths containing `:`/`|` are handled safely,
  and malformed Basic/Digest `Authorization` headers are ignored instead of throwing.
- **The `Server` header no longer includes the assembly version (M-04)**; the default value is
  `Dav.AspNetCore.Server`.
- **Lock timeouts default to one hour (M-05)**: `Infinite` and larger requests are clamped to
  `WebDavOptions.MaxLockTimeout` (1 hour by default) and `InMemoryLockManager` caps the number of locks
  (`AddInMemoryLocks(maxLocks)`, 10 000 by default), returning `507` when exhausted.
- **Duplicate properties no longer cause an HTTP 500 (M-06)**: `PROPPATCH`/`PROPFIND` requests that set or
  request the same property twice are handled (last value wins).
- **Header and log hardening (L-01, L-05)**: the Basic/Digest challenge `realm` is sanitized so it cannot
  inject headers, and the request method/path are stripped of control characters before logging.
- **Lock detection hardened (L-04)**: recursive and case-insensitive (Windows) path matching, trailing
  slashes normalized, and expired locks are ignored.
- **Lock owner size bounded (L-07)**: a `LOCK` body with an owner larger than 16 KB returns `400`.

### Changed

- `COPY`/`MOVE` now default to `Overwrite: T` as required by RFC 4918 (they used to return `412` when the
  destination existed).
- `GET`/`HEAD` now emit `Last-Modified`, `Content-Language` and a quoted `ETag`; the `Server` header no
  longer includes the version.

### Fixed

- `Content-Language`/`Last-Modified`/`ETag` were guarded by the content-type check (L-03).
- Basic/Digest challenge `realm` sanitization (L-01).
- Log injection via the request method/path (L-05).

### Added

- `WebDavOptions.AllowAnonymousAccess`.

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

[Unreleased]: https://github.com/danielstanus/Dav.AspNetCore.Server/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/danielstanus/Dav.AspNetCore.Server/releases/tag/v1.2.0
[1.1.0]: https://github.com/danielstanus/Dav.AspNetCore.Server/releases/tag/v1.1.0
[1.0.0]: https://github.com/danielstanus/Dav.AspNetCore.Server/releases/tag/v1.0.0
