# Input Handling

`RatatuiRenderer` exposes virtual hooks for keyboard, mouse, and hover events. All input coordinates are in **terminal cells** (not pixels), and every interactive region is identified by the `uint` area ID returned from `Split` / `Inner`.

> Running more than one `RatatuiRenderer` in the same scene? See [Focus & Multi-Terminal](focus-and-multi-terminal.md) — keyboard is gated to the focused renderer, mouse continues to route by hit-test.

## Hooks

Override any of these on your `RatatuiRenderer` subclass:

```csharp
protected override void OnTerminalKeyDown(TerminalKeyEvent e) { }
protected override void OnTerminalMouseEvent(TerminalMouseEvent e) { }
protected override void OnTerminalHoverChanged(
    TerminalHoverState oldState, TerminalHoverState newState) { }
```

## Keyboard

```csharp
protected override void OnTerminalKeyDown(TerminalKeyEvent e)
{
    if (e.Key == KeyCode.RightArrow || e.Character == 'd')
        _activeTab = (_activeTab + 1) % _tabs.Length;

    if (e.Key == KeyCode.LeftArrow || e.Character == 'a')
        _activeTab = (_activeTab + _tabs.Length - 1) % _tabs.Length;
}
```

`TerminalKeyEvent` carries:

- `Key` — `UnityEngine.KeyCode` for non-character keys (arrows, Tab, Return, F-keys, etc.)
- `Character` — printable char for letter / number / symbol keys
- `Modifiers` — `KeyModifiers` flags (`Shift`, `Ctrl`, `Alt`)

## Mouse — Click, Scroll, Hit-Testing

Every mouse event includes the `AreaId` it landed on. Store area IDs from your render pass, compare them in the handler:

```csharp
private uint _inboxArea;
private int  _inboxTop;
private int  _scrollOffset;

protected override void OnTerminalMouseEvent(TerminalMouseEvent e)
{
    if (e.AreaId != _inboxArea) return;

    if (e.Type == MouseEventType.Click && e.Button == MouseButton.Left)
    {
        int localRow = e.Row - _inboxTop;
        SelectItem(_scrollOffset + localRow);
    }

    if (e.Type == MouseEventType.Scroll)
    {
        if (e.ScrollDelta > 0) SelectPrevious();
        else SelectNext();
    }
}
```

`TerminalMouseEvent` fields:

- `Type` — `MouseEventType.Click`, `Move`, `Scroll`
- `Button` — `MouseButton.Left`, `Right`, `Middle` (Click only)
- `Col`, `Row` — terminal-space cell coordinates
- `ScrollDelta` — `±1` per notch (Scroll only)
- `AreaId` — hit-tested area ID (`0` if no match)

To convert global terminal coords to area-local, query the area rect:

```csharp
if (term.TryGetAreaRect(_inboxArea, out int ax, out int ay, out int aw, out int ah))
{
    int localCol = e.Col - ax;
    int localRow = e.Row - ay;
}
```

## Hover

Hover fires when the cell-under-cursor changes — useful for highlighting list rows without click:

```csharp
private int _hoveredRow = -1;

protected override void OnTerminalHoverChanged(
    TerminalHoverState oldState, TerminalHoverState newState)
{
    _hoveredRow = (newState.IsInside && newState.AreaId == _inboxArea)
        ? newState.Row - _inboxTop + _scrollOffset
        : -1;
}
```

`TerminalHoverState` carries `IsInside` (cursor within terminal at all?), `AreaId`, `Col`, `Row`.

## Text Input Fields — `TerminalInput`

For multi-character editing (search boxes, command lines), use the `TerminalInput` helper:

```csharp
private readonly TerminalInput _name = new TerminalInput(initial: "Faruk");

protected override void OnTerminalKeyDown(TerminalKeyEvent e)
{
    if (e.Key == KeyCode.Tab) { /* switch field */; return; }
    if (e.Key == KeyCode.Return) { Submit(_name.Value); return; }

    _name.HandleKeyEvent(e);  // typing, Backspace, arrows, Home/End, Ctrl+Arrow
}

protected override void OnTerminalMouseEvent(TerminalMouseEvent e)
{
    if (e.AreaId == _nameArea)
        _name.HandleMouseEvent(e);  // click to reposition cursor
}

protected override void BuildFrame(RatatuiTerminal term)
{
    term.Block(area, "Name", Borders.All);
    _nameArea = term.Inner(area);
    _name.Render(term, _nameArea,
        cursorFg: Color.black, cursorBg: Color.white);
}
```

Supports: Backspace, Delete, Left/Right, Home/End, Ctrl+Left/Right (word jump), printable chars, scroll when text exceeds field width.

## Click-Through Pattern: Tab Bar Routing

Real apps usually delegate input to whichever sub-component owns the area. Pattern from the BasicUsage demo:

```csharp
protected override void OnTerminalKeyDown(TerminalKeyEvent e)
{
    // Global shortcut: tab switching
    if (e.Character == 'd') { _activeTab = (_activeTab + 1) % _tabs.Length; return; }
    if (e.Character == 'a') { _activeTab = (_activeTab + _tabs.Length - 1) % _tabs.Length; return; }

    // Otherwise hand off to the active sub-component
    _tabs[_activeTab].OnKeyEvent(e);
}
```
