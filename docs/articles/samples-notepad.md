# Notepad Sample

The Notepad sample is a scene-independent terminal app that lets you create, edit, and delete notes with a filename and multiline body. Notes persist as JSON files under `Application.persistentDataPath/ratatui-notepad/`.

Import via **Window → Package Manager → ratatui-unity → Samples → Notepad → Import**.

## Quick Start

The app boots automatically before the first scene — no scene setup required.

| Action | Input |
|--------|-------|
| Toggle notepad | **F12** |
| New note | **F1** or click **NEW** |
| Save note | **Ctrl+S** or click **SAVE** |
| Delete note | **F2** or click **DEL** |
| Close | **Esc** or **F12** |
| Cycle focus (list → filename → note) | **Tab** / **Shift+Tab** |
| Navigate note list | **↑** / **↓** (when list is focused) |

From code:

```csharp
using RatatuiUnity.Samples.Notepad;

RatatuiNotepad.Open();
RatatuiNotepad.Toggle();
bool open = RatatuiNotepad.IsOpen;
string path = RatatuiNotepad.StoragePath;
```

From the developer console (when the Console sample is also imported):

```
open_notepad
close_notepad
```

## Architecture

```mermaid
flowchart TD
  Bootstrap["BeforeSceneLoad\nRatatuiNotepad.Bootstrap()"]
  Register["AfterAssembliesLoaded\nRegister&lt;RatatuiNotepadRenderer&gt;()"]
  Apps["RatatuiTerminalApps.Bootstrap()\ninstantiate notepad GameObject"]
  Storage["NotepadStorage\npersistentDataPath/ratatui-notepad/*.json"]
  Register --> Apps
  Bootstrap --> Storage
  Apps --> Renderer["RatatuiNotepadRenderer\nF12 toggle, UI"]
  Renderer --> Storage
```

| Type | Role |
|------|------|
| `RatatuiNotepad` | Public facade: `Open` / `Close` / `Toggle`, config, storage path |
| `RatatuiNotepadRenderer` | Terminal app UI: note list, filename field, `TerminalTextArea` |
| `NotepadStorage` | Load/save/delete JSON files in persistent storage |
| `RatatuiNotepadConfig` | Terminal dimensions, display mode, toggle key |

## Persistence

Each note is stored as `{id}.json`:

```json
{
  "filename": "Shopping list",
  "content": "Milk\nEggs\nBread"
}
```

- **Directory:** `Application.persistentDataPath/ratatui-notepad/`
- **Id:** GUID filename (stable even when the display filename changes)
- **Auto-save:** unsaved edits are written when switching notes or closing the app

## Configuration

Defaults are built in when no asset is present. Optional ScriptableObject:

**Create → Ratatui → Notepad Config**, then place it at `Resources/RatatuiNotepadConfig` if you want overrides at boot.

| Field | Default | Purpose |
|-------|---------|---------|
| `toggleKey` | `F12` | Keyboard toggle |
| `cols` / `rows` | 100 × 28 | Terminal size |
| `displayMode` | `Window` | OnGUI display mode |
| `windowStartMaximized` | `false` | Initial window state |

## See Also

- [Terminal Apps](terminal-apps.md)
- [Developer Console](samples-console.md)
- [Samples Overview](samples-overview.md)
