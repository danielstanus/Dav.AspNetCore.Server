# Contributing

Thanks for your interest in `DCS.WebDav.AspNetCore.Server`! This document explains how to build, test and
submit changes.

By participating you agree to the [Code of Conduct](CODE_OF_CONDUCT.md).

## Reporting bugs and security issues

- **Bugs / feature requests:** open a GitHub issue.
- **Security vulnerabilities:** do **not** open a public issue. Follow [SECURITY.md](SECURITY.md).

## Prerequisites

- **.NET 10 SDK** (or later).
- Windows, Linux or macOS.

## Build

```cmd
dotnet build src/Dav.AspNetCore.Server.sln -c Release
```

## Test

```cmd
dotnet test src/Dav.AspNetCore.Server.sln -c Release
```

All tests must pass. New behaviour (especially security fixes) should come with a regression test.

## Project layout

- `src/Dav.AspNetCore.Server` — core library (middleware, handlers, stores, locks, authentication).
- `src/Dav.AspNetCore.Server.Extensions.*` — SQL lock managers and property stores.
- `src/Dav.AspNetCore.Server.Tests` — xUnit tests.
- `examples/` — sample application.

## Making a change

1. Fork the repository and create a branch from `main`, e.g. `fix/range-infinite-loop` or `feat/xyz`.
2. Keep changes focused: one topic per pull request.
3. Follow the existing style: file-scoped namespaces, nullable reference types, XML doc comments on public APIs.
4. Prefer **additive** public API changes. If you must introduce a breaking change, document it in the
   [CHANGELOG](CHANGELOG.md) and in the README *Breaking changes* section.
5. Update the documentation (`README.md`, `CHANGELOG.md`) when behaviour, configuration or defaults change.
6. Make sure `dotnet build` and `dotnet test` are green.

## Pull requests

- Describe **what** and **why**, and link the related issue.
- Include the tests you added or updated.
- CI must pass.

## Commits

Use clear, imperative commit messages (e.g. `Fix Range request infinite loop`). Conventional Commits are
welcome but not required.

## License

By contributing you agree that your contributions are licensed under the [MIT License](LICENSE).
