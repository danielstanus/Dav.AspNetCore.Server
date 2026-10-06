# Security Policy

## Supported versions

Only the latest published release receives security fixes.

| Version | Supported          |
| ------- | ------------------ |
| 1.1.x   | :white_check_mark: |
| < 1.1   | :x:                |

## Reporting a vulnerability

Please **do not** open a public GitHub issue for security problems.

Use GitHub's private vulnerability reporting:

1. Go to the **Security** tab of this repository.
2. Click **Report a vulnerability**.
3. Describe the issue privately.

<!-- Optional: add a contact e-mail if you prefer it over GitHub advisories.
If you do, replace the line below and keep the rest.

Alternatively, e-mail: security@example.com
-->

Please include as much detail as possible:

- Affected version(s).
- A description of the issue and its impact.
- Steps to reproduce (a minimal proof of concept if possible).
- Any suggested fix or mitigation.

## What to expect

- Acknowledgement within **72 hours**.
- An initial assessment within **7 days**.
- We follow **coordinated disclosure**: we aim to release a fix within **90 days** and we will credit you in
  the release notes unless you prefer to stay anonymous.

## Scope

In scope:

- The `DCS.WebDav.AspNetCore.Server` package.
- The `DCS.WebDav.AspNetCore.Server.Extensions.*` packages.

Out of scope:

- Vulnerabilities in your own `IStore`, `ILockManager` or `IPropertyStore` implementations.
- Misconfiguration (for example, exposing `RootPath` through static files, or disabling authentication and
  TLS on a public endpoint).
- Issues that require an already-compromised host.
