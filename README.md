# ratatui-unity

[![Build Native Plugin](https://github.com/farukcan/ratatui-unity/actions/workflows/build.yml/badge.svg)](https://github.com/farukcan/ratatui-unity/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](Packages/com.farukcan.ratatui.unity/LICENSE)
[![Rust Edition 2021](https://img.shields.io/badge/Rust-2021_Edition-orange?logo=rust&logoColor=white)](https://www.rust-lang.org/)
[![ratatui 0.30](https://img.shields.io/badge/ratatui-0.30-blue?logo=rust)](https://ratatui.rs)
[![Unity](https://img.shields.io/badge/Unity-2021%2B-black?logo=unity&logoColor=white)](https://unity.com)
[![UPM](https://img.shields.io/badge/UPM-git--url-blue?logo=unity&logoColor=white)](https://github.com/farukcan/ratatui-unity.git#latest)
[![GitHub stars](https://img.shields.io/github/stars/farukcan/ratatui-unity?style=social)](https://github.com/farukcan/ratatui-unity/stargazers)
[![GitHub forks](https://img.shields.io/github/forks/farukcan/ratatui-unity?style=social)](https://github.com/farukcan/ratatui-unity/network/members)
[![GitHub last commit](https://img.shields.io/github/last-commit/farukcan/ratatui-unity)](https://github.com/farukcan/ratatui-unity/commits/main)
[![GitHub issues](https://img.shields.io/github/issues/farukcan/ratatui-unity)](https://github.com/farukcan/ratatui-unity/issues)

![Platform macOS](https://img.shields.io/badge/platform-macOS-lightgrey?logo=apple&logoColor=white)
![Platform iOS](https://img.shields.io/badge/platform-iOS-lightgrey?logo=apple&logoColor=white)
![Platform Windows](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows&logoColor=white)
![Platform Linux](https://img.shields.io/badge/platform-Linux-FCC624?logo=linux&logoColor=black)
![Platform Android](https://img.shields.io/badge/platform-Android-3DDC84?logo=android&logoColor=white)
![Platform WebGL](https://img.shields.io/badge/platform-WebGL-990000?logo=webgl&logoColor=white)

ratatui-unity is a Rust native plugin that brings [Ratatui](https://ratatui.rs)'s TUI ecosystem to Unity 3D game engine — for all platforms. 

<img width="958" height="598" alt="2026-06-10 at 22 51 59" src="https://github.com/user-attachments/assets/fe1dcbcc-ff08-43da-b380-72f3dc912968" />

Try WebGL **Demo** on your browser: [ratatui-unity-demo.farukcan.dev](https://ratatui-unity-demo.farukcan.dev/)


## UPM Installation

Open **Window → Package Manager → + → Add package from git URL** and paste:

```
https://github.com/farukcan/ratatui-unity.git#latest
```

<img width="557" height="199" alt="Screenshot 2026-06-10 at 23 21 02" src="https://github.com/user-attachments/assets/0b8dc545-8e07-414e-8315-a0359b0e7036" />

Or add to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.farukcan.ratatui.unity": "https://github.com/farukcan/ratatui-unity.git#latest"
  }
}
```

See [`ratatui-unity.farukcan.dev`](https://ratatui-unity.farukcan.dev/) for full documentation.

## Sample Usage

```csharp
private void RenderHelpArea(RatatuiTerminal term, uint area)
{
    term.Block(area, "About Ratatui + Unity", Borders.All);
    uint inner = term.Inner(area);

    term.BeginStyledParagraph(inner, Alignment.Left, true)
        .Span("ratatui-unity", fg: Color.cyan, modifiers: Modifier.Bold)
        .Span(" — a Rust ")
        .Span("ratatui", fg: Color.yellow, modifiers: Modifier.Italic)
        .Span(" rendering backend for Unity via FFI.").Line().Line()
        .Span("• All widget ", modifiers: Modifier.Bold).Span("data is owned in C#").Line()
        .Span("• Rust acts as a ").Span("pure rendering engine", fg: Color.green).Line()
        .Span("• Zero-copy pixel buffer via raw pointer").Line() .Line()
        .Span("Keyboard", modifiers: Modifier.Bold | Modifier.Underlined).Line()
        .Span("  A / D        ", fg: Color.cyan).Span("switch tabs").Line()
        .Span("  W / S        ", fg: Color.cyan).Span("navigate lists").Line()
        .Span("  Arrows       ", fg: Color.cyan).Span("same as W/S/A/D").Line().Line()
        .Span("Mouse", modifiers: Modifier.Bold | Modifier.Underlined).Line()
        .Span("  Click tab    ", fg: Color.cyan).Span("switch tabs").Line()
        .Span("  Click item   ", fg: Color.cyan).Span("select list item").Line()
        .Span("  Hover item   ", fg: Color.cyan).Span("highlight list item").Line()
        .Span("  Scroll       ", fg: Color.cyan).Span("navigate lists / cycle tabs").Line()
        .Render();
}
private void RenderForecastArea(RatatuiTerminal term, uint area)
{
    var cols = term.Split(area, Direction.Horizontal,
        Constraint.Percentage(50),
        Constraint.Percentage(50)
    );

    // High temps bar chart
    term.Block(cols[0], "Highs °C", Borders.All);
    uint highInner = term.Inner(cols[0]);
    var highSb = new StringBuilder();
    foreach (var f in Forecast) highSb.AppendLine($"{f.Day}\t{f.High}");
    term.SetStyle(new Color(1f, 0.6f, 0f), Color.clear);
    term.BarChart(highInner, highSb.ToString().TrimEnd(), barWidth: 3, barGap: 1);

    // Low temps bar chart
    term.Block(cols[1], "Lows °C", Borders.All);
    uint lowInner = term.Inner(cols[1]);
    var lowSb = new StringBuilder();
    foreach (var f in Forecast) lowSb.AppendLine($"{f.Day}\t{f.Low}");
    term.SetStyle(new Color(0.4f, 0.7f, 1f), Color.clear);
    term.BarChart(lowInner, lowSb.ToString().TrimEnd(), barWidth: 3, barGap: 1);
}
```

## Contributing

Contributions are welcome — bug reports, docs, samples, and pull requests. See [`CONTRIBUTING.md`](CONTRIBUTING.md) for setup, build instructions, PR workflow, and issue templates.

## License

MIT — see [`Packages/com.farukcan.ratatui.unity/LICENSE`](Packages/com.farukcan.ratatui.unity/LICENSE).  
JetBrains Mono font: SIL Open Font License 1.1.
