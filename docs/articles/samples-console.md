# Developer Console Sample

`Samples~/Console/` is a drop-in runtime developer console: import the sample, hit the toggle key, and you have a live log viewer + command line in any scene without writing any code.

## What You Get (Zero Setup)

- Captures `Debug.Log` / `LogWarning` / `LogError` / exceptions automatically
- Auto-bootstrapped before the first scene loads (no GameObject to drag in)
- Toggle with **`` ` ``** (backtick) by default
- Built-in commands: `help`, `clear`, `quit`, `echo`, `version`, `fps`, `scene`, `time_scale`, `target_fps`, `sysinfo`, `pause`, `resume`, `gc`, `log_warning`, `log_error`, `log_exception`
- Command history (Up/Down arrows), scrollback, log timestamps
- **Window mode** (default): draggable macOS-style frame with title-bar **zoom** (`+` / `−`, ~10% per click) and **resize** (drag the blue `✴︎` handle) — inherited from `RatatuiRenderer`; see [Resolution & Readability → OnGUI Window Mode](resolution-and-readability.md#ongui-window-mode)

## Boot Flow

```mermaid
sequenceDiagram
    participant U as Unity
    participant RC as RatatuiConsole
    participant LC as ConsoleLogCapture
    participant R as Renderer

    U->>RC: BeforeSceneLoad
    RC->>RC: Bootstrap()
    RC->>LC: Install() (subscribes to logMessageReceivedThreaded)
    RC->>RC: BuiltinCommands.Register()
    RC->>U: new GameObject("[RatatuiConsole]") + DontDestroyOnLoad
    RC->>R: AddComponent<RatatuiConsoleRenderer>
    Note over R: Idle until toggle key pressed
```

The whole pipeline is `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` — no GameObject to add manually.

## Pieces

| File | Role |
|------|------|
| `RatatuiConsole.cs` | Public facade: `Open/Close/Toggle`, `RegisterCommand`, `Log`, `ClearLogs`, accessors |
| `RatatuiConsoleConfig.cs` | `ScriptableObject` for dimensions, font size, toggle key, buffer sizes, colors |
| `RatatuiConsoleRenderer.cs` | The `RatatuiRenderer` that paints the log + prompt and handles input |
| `ConsoleLogCapture.cs` | Hooks `Application.logMessageReceivedThreaded`, owns the log ring buffer |
| `ConsoleCommandRegistry.cs` | Dictionary of registered commands, plus parser (`Parse(raw, out name, out args)`) |
| `ConsoleHistory.cs` | Command-line history (up/down recall) |
| `BuiltinCommands.cs` | Registration of the built-in commands listed above |
| `Resources/RatatuiConsoleConfig.asset` | Default config asset loaded at boot |

## Usage from Game Code

### Toggle / state

```csharp
using RatatuiUnity.Samples.Console;

RatatuiConsole.Toggle();              // open or close
RatatuiConsole.Open();                // takes scene keyboard focus (RatatuiFocusManager)
RatatuiConsole.Close();
bool open = RatatuiConsole.IsOpen;
```

### Register a custom command

```csharp
RatatuiConsole.RegisterCommand("spawn", "Spawn N enemies. Usage: spawn 10",
    args =>
    {
        if (args.Length == 0 || !int.TryParse(args[0], out int n))
        {
            Debug.LogWarning("Usage: spawn <count>");
            return;
        }
        for (int i = 0; i < n; i++) EnemySpawner.Spawn();
    });
```

Anything sent to `Debug.Log` from inside a command shows up in the console output.

### Push a message directly

```csharp
RatatuiConsole.Log("Player connected: " + playerId);
```

### Execute a command programmatically

```csharp
RatatuiConsole.ExecuteCommand("time_scale 0.5");
```

## Configuration

Either edit `Samples~/Console/Resources/RatatuiConsoleConfig.asset` after import, or create your own via **Assets → Create → Ratatui → Console Config** and drop it under any `Resources/` folder named exactly `RatatuiConsoleConfig`.

Knobs:

| Field | Default | Purpose |
|-------|---------|---------|
| `cols`, `rows` | 120 × 32 | Fallback terminal grid. The console enables **Fit Cols And Rows**, so the grid is derived from the available pixel area at startup (these values are only used if that area is unavailable). |
| `fontSize` | 1.6 | Glyph size. Interpreted per `sizingMode`: absolute pixels in `Pixel`, or percent of the viewport in `Vh` / `Vw` / `Vmin` / `Vmax` |
| `sizingMode` | `Vmin` | How `fontSize` is interpreted. `Vmin` = percent of the smaller viewport dimension, so glyphs stay readable in both portrait and landscape |
| `displayMode` | `Window` | `Full` stretches to screen, `Partial` uses native pixel size, `Window` is a draggable frame whose title bar shows the host GameObject name |
| `windowStartMaximized` | `true` | When `displayMode` is `Window`, maximize on first open |
| `horizontalAlign` / `verticalAlign` | Center / Top | Placement in `Partial` mode |
| `backgroundColor` | `#121221` | Terminal background |
| `toggleKey` | `` ` `` (BackQuote) | Open/close key |
| `maxLogEntries` | 2000 | Log ring buffer size |
| `maxHistoryEntries` | 64 | Command history size |
| `showTimestamp` | true | Prefix each line with `[HH:mm:ss]` |

`RatatuiConsoleRenderer` applies the config in `ApplyConfigToBase` and always enables **Fit Cols And Rows**, so zoom and resize both keep the log grid filled to the current window content area.

### Adjusting readability at runtime

With the default `displayMode` of `Window`:

1. Open the console (`` ` `` by default).
2. Use the blue **+** / **−** buttons on the title bar to zoom glyph size in or out (~10% per click, clamped to 1–200 in the active sizing units).
3. Drag the blue **✴︎ resize handle** (far right) to change how much screen area the window occupies without changing glyph scale; the column/row count adapts on release.

Zoom works even when the window is maximized (green traffic-light). Resize is disabled while maximized. Initial `fontSize` and `sizingMode` from the config asset set the starting scale; zoom adjusts from there for the session (not persisted back to the asset).

For viewport-relative sizing (`Vmin`, etc.), see [Resolution & Readability](resolution-and-readability.md).

## Caveat: Input System

The renderer uses `UnityEngine.Input` (legacy). If your project has **Player Settings → Active Input Handling = "Input System Package (New)"**, the console will log a warning at boot and refuse to start. Set it to **"Both"** or **"Input Manager (Old)"** to use this sample as-is.

## Extending

To replace just the renderer (custom layout, different keybinds) while keeping the log capture + command registry: implement your own `MonoBehaviour : RatatuiRenderer` and use `RatatuiConsole.Logs`, `RatatuiConsole.Registry`, `RatatuiConsole.History` as data sources. The facade stays — only the visual layer changes.
