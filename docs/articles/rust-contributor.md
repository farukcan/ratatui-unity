# Rust Contributor Guide

This guide is for working on the Rust crate itself (`src/`). End users of the UPM package don't need any of this.

## Toolchain

`rust-toolchain.toml` pins the channel — `rustup` will install the right one automatically when you `cargo build`.

## Building Locally

```bash
cargo build              # debug, host platform
cargo build --release    # release, host platform
cargo doc --no-deps      # render Rust API docs to target/doc/
```

## Cross-Compiling All Platforms

```bash
./build_all.sh           # all platforms (needs cross, Android NDK, Emscripten)
./build_all.sh macos     # just one
```

See the comments at the top of `build_all.sh` for prerequisites per target.

## FFI Conventions

- Every `pub extern "C"` function in `lib.rs` must:
  - take only C-ABI-safe types (`u32`, `*const u8`, opaque pointers),
  - be `#[no_mangle]`,
  - document ownership clearly in `///` — who frees what.
- Strings cross the boundary as `*const u8 + len`, never `CString`.
- The pixel buffer is owned by Rust; C# only borrows. Never return a `Vec<u8>` by value.
- Panics must not unwind into C# — `panic = "abort"` is set in release.

## Module Layout

| File          | Owns                                                    |
|---------------|---------------------------------------------------------|
| `lib.rs`      | Public C ABI (`ratatui_*` exports), nothing else        |
| `terminal.rs` | `Terminal` struct, frame lifecycle, command queue       |
| `commands.rs` | Widget enum + per-widget render dispatch                |
| `renderer.rs` | `hash_cells`, `rasterize_buffer`, dirty-rect logic      |
| `font.rs`     | `FontManager` (fontdue glyph cache)                     |
| `color.rs`    | `color_to_rgb`, `DEFAULT_FG`/`DEFAULT_BG` constants     |

## Adding a Widget

1. Add the widget variant to the command enum in `commands.rs`.
2. Add an `extern "C"` enqueue function in `lib.rs`.
3. Add a `DllImport` in `RatatuiNative.cs` plus a high-level method on `RatatuiTerminal`.
4. Document both sides (`///` and `/// <summary>`).
5. `cargo doc --no-deps` and `docfx build` (see [docs README](#building-the-docs-site)) should both pick it up automatically.

## Building the Docs Site

```bash
# from repo root
cargo doc --no-deps
mkdir -p docs/rust && cp -r target/doc/* docs/rust/
cd docs && docfx metadata && docfx build && docfx serve _site
# open http://localhost:8080
```

CI does this automatically on every push to `main` (`.github/workflows/docs.yml`).
