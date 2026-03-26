# ratatui-unity

A Rust native plugin that renders [Ratatui](https://ratatui.rs) TUI widgets as RGBA pixel textures in Unity — for all platforms.

## Repository Layout

```
ratatui-unity/
  Cargo.toml                     ← Rust crate (cdylib + staticlib)
  src/                           ← Rust source
    lib.rs                       ← C API entry points
    terminal.rs                  ← Terminal state & lifecycle
    commands.rs                  ← Widget command queue & layout
    renderer.rs                  ← Buffer → RGBA pixel pipeline
    font.rs                      ← fontdue font manager
    color.rs                     ← Ratatui Color → RGBA
  fonts/
    JetBrainsMono-Regular.ttf    ← Embedded default font (OFL)
  build_all.sh                   ← Cross-compile script
  .github/workflows/build.yml    ← CI/CD (matrix build + release)
  Packages/
    com.farukcan.ratatui.unity/  ← Unity Package Manager package
      package.json
      Runtime/                   ← C# scripts + .asmdef
      Plugins/                   ← Native binaries (generated)
      Samples~/BasicUsage/       ← Demo scene script
      link.xml                   ← IL2CPP stripping protection
```

## UPM Installation

Open **Window → Package Manager → + → Add package from git URL** and paste:

```
https://github.com/farukcan/ratatui-unity.git#latest
```

Or add to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.farukcan.ratatui.unity": "https://github.com/farukcan/ratatui-unity.git#latest"
  }
}
```

See [`Packages/com.farukcan.ratatui.unity/README.md`](Packages/com.farukcan.ratatui.unity/README.md) for full documentation.

## Building Native Binaries

```bash
# macOS only (no extra tools needed)
./build_all.sh macos

# All platforms
./build_all.sh
```

See `build_all.sh` for platform-specific prerequisites (cross, Android NDK, Emscripten).

## License

MIT — see [`Packages/com.farukcan.ratatui.unity/LICENSE`](Packages/com.farukcan.ratatui.unity/LICENSE).  
JetBrains Mono font: SIL Open Font License 1.1.
