# Samples Overview

The UPM package ships three runnable samples under `Packages/com.farukcan.ratatui.unity/Samples~/`. Import them via **Window → Package Manager → ratatui-unity → Samples → Import**.

| Sample | Folder | What it shows |
|--------|--------|---------------|
| BasicUsage | `Samples~/BasicUsage/` | Full tabbed demo: 9 tabs covering every widget, layout, input, hover, animation. |
| Developer Console | `Samples~/Console/` | Drop-in runtime console (logs + command registry) auto-bootstrapped before scene load. |
| Notepad | `Samples~/Notepad/` | Persistent notepad terminal app (`TerminalInput` title + `TerminalTextArea` body, F9 toggle). |

Each sample is self-contained — it has its own `.asmdef`, only depends on `RatatuiUnity.Runtime`.

## At-a-Glance

```mermaid
graph LR
    A[BasicUsage] --> A1[RatatuiDemo<br/>tab host]
    A1 --> A2[Dashboard]
    A1 --> A3[Servers]
    A1 --> A4[Colors]
    A1 --> A5[Email]
    A1 --> A6[Recipe]
    A1 --> A7[Weather]
    A1 --> A8[Traceroute]
    A1 --> A9[Input]
    A1 --> A10[About]
    A --> B[ESP32Terminal<br/>standalone device sim]

    C[Console] --> C0[RatatuiTerminalApps<br/>framework bootstrap]
    C0 --> C5[RatatuiConsoleRenderer<br/>TerminalApp]
    C --> C1[RatatuiConsole<br/>public facade]
    C1 --> C2[ConsoleLogCapture]
    C1 --> C3[ConsoleCommandRegistry]
    C1 --> C4[ConsoleHistory]

    D[Notepad] --> D0[RatatuiTerminalApps]
    D0 --> D1[RatatuiNotepadRenderer]
    D --> D2[RatatuiNotepad facade]
    D2 --> D3[NotepadStorage]
```

## Next

- [BasicUsage (Tabs Demo)](samples-basic-usage.md) — what each tab demonstrates and how to read its code.
- [Developer Console](samples-console.md) — how to use, configure, and extend the runtime console.
- [Notepad](samples-notepad.md) — persistent notes terminal app.
- [Terminal Apps](terminal-apps.md) — framework for scene-independent terminal apps (open/close, attribute discovery).
