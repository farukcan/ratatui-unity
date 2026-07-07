using System;
using UnityEngine;

namespace RatatuiUnity
{
    /// <summary>
    /// A reusable command-line input built on top of <see cref="TerminalInput"/>.
    /// Adds the keystrokes a console/REPL needs (Enter/Escape/Tab/Up/Down) as
    /// events, while delegating editing (typing, selection, clipboard, undo/redo,
    /// horizontal scroll, mobile IME) to the underlying widget.
    ///
    /// Composition rather than inheritance: <see cref="TerminalInput"/> methods are
    /// not virtual, but its public API is rich enough that a wrapper can drive it.
    /// </summary>
    public class TerminalCommandInput
    {
        public TerminalInput Input { get; } = new TerminalInput();

        // ── Convenience proxies ──────────────────────────────────────────
        public string Text   { get => Input.Value;  set => Input.Value  = value; }
        public int    Cursor { get => Input.Cursor; set => Input.Cursor = value; }
        public string Prefix { get => Input.Prefix; set => Input.Prefix = value; }
        public Color  PrefixFg { get => Input.PrefixFg; set => Input.PrefixFg = value; }
        public Color  PrefixBg { get => Input.PrefixBg; set => Input.PrefixBg = value; }
        public string Placeholder { get => Input.Placeholder; set => Input.Placeholder = value; }
        public bool   ReadOnly { get => Input.ReadOnly; set => Input.ReadOnly = value; }
        public bool   HasSelection => Input.HasSelection;

        // ── Console-shaped events ────────────────────────────────────────
        /// <summary>Enter or KeypadEnter pressed. The wrapper consumes the key.</summary>
        public event Action OnSubmit;
        /// <summary>Escape pressed. The wrapper consumes the key.</summary>
        public event Action OnEscape;
        /// <summary>Tab pressed (no Shift). Consumed only if a handler is attached.</summary>
        public event Action OnTab;
        /// <summary>Shift+Tab pressed. Consumed only if a handler is attached.</summary>
        public event Action OnShiftTab;
        /// <summary>UpArrow → -1, DownArrow → +1. Only fires when no modifier is held so
        /// Cmd/Ctrl-modified arrows fall through to <see cref="TerminalInput"/>.</summary>
        public event Action<int> OnHistoryStep;
        /// <summary>Fires whenever a key event mutated <see cref="Text"/>. Useful for
        /// resetting external history-search state on any kind of edit (typing,
        /// paste, cut, undo, delete-word, …) without enumerating individual keys.</summary>
        public event Action OnEdit;

        // ── Key/mouse dispatch ───────────────────────────────────────────
        public bool HandleKeyEvent(TerminalKeyEvent e)
        {
            // 1. Intercept console-shaped shortcuts FIRST so they don't reach
            //    TerminalInput's printable-char path (Return/Tab/etc. would no-op
            //    there anyway, but we want to publish them as events).
            //    Skip the intercept when Ctrl/Cmd/Alt is held so combinations like
            //    Cmd+Z (undo) still reach TerminalInput.
            if (!e.HasCmdOrCtrl && !e.HasAlt)
            {
                switch (e.Key)
                {
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        OnSubmit?.Invoke();
                        return true;
                    case KeyCode.Escape:
                        OnEscape?.Invoke();
                        return true;
                    case KeyCode.Tab:
                        if (e.HasShift)
                        {
                            if (OnShiftTab == null) return false;
                            OnShiftTab.Invoke();
                        }
                        else
                        {
                            if (OnTab == null) return false;
                            OnTab.Invoke();
                        }
                        return true;
                    case KeyCode.UpArrow:
                        if (OnHistoryStep == null) break;  // fall through to Input
                        OnHistoryStep.Invoke(-1);
                        return true;
                    case KeyCode.DownArrow:
                        if (OnHistoryStep == null) break;
                        OnHistoryStep.Invoke(+1);
                        return true;
                }
            }

            // 2. Delegate to the editor. Diff Value before/after to decide whether
            //    this keystroke mutated the buffer — covers typing, paste, cut,
            //    delete, undo, redo without any per-key bookkeeping. The value
            //    diff is what authoritatively fires OnEdit; `handled` is only the
            //    function's return contract.
            string before = Input.Value;
            bool handled = Input.HandleKeyEvent(e);
            if (!ReferenceEquals(before, Input.Value) && before != Input.Value)
                OnEdit?.Invoke();
            return handled;
        }

        public bool HandleMouseEvent(TerminalMouseEvent e) => Input.HandleMouseEvent(e);

        public void OnFocus() => Input.OnFocus();
        public void OnBlur()  => Input.OnBlur();

        public void Render(
            RatatuiTerminal term, uint areaId,
            Color fg = default, Color bg = default,
            Color cursorFg = default, Color cursorBg = default,
            Color selectionFg = default, Color selectionBg = default,
            Color placeholderFg = default,
            bool focused = true)
        {
            Input.Render(term, areaId,
                fg: fg, bg: bg,
                cursorFg: cursorFg, cursorBg: cursorBg,
                selectionFg: selectionFg, selectionBg: selectionBg,
                placeholderFg: placeholderFg,
                focused: focused);
        }
    }
}
