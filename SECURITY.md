# Security Policy

## Supported Versions

Security fixes are applied to the latest release on the `main` branch and, when
practical, backported to the most recent tagged release. Older tags are not
actively supported unless a maintainer explicitly says otherwise in a release
note.

| Version | Supported          |
| ------- | ------------------ |
| latest `main` | :white_check_mark: |
| latest tag    | :white_check_mark: |
| older tags    | :x:                |

Check [`package.json`](Packages/com.farukcan.ratatui.unity/package.json) or
[GitHub releases](https://github.com/farukcan/ratatui-unity/releases) for the
current version.

## Reporting a Vulnerability

**Please do not file a public GitHub Issue for security vulnerabilities.**

Report them privately so maintainers can assess impact and ship a fix before
details are disclosed:

1. **Preferred:** [Open a private GitHub Security Advisory](https://github.com/farukcan/ratatui-unity/security/advisories/new)
   against this repository. Only repository maintainers and invited
   collaborators can see the report.
2. **Alternative:** Send a private message to the project maintainer via
   [GitHub](https://github.com/farukcan).

Include as much of the following as you can:

- A clear description of the vulnerability and its impact
- Steps to reproduce, or a minimal proof-of-concept
- Affected component (Rust FFI layer, C# bindings, native plugin binary,
  samples, build tooling, docs site, etc.)
- Affected platforms (Editor, Standalone, iOS, Android, WebGL, …)
- Your `ratatui-unity` version (git ref, tag, or `package.json` version)
- Any suggested mitigation or fix, if you have one

## What to Expect

- **Acknowledgement** within a few business days
- **Status updates** as the report is triaged and a fix is prepared
- **Coordinated disclosure** — we will agree on a disclosure timeline before
  publishing details or crediting you (if you want credit)

## Out of Scope

The following are generally **not** treated as security vulnerabilities for this
project:

- Bugs that only affect sample scenes or demo content, without a plausible
  exploit path in a typical game integration
- Crashes or visual glitches that require already-trusted, attacker-controlled
  game code calling the public API
- Issues in third-party dependencies that are already tracked upstream (please
  still tell us if you think ratatui-unity users are uniquely exposed)

When in doubt, report privately anyway — we would rather receive a report that
turns out to be low severity than miss a real issue.

## Safe Harbor

We support good-faith security research. We will not pursue legal action against
researchers who:

- Make a reasonable effort to avoid privacy violations, destruction of data,
  and service disruption
- Give us a reasonable window to fix the issue before public disclosure
- Do not exploit the vulnerability beyond what is necessary to demonstrate it

Thank you for helping keep ratatui-unity and its users safe.
