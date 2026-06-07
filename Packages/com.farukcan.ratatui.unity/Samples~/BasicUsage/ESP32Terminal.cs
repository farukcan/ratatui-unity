using System;
using UnityEngine;
using RatatuiUnity;

/// <summary>
/// Simulates an ESP32 embedded device terminal screen.
/// Type commands in the serial input, scroll logs with mouse wheel or PageUp/Down.
/// Commands: help, status, reboot, clear, gpio set [pin] [0|1], wifi scan
/// </summary>
public class ESP32Terminal : RatatuiRenderer
{
    // ── Simulated device state ───────────────────────────────────────────────

    private float _uptime;
    private float _cpuTemp = 42f;
    private float _heapFree = 180f;
    private float _wifiRssi = -52f;
    private int _gpioValue;
    private float _adcVoltage = 1.8f;

    // ── Sparkline ────────────────────────────────────────────────────────────

    private readonly ulong[] _tempHistory = new ulong[60];

    // ── Log buffer & scroll ──────────────────────────────────────────────────

    private readonly string[] _logBuffer = new string[128];
    private readonly string[] _logLevels = new string[128];
    private int _logCount;
    private int _logScroll; // 0 = bottom (newest), positive = scrolled up

    // ── Command input ────────────────────────────────────────────────────────

    private readonly TerminalInput _cmdInput = new TerminalInput("");

    // ── Area IDs for mouse hit-testing ───────────────────────────────────────

    private uint _logArea;
    private uint _inputArea;

    // ── Timing ───────────────────────────────────────────────────────────────

    private float _sensorTimer;
    private const float SensorInterval = 0.5f;
    private readonly System.Random _rng = new System.Random(42);

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();

        for (int i = 0; i < _tempHistory.Length; i++)
            _tempHistory[i] = 42;

        AddLog("I", "ESP32 boot complete");
        AddLog("I", "WiFi connected: HomeNetwork");
        AddLog("I", "IP: 192.168.1.105");
        AddLog("I", "MQTT broker connected");
        AddLog("I", "Sensors initialized");
        AddLog("I", "Type 'help' for commands");
    }

    protected override void Update()
    {
        float dt = Time.deltaTime;
        _uptime += dt;
        _sensorTimer += dt;

        if (_sensorTimer >= SensorInterval)
        {
            _sensorTimer -= SensorInterval;
            UpdateSensors();
        }

        base.Update();
    }

    // ── Sensor simulation ────────────────────────────────────────────────────

    private void UpdateSensors()
    {
        _cpuTemp = Mathf.Clamp(_cpuTemp + (_rng.Next(-10, 11) * 0.1f), 35f, 75f);
        _heapFree = Mathf.Clamp(_heapFree + (_rng.Next(-5, 6) * 0.5f), 80f, 250f);
        _wifiRssi = Mathf.Clamp(_wifiRssi + (_rng.Next(-3, 4) * 0.5f), -80f, -30f);
        _adcVoltage = Mathf.Clamp(_adcVoltage + (_rng.Next(-5, 6) * 0.01f), 0f, 3.3f);
        _gpioValue = _rng.Next(0, 2);

        Array.Copy(_tempHistory, 1, _tempHistory, 0, _tempHistory.Length - 1);
        _tempHistory[_tempHistory.Length - 1] = (ulong)Mathf.RoundToInt(_cpuTemp);

        if (_cpuTemp > 65f)
            AddLog("W", $"CPU temp high: {_cpuTemp:F1}C");
        if (_heapFree < 100f)
            AddLog("W", $"Low heap: {_heapFree:F0}KB free");
        if (_rng.Next(0, 20) == 0)
            AddLog("I", $"MQTT publish: sensor/temp={_cpuTemp:F1}");
    }

    private void AddLog(string level, string message)
    {
        string ts = TimeSpan.FromSeconds(_uptime).ToString(@"mm\:ss");
        string entry = $"[{ts}] [{level}] {message}";

        if (_logCount < _logBuffer.Length)
        {
            _logBuffer[_logCount] = entry;
            _logLevels[_logCount] = level;
            _logCount++;
        }
        else
        {
            Array.Copy(_logBuffer, 1, _logBuffer, 0, _logBuffer.Length - 1);
            Array.Copy(_logLevels, 1, _logLevels, 0, _logLevels.Length - 1);
            _logBuffer[_logBuffer.Length - 1] = entry;
            _logLevels[_logBuffer.Length - 1] = level;
        }

        // Auto-scroll to bottom on new log
        _logScroll = 0;
    }

    // ── Command execution ────────────────────────────────────────────────────

    private void ExecuteCommand(string raw)
    {
        string cmd = raw.Trim().ToLowerInvariant();
        AddLog(">", raw.Trim());

        if (cmd == "help")
        {
            AddLog("I", "Commands: help, status, reboot, clear, wifi scan");
        }
        else if (cmd == "status")
        {
            AddLog("I", $"Temp={_cpuTemp:F1}C Heap={_heapFree:F0}KB RSSI={_wifiRssi:F0}dBm ADC={_adcVoltage:F2}V GPIO2={_gpioValue}");
        }
        else if (cmd == "reboot")
        {
            AddLog("W", "Rebooting...");
            _uptime = 0;
            _cpuTemp = 42f;
            _heapFree = 180f;
            AddLog("I", "ESP32 boot complete");
        }
        else if (cmd == "clear")
        {
            _logCount = 0;
            _logScroll = 0;
        }
        else if (cmd == "wifi scan")
        {
            AddLog("I", "Scanning...");
            AddLog("I", "  HomeNetwork     CH6  -52dBm");
            AddLog("I", "  Neighbor_5G     CH36 -71dBm");
            AddLog("I", "  CoffeeShop      CH1  -83dBm");
        }
        else if (cmd.Length > 0)
        {
            AddLog("E", $"Unknown command: {cmd}");
        }
    }

    // ── Keyboard ─────────────────────────────────────────────────────────────

    protected override void OnTerminalKeyDown(TerminalKeyEvent e)
    {
        if (e.Key == KeyCode.Return || e.Key == KeyCode.KeypadEnter)
        {
            ExecuteCommand(_cmdInput.Value);
            _cmdInput.Value = "";
            _cmdInput.Cursor = 0;
            return;
        }

        if (e.Key == KeyCode.PageUp) { _logScroll = Mathf.Min(_logScroll + 5, Mathf.Max(0, _logCount - 1)); return; }
        if (e.Key == KeyCode.PageDown) { _logScroll = Mathf.Max(_logScroll - 5, 0); return; }

        _cmdInput.HandleKeyEvent(e);
    }

    // ── Mouse ────────────────────────────────────────────────────────────────

    protected override void OnTerminalMouseEvent(TerminalMouseEvent e)
    {
        // Scroll logs
        if (e.AreaId == _logArea && e.Type == MouseEventType.Scroll)
        {
            if (e.ScrollDelta > 0)
                _logScroll = Mathf.Min(_logScroll + 3, Mathf.Max(0, _logCount - 1));
            else
                _logScroll = Mathf.Max(_logScroll - 3, 0);
            return;
        }

        // Click on input to position cursor
        if (e.AreaId == _inputArea && e.Type == MouseEventType.Click && e.Button == MouseButton.Left)
        {
            _cmdInput.HandleMouseEvent(e);
        }
    }

    // ── Render ───────────────────────────────────────────────────────────────

    protected override void BuildFrame(RatatuiTerminal term)
    {
        uint root = term.RootArea;

        var rows = term.Split(root, Direction.Vertical,
            Constraint.Length(3),  // header
            Constraint.Length(7),  // system info
            Constraint.Length(5),  // sparkline
            Constraint.Min(5),    // log
            Constraint.Length(3)); // command input

        if (rows.Length < 5) return;

        RenderHeader(term, rows[0]);
        RenderSystemInfo(term, rows[1]);
        RenderTempGraph(term, rows[2]);
        RenderLog(term, rows[3]);
        RenderInput(term, rows[4]);
    }

    private void RenderHeader(RatatuiTerminal term, uint area)
    {
        term.Block(area, "ESP32-S3 DevKit", Borders.All);
        uint inner = term.Inner(area);
        string uptimeStr = TimeSpan.FromSeconds(_uptime).ToString(@"hh\:mm\:ss");
        term.SetStyle(new Color(0.2f, 1f, 0.4f), Color.clear, Modifier.Bold);
        term.Paragraph(inner, $"  Uptime: {uptimeStr}    Chip: ESP32-S3    Flash: 4MB    PSRAM: 2MB");
    }

    private void RenderSystemInfo(RatatuiTerminal term, uint area)
    {
        var cols = term.Split(area, Direction.Horizontal,
            Constraint.Percentage(40),
            Constraint.Percentage(30),
            Constraint.Percentage(30));

        if (cols.Length < 3) return;

        // CPU & Memory
        term.Block(cols[0], "CPU / Memory", Borders.All);
        uint cpuInner = term.Inner(cols[0]);
        var cpuRows = term.Split(cpuInner, Direction.Vertical,
            Constraint.Length(1),
            Constraint.Length(2),
            Constraint.Min(0));

        if (cpuRows.Length >= 2)
        {
            Color tempColor = _cpuTemp > 60f ? new Color(1f, 0.3f, 0.3f)
                            : _cpuTemp > 50f ? Color.yellow
                            : new Color(0.3f, 1f, 0.5f);
            term.SetStyle(tempColor, Color.clear);
            term.Paragraph(cpuRows[0], $"  Temp: {_cpuTemp:F1} C");

            float heapRatio = _heapFree / 320f;
            term.SetStyle(new Color(0.3f, 0.7f, 1f), Color.clear);
            term.Gauge(cpuRows[1], heapRatio, $"Heap: {_heapFree:F0}/320 KB");
        }

        // WiFi
        term.Block(cols[1], "WiFi", Borders.All);
        uint wifiInner = term.Inner(cols[1]);
        int bars = _wifiRssi > -40f ? 4 : _wifiRssi > -55f ? 3 : _wifiRssi > -70f ? 2 : 1;
        string signal = new string('|', bars) + new string('.', 4 - bars);
        term.BeginStyledParagraph(wifiInner, Alignment.Left, false)
            .SpanLine("  SSID: HomeNetwork")
            .Span("  RSSI: ").Span($"{_wifiRssi:F0} dBm", _wifiRssi > -60f ? Color.green : Color.yellow).Line()
            .Span("  Signal: ").Span(signal, new Color(0.2f, 1f, 0.4f), modifiers: Modifier.Bold).Line()
            .SpanLine("  IP: 192.168.1.105")
            .Render();

        // GPIO / ADC
        term.Block(cols[2], "GPIO / ADC", Borders.All);
        uint gpioInner = term.Inner(cols[2]);
        Color gpioColor = _gpioValue == 1 ? Color.green : Color.red;
        term.BeginStyledParagraph(gpioInner, Alignment.Left, false)
            .Span("  GPIO2: ").Span(_gpioValue == 1 ? "HIGH" : "LOW ", gpioColor, modifiers: Modifier.Bold).Line()
            .SpanLine($"  ADC0:  {_adcVoltage:F2} V")
            .SpanLine("  PWM:   50%")
            .SpanLine("  I2C:   0x3C (SSD1306)")
            .Render();
    }

    private void RenderTempGraph(RatatuiTerminal term, uint area)
    {
        term.Block(area, "CPU Temperature (60s)", Borders.All);
        uint inner = term.Inner(area);
        Color sparkColor = _cpuTemp > 60f ? new Color(1f, 0.3f, 0.3f) : new Color(0.2f, 0.8f, 1f);
        term.SetStyle(sparkColor, Color.clear);
        term.Sparkline(inner, _tempHistory);
    }

    private void RenderLog(RatatuiTerminal term, uint area)
    {
        _logArea = area;
        string scrollInfo = _logScroll > 0 ? $" [+{_logScroll}]" : "";
        term.Block(area, $"Serial Monitor{scrollInfo}", Borders.All);
        uint inner = term.Inner(area);

        if (!term.TryGetAreaRect(inner, out int ax, out int ay, out int aw, out int ah))
            return;

        int visibleLines = ah;
        int startIndex = Mathf.Max(0, _logCount - visibleLines - _logScroll);
        int endIndex = Mathf.Min(_logCount, startIndex + visibleLines);

        var builder = term.BeginStyledParagraph(inner, Alignment.Left, false);
        for (int i = startIndex; i < endIndex; i++)
        {
            Color lineColor = _logLevels[i] switch
            {
                "W" => Color.yellow,
                "E" => Color.red,
                ">" => Color.cyan,
                _ => new Color(0.6f, 0.8f, 0.6f),
            };
            builder.SpanLine(_logBuffer[i], lineColor);
        }
        builder.Render();

        // Scrollbar
        if (_logCount > visibleLines)
        {
            int scrollPos = Mathf.Max(0, _logCount - visibleLines - _logScroll);
            term.Scrollbar(area, _logCount, scrollPos, visibleLines);
        }
    }

    private void RenderInput(RatatuiTerminal term, uint area)
    {
        term.SetStyle(Color.cyan, Color.clear, Modifier.Bold);
        term.Block(area, "Command (Enter=send)", Borders.All);
        _inputArea = term.Inner(area);
        _cmdInput.Render(term, _inputArea, cursorFg: Color.black, cursorBg: Color.white);
    }
}
