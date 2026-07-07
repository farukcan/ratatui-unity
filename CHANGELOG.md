# Changelog

All notable changes to this package will be documented in this file.

## [Unreleased]

### Added
- **`RatatuiTerminalApps` framework** — static bootstrap and registry for scene-independent terminal apps with open/close/toggle API, attribute-based discovery (`[RatatuiTerminalApp]`), and per-app `DontDestroyOnLoad` GameObjects.
- **`RatatuiTerminalApp`** abstract base — shared toggle key, 4-finger touch toggle, open/close lifecycle hooks, and render guards.
- **`TerminalAppHandle`** — registry descriptor (`Id`, `DisplayName`, `Order`, live `Instance`).
- Developer Console sample refactored to use the Terminal Apps framework (`Id = "console"`).
- **Notepad sample** — terminal app (`Id = "notepad"`) with filename + multiline note editor, F9 toggle, and JSON persistence under `Application.persistentDataPath/ratatui-notepad/`.
- **OnGUI display modes** on `RatatuiRenderer`: `Full` (stretch to entire screen), `Partial` (native texture size with horizontal/vertical alignment), and `Window` (draggable macOS-style chrome).
- **Window mode title bar** shows the host GameObject name (same as the object name in the Hierarchy).
- **`TerminalTextArea` scrollbars**: auto-hide vertical and horizontal scrollbars when content exceeds the viewport; the text area shrinks by one column/row internally so scrollbars do not overlap text.
- **`TerminalTextArea` mouse-wheel scrolling**: the wheel scrolls the view one line per notch without moving the cursor; the view only re-centers on the cursor when the cursor itself moves.
- **`TerminalTextArea.OwnsArea`**: reports whether an area id belongs to the widget (outer area or scrollbar sub-areas) so callers can route clicks/scrolls correctly even when hit-testing resolves to a split sub-area.

### Fixed
- **`RatatuiTerminalApps.Open` / `SetOpen(true)`** now calls `RequestFocus()` even when the app is already open, bringing keyboard input and OnGUI window z-order to the front.
- **Notepad sample**: picking a note from the list after deleting the active note no longer overwrites a sibling note's filename/content. The editor-to-memory sync now follows `_editorNoteId` instead of the post-delete clamped `_selectedIndex`.
- Pixel buffer is now flipped vertically before upload so Unity's `Texture2D.LoadRawTextureData` (bottom-to-top / OpenGL row order) displays the terminal the right way up.
- **Colors tab**: Left column was empty because two `BeginStyledParagraph` builders were created simultaneously; the FFI layer holds only one pending paragraph at a time so the second call overwrote the first. Builders are now created and rendered sequentially.
- **Dashboard tab**: Sparkline was invisible on startup because the backing array was initialized to all-zeros. The array is now pre-filled with random values in the constructor.

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
- RGB24 pixel buffer rendering via fontdue (JetBrains Mono embedded)
- Callback-based C API: `ratatui_create`, `ratatui_begin_frame`, `ratatui_end_frame`, widget commands
- Unity UPM package with Assembly Definition (`RatatuiUnity.Runtime`)
- C# high-level API: `RatatuiTerminal`, `RatatuiRenderer` MonoBehaviour
- Widgets: Block, Paragraph, List, Gauge, Tabs, Sparkline, Table
- Platform support: Windows, macOS (Universal), Linux, iOS (XCFramework), Android, WebGL
- `RatatuiRenderer` base class with virtual `BuildFrame` override point
- BasicUsage sample demonstrating all major widgets
- GitHub Actions CI/CD with matrix builds and automatic releases
