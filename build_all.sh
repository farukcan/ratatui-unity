#!/usr/bin/env bash
# build_all.sh — cross-compile ratatui_unity for all Unity platforms.
#
# Prerequisites (install once):
#   cargo install cross        # Docker-based cross-compiler
#   cargo install cargo-lipo   # iOS universal binary helper
#   rustup target add <see list below>
#
# Environment variables:
#   ANDROID_NDK_HOME   — path to Android NDK (required for Android builds)
#   EMSDK              — path to emsdk root (required for WebGL builds)
#
# Usage:
#   ./build_all.sh            # build all platforms
#   ./build_all.sh macos      # build only macOS
#   ./build_all.sh ios android webgl  # build selected platforms

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PLUGINS="$SCRIPT_DIR/Packages/com.farukcan.ratatui.unity/Plugins"
RELEASE="$SCRIPT_DIR/target"

if [[ $# -eq 0 ]]; then
  build_targets=(macos ios android linux windows webgl)
else
  build_targets=("$@")
fi

log() { echo "[build_all] $*"; }

# ── macOS ─────────────────────────────────────────────────────────────────────
build_macos() {
  log "=== macOS Universal Binary ==="
  rustup target add aarch64-apple-darwin x86_64-apple-darwin

  cargo build --release --target aarch64-apple-darwin
  cargo build --release --target x86_64-apple-darwin

  mkdir -p "$PLUGINS/macOS"
  lipo -create \
    "$RELEASE/aarch64-apple-darwin/release/libratatui_unity.dylib" \
    "$RELEASE/x86_64-apple-darwin/release/libratatui_unity.dylib" \
    -output "$PLUGINS/macOS/libratatui_unity.bundle"

  log "macOS: $PLUGINS/macOS/libratatui_unity.bundle"
}

# ── iOS ───────────────────────────────────────────────────────────────────────
build_ios() {
  log "=== iOS XCFramework ==="
  rustup target add aarch64-apple-ios x86_64-apple-ios aarch64-apple-ios-sim

  cargo build --release --target aarch64-apple-ios
  cargo build --release --target x86_64-apple-ios       # Simulator (Intel)
  cargo build --release --target aarch64-apple-ios-sim  # Simulator (Apple Silicon)

  # Combine device + simulator into XCFramework
  lipo -create \
    "$RELEASE/x86_64-apple-ios/release/libratatui_unity.a" \
    "$RELEASE/aarch64-apple-ios-sim/release/libratatui_unity.a" \
    -output "$RELEASE/libratatui_unity_sim.a"

  XCF="$PLUGINS/iOS/ratatui_unity.xcframework"
  rm -rf "$XCF"
  xcodebuild -create-xcframework \
    -library "$RELEASE/aarch64-apple-ios/release/libratatui_unity.a" \
    -library "$RELEASE/libratatui_unity_sim.a" \
    -output "$XCF"

  log "iOS: $XCF"
}

# ── Android ───────────────────────────────────────────────────────────────────
build_android() {
  log "=== Android (arm64-v8a + armeabi-v7a) ==="
  : "${ANDROID_NDK_HOME:?ANDROID_NDK_HOME is not set}"

  rustup target add aarch64-linux-android armv7-linux-androideabi x86_64-linux-android

  NDK_BIN="$ANDROID_NDK_HOME/toolchains/llvm/prebuilt"
  # Detect host OS for NDK prebuilt path
  case "$(uname -s)" in
    Darwin) HOST="darwin-x86_64" ;;
    Linux)  HOST="linux-x86_64"  ;;
    *)      HOST="linux-x86_64"  ;;
  esac
  NDK_BIN="$NDK_BIN/$HOST/bin"

  CARGO_TARGET_AARCH64_LINUX_ANDROID_LINKER="$NDK_BIN/aarch64-linux-android30-clang" \
    cargo build --release --target aarch64-linux-android

  CARGO_TARGET_ARMV7_LINUX_ANDROIDEABI_LINKER="$NDK_BIN/armv7a-linux-androideabi30-clang" \
    cargo build --release --target armv7-linux-androideabi

  CARGO_TARGET_X86_64_LINUX_ANDROID_LINKER="$NDK_BIN/x86_64-linux-android30-clang" \
    cargo build --release --target x86_64-linux-android

  mkdir -p \
    "$PLUGINS/Android/libs/arm64-v8a" \
    "$PLUGINS/Android/libs/armeabi-v7a" \
    "$PLUGINS/Android/libs/x86_64"

  cp "$RELEASE/aarch64-linux-android/release/libratatui_unity.so"     "$PLUGINS/Android/libs/arm64-v8a/"
  cp "$RELEASE/armv7-linux-androideabi/release/libratatui_unity.so"   "$PLUGINS/Android/libs/armeabi-v7a/"
  cp "$RELEASE/x86_64-linux-android/release/libratatui_unity.so"      "$PLUGINS/Android/libs/x86_64/"

  log "Android: $PLUGINS/Android/libs/"
}

# ── Linux ─────────────────────────────────────────────────────────────────────
build_linux() {
  log "=== Linux x86_64 (via cross) ==="
  cross build --release --target x86_64-unknown-linux-gnu

  mkdir -p "$PLUGINS/Linux/x86_64"
  cp "$RELEASE/x86_64-unknown-linux-gnu/release/libratatui_unity.so" \
     "$PLUGINS/Linux/x86_64/"

  log "Linux: $PLUGINS/Linux/x86_64/libratatui_unity.so"
}

# ── Windows ───────────────────────────────────────────────────────────────────
build_windows() {
  log "=== Windows x86_64 (via cross) ==="
  cross build --release --target x86_64-pc-windows-gnu

  mkdir -p "$PLUGINS/Windows/x86_64"
  cp "$RELEASE/x86_64-pc-windows-gnu/release/ratatui_unity.dll" \
     "$PLUGINS/Windows/x86_64/"

  log "Windows: $PLUGINS/Windows/x86_64/ratatui_unity.dll"
}

# ── WebGL (Emscripten) ────────────────────────────────────────────────────────
build_webgl() {
  log "=== WebGL (wasm32-unknown-emscripten) ==="
  : "${EMSDK:?EMSDK is not set}"
  # shellcheck source=/dev/null
  source "$EMSDK/emsdk_env.sh"

  rustup target add wasm32-unknown-emscripten

  EMCC_CFLAGS="-O3" cargo build --release --target wasm32-unknown-emscripten

  mkdir -p "$PLUGINS/WebGL"
  cp "$RELEASE/wasm32-unknown-emscripten/release/libratatui_unity.a" \
     "$PLUGINS/WebGL/"

  log "WebGL: $PLUGINS/WebGL/libratatui_unity.a"
}

# ── Dispatch ──────────────────────────────────────────────────────────────────
for platform in "${build_targets[@]}"; do
  case "$platform" in
    macos)   build_macos   ;;
    ios)     build_ios     ;;
    android) build_android ;;
    linux)   build_linux   ;;
    windows) build_windows ;;
    webgl)   build_webgl   ;;
    *)       log "Unknown platform: $platform (skip)" ;;
  esac
done

log "=== Done ==="
