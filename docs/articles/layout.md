# Layout

A frame is built top-down: you ask the terminal for the root area, split it into sub-areas, optionally take the "inner" rectangle of a bordered block, and draw widgets into those areas.

## Root Area

```csharp
uint root = term.RootArea;
```

This is the full terminal rectangle (`Cols × Rows`). Every layout starts here.

## Splitting

`Split(area, direction, constraints…)` returns child area IDs in declaration order.

```csharp
// Top bar (3 rows) + main content (rest)
var rows = term.Split(root, Direction.Vertical,
    Constraint.Length(3),
    Constraint.Min(0));

uint topBar = rows[0];
uint main   = rows[1];
```

Always guard against degenerate sizes:

```csharp
if (rows.Length < 2) return;
```

When the terminal is too small to satisfy every constraint, `Split` may return fewer areas than requested.

## Constraint Types

| Constraint              | Meaning                                                 |
|-------------------------|---------------------------------------------------------|
| `Constraint.Length(n)`  | Exactly `n` rows/columns                                |
| `Constraint.Min(n)`     | At least `n`, grows to absorb leftover space            |
| `Constraint.Max(n)`     | At most `n`, shrinks if needed                          |
| `Constraint.Percentage(p)` | Percent of parent area (0–100)                        |
| `Constraint.Fill(weight)` | Proportional fill against other `Fill` siblings        |

Example with mixed types:

```csharp
var cols = term.Split(main, Direction.Horizontal,
    Constraint.Length(20),       // fixed 20-col sidebar
    Constraint.Percentage(60),   // 60% of remaining
    Constraint.Min(10));         // at least 10, takes leftover
```

## Borders + Inner

`Block` draws a bordered frame; `Inner(area)` returns the rectangle inside the border so children don't overdraw the frame.

```csharp
term.Block(area, "Title", Borders.All);
uint inner = term.Inner(area);
term.Paragraph(inner, "Body text inside the bordered block.");
```

`Borders` is a flag enum: `Borders.Top | Borders.Bottom`, `Borders.Left | Borders.Right`, `Borders.All`, `Borders.None`.

## Area Hit-Testing

Every area ID can be reverse-queried for its terminal rectangle, useful for mouse input:

```csharp
if (term.TryGetAreaRect(inner, out int x, out int y, out int w, out int h))
{
    // x, y, w, h in terminal cells
}
```

See [Input Handling](input-handling.md) for using these IDs in mouse routing.

## Nested Example

```csharp
protected override void BuildFrame(RatatuiTerminal term)
{
    uint root = term.RootArea;

    // Header + body
    var outerRows = term.Split(root, Direction.Vertical,
        Constraint.Length(3),
        Constraint.Min(0));
    if (outerRows.Length < 2) return;

    // Two-column body
    var bodyCols = term.Split(outerRows[1], Direction.Horizontal,
        Constraint.Percentage(30),
        Constraint.Percentage(70));
    if (bodyCols.Length < 2) return;

    term.Block(outerRows[0], "Header", Borders.All);
    term.Block(bodyCols[0],  "Sidebar", Borders.All);
    term.Block(bodyCols[1],  "Content", Borders.All);

    term.Paragraph(term.Inner(bodyCols[1]), "Hello, layout.");
}
```
