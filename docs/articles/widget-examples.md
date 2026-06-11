# Widgets — Examples

Every widget call goes into the current frame between `BeginFrame()` / `EndFrame()` (or inside `BuildFrame(term)` if you inherit `RatatuiRenderer`). All examples assume you already have `RatatuiTerminal term` and a target `uint area`.

> Each section is a copy-pasteable snippet. Combine them freely.

## Block (bordered frame + title)

```csharp
term.Block(area, "Section Title", Borders.All);
uint inner = term.Inner(area);   // rect inside the border
```
<img width="215" height="130" alt="image" src="https://github.com/user-attachments/assets/fc271246-399e-4a9f-a741-b16f5715e543" />

## Paragraph

Plain text, wrapped or not:

```csharp
term.Paragraph(area, "Single-line or multi-line\nplain text body.");
```
<img width="545" height="108" alt="image" src="https://github.com/user-attachments/assets/6c3d33dc-d8c8-4d85-b202-6b6d5fd23fdc" />

## StyledParagraph (multi-span text)

Fluent builder for inline color/modifier runs. Always end with `.Render()`:

```csharp
term.BeginStyledParagraph(area, Alignment.Left, wrap: true)
    .Span("ratatui-unity", fg: Color.cyan, modifiers: Modifier.Bold)
    .Span(" — a Rust ")
    .Span("ratatui", fg: Color.yellow, modifiers: Modifier.Italic)
    .Span(" rendering backend for Unity.")
    .Line()
    .Span("Keyboard", modifiers: Modifier.Bold | Modifier.Underlined).Line()
    .Span("  A / D     ", fg: Color.cyan).Span("switch tabs").Line()
    .Render();
```
<img width="411" height="205" alt="image" src="https://github.com/user-attachments/assets/3c97edca-5e18-419b-9278-0217fff87746" />

Only one styled paragraph can be pending at a time — call `.Render()` before starting another.

## Table

Tab-separated header + rows:

```csharp
var sb = new System.Text.StringBuilder();
sb.AppendLine("Name\tLocation\tStatus");
sb.AppendLine("NYC\tNew York, US\tUp");
sb.AppendLine("LON\tLondon, UK\tUp");
sb.AppendLine("SAO\tSao Paulo, BR\tDown");
term.Table(area, sb.ToString().TrimEnd());
```
<img width="193" height="93" alt="image" src="https://github.com/user-attachments/assets/55dd2a01-6690-4020-90ef-7a32bb0dde73" />

## List

Newline-separated items:

```csharp
term.List(area, "Item 1\nItem 2\nItem 3");
```
<img width="110" height="94" alt="image" src="https://github.com/user-attachments/assets/00fab5d1-1840-421b-81a1-a66553a3035b" />

For selection / scrolling, see the `StatefulList<T>` helper used in the demo tabs ([BasicUsage](samples-basic-usage.md)).

## BarChart

Tab-separated `label\tvalue`:

```csharp
term.SetStyle(new Color(1f, 0.6f, 0f), Color.clear);
term.BarChart(area,
    "Mon\t22\nTue\t19\nWed\t17\nThu\t21\nFri\t25",
    barWidth: 3, barGap: 1);
```
<img width="177" height="129" alt="image" src="https://github.com/user-attachments/assets/66bdcb81-7404-42c0-a756-a89e0df64a7e" />

## Sparkline

```csharp
ulong[] history = new ulong[60];
// ...fill with samples...
term.SetStyle(Color.cyan, Color.clear);
term.Sparkline(area, history);
```
<img width="130" height="78" alt="image" src="https://github.com/user-attachments/assets/3d172b51-3f08-4a34-959a-09a4a4a2ba75" />

## Gauge / LineGauge

`progress` in `[0, 1]`:

```csharp
term.Gauge(area, ratio: 0.42f, label: "Loading…");

term.SetStyle(new Color(0.4f, 0.9f, 0.4f), Color.clear);
term.LineGauge(area, ratio: 0.91f, label: "package.unityp  [1240 KB/s]");
```
<img width="307" height="29" alt="image" src="https://github.com/user-attachments/assets/4ab3c913-ffc2-4099-8048-13bb67d8c564" />


## Chart (line chart with axes)

Two sine waves, with named X/Y axes:

```csharp
var chart = term.BeginChart(area);
chart.XAxis("x", xMin: 0, xMax: 100);
chart.YAxis("y", yMin: -20, yMax: 20);

double[] sin1 = new double[200];   // 100 (x,y) pairs interleaved
for (int i = 0; i < 100; i++)
{
    sin1[i * 2]     = i;
    sin1[i * 2 + 1] = Math.Sin(i / 3.0) * 18.0;
}
chart.Dataset("sin(x/3)", Marker.Braille, Color.cyan, sin1);

chart.Render();
```
<img width="403" height="216" alt="image" src="https://github.com/user-attachments/assets/ce1bead8-ed79-43f4-8811-21878704ab39" />


## Tabs

Newline-separated tab titles, plus the active index:

```csharp
term.SetStyle(new Color(0.2f, 0.8f, 1f), Color.clear, Modifier.Bold);
term.Tabs(area, "Dashboard\nServers\nColors\nAbout", selected: 0);
```
<img width="529" height="22" alt="image" src="https://github.com/user-attachments/assets/49b07024-b988-4319-a325-236474085053" />

## Calendar

```csharp
DateTime now = DateTime.Now;
term.Calendar(area, now.Year, now.Month, now.Day);
```
<img width="133" height="86" alt="image" src="https://github.com/user-attachments/assets/b5a29bf1-3bb4-45ba-ae2f-bd1fbd58cfde" />


## Scrollbar

Attach a vertical scrollbar to a parent area (typically a bordered block):

```csharp
term.Scrollbar(area,
    contentLength: items.Length,
    position: scrollOffset,
    viewportLength: Math.Max(1, visibleRows),
    orientation: ScrollbarOrientation.VerticalRight);
```
<img width="372" height="229" alt="image" src="https://github.com/user-attachments/assets/03b5f0d5-99ca-4584-bf82-2f613d403dd3" />


## Canvas (free-form drawing)

Coordinate space is your own; pick `xMin..xMax`, `yMin..yMax`. `Marker.Braille` gives the highest resolution.

```csharp
var canvas = term.BeginCanvas(area,
    xMin: -180, xMax: 180,
    yMin:  -90, yMax:  90,
    marker: Marker.Braille);

canvas.Map(MapResolution.High);     // built-in world map outline
canvas.Layer();                     // separator — draws below cover above

canvas.Line(-74, 40.7, -0.1, 51.5, Color.yellow);                    // NYC → LON
canvas.Points(new double[] { -74, 40.7 }, new Color(0.2f, 1f, 0.2f)); // green dot
canvas.Text(-72.5, 42.2, "NYC", Color.white);

canvas.Rectangle(20, 30, 10, 5, Color.cyan);
canvas.Circle(x: 0, y: 0, radius: 15, Color.magenta);

canvas.Render();
```
<img width="596" height="386" alt="image" src="https://github.com/user-attachments/assets/f218e60b-3006-4a56-9ea3-26387dc6831b" />

## Setting Style Before a Widget

Most widgets pick up the most recent `SetStyle` call as their default style (border, text color, bar color):

```csharp
term.SetStyle(Color.cyan, Color.clear, Modifier.Bold);
term.Block(area, "Focused", Borders.All);   // border in cyan + bold
```

For per-span styling inside text, use `StyledParagraph` instead.

## Background Color

```csharp
term.SetBackgroundColor(new Color(0.07f, 0.07f, 0.11f));
```
<img width="544" height="107" alt="image" src="https://github.com/user-attachments/assets/eb0faca5-ca93-44ed-b05a-e997acacf2d9" />

Affects the entire terminal background until changed.
