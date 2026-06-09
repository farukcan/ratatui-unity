# Ratatui Unity

[![Build Native Plugin](https://github.com/farukcan/ratatui-unity/actions/workflows/build.yml/badge.svg)](https://github.com/farukcan/ratatui-unity/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Rust Edition 2021](https://img.shields.io/badge/Rust-2021_Edition-orange?logo=rust&logoColor=white)](https://www.rust-lang.org/)
[![ratatui 0.30](https://img.shields.io/badge/ratatui-0.30-blue?logo=rust)](https://ratatui.rs)
[![Unity](https://img.shields.io/badge/Unity-2021%2B-black?logo=unity&logoColor=white)](https://unity.com)
[![UPM](https://img.shields.io/badge/UPM-git--url-blue?logo=unity&logoColor=white)](https://github.com/farukcan/ratatui-unity.git#latest)

![Platform macOS](https://img.shields.io/badge/platform-macOS-lightgrey?logo=apple&logoColor=white)
![Platform iOS](https://img.shields.io/badge/platform-iOS-lightgrey?logo=apple&logoColor=white)
![Platform Windows](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows&logoColor=white)
![Platform Linux](https://img.shields.io/badge/platform-Linux-FCC624?logo=linux&logoColor=black)
![Platform Android](https://img.shields.io/badge/platform-Android-3DDC84?logo=android&logoColor=white)
![Platform WebGL](https://img.shields.io/badge/platform-WebGL-990000?logo=webgl&logoColor=white)

A Rust native plugin that renders [Ratatui](https://ratatui.rs) TUI widgets as RGBA pixel textures in Unity — for all platforms.

Layout, widgets, styling, and input are driven by Ratatui in Rust; rasterization to RGB24 pixels happens natively; Unity sees a `Texture2D` it can blit to a **RawImage**, **MeshRenderer**, or an **OnGUI** fallback.

## Installation

### Via Git URL (recommended)

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

### Local development

Clone the repository and point your Unity project at the package folder:

```json
{
  "dependencies": {
    "com.farukcan.ratatui.unity": "file:../../ratatui-unity/Packages/com.farukcan.ratatui.unity"
  }
}
```

## Samples

Import samples via **Window → Package Manager → Ratatui Unity → Samples → Import**.

| Sample | Toggle | What it shows |
| ------ | ------ | ------------- |
| **Basic Usage** | — | Tabbed demo (`RatatuiDemo`) with 9 tabs covering every widget, layout, input, hover, and animation. Includes `ESP32Terminal` as a second standalone renderer. |
| **Developer Console** | `` ` `` (backquote) | Half-Life style runtime console with log capture and a static command registry. Auto-boots before scene load. |
| **Notepad** | F9 | Persistent multi-line notepad (`TerminalTextArea`). Notes saved under `Application.persistentDataPath`. |
| **Profiler** | F10 | Read-only real-time telemetry overlay (FPS, frame time, GC, rendering stats, memory). |

Terminal-app samples (Console, Notepad, Profiler) use the `RatatuiTerminalApps` framework and require **Player Settings → Active Input Handling** = `Both` or `Input Manager (Old)`.

See the [samples documentation](https://github.com/farukcan/ratatui-unity/tree/main/docs/articles) for a tab-by-tab walkthrough.

## Quick Start

1. Import the **Basic Usage** sample.
2. Add a `RatatuiDemo` component to a GameObject (or subclass `RatatuiRenderer` yourself).
3. Optionally assign a UI **RawImage** or **MeshRenderer** to the renderer's target fields.
4. Press Play.

Minimal custom renderer:

```csharp
using RatatuiUnity;
using UnityEngine;

public class MyTerminal : RatatuiRenderer
{
    protected override void BuildFrame(RatatuiTerminal term)
    {
        uint[] areas = term.Split(term.RootArea, Direction.Vertical,
            Constraint.Length(3),
            Constraint.Min(0));

        term.Block(areas[0], "Header", Borders.All);
        term.Paragraph(areas[1], "Hello from Ratatui!", Alignment.Center, wrap: true);
    }
}
```

## OnGUI Fallback

When neither **Raw Image** nor **Mesh Renderer** is assigned, `RatatuiRenderer` draws the terminal via `OnGUI`.

| Mode | Behavior |
| ---- | -------- |
| **Full** | Stretches the terminal texture to fill the entire screen. Serialized cols/rows still define the character grid. |
| **Partial** | Draws at the terminal's native pixel size. Position with **Horizontal Align** and **Vertical Align**. |
| **Window** | Draggable macOS-style window with a title bar (close, minimize, fullscreen). Native pixel size; position is interactive. |

Default mode is **Full**. Mouse and keyboard input are mapped to the active OnGUI rect in all modes.

## Resolution & Readability

`RatatuiRenderer` supports CSS-style viewport-relative font sizing via **Sizing Mode**:

| Mode | `fontSize` interpretation |
| ---- | ------------------------- |
| `Pixel` | Absolute pixels (terminal created once). |
| `Vh` / `Vw` | Percent of viewport height / width. |
| `Vmin` / `Vmax` | Percent of the smaller / larger viewport edge. |

Enable **Fit Cols And Rows** to derive the character grid from the target pixel area. The renderer refits automatically when the viewport or DPI changes (non-Pixel modes).

## Terminal Apps

`RatatuiTerminalApps` bootstraps scene-independent terminal apps before the first scene loads. Each app is a `RatatuiTerminalApp` subclass that registers itself and exposes open/close/toggle via a static API:

```csharp
RatatuiTerminalApps.Toggle("console");
RatatuiTerminalApps.Open("notepad");
RatatuiTerminalApps.Close("profiler");
```

See the Console, Notepad, and Profiler samples for full implementations.

## Input

Override `OnTerminalKeyDown`, `OnTerminalMouseEvent`, and `OnTerminalHoverChanged` on `RatatuiRenderer` for low-level events. Higher-level helpers ship in the runtime:

| Type | Purpose |
| ---- | ------- |
| `TerminalInput` | Single-line text field with cursor, selection, clipboard. |
| `TerminalTextArea` | Multi-line editor with scroll and word wrap. |
| `TerminalCommandInput` | Command-line input with history navigation. |
| `MobileKeyboardBridge` | Bridges Unity's mobile keyboard to focused inputs. |

## API Reference

### RatatuiTerminal — layout & frame

| Method | Description |
| ------ | ----------- |
| `BeginFrame()` | Start a new frame |
| `EndFrameRaw()` | Render and return a pointer to the native RGB24 buffer (zero GC) |
| `EndFrameRawIfDirty()` | Like `EndFrameRaw()`, but returns `IntPtr.Zero` when unchanged |
| `EndFrame()` | Render and copy pixels into a new `byte[]` |
| `Split(area, direction, constraints)` | Divide an area into children |
| `Inner(area, horizontal, vertical)` | Shrink an area by a border margin |
| `HitTest(col, row)` | Return the area ID at a cell position |
| `TryGetAreaRect(areaId, …)` | Get cell rect for an area |
| `SetCustomFont(ttfBytes)` | Override the embedded JetBrains Mono font |
| `SetBackgroundColor(color)` | Terminal-wide background (call before `BeginFrame`) |

### RatatuiTerminal — widgets

| Method | Description |
| ------ | ----------- |
| `Block(area, title, borders)` | Bordered box |
| `Paragraph(area, text, alignment, wrap)` | Text block |
| `BeginStyledParagraph(area, alignment, wrap)` | Rich-text builder → `StyledText.Render()` |
| `List(area, items, selected)` | Newline-separated list |
| `Gauge(area, ratio, label)` | Progress bar |
| `LineGauge(area, ratio, label)` | Horizontal line gauge |
| `Tabs(area, titles, selected)` | Tab bar |
| `Sparkline(area, data)` | Spark line chart |
| `BarChart(area, data, barWidth, barGap)` | Bar chart (tab-separated data) |
| `Table(area, data)` | Tab/newline-delimited table |
| `TableEx(area, data, columnWidths, selectedRow)` | Table with per-column constraints |
| `Calendar(area, year, month, day)` | Monthly calendar |
| `Scrollbar(area, contentLength, position, viewportLength, …)` | Scrollbar with scroll-offset semantics |
| `BeginChart(area)` | Chart builder → `ChartBuilder.Render()` |
| `BeginCanvas(area, xMin, xMax, yMin, yMax, marker)` | Canvas builder (polylines, points, map) → `CanvasBuilder.Render()` |
| `SetStyle(fg, bg, modifiers)` | Style for the next widget |

### Constraint factory methods

```csharp
Constraint.Length(20)     // fixed cells
Constraint.Min(5)         // minimum cells
Constraint.Max(40)        // maximum cells
Constraint.Percentage(50) // percent of parent
Constraint.Fill(1)        // proportional fill
```

## Platform Support

| Platform | Binary | Unity DllImport |
| -------- | ------ | --------------- |
| Windows | `ratatui_unity.dll` | `"ratatui_unity"` |
| macOS | `libratatui_unity.bundle` | `"ratatui_unity"` |
| Linux | `libratatui_unity.so` | `"ratatui_unity"` |
| iOS | `ratatui_unity.xcframework` | `"__Internal"` |
| Android | `libratatui_unity.so` | `"ratatui_unity"` |
| WebGL | `libratatui_unity.a` | `"__Internal"` |

## Building the Native Library

Native binaries are built from the **repository root** (not this package folder):

```bash
# macOS only (no extra tools needed)
./build_all.sh macos

# All platforms (requires cross, Android NDK, Emscripten)
./build_all.sh
```

See `build_all.sh` in the repo root for platform-specific prerequisites. CI builds and releases binaries via `.github/workflows/build.yml`.

## Documentation

Full guides live in the repository under [`docs/`](https://github.com/farukcan/ratatui-unity/tree/main/docs):

- [Getting Started](https://github.com/farukcan/ratatui-unity/blob/main/docs/articles/getting-started.md)
- [Architecture](https://github.com/farukcan/ratatui-unity/blob/main/docs/articles/architecture.md)
- [Layout](https://github.com/farukcan/ratatui-unity/blob/main/docs/articles/layout.md)
- [Widget Examples](https://github.com/farukcan/ratatui-unity/blob/main/docs/articles/widget-examples.md)
- [Input Handling](https://github.com/farukcan/ratatui-unity/blob/main/docs/articles/input-handling.md)
- [Terminal Apps](https://github.com/farukcan/ratatui-unity/blob/main/docs/articles/terminal-apps.md)
- [Resolution & Readability](https://github.com/farukcan/ratatui-unity/blob/main/docs/articles/resolution-and-readability.md)
- [Samples Overview](https://github.com/farukcan/ratatui-unity/blob/main/docs/articles/samples-overview.md)

## License

MIT — see [LICENSE](LICENSE).  
JetBrains Mono font: SIL Open Font License 1.1.
