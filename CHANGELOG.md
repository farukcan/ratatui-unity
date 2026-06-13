# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.1] - 2026-06-14

Internal-only release. The C ABI surface is unchanged; no host-side code changes
are required to upgrade.

### Changed
- Split the monolithic `lib.rs` (≈1125 lines) into a modular `ffi` layer:
  `lifecycle`, `layout`, `style`, `widgets`, `builders`, and shared `util`
  helpers, re-exported from `lib.rs`.
- Split the monolithic `commands.rs` (≈675 lines) into per-widget modules under
  `commands/`: `calendar`, `canvas`, `chart`, `decode`, `layout`, and `table`.
- Confined every raw-pointer dereference behind documented `util` helpers
  (`state_mut`, `state_ref`, `cstr_to_string`, `slice_from`, `slice_mut_from`),
  keeping the `extern "C"` entry points free of inline `unsafe`.

### Added
- Clippy linting configuration and a GitHub Actions `lint` workflow.

## [0.1.0]

- Initial release: C ABI wrapper around `ratatui` that renders terminal UIs to
  RGB24 pixel buffers for embedding in game engines such as Unity.
