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
- `Modifiers` — `KeyModifiers` flags (`Shift`, `Ctrl`, `Alt`, `Cmd`)
- `HasCmdOrCtrl` — `true` when the platform command modifier is held (Cmd on macOS, Ctrl elsewhere). Use this for clipboard / undo shortcuts so they feel native on every OS.

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

- `Type` — `MouseEventType.Down`, `Up`, `Click`, `Move`, `Scroll`
- `Button` — `MouseButton.Left`, `Right`, `Middle` (Down / Up / Click)
- `Col`, `Row` — terminal-space cell coordinates
- `ScrollDelta` — `±1` per notch (Scroll only)
- `AreaId` — hit-tested area ID (`0` if no match)

Text widgets (`TerminalInput`, `TerminalTextArea`) use **Down → Move → Up** for click-and-drag selection. A trailing `Click` after Down/Up is suppressed internally — route all three event types to `HandleMouseEvent`.

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

### Features

| Category | Behavior |
|----------|----------|
| Editing | Printable chars, Backspace, Delete, Ctrl+Backspace/Delete (delete word) |
| Movement | Left/Right, Home/End, Ctrl+Left/Right (word jump), Shift+arrows (extend selection) |
| Selection | Click, double-click (word), triple-click (all), click-and-drag |
| Clipboard | Cmd/Ctrl+A/C/X/V (select all, copy, cut, paste) |
| Undo/redo | Cmd/Ctrl+Z, Cmd/Ctrl+Shift+Z, Cmd/Ctrl+Y |
| Scroll | Horizontal auto-scroll keeps the cursor visible when text exceeds field width |
| Unicode | CJK-wide codepoints use display-width-aware cursor positioning |
| Options | `Placeholder`, `MaskChar` (password), `MaxLength`, `CharFilter`, `ReadOnly`, `Prefix` (non-editable prompt, e.g. `"> "`), `BlinkPeriod` |
| Mobile | Opens `TouchScreenKeyboard` on iOS / Android / mobile WebGL when focused (see [Mobile keyboard](#mobile-virtual-keyboard)) |

Call `OnFocus()` / `OnBlur()` when the field gains or loses focus (closes the mobile keyboard, optionally select-all on focus).

## Multiline Text — `TerminalTextArea`

For note bodies, chat boxes, or any multiline editor, use `TerminalTextArea`. It shares the same selection, clipboard, undo/redo, and mobile-keyboard stack as `TerminalInput`, plus line-aware cursor movement and built-in scrollbars.

```csharp
private readonly TerminalTextArea _body = new TerminalTextArea(initialValue: "");

protected override void OnTerminalKeyDown(TerminalKeyEvent e)
{
    if (_focus != FocusTarget.Body) return;
    _body.HandleKeyEvent(e);
}

protected override void OnTerminalMouseEvent(TerminalMouseEvent e)
{
    if (e.Type == MouseEventType.Scroll && _body.OwnsArea(e.AreaId))
    {
        _body.HandleMouseEvent(e);  // wheel scrolls view, cursor stays put
        return;
    }
    if (_body.OwnsArea(e.AreaId))
        _body.HandleMouseEvent(e);  // click, drag-select, scrollbar areas
}

protected override void BuildFrame(RatatuiTerminal term)
{
    _bodyArea = term.Inner(area);
    _body.Render(term, _bodyArea, focused: _focus == FocusTarget.Body);
}
```

### Features

| Category | Behavior |
|----------|----------|
| Lines | Enter inserts `\n`; Up/Down move between lines (column preserved) |
| Movement | Home/End (line), Cmd/Ctrl+Home/End (document), PageUp/PageDown |
| Scrollbars | Auto-hide vertical (line count) and horizontal (long cursor line); text area shrinks internally so bars never overlap content |
| Mouse wheel | Scrolls the view one line per notch **without** moving the cursor; view recenters on the cursor only when the cursor itself moves |
| `OwnsArea` | Returns `true` for the outer area **and** scrollbar sub-areas created during `Render` — required because hit-testing resolves to the deepest split child |

Clipboard shortcuts match `TerminalInput`. Paste preserves newlines.

The [Notepad sample](samples-notepad.md) is the reference implementation: title field (`TerminalInput`) + note body (`TerminalTextArea`) with Tab focus cycling.

## Command Line — `TerminalCommandInput`

A thin wrapper around `TerminalInput` for REPL / console prompts. Delegates all editing to the inner `TerminalInput` and surfaces console-shaped keys as events:

```csharp
private readonly TerminalCommandInput _prompt = new TerminalCommandInput { Prefix = "> " };

_prompt.OnSubmit += () => Execute(_prompt.Text);
_prompt.OnHistoryStep += delta => LoadHistoryEntry(delta);
_prompt.OnEscape += () => Close();

protected override void OnTerminalKeyDown(TerminalKeyEvent e)
{
    _prompt.HandleKeyEvent(e);  // Enter/Escape/Tab/Up/Down intercepted first
}
```

| Event | Key | Notes |
|-------|-----|-------|
| `OnSubmit` | Enter / KeypadEnter | Always consumed |
| `OnEscape` | Escape | Always consumed |
| `OnTab` / `OnShiftTab` | Tab / Shift+Tab | Consumed only when a handler is attached |
| `OnHistoryStep` | Up / Down (no modifiers) | `delta` is `-1` / `+1`; Cmd/Ctrl+arrows fall through to `TerminalInput` |
| `OnEdit` | any edit | Fires when `Text` changes (typing, paste, cut, undo, …) |

The [Developer Console](samples-console.md) sample uses this for the prompt line.

## Field Focus Management

When multiple widgets share one renderer, call `OnFocus()` / `OnBlur()` on the outgoing and incoming field:

```csharp
private void SetFocus(FocusTarget next)
{
    if (_focus == next) return;
    switch (_focus)
    {
        case FocusTarget.Title: _title.OnBlur(); break;
        case FocusTarget.Body:  _body.OnBlur();  break;
    }
    _focus = next;
    switch (next)
    {
        case FocusTarget.Title: _title.OnFocus(); break;
        case FocusTarget.Body:  _body.OnFocus();  break;
    }
}
```

Route keyboard to the focused widget only; route mouse to whichever widget was clicked (and switch focus on Down/Click).

## Mobile Virtual Keyboard

On platforms where `TouchScreenKeyboard.isSupported` is true (iOS, Android, mobile WebGL), `TerminalInput` and `TerminalTextArea` open the native IME on `OnFocus()` and close it on `OnBlur()`.

- `TerminalInput` — single-line, `secure` when `MaskChar` is set
- `TerminalTextArea` — multiline
- Configure via `KeyboardType` and `AutoCorrection`
- `Render(..., focused: true)` calls `SyncMobileKeyboard()` each frame to pull IME text and caret position into the widget

On desktop and console builds the bridge is a no-op; input continues through `Input.inputString` and the normal `OnTerminalKeyDown` path.

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
