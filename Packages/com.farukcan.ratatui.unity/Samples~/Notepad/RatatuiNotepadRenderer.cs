using System;
using System.Collections.Generic;
using UnityEngine;

namespace RatatuiUnity.Samples.Notepad
{
    /// <summary>
    /// Terminal app for editing and persisting notes. Created automatically by
    /// <see cref="RatatuiTerminalApps"/> — do not add to scenes manually.
    /// </summary>
    [RatatuiTerminalApp("notepad", DisplayName = "Notepad", Order = 10)]
    [DefaultExecutionOrder(-100)]
    public sealed class RatatuiNotepadRenderer : RatatuiTerminalApp
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterApp()
        {
            RatatuiTerminalApps.Register<RatatuiNotepadRenderer>("notepad", "Notepad", 10);
        }

        private enum FocusTarget : byte { List = 0, Title = 1, Note = 2 }

        private RatatuiNotepadConfig _config;
        private readonly List<NotepadEntry> _notes = new List<NotepadEntry>();
        private int _selectedIndex = -1;
        private string _editorNoteId;
        private bool _dirty;
        private bool _listOpen;
        private bool _sessionInitialized;
        private FocusTarget _lastEditorFocus = FocusTarget.Note;

        private readonly TerminalInput _title = new TerminalInput
        {
            Placeholder = "Untitled",
            SelectAllOnFocus = false,
        };
        private readonly TerminalTextArea _note = new TerminalTextArea
        {
            Placeholder = "Start writing…",
            SelectAllOnFocus = false,
        };

        private FocusTarget _focus = FocusTarget.Note;

        private uint _listArea;
        private int _listTop;
        private int _listVisibleRows;
        private int _listScroll;
        private int _hoveredNote = -1;

        private uint _titleArea;
        private uint _openBtnArea;
        private uint _newBtnArea;
        private uint _saveBtnArea;
        private uint _deleteBtnArea;

        private static readonly Color ColorBorder = new Color(0.45f, 0.45f, 0.5f);
        private static readonly Color ColorFocus = new Color(0.4f, 0.8f, 1.0f);
        private static readonly Color ColorText = new Color(0.85f, 0.85f, 0.9f);
        private static readonly Color ColorDim = new Color(0.5f, 0.5f, 0.55f);
        private static readonly Color ColorButtonHi = new Color(0.4f, 0.8f, 1.0f);
        private static readonly Color ColorHoverBg = new Color(0.12f, 0.2f, 0.32f);
        private static readonly Color ColorOpenBtn = new Color(1.0f, 0.85f, 0.35f);
        private static readonly Color ColorSaveBtn = new Color(0.55f, 0.9f, 0.55f);
        private static readonly Color ColorDeleteBtn = new Color(1.0f, 0.5f, 0.5f);
        private static readonly Color ColorNewBtn = new Color(0.55f, 0.95f, 0.95f);

        private const string OpenBtnLabel = "[ F2 OPEN ]";
        private const string NewBtnLabel = "[ F5 NEW ]";
        private const string SaveBtnLabel = "[ F3 SAVE ]";
        private const string DeleteBtnLabel = "[ F4 DELETE ]";

        protected override KeyCode ToggleKey => _config != null ? _config.toggleKey : KeyCode.F12;

        protected override void Awake()
        {
            RatatuiNotepad.EnsureServicesBooted();
            _config = RatatuiNotepad.Config;
            if (_config == null) _config = RatatuiNotepadConfig.CreateDefault();
            ApplyConfigToBase(_config);
            base.Awake();
        }

        protected override void OnOpened()
        {
            _listOpen = false;
            if (!_sessionInitialized)
            {
                LoadInitialNotesFromDisk();
                _sessionInitialized = true;
            }
            RestoreEditorFocus();
        }

        protected override void OnClosed()
        {
            _lastEditorFocus = _focus == FocusTarget.Title ? FocusTarget.Title : FocusTarget.Note;
            _listOpen = false;
            _title.OnBlur();
            _note.OnBlur();
            _focus = FocusTarget.List;
        }

        protected override void BuildFrame(RatatuiTerminal term)
        {
            if (!IsOpen) return;

            uint root = term.RootArea;
            var rows = term.Split(root, Direction.Vertical,
                Constraint.Length(3),
                Constraint.Fill(1));

            BuildToolbar(term, rows[0]);

            if (_listOpen)
            {
                var cols = term.Split(rows[1], Direction.Horizontal,
                    Constraint.Percentage(28),
                    Constraint.Percentage(72));
                BuildNoteList(term, cols[0]);
                BuildEditor(term, cols[1]);
                return;
            }

            BuildEditor(term, rows[1]);
        }

        private void BuildToolbar(RatatuiTerminal term, uint area)
        {
            term.SetStyle(ColorBorder, Color.clear, Modifier.None);
            term.Block(area, " Notepad ", Borders.All);
            uint inner = term.Inner(area);

            if (_listOpen)
            {
                var cols = term.Split(inner, Direction.Horizontal,
                    Constraint.Length(16),
                    Constraint.Length(14),
                    Constraint.Length(16),
                    Constraint.Length(18),
                    Constraint.Fill(1));

                if (cols.Length < 5) return;

                _openBtnArea = DrawToolbarButton(term, cols[0], OpenBtnLabel, ColorOpenBtn);
                _newBtnArea = DrawToolbarButton(term, cols[1], NewBtnLabel, ColorNewBtn);
                _saveBtnArea = DrawToolbarButton(term, cols[2], SaveBtnLabel, ColorSaveBtn);
                _deleteBtnArea = DrawToolbarButton(term, cols[3], DeleteBtnLabel, ColorDeleteBtn);

                term.SetStyle(ColorDim, Color.clear, Modifier.None);
                term.Paragraph(cols[4], "pick a note or Esc to close list");
                return;
            }

            var idleCols = term.Split(inner, Direction.Horizontal,
                Constraint.Length(16),
                Constraint.Length(14),
                Constraint.Length(16),
                Constraint.Fill(1));

            if (idleCols.Length < 4) return;

            _openBtnArea = DrawToolbarButton(term, idleCols[0], OpenBtnLabel, ColorOpenBtn);
            _newBtnArea = DrawToolbarButton(term, idleCols[1], NewBtnLabel, ColorNewBtn);
            _saveBtnArea = DrawToolbarButton(term, idleCols[2], SaveBtnLabel, ColorSaveBtn);
            _deleteBtnArea = 0;

            string hint = _dirty ? "unsaved" : string.Empty;
            term.SetStyle(ColorDim, Color.clear, Modifier.None);
            term.Paragraph(idleCols[3], hint);
        }

        private void BuildEditor(RatatuiTerminal term, uint area)
        {
            var rows = term.Split(area, Direction.Vertical,
                Constraint.Length(3),
                Constraint.Fill(1));

            if (rows.Length < 2) return;

            bool titleFocused = !_listOpen && _focus == FocusTarget.Title;
            term.SetStyle(titleFocused ? ColorFocus : ColorBorder, Color.clear,
                titleFocused ? Modifier.Bold : Modifier.None);
            term.Block(rows[0], " Title ", Borders.All);
            _titleArea = term.Inner(rows[0]);
            _title.Render(term, _titleArea, focused: titleFocused);

            bool noteFocused = !_listOpen && _focus == FocusTarget.Note;
            term.SetStyle(noteFocused ? ColorFocus : ColorBorder, Color.clear,
                noteFocused ? Modifier.Bold : Modifier.None);
            term.Block(rows[1], " Note ", Borders.All);
            _note.Render(term, term.Inner(rows[1]), focused: noteFocused);
        }

        private void BuildNoteList(RatatuiTerminal term, uint area)
        {
            bool focused = _focus == FocusTarget.List;
            term.SetStyle(focused ? ColorFocus : ColorBorder, Color.clear,
                focused ? Modifier.Bold : Modifier.None);
            term.Block(area, " Notes ", Borders.All);
            uint inner = term.Inner(area);

            _listArea = inner;
            _listVisibleRows = 0;
            if (term.TryGetAreaRect(inner, out _, out int ay, out _, out int ah))
            {
                _listTop = ay;
                _listVisibleRows = ah;
            }

            EnsureSelectedVisible(_listVisibleRows);
            RenderNoteList(term, inner, _selectedIndex, _hoveredNote, _listScroll, _listVisibleRows);

            if (_notes.Count > 0)
            {
                term.Scrollbar(area, _notes.Count, _listScroll,
                    viewportLength: Math.Max(1, _listVisibleRows),
                    orientation: ScrollbarOrientation.VerticalRight);
            }
        }

        private void RenderNoteList(RatatuiTerminal term, uint area, int selected, int hovered,
            int scroll, int visibleRows)
        {
            var builder = term.BeginStyledParagraph(area, Alignment.Left, wrap: false);

            if (_notes.Count == 0)
            {
                builder.SpanLine("(no saved notes)", ColorDim);
                builder.Render();
                return;
            }

            int end = visibleRows <= 0
                ? _notes.Count
                : Math.Min(_notes.Count, scroll + visibleRows);

            for (int i = scroll; i < end; i++)
            {
                bool isSelected = i == selected;
                bool isHovered = i == hovered && !isSelected;
                Color fg = isSelected ? Color.black
                         : isHovered ? Color.white
                         : ColorText;
                Color bg = isSelected ? ColorFocus
                         : isHovered ? ColorHoverBg
                         : Color.clear;

                string label = string.IsNullOrWhiteSpace(_notes[i].Title)
                    ? "Untitled"
                    : _notes[i].Title;
                builder.SpanLine(Truncate(label, 24), fg, bg);
            }

            builder.Render();
        }

        protected override void OnTerminalKeyDown(TerminalKeyEvent e)
        {
            if (!IsOpen) return;
            if (ShouldSuppressToggleKeyEvent(e)) return;

            if (e.Key == KeyCode.Escape)
            {
                if (_listOpen)
                {
                    CloseNoteList();
                    return;
                }
                SetOpen(false);
                return;
            }

            if (e.Key == KeyCode.Tab)
            {
                CycleFocus(e.HasShift ? -1 : 1);
                return;
            }

            switch (e.Key)
            {
                case KeyCode.F2:
                    OpenNoteList();
                    return;
                case KeyCode.F3:
                    SaveCurrent();
                    return;
                case KeyCode.F4:
                    if (_listOpen) DeleteSelectedNote();
                    return;
                case KeyCode.F5:
                    CreateNewNote();
                    return;
            }

            if (_listOpen && _focus == FocusTarget.List)
            {
                if (e.Key == KeyCode.UpArrow)
                {
                    SelectRelative(-1);
                    return;
                }
                if (e.Key == KeyCode.DownArrow)
                {
                    SelectRelative(1);
                    return;
                }
                if (e.Key == KeyCode.Return || e.Key == KeyCode.KeypadEnter)
                {
                    if (_selectedIndex >= 0)
                        PickNoteFromList(_selectedIndex);
                    else
                        CloseNoteList();
                    return;
                }
                return;
            }

            if (_listOpen) return;

            if (_focus == FocusTarget.Title)
            {
                _title.HandleKeyEvent(e);
                MarkDirty();
                return;
            }

            _note.HandleKeyEvent(e);
            MarkDirty();
        }

        protected override void OnTerminalMouseEvent(TerminalMouseEvent e)
        {
            if (!IsOpen) return;
            if (ShouldIgnoreMouseThisFrame()) return;

            if (e.Type == MouseEventType.Scroll)
            {
                if (_listOpen && HitList(e.AreaId))
                {
                    if (e.ScrollDelta > 0) SelectRelative(-1);
                    else SelectRelative(1);
                    return;
                }
                if (!_listOpen && _note.OwnsArea(e.AreaId))
                {
                    _note.HandleMouseEvent(e);
                    return;
                }
                return;
            }

            if (e.Type == MouseEventType.Click && e.Button == MouseButton.Left)
            {
                if (e.AreaId == _openBtnArea) { OpenNoteList(); return; }
                if (e.AreaId == _newBtnArea) { CreateNewNote(); return; }
                if (e.AreaId == _saveBtnArea) { SaveCurrent(); return; }
                if (_listOpen && e.AreaId == _deleteBtnArea) { DeleteSelectedNote(); return; }

                if (_listOpen && HitList(e.AreaId))
                {
                    int row = e.Row - _listTop;
                    int index = _listScroll + row;
                    if (index >= 0 && index < _notes.Count)
                        PickNoteFromList(index);
                    return;
                }

                if (!_listOpen && e.AreaId == _titleArea)
                {
                    SetFocus(FocusTarget.Title);
                    _title.HandleMouseEvent(e);
                    return;
                }

                if (!_listOpen && _note.OwnsArea(e.AreaId))
                {
                    SetFocus(FocusTarget.Note);
                    _note.HandleMouseEvent(e);
                    return;
                }
            }

            if (e.Button != MouseButton.Left || _listOpen) return;

            if (e.AreaId == _titleArea)
            {
                if (e.Type == MouseEventType.Down) SetFocus(FocusTarget.Title);
                _title.HandleMouseEvent(e);
                return;
            }

            if (_note.OwnsArea(e.AreaId))
            {
                if (e.Type == MouseEventType.Down) SetFocus(FocusTarget.Note);
                _note.HandleMouseEvent(e);
                return;
            }

            if (_focus == FocusTarget.Title
                && (e.Type == MouseEventType.Move || e.Type == MouseEventType.Up))
            {
                _title.HandleMouseEvent(e);
            }
            else if (_focus == FocusTarget.Note
                && (e.Type == MouseEventType.Move || e.Type == MouseEventType.Up))
            {
                _note.HandleMouseEvent(e);
            }
        }

        protected override void OnTerminalHoverChanged(TerminalHoverState oldState, TerminalHoverState newState)
        {
            if (!IsOpen || !_listOpen) return;

            _hoveredNote = HitList(newState.AreaId)
                ? _listScroll + (newState.Row - _listTop)
                : -1;
        }

        private void OpenNoteList()
        {
            MergeNotesForListPicker();
            _listOpen = true;
            SetFocus(FocusTarget.List);
        }

        private void CloseNoteList()
        {
            _listOpen = false;
            SetFocus(FocusTarget.Note);
        }

        private void PickNoteFromList(int index)
        {
            if (index < 0 || index >= _notes.Count)
            {
                CloseNoteList();
                return;
            }

            if (index == _selectedIndex && _notes[index].Id == _editorNoteId)
            {
                CloseNoteList();
                return;
            }

            SelectNote(index);
            CloseNoteList();
        }

        private void LoadInitialNotesFromDisk()
        {
            _notes.Clear();
            _notes.AddRange(NotepadStorage.LoadAll());

            if (_notes.Count == 0)
            {
                _selectedIndex = -1;
                _editorNoteId = null;
                _title.Value = string.Empty;
                _note.Value = string.Empty;
                _dirty = false;
                return;
            }

            SelectNote(0, skipSync: true);
        }

        /// <summary>
        /// Refresh the picker list from disk while keeping in-memory editor text intact.
        /// </summary>
        private void MergeNotesForListPicker()
        {
            SyncEditorToMemoryEntry();

            var merged = new Dictionary<string, NotepadEntry>(StringComparer.Ordinal);
            foreach (var entry in NotepadStorage.LoadAll())
                merged[entry.Id] = entry;

            foreach (var entry in _notes)
                merged[entry.Id] = entry;

            _notes.Clear();
            _notes.AddRange(merged.Values);
            _notes.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(_editorNoteId))
            {
                _selectedIndex = _notes.Count > 0 ? 0 : -1;
                return;
            }

            _selectedIndex = _notes.FindIndex(n => n.Id == _editorNoteId);
            if (_selectedIndex < 0 && _notes.Count > 0)
                _selectedIndex = 0;
        }

        private void SelectNote(int index, bool skipSync = false)
        {
            if (index < 0 || index >= _notes.Count) return;
            if (!skipSync) SyncEditorToMemoryEntry();

            _selectedIndex = index;
            var entry = _notes[index];
            _editorNoteId = entry.Id;
            _title.Value = entry.Title ?? string.Empty;
            _note.Value = entry.Content ?? string.Empty;
            _dirty = EntryDiffersFromDisk(entry);
        }

        private void SyncEditorToMemoryEntry()
        {
            if (HasEditorContent() || _dirty)
                EnsureCurrentEntry();

            // Use _editorNoteId — not _selectedIndex — because after a delete
            // the index can clamp onto a sibling note while the editor is empty.
            // Writing through _selectedIndex would erase that sibling's data.
            if (string.IsNullOrEmpty(_editorNoteId)) return;

            int idx = _notes.FindIndex(n => n.Id == _editorNoteId);
            if (idx < 0) return;

            var entry = _notes[idx];
            entry.Title = _title.Value;
            entry.Content = _note.Value;
        }

        private bool HasEditorContent()
        {
            return !string.IsNullOrEmpty(_title.Value) || !string.IsNullOrEmpty(_note.Value);
        }

        private static bool EntryDiffersFromDisk(NotepadEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                return true;

            foreach (var disk in NotepadStorage.LoadAll())
            {
                if (!string.Equals(disk.Id, entry.Id, StringComparison.Ordinal))
                    continue;

                return !string.Equals(disk.Title ?? string.Empty, entry.Title ?? string.Empty, StringComparison.Ordinal)
                    || !string.Equals(disk.Content ?? string.Empty, entry.Content ?? string.Empty, StringComparison.Ordinal);
            }

            return true;
        }

        private void RestoreEditorFocus()
        {
            if (_listOpen)
            {
                SetFocus(FocusTarget.List);
                return;
            }

            SetFocus(_lastEditorFocus);
        }

        private void SelectRelative(int delta)
        {
            if (_notes.Count == 0) return;
            int next = _selectedIndex < 0 ? 0 : (_selectedIndex + delta + _notes.Count) % _notes.Count;
            _selectedIndex = next;
        }

        private void EnsureCurrentEntry()
        {
            // The editor is "bound" to _editorNoteId, not _selectedIndex.
            // After a delete _selectedIndex can point to a sibling while the
            // editor itself has no backing note — in that case a fresh entry
            // must be created instead of hijacking the sibling.
            if (!string.IsNullOrEmpty(_editorNoteId))
            {
                int existing = _notes.FindIndex(n => n.Id == _editorNoteId);
                if (existing >= 0)
                {
                    _selectedIndex = existing;
                    return;
                }
            }

            var entry = new NotepadEntry
            {
                Id = NotepadStorage.CreateId(),
                Title = string.Empty,
                Content = string.Empty,
            };
            _notes.Add(entry);
            _selectedIndex = _notes.Count - 1;
            _editorNoteId = entry.Id;
        }

        private void CreateNewNote()
        {
            SyncEditorToMemoryEntry();
            _listOpen = false;

            var entry = new NotepadEntry
            {
                Id = NotepadStorage.CreateId(),
                Title = string.Empty,
                Content = string.Empty,
            };

            _notes.Add(entry);
            _selectedIndex = _notes.Count - 1;
            _editorNoteId = entry.Id;
            _title.Value = string.Empty;
            _note.Value = string.Empty;
            _dirty = false;
            SetFocus(FocusTarget.Title);
        }

        private void SaveCurrent()
        {
            EnsureCurrentEntry();
            if (_selectedIndex < 0 || _selectedIndex >= _notes.Count) return;

            var entry = _notes[_selectedIndex];
            entry.Title = _title.Value.Trim();
            entry.Content = _note.Value;

            NotepadStorage.Save(entry);
            _dirty = false;
        }

        private void DeleteSelectedNote()
        {
            if (!_listOpen) return;
            if (_selectedIndex < 0 || _selectedIndex >= _notes.Count)
                return;

            var entry = _notes[_selectedIndex];
            string deletedId = entry.Id;
            NotepadStorage.Delete(deletedId);
            _notes.RemoveAt(_selectedIndex);

            if (deletedId == _editorNoteId)
            {
                _editorNoteId = null;
                _title.Value = string.Empty;
                _note.Value = string.Empty;
                _dirty = false;
            }

            if (_notes.Count == 0)
            {
                _selectedIndex = -1;
                return;
            }

            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _notes.Count - 1);
        }

        private void MarkDirty()
        {
            _dirty = true;
            EnsureCurrentEntry();
        }

        private void SetFocus(FocusTarget next)
        {
            if (_focus == next) return;

            switch (_focus)
            {
                case FocusTarget.Title: _title.OnBlur(); break;
                case FocusTarget.Note: _note.OnBlur(); break;
            }

            _focus = next;

            switch (next)
            {
                case FocusTarget.Title: _title.OnFocus(); break;
                case FocusTarget.Note: _note.OnFocus(); break;
            }
        }

        private void CycleFocus(int dir)
        {
            if (_listOpen)
            {
                SetFocus(FocusTarget.List);
                return;
            }

            SetFocus(_focus == FocusTarget.Title ? FocusTarget.Note : FocusTarget.Title);
        }

        private void EnsureSelectedVisible(int visibleRows)
        {
            if (visibleRows <= 0)
            {
                _listScroll = 0;
                return;
            }

            if (_selectedIndex >= 0)
            {
                if (_selectedIndex < _listScroll) _listScroll = _selectedIndex;
                else if (_selectedIndex >= _listScroll + visibleRows)
                    _listScroll = _selectedIndex - visibleRows + 1;
            }

            int maxScroll = Math.Max(0, _notes.Count - visibleRows);
            _listScroll = Mathf.Clamp(_listScroll, 0, maxScroll);
        }

        private bool HitList(uint areaId) => areaId == _listArea;

        // Render on the split column directly — same pattern as console filter tabs.
        // Nesting Block inside the 1-row toolbar inner area leaves zero content rows
        // and breaks both label layout and hit-testing.
        private uint DrawToolbarButton(RatatuiTerminal term, uint area, string label, Color buttonColor)
        {
            bool hover = IsHovering(area);
            Color fg = hover ? ColorButtonHi : buttonColor;
            Color bg = hover ? ColorHoverBg : Color.clear;
            term.BeginStyledParagraph(area, Alignment.Center, false)
                .Span(label, fg: fg, bg: bg, modifiers: hover ? Modifier.Bold : Modifier.None)
                .Render();
            return area;
        }

        private bool IsHovering(uint areaId)
        {
            return areaId != 0 && HoverState.IsInside && HoverState.AreaId == areaId;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? string.Empty;
            return s.Substring(0, max) + "…";
        }

        private void ApplyConfigToBase(RatatuiNotepadConfig cfg)
        {
            Cols = cfg.cols;
            Rows = cfg.rows;
            FontSize = cfg.fontSize;
            SizingMode = cfg.sizingMode;
            BackgroundColor = cfg.backgroundColor;
            OnGuiDisplayMode = cfg.displayMode;
            OnGuiHorizontalAlignment = cfg.horizontalAlign;
            OnGuiVerticalAlignment = cfg.verticalAlign;
            WindowStartMaximized = cfg.windowStartMaximized;
            WindowChromeFont = cfg.windowChromeFont;
            FitColsAndRows = true;
            EnableInput = true;
            EnableMouseInput = true;
            EnableKeyboardInput = true;
        }
    }
}
