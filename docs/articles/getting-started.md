# Getting Started

## Install via UPM

Open **Window → Package Manager → + → Add package from git URL** in Unity and paste:

```
https://github.com/farukcan/ratatui-unity.git#latest
```


<img width="557" height="199" alt="Screenshot 2026-06-10 at 23 21 02" src="https://github.com/user-attachments/assets/0b8dc545-8e07-414e-8315-a0359b0e7036" />

Or edit `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.farukcan.ratatui.unity": "https://github.com/farukcan/ratatui-unity.git#latest"
  }
}
```

## Minimal Usage

The easiest path is to subclass `RatatuiRenderer` — it owns the `Texture2D`, the
frame loop, input dispatch, and the `OnGUI` fallback. Override `BuildFrame` to
draw widgets:

```csharp
using RatatuiUnity;
using UnityEngine;

public class Demo : RatatuiRenderer
{
    protected override void BuildFrame(RatatuiTerminal term)
    {
        uint[] rows = term.Split(term.RootArea, Direction.Vertical,
            Constraint.Length(3),
            Constraint.Min(0));

        term.Block(rows[0], "Header", Borders.All);
        term.Paragraph(rows[1], "Hello from Ratatui!", Alignment.Center, wrap: true);
    }
}
```
---
Result:

<img width="349" height="145" alt="image" src="https://github.com/user-attachments/assets/72e1ecfd-193a-464d-b2d2-b210c76ac235" />

---

Attach the component to a GameObject. If you assign a UI **RawImage** or a
**MeshRenderer** to its inspector fields, the rendered texture is blitted there
each frame. Otherwise the renderer falls back to `OnGUI` (Full / Partial /
Window — see [Resolution & Readability](resolution-and-readability.md)).

---

<img width="537" height="720" alt="image" src="https://github.com/user-attachments/assets/4cf36f92-fbcc-48bf-9fd7-515c61a77913" />


### Driving `RatatuiTerminal` directly

If you need the lower-level API (custom frame pacing, manual texture
management), use `RatatuiTerminal` from any `MonoBehaviour`:

```csharp
using RatatuiUnity;
using System;
using UnityEngine;

public class Demo : MonoBehaviour
{
    RatatuiTerminal _term;
    Texture2D _tex;

    void Start()
    {
        _term = new RatatuiTerminal(cols: 80, rows: 24, fontSize: 14f);
        _tex = new Texture2D(_term.PixelWidth, _term.PixelHeight,
            TextureFormat.RGB24, mipChain: false);
        GetComponent<Renderer>().material.mainTexture = _tex;
    }

    void Update()
    {
        _term.BeginFrame();
        _term.Block(_term.RootArea, "Hello", Borders.All);
        IntPtr ptr = _term.EndFrameRaw();
        _tex.LoadRawTextureData(ptr, _term.PixelWidth * _term.PixelHeight * RatatuiTerminal.BytesPerPixel);
        _tex.Apply(updateMipmaps: false);
    }

    void OnDestroy()
    {
        _term?.Dispose();
        if (_tex != null) Destroy(_tex);
    }
}
```

`RatatuiTerminal` owns native memory and **must** be `Dispose()`d.
`EndFrameRaw()` returns a pointer into the native pixel buffer; the bytes are
RGB24 (`PixelWidth * PixelHeight * 3`) and remain valid until the next
`BeginFrame()` call.

See the `Samples~/BasicUsage` sample inside the UPM package for a full scene.

## Next

- [Architecture](architecture.md) — what happens between `BeginFrame()` and `EndFrame()`.
- [C# API Reference](xref:RatatuiUnity) — every public type.
