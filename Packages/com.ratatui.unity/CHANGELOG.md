# Changelog

All notable changes to this package will be documented in this file.

## [0.2.0] - 2026-03-25

### Added
- **New widgets**: `BarChart`, `LineGauge`, `Scrollbar`, `Calendar` (monthly, requires `widget-calendar` Rust feature), `TableEx` (column constraints + row selection)
- **StyledParagraph builder** (`BeginStyledParagraph`) — per-span fg/bg color and text modifiers
- **Chart builder** (`BeginChart`) — labeled X/Y axes, multiple datasets, Braille/Dot/Block/HalfBlock markers
- **Canvas builder** (`BeginCanvas`) — world map, lines, circles, rectangles, text labels, point clouds, layer flush
- **New enums**: `Marker`, `ScrollbarOrientation`, `Modifier` (flags), `MapResolution`
- **`ITab` interface** — clean separation of demo tabs with `Title`, `Update(dt)`, `OnInput(key)`, `Render(term, area)`
- **Combined 8-tab demo** (`RatatuiDemo`) replacing the old single-widget demo: Dashboard, Servers, Colors, About, Recipe, Email, Traceroute, Weather
- **`SetStyle` overload** accepting the new `Modifier` flags enum
- **`RatatuiTerminal.Inner()`** FFI binding exposed as a public method
- Rust: all data types and widget commands consolidated in `terminal.rs` to avoid circular module dependencies
- Rust: List widget now uses `render_stateful_widget + ListState` for proper selection highlighting

### Changed
- `Cargo.toml`: added `features = ["widget-calendar"]` to ratatui dependency and explicit `time = "0.3"` dependency
- `src/terminal.rs` now owns `WidgetCommand` enum + all shared data types (`AxisInfo`, `DatasetInfo`, `SpanInfo`, `CanvasShape`) + pending builder state
- `src/commands.rs` is now a pure render module — no bidirectional imports

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
