# Architecture

`ratatui-unity` is two halves stitched by a thin C ABI:

- **Rust core** (`src/`) — wraps [`ratatui`](https://ratatui.rs), maintains terminal state, layouts widgets, rasterizes the resulting buffer of cells into RGB24 pixels via [`fontdue`](https://github.com/mooman219/fontdue).
- **Unity C# bindings** (`Packages/com.farukcan.ratatui.unity/Runtime/`) — calls into the native crate via `DllImport`, marshals a `Texture2D` per frame.

## Frame Flow

```mermaid
sequenceDiagram
    participant U as Unity (C#)
    participant N as Native (Rust)
    participant R as Ratatui buffer
    participant P as Pixel buffer

    U->>N: BeginFrame()
    N->>R: clear cells
    U->>N: Block / Paragraph / Chart / ...
    N->>R: enqueue widget commands
    U->>N: EndFrameRaw() / EndFrameRawIfDirty()
    N->>R: render widgets into cell buffer
    N->>P: rasterize cells (font + colors → RGB24)
    N-->>U: pixel ptr + size
    U->>U: Texture2D.LoadRawTextureData(ptr)
```

## Module Map (Rust)

| Module          | Responsibility                                                        |
|-----------------|-----------------------------------------------------------------------|
| `lib.rs`        | C ABI exports (`ratatui_*` functions)                                 |
| `terminal.rs`   | `TerminalState`, `WidgetCommand` enum + shared data types, frame state |
| `commands.rs`   | Render dispatch — translates queued `WidgetCommand`s into ratatui calls |
| `renderer.rs`   | Cell buffer → RGB24 pixel pipeline, `compute_buffer_hash`             |
| `font.rs`       | `fontdue` font cache, glyph rasterization                             |
| `color.rs`      | Ratatui `Color` → RGB conversion                                      |

## C# Surface (Unity)

| Type                  | Role                                                 |
|-----------------------|------------------------------------------------------|
| `RatatuiTerminal`     | High-level handle: `BeginFrame` / widgets / `EndFrameRaw` |
| `RatatuiRenderer`     | `MonoBehaviour` host: texture management, input, OnGUI fallback |
| `RatatuiNative`       | Raw `DllImport` declarations                         |
| `CanvasBuilder`       | Fluent builder for the Canvas widget                 |
| `ChartBuilder`        | Fluent builder for Chart widget                      |
| `Constraint`          | Layout constraints (`Length`, `Min`, `Percentage` …) |
| `StyledText`, `ITab`  | Styling and tabular widget helpers                   |
| `RatatuiTerminalApps` | Static bootstrap/registry for scene-independent terminal apps |
| `RatatuiTerminalApp`  | Abstract base for apps with open/close/toggle lifecycle |
| `RatatuiFocusManager` | Keyboard focus arbitration across multiple renderers |

See [Terminal Apps](terminal-apps.md) for the app discovery and open/close API.

## Memory & Lifetime

- Each `RatatuiTerminal` owns a Rust-side `TerminalState` allocated by `ratatui_create` and freed by `ratatui_destroy`.
- The pixel buffer lives in Rust; C# receives a borrowed pointer per frame and must copy via `Texture2D.LoadRawTextureData` *before* the next `BeginFrame()`.
- `RatatuiTerminal.Dispose()` is mandatory — `using` block or explicit call. Letting the C# object get GC'd without Dispose leaks the Rust terminal.

See the [Rust API](../rust/ratatui_unity/index.html) for the exact `extern "C"` surface, and the [C# API](xref:RatatuiUnity) for the wrappers Unity code touches.
