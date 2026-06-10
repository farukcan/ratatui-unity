# ratatui-unity

[![Build Native Plugin](https://github.com/farukcan/ratatui-unity/actions/workflows/build.yml/badge.svg)](https://github.com/farukcan/ratatui-unity/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](Packages/com.farukcan.ratatui.unity/LICENSE)
[![Rust Edition 2021](https://img.shields.io/badge/Rust-2021_Edition-orange?logo=rust&logoColor=white)](https://www.rust-lang.org/)
[![ratatui 0.30](https://img.shields.io/badge/ratatui-0.30-blue?logo=rust)](https://ratatui.rs)
[![Unity](https://img.shields.io/badge/Unity-2021%2B-black?logo=unity&logoColor=white)](https://unity.com)
[![UPM](https://img.shields.io/badge/UPM-git--url-blue?logo=unity&logoColor=white)](https://github.com/farukcan/ratatui-unity.git#latest)
[![GitHub stars](https://img.shields.io/github/stars/farukcan/ratatui-unity?style=social)](https://github.com/farukcan/ratatui-unity/stargazers)
[![GitHub forks](https://img.shields.io/github/forks/farukcan/ratatui-unity?style=social)](https://github.com/farukcan/ratatui-unity/network/members)
[![GitHub last commit](https://img.shields.io/github/last-commit/farukcan/ratatui-unity)](https://github.com/farukcan/ratatui-unity/commits/main)
[![GitHub issues](https://img.shields.io/github/issues/farukcan/ratatui-unity)](https://github.com/farukcan/ratatui-unity/issues)

![Platform macOS](https://img.shields.io/badge/platform-macOS-lightgrey?logo=apple&logoColor=white)
![Platform iOS](https://img.shields.io/badge/platform-iOS-lightgrey?logo=apple&logoColor=white)
![Platform Windows](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows&logoColor=white)
![Platform Linux](https://img.shields.io/badge/platform-Linux-FCC624?logo=linux&logoColor=black)
![Platform Android](https://img.shields.io/badge/platform-Android-3DDC84?logo=android&logoColor=white)
![Platform WebGL](https://img.shields.io/badge/platform-WebGL-990000?logo=webgl&logoColor=white)

ratatui-unity is a Rust native plugin that brings [Ratatui](https://ratatui.rs)'s TUI ecosystem to Unity 3D game engine — for all platforms. 

<img width="958" height="598" alt="2026-06-10 at 22 51 59" src="https://github.com/user-attachments/assets/fe1dcbcc-ff08-43da-b380-72f3dc912968" />

Try WebGL **Demo** on your browser: [ratatui-unity-demo.farukcan.dev](https://ratatui-unity-demo.farukcan.dev/)

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
