---
_layout: landing
---

# ratatui-unity

A Rust native plugin that renders [Ratatui](https://ratatui.rs) TUI widgets as RGBA pixel textures in Unity — for all platforms.

## Quick Links

- [Getting Started](articles/getting-started.md) — install the UPM package and render your first widget.
- [Architecture](articles/architecture.md) — how the Rust core and Unity C# bridge fit together.
- [C# API Reference](xref:RatatuiUnity) — public Unity-facing classes (`RatatuiTerminal`, `RatatuiRenderer`, builders).
- [Rust API Reference](rust/ratatui_unity/index.html) — internal Rust crate, for contributors.
- [Rust Contributor Guide](articles/rust-contributor.md) — building native binaries, FFI conventions.

## What it does

`ratatui-unity` lets you embed any Ratatui terminal UI inside a Unity scene as a `Texture2D`. Layout, widgets, styling, and input are driven by Ratatui in Rust; rasterization to RGBA pixels happens natively; Unity sees a texture it can blit anywhere.

## Supported Platforms

macOS · iOS · Windows · Linux · Android · WebGL
