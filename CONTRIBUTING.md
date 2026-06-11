# Contributing to ratatui-unity

Welcome, and thank you for taking the time to look at this guide. **ratatui-unity** lives or dies on its community — every bug report, doc tweak, sample, and PR helps move it forward, and contributions of all sizes are genuinely appreciated.

Whether you are fixing a typo, building a new widget, or porting the native plugin to a new platform: you are welcome here.

> **New here?** Skim the [Architecture](docs/articles/architecture.md) doc first, then check the [`good first issue`][good-first-issue] and [`help wanted`][help-wanted] labels on GitHub for friendly starting points.

[good-first-issue]: https://github.com/farukcan/ratatui-unity/issues?q=is%3Aissue+is%3Aopen+label%3A%22good+first+issue%22
[help-wanted]: https://github.com/farukcan/ratatui-unity/issues?q=is%3Aissue+is%3Aopen+label%3A%22help+wanted%22

---

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Where to Ask Questions](#where-to-ask-questions)
- [Reporting Bugs](#reporting-bugs)
- [Requesting Features](#requesting-features)
- [Reporting Security Issues](#reporting-security-issues)
- [Repository Layout](#repository-layout)
- [Development Environment Setup](#development-environment-setup)
- [Building](#building)
- [Running the Samples](#running-the-samples)
- [Pull Request Workflow](#pull-request-workflow)
- [Adding a Widget (End-to-End)](#adding-a-widget-end-to-end)
- [FFI Conventions](#ffi-conventions)
- [Code Style](#code-style)
- [Docs](#docs)
- [Recognition](#recognition)
- [License](#license)

---

## Code of Conduct

This project follows the [Contributor Covenant](https://www.contributor-covenant.org/) Code of Conduct. By participating, you agree to uphold it — see [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md). Report unacceptable behavior to the maintainers via the contact listed in that file.

The short version: **be kind, assume good faith, critique code not people.**

## Where to Ask Questions

Pick the channel that fits your question — please **do not** open a bug Issue just to ask "how do I do X?".

| Question type                       | Best place                                               |
|-------------------------------------|----------------------------------------------------------|
| "How do I…?" / general usage        | [GitHub Discussions][discussions]                        |
| "Is this a bug?" / I can repro it   | [GitHub Issues][issues] with the Bug Report template     |
| "Could the project do…?"            | [Discussions → Ideas][ideas] first, then a Feature Issue |
| Security vulnerability              | Privately — see [Reporting Security Issues](#reporting-security-issues) |

[discussions]: https://github.com/farukcan/ratatui-unity/discussions
[issues]: https://github.com/farukcan/ratatui-unity/issues
[ideas]: https://github.com/farukcan/ratatui-unity/discussions/categories/ideas

## Reporting Bugs

Before filing, please:

1. Search [open and closed Issues][issues] to confirm it is not already known.
2. Try reproducing on the latest `main` (or the latest tagged release).
3. Reduce the case to the smallest scene / script that still triggers it.

Open a Bug Report via [`.github/ISSUE_TEMPLATE/bug_report.md`][bug-template] and include:

- **Unity version** and target platform (Editor / Standalone / iOS / Android / WebGL).
- **ratatui-unity version** (git ref, tag, or `package.json` version).
- **Host OS** (relevant for native plugin issues).
- **Minimal repro** — ideally a small `RatatuiRenderer` subclass.
- **Expected vs. actual** behavior.
- **Logs**: Unity Console / Player log excerpt.
- **Screenshot** of the rendered texture for visual bugs.
- For build failures: full `cargo` / `build_all.sh` output and the failing target.

[bug-template]: .github/ISSUE_TEMPLATE/bug_report.md

## Requesting Features

Big or cross-cutting ideas (new widget, API change, build matrix change) deserve a quick design conversation **before** you write code.

1. **Search** existing Issues, Discussions, and the [`docs/articles/`](docs/articles/) folder — it may already be planned or intentionally out of scope.
2. **Open a thread** in [Discussions → Ideas][ideas] describing the use case, the proposed API, and any alternatives you considered.
3. Once the shape is agreed, convert it to an Issue using [`.github/ISSUE_TEMPLATE/feature_request.md`][feature-template] and link the discussion.
4. Open the PR once a maintainer has acknowledged the Issue.

Tiny improvements (one-line tweaks, doc fixes, obvious bug fixes) can skip the discussion — just open a PR.

[feature-template]: .github/ISSUE_TEMPLATE/feature_request.md

## Reporting Security Issues

**Please do not file public Issues for security vulnerabilities.** Follow the disclosure process in [`SECURITY.md`](SECURITY.md) so the report stays private until a fix is available.

---

## Repository Layout

```
ratatui-unity/
├── src/                                       # Rust crate (cdylib + staticlib)
│   ├── lib.rs                                 # Public C ABI exports (ratatui_*)
│   ├── terminal.rs                            # TerminalState, WidgetCommand
│   ├── commands.rs                            # Per-widget render dispatch
│   ├── renderer.rs                            # Cell buffer → RGB24
│   ├── font.rs                                # fontdue glyph cache
│   └── color.rs                               # Color → RGB
├── Packages/com.farukcan.ratatui.unity/       # UPM package
│   ├── Runtime/                               # C# bindings + high-level API
│   ├── Plugins/                               # Prebuilt native libs (per platform)
│   ├── Samples~/                              # UPM-importable samples
│   └── package.json
├── docs/                                      # docfx + handwritten articles
│   ├── articles/                              # Markdown guides
│   └── docfx.json
├── .github/                                   # Issue / PR templates, workflows
├── build_all.sh                               # Cross-compile native plugin
├── Cargo.toml
└── rust-toolchain.toml                        # Pins Rust channel
```

See [`docs/articles/architecture.md`](docs/articles/architecture.md) for the frame-flow diagram and [`docs/articles/rust-contributor.md`](docs/articles/rust-contributor.md) for FFI conventions.

## Development Environment Setup

### Prerequisites

| Area               | Tooling                                                                  |
|--------------------|--------------------------------------------------------------------------|
| Rust crate         | `rustup` (channel is pinned in `rust-toolchain.toml`)                    |
| Cross-compilation  | `cargo install cross`, `cargo install cargo-lipo`                        |
| iOS / macOS        | Xcode command-line tools (`lipo`, `xcodebuild`)                          |
| Android            | `ANDROID_NDK_HOME` env var pointing to a valid NDK                       |
| WebGL              | `EMSDK` env var pointing to an `emsdk` install                           |
| Unity              | Unity 2021.3 LTS or newer (matches `package.json` `unity` field)         |
| Docs site          | [docfx](https://dotnet.github.io/docfx/) on PATH                         |

### Fork & Clone

External contributors do not have write access to the main repo. The standard flow:

```bash
# 1. Fork via the GitHub UI:
#    https://github.com/farukcan/ratatui-unity → Fork

# 2. Clone YOUR fork (replace <your-user>):
git clone https://github.com/<your-user>/ratatui-unity.git
cd ratatui-unity

# 3. Add the upstream remote so you can pull in new changes:
git remote add upstream https://github.com/farukcan/ratatui-unity.git
git fetch upstream

# 4. Create a topic branch off the latest main:
git switch -c feat/sparkline-widget upstream/main

# …make your changes…

# 5. Push to your fork and open a PR against farukcan/ratatui-unity:main
git push -u origin feat/sparkline-widget
```

### Branch Naming

Use a short, kebab-cased prefix that signals intent:

| Prefix       | When to use                                |
|--------------|--------------------------------------------|
| `feat/…`     | New feature or public-API addition         |
| `fix/…`      | Bug fix                                    |
| `docs/…`     | Documentation only                         |
| `refactor/…` | Internal restructure, no behavior change   |
| `chore/…`    | Build, tooling, CI                         |
| `test/…`     | Tests or sample-only changes               |

Example: `fix/webgl-pixel-buffer-alignment`.

## Building

### Rust crate (host platform)

```bash
cargo build              # debug
cargo build --release    # release
cargo doc --no-deps      # render Rust API docs → target/doc/
```

### All Unity target platforms

`build_all.sh` cross-compiles the native plugin and drops binaries directly into `Packages/com.farukcan.ratatui.unity/Plugins/<Platform>/`.

```bash
./build_all.sh                       # everything
./build_all.sh macos                 # one
./build_all.sh ios android webgl     # subset
```

Read the comments at the top of `build_all.sh` for per-target prerequisites. CI (`.github/workflows/build.yml`) is the source of truth for the supported matrix.

## Running the Samples

1. Open the repo as a Unity project (it is already a valid Unity project layout — UPM resolves the package from the embedded path).
2. In **Window → Package Manager**, select **Ratatui Unity** → **Samples** → **Import** the sample you want (`BasicUsage`, `Console`, `Notepad`, `Profiler`).
3. Open the imported scene and press **Play**.

If you only changed C#, you do not need to rebuild the native plugin.

## Pull Request Workflow

1. **Discuss first** for non-trivial changes (see [Requesting Features](#requesting-features)). Drive-by typo fixes can skip this.
2. **Branch off `main`** using the [naming convention](#branch-naming). One concern per PR.
3. **Match existing style.** Rust: `cargo fmt`. C#: follow the surrounding file. Comments in English only.
4. **Test what you change.**
   - Rust: `cargo build --release` for the host target at minimum.
   - C#: import and run the relevant sample in Unity.
   - Cross-platform native changes: at least one non-host target via `build_all.sh`.
5. **Update docs.** If you changed a public API (FFI or C#), update both the doc-comments (`///`) and the relevant article under `docs/articles/`.
6. **Keep the diff surgical.** Do not reformat or "improve" code outside the scope of your change.
7. **Open the PR** against `main`. Fill in [`.github/PULL_REQUEST_TEMPLATE.md`][pr-template] — link the Issue, describe what changed and why, list the platforms you tested.
8. **Respond to review.** Push fixup commits; do not force-push your branch until the review converges (so reviewers can see what changed). A maintainer will squash on merge.

[pr-template]: .github/PULL_REQUEST_TEMPLATE.md

### Commit Messages

- Imperative subject line, ≤ 72 chars (`Add Sparkline widget binding`).
- One logical change per commit.
- Optional body: explain *why* the change was needed, not *what* the diff shows.
- Do **not** add AI tools (Claude, Copilot, etc.) as co-authors or committers.

## Adding a Widget (End-to-End)

The most common cross-cutting change. Steps:

1. Add a variant to `WidgetCommand` in `src/terminal.rs`.
2. Add the render arm in `src/commands.rs` (`render_all_commands`).
3. Add a `pub extern "C"` enqueue function in `src/lib.rs` — C-ABI-safe types only, document ownership in `///`.
4. Add a `[DllImport]` in `Runtime/RatatuiNative.cs`.
5. Add a high-level method on `RatatuiTerminal` (with `/// <summary>`).
6. If the widget has rich configuration, add a fluent builder (see `CanvasBuilder`, `ChartBuilder` for the pattern).
7. Add or extend a sample under `Packages/com.farukcan.ratatui.unity/Samples~/` that exercises it.
8. Update `docs/articles/widget-examples.md`.
9. Run `cargo doc --no-deps` and a docfx build (see [Docs](#docs)) to confirm both API references render.

## FFI Conventions

Hard rules at the Rust ↔ C# boundary:

- Every `pub extern "C"` function in `lib.rs` is `#[no_mangle]` and uses only C-ABI-safe types (`u32`, `*const u8`, opaque pointers).
- Strings cross as `*const u8 + len`, never `CString`.
- The pixel buffer is **owned by Rust**; C# borrows a pointer per frame and must copy before the next `BeginFrame()`.
- No `Vec<u8>` returned by value across the boundary.
- Panics must not unwind into C# — `panic = "abort"` is set on the release profile; keep it that way.
- Document ownership (who frees what) in the `///` doc-comment on each export.

`RatatuiTerminal.Dispose()` on the C# side is mandatory. Letting the C# wrapper get GC'd without `Dispose` leaks the Rust `TerminalState`.

## Code Style

### Rust

- `cargo fmt` is the formatter; do not hand-format.
- Prefer small, pure functions; avoid hidden global state outside of `TerminalState`.
- No `unwrap()` / `expect()` on paths reachable from FFI — convert to a recoverable error or a documented sentinel.
- Comments in English only, and only when the *why* is non-obvious.

### C#

- Match the surrounding file's bracing, naming, and field ordering.
- Public APIs get an XML `/// <summary>`. Internal helpers do not need one.
- No allocations in per-frame hot paths if you can help it (the renderer runs in `Update`).
- Use `IntPtr` and explicit `[DllImport]` declarations — no `unsafe` blocks in high-level API.

### General

- Keep changes surgical — touch only what the task requires.
- Do not refactor unrelated code, reformat unrelated files, or "improve" adjacent style.
- Do not add features, abstractions, or configuration beyond what was asked.

## Docs

The docs site lives under `docs/` and is built with docfx + the rendered Rust API docs.

```bash
# from repo root
cargo doc --no-deps
mkdir -p docs/rust && cp -r target/doc/* docs/rust/
cd docs && docfx metadata && docfx build && docfx serve _site
# open http://localhost:8080
```

CI publishes the site automatically on every push to `main` (`.github/workflows/docs.yml`). When you change a public API, update the relevant article under `docs/articles/` in the same PR.

## Recognition

Every merged contribution — code, docs, design, samples, translations, bug triage — is appreciated and credited. Contributors are listed automatically on the [GitHub contributors page][contributors]; significant work is also highlighted in [`CHANGELOG.md`](Packages/com.farukcan.ratatui.unity/CHANGELOG.md).

If you would like to be acknowledged under a different name or with a specific link, mention it in your PR description.

[contributors]: https://github.com/farukcan/ratatui-unity/graphs/contributors

## License

By contributing, you agree that your contributions are licensed under the MIT License (see [`Packages/com.farukcan.ratatui.unity/LICENSE`](Packages/com.farukcan.ratatui.unity/LICENSE)). JetBrains Mono ships under SIL Open Font License 1.1.
