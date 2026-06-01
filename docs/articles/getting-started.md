# Getting Started

## Install via UPM

Open **Window → Package Manager → + → Add package from git URL** in Unity and paste:

```
https://github.com/farukcan/ratatui-unity.git#latest
```

Or edit `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.farukcan.ratatui.unity": "https://github.com/farukcan/ratatui-unity.git#latest"
  }
}
```

## Minimal Usage

```csharp
using RatatuiUnity;
using UnityEngine;

public class Demo : MonoBehaviour
{
    RatatuiTerminal terminal;

    void Start()
    {
        terminal = new RatatuiTerminal(width: 80, height: 24);
    }

    void Update()
    {
        terminal.BeginFrame();
        terminal.DrawBlock(x: 0, y: 0, w: 80, h: 24, title: "Hello");
        Texture2D tex = terminal.EndFrame();
        GetComponent<Renderer>().material.mainTexture = tex;
    }

    void OnDestroy() => terminal.Dispose();
}
```

See the `Samples~/BasicUsage` sample inside the UPM package for a full scene.

## Next

- [Architecture](architecture.md) — what happens between `BeginFrame()` and `EndFrame()`.
- [C# API Reference](xref:RatatuiUnity) — every public type.
