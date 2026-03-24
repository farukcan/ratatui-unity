# Changelog

All notable changes to this package will be documented in this file.

## [0.1.0] - 2026-03-24

### Added
- Initial release
- Ratatui 0.30 native backend (TestBackend, no crossterm dependency)
- RGBA32 pixel buffer rendering via fontdue (JetBrains Mono embedded)
- Callback-based C API: `ratatui_create`, `ratatui_begin_frame`, `ratatui_end_frame`, widget commands
- Unity UPM package with Assembly Definition (`RatatuiUnity.Runtime`)
- C# high-level API: `RatatuiTerminal`, `RatatuiRenderer` MonoBehaviour
- Widgets: Block, Paragraph, List, Gauge, Tabs, Sparkline, Table
- Platform support: Windows, macOS (Universal), Linux, iOS (XCFramework), Android, WebGL
- `RatatuiRenderer` base class with virtual `BuildFrame` override point
- BasicUsage sample demonstrating all major widgets
- GitHub Actions CI/CD with matrix builds and automatic releases
