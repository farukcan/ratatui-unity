# Ratatui Unity

Renders [Ratatui](https://ratatui.rs) TUI widgets as RGBA pixel textures in Unity using a native Rust library.

## Installation

### Via Git URL (recommended)

In your Unity project, open **Window → Package Manager → + → Add package from git URL** and paste:

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

```json
{
  "dependencies": {
    "com.farukcan.ratatui.unity": "file:../../ratatui-unity/Packages/com.farukcan.ratatui.unity"
  }
}
```

## Quick Start

1. Import the **BasicUsage** sample from the Package Manager.
2. Add a `RatatuiDemo` component to a GameObject.
3. Create a UI Canvas with a **RawImage** and assign it to the `Raw Image` field.
4. Press Play.

## Custom Layout

Subclass `RatatuiRenderer` and override `BuildFrame`:

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

## API Reference

### RatatuiTerminal

| Method                                   | Description                         |
| ---------------------------------------- | ----------------------------------- |
| `BeginFrame()`                           | Start a new frame                   |
| `EndFrame()`                             | Render and return RGBA32 pixel data |
| `Split(area, direction, constraints)`    | Divide an area into children        |
| `Block(area, title, borders)`            | Bordered box                        |
| `Paragraph(area, text, alignment, wrap)` | Text block                          |
| `List(area, items, selected)`            | Newline-separated list              |
| `Gauge(area, ratio, label)`              | Progress bar                        |
| `Tabs(area, titles, selected)`           | Tab bar                             |
| `Sparkline(area, data)`                  | Spark line chart                    |
| `Table(area, data)`                      | Tab/newline-delimited table         |
| `SetStyle(fg, bg, modifiers)`            | Style for next widget               |
| `SetCustomFont(ttfBytes)`                | Override embedded font              |

### Constraint factory methods

```csharp
Constraint.Length(20)     // fixed cells
Constraint.Min(5)         // minimum cells
Constraint.Max(40)        // maximum cells
Constraint.Percentage(50) // percent of parent
Constraint.Fill(1)        // proportional fill
```

## Platform Support

| Platform | Binary                      | Unity DllImport   |
| -------- | --------------------------- | ----------------- |
| Windows  | `ratatui_unity.dll`         | `"ratatui_unity"` |
| macOS    | `libratatui_unity.bundle`   | `"ratatui_unity"` |
| Linux    | `libratatui_unity.so`       | `"ratatui_unity"` |
| iOS      | `ratatui_unity.xcframework` | `"__Internal"`    |
| Android  | `libratatui_unity.so`       | `"ratatui_unity"` |
| WebGL    | `libratatui_unity.a`        | `"__Internal"`    |

## Building the Native Library

```bash
# macOS only (default)
./build_all.sh macos

# All platforms (requires cross, Android NDK, Emscripten)
./build_all.sh
```

See `build_all.sh` for full prerequisites.
