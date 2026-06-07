---
_layout: landing
---

# ratatui-unity

A Rust native plugin that renders [Ratatui](https://ratatui.rs) TUI widgets as RGBA pixel textures in Unity — for all platforms.

## Quick Links

- [Getting Started](articles/getting-started.md) — install the UPM package and render your first widget.
- [Architecture](articles/architecture.md) — how the Rust core and Unity C# bridge fit together.
- [Layout](articles/layout.md) — `Split`, `Constraint`, `Block`, `Inner`, area IDs.
- [Widget Examples](articles/widget-examples.md) — copy-pasteable snippets for every widget.
- [Input Handling](articles/input-handling.md) — keyboard, mouse, hover, area hit-testing, `TerminalInput`.
- [Terminal Apps](articles/terminal-apps.md) — scene-independent terminal apps: bootstrap, app list, open/close API.
- [Samples Overview](articles/samples-overview.md) — what ships in `Samples~/` and how each piece is wired.
  - [BasicUsage tabs demo](articles/samples-basic-usage.md) · [Developer Console](articles/samples-console.md) · [Notepad](articles/samples-notepad.md)
- [C# API Reference](xref:RatatuiUnity) — public Unity-facing classes (`RatatuiTerminal`, `RatatuiRenderer`, builders).
- [Rust API Reference](rust/ratatui_unity/index.html) — internal Rust crate, for contributors.
- [Rust Contributor Guide](articles/rust-contributor.md) — building native binaries, FFI conventions.

## What it does

`ratatui-unity` lets you embed any Ratatui terminal UI inside a Unity scene as a `Texture2D`. Layout, widgets, styling, and input are driven by Ratatui in Rust; rasterization to RGBA pixels happens natively; Unity sees a texture it can blit anywhere.

## Supported Platforms

macOS · iOS · Windows · Linux · Android · WebGL
