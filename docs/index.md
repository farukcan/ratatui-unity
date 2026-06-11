---
_layout: landing
---

# ratatui-unity

ratatui-unity is a Rust native plugin that brings [Ratatui](https://ratatui.rs)'s TUI ecosystem to Unity 3D game engine — for all platforms. 

<img width="958" height="598" alt="2026-06-10 at 22 51 59" src="https://github.com/user-attachments/assets/fe1dcbcc-ff08-43da-b380-72f3dc912968" />

Try WebGL **Demo** on your browser: [ratatui-unity-demo.farukcan.dev](https://ratatui-unity-demo.farukcan.dev/)

## Quick Links

- [Getting Started](articles/getting-started.md) — install the UPM package and render your first widget.
- [Architecture](articles/architecture.md) — how the Rust core and Unity C# bridge fit together.
- [Layout](articles/layout.md) — `Split`, `Constraint`, `Block`, `Inner`, area IDs.
- [Widget Examples](articles/widget-examples.md) — copy-pasteable snippets for every widget.
- [Input Handling](articles/input-handling.md) — keyboard, mouse, hover, `TerminalInput`, `TerminalTextArea`, `TerminalCommandInput`, mobile keyboard.
- [Terminal Apps](articles/terminal-apps.md) — scene-independent terminal apps: bootstrap, app list, open/close API.
- [Samples Overview](articles/samples-overview.md) — what ships in `Samples~/` and how each piece is wired.
  - [BasicUsage tabs demo](articles/samples-basic-usage.md) · [Developer Console](articles/samples-console.md) · [Notepad](articles/samples-notepad.md) · [Profiler](articles/samples-profiler.md)
- [C# API Reference](xref:RatatuiUnity) — public Unity-facing classes (`RatatuiTerminal`, `RatatuiRenderer`, builders).
- [Rust API Reference](rust/ratatui_unity/index.html) — internal Rust crate, for contributors.
- [Rust Contributor Guide](articles/rust-contributor.md) — building native binaries, FFI conventions.

## What it does

`ratatui-unity` lets you embed any Ratatui terminal UI inside a Unity scene as a `Texture2D`. Layout, widgets, styling, and input are driven by Ratatui in Rust; rasterization to RGB24 pixels happens natively; Unity sees a texture it can blit anywhere.

## Supported Platforms

macOS · iOS · Windows · Linux · Android · WebGL
