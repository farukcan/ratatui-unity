# BasicUsage Sample

`Samples~/BasicUsage/` is the showcase application: a single `RatatuiDemo` host that swaps between 9 tabs, plus a standalone `ESP32Terminal` device simulator. Every widget in the library is touched somewhere.

## The Host: `RatatuiDemo`

`RatatuiDemo` inherits `RatatuiRenderer` and owns an array of `ITab` instances. Each frame it:

1. Calls `Update(dt)` on every tab so their state advances even when not visible.
2. Renders a tab bar at the top.
3. Renders the *active* tab into the remaining area.
4. Routes keyboard / mouse / hover events — tab-bar clicks switch tabs; everything else is forwarded to the active tab.

The whole host is **under 170 lines** because each tab implements `ITab` and owns its own state & rendering.

```csharp
public class RatatuiDemo : RatatuiRenderer
{
    private ITab[] _tabs;
    private int _activeTab;

    protected override void BuildFrame(RatatuiTerminal term)
    {
        uint root = term.RootArea;
        var main = term.Split(root, Direction.Vertical,
            Constraint.Length(3),   // tab bar
            Constraint.Min(0));     // active tab
        if (main.Length < 2) return;

        RenderTabBar(term, main[0]);
        _tabs[_activeTab].Render(term, main[1]);
    }
}
```

## The `ITab` Pattern

```csharp
public interface ITab
{
    string Title { get; }
    void Update(float dt);
    void OnKeyEvent(TerminalKeyEvent e);
    void OnMouseEvent(TerminalMouseEvent e);
    void OnHoverChanged(TerminalHoverState oldState, TerminalHoverState newState);
    void Render(RatatuiTerminal term, uint area);
}
```

Use this same pattern for any multi-screen app — it isolates each screen's state, input handling, and layout into a single small class.

## Tab-by-Tab

### Dashboard (`DashboardTab.cs`)
Gauge + LineGauge progress, rolling random Sparkline, BarChart rotating event data, Chart with two animated sine waves, task list with hover/selection (`StatefulList<T>`), log list with scrolling.
Best file to read for: **animated data, multiple widgets composed into one screen, mouse hit-testing for list rows.**

### Servers (`ServersTab.cs`)
Server `Table` on the left, world-map `Canvas` on the right (`Map(MapResolution.High)`), polyline between servers, colored points + labels.
Best file to read for: **Canvas drawing, Map widget, geographic plotting.**

### Colors (`ColorsTab.cs`)
All 16 named colors shown as styled spans — both as foreground and background. Two parallel `StyledParagraph` builders (one per column).
Best file to read for: **multi-column `StyledParagraph` usage, the limitation that only one styled paragraph can be pending at a time.**

### About (`AboutTab.cs`)
Pure `StyledParagraph` essay with bold, italic, underline modifiers and cyan key bindings. No state.
Best file to read for: **rich-text formatting reference.**

### Email (`EmailTab.cs`)
Inbox `StyledParagraph` list with selection + hover highlighting + scrollbar; reading pane below showing the selected message body.
Best file to read for: **scroll/selection bookkeeping, `Scrollbar` widget, `EnsureSelectedVisible` pattern.**

### Recipe (`RecipeTab.cs`)
Ingredient list with scrollbar, step-by-step instructions in a styled paragraph using `Span(line, Color, Color)` for inline emphasis.
Best file to read for: **inline emphasis spans, scrollbar + selection combined.**

### Weather (`WeatherTab.cs`)
Two BarCharts (Highs / Lows) side by side, a live `Calendar` showing today, and animated `LineGauge` download progress bars.
Best file to read for: **BarChart with tab-separated data, Calendar, multiple animated LineGauges.**

### Traceroute (`TracerouteTab.cs`)
Hop `Table` with per-hop `Sparkline` ping graphs, and a Türkiye Canvas map with hand-drawn coastline polylines, the route in yellow, source (Ankara, green) and destination (İstanbul, red).
Best file to read for: **custom polyline drawing on Canvas, layered rendering with `canvas.Layer()`, sparkline-per-row layouts.**

### Input (`InputTab.cs`)
Two `TerminalInput` fields, Tab to switch focus, Enter to submit, click-to-position cursor, help-line, output panel.
Best file to read for: **`TerminalInput` for single-line text editing, field focus management, mouse-aware cursor positioning.**

For multiline editing (`TerminalTextArea`), see the [Notepad sample](samples-notepad.md).

## Standalone: `ESP32Terminal.cs`

A second `RatatuiRenderer` subclass simulating an ESP32 serial console. It's not a tab — attach this script to a separate GameObject as its own renderer. Demonstrates:

- Rolling log buffer with scroll-back via mouse wheel / PageUp / PageDown
- Animated CPU temperature sparkline
- Command parsing & dispatch (`help`, `status`, `gpio set 5 1`, `wifi scan`, …)
- `TerminalInput` for the command line

Together with `RatatuiDemo` this proves you can have **multiple independent terminals** in the same scene.

## Shared Helpers

- `RatatuiShared.cs` — `StatefulList<T>` (selectable list with `Next` / `Previous` / `SelectFirst`)
- `ITab.cs` — the tab interface used by `RatatuiDemo`
- `CameraLerp.cs` — Unity-only utility for the demo scene camera, irrelevant to ratatui

## Reading Order

If you're new to the library, read in this order:

1. `AboutTab.cs` — minimal: just a styled paragraph, no state.
2. `ColorsTab.cs` — split layout + two builders.
3. `ServersTab.cs` — table + canvas + map.
4. `EmailTab.cs` — selection + scrollbar + hover.
5. `DashboardTab.cs` — everything at once.
6. `RatatuiDemo.cs` — how the host glues tabs together.
