using UnityEngine;
using RatatuiUnity;
using RatatuiUnity.Demo;

/// <summary>
/// Combined ratatui-unity demo: Demo1 (Dashboard, Servers, Colors) + Demo2 (About, Recipe,
/// Email, Traceroute, Weather) united as a single tabbed application.
///
/// Attach to any GameObject alongside a <see cref="RatatuiRenderer"/> component.
/// Inherits the standard lifecycle: Awake → Update → BuildFrame.
/// </summary>
public class RatatuiDemo : RatatuiRenderer
{
    // Terminal size and font size are configured via the RatatuiRenderer Inspector fields.

    // ── State ─────────────────────────────────────────────────────────────────

    private ITab[] _tabs;
    private int    _activeTab;

    // ── ITab instances ────────────────────────────────────────────────────────

    private DashboardTab  _dashboard;
    private ServersTab    _servers;
    private ColorsTab     _colors;
    private AboutTab      _about;
    private RecipeTab     _recipe;
    private EmailTab      _email;
    private TracerouteTab _traceroute;
    private WeatherTab    _weather;

    // ─────────────────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();

        _dashboard  = new DashboardTab();
        _servers    = new ServersTab();
        _colors     = new ColorsTab();
        _about      = new AboutTab();
        _recipe     = new RecipeTab();
        _email      = new EmailTab();
        _traceroute = new TracerouteTab();
        _weather    = new WeatherTab();

        _tabs = new ITab[]
        {
            _dashboard, _servers, _colors,          // Demo1
            _about, _recipe, _email, _traceroute, _weather, // Demo2
        };
    }

    protected override void Update()
    {
        HandleInput();

        float dt = Time.deltaTime;
        foreach (var tab in _tabs)
            tab.Update(dt);

        base.Update();
    }

    private void HandleInput()
    {
        // Tab switching
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            _activeTab = (_activeTab + 1) % _tabs.Length;
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            _activeTab = (_activeTab + _tabs.Length - 1) % _tabs.Length;

        // Forward navigation keys to active tab
        foreach (KeyCode key in new[] {
            KeyCode.UpArrow, KeyCode.DownArrow,
            KeyCode.W, KeyCode.S,
            KeyCode.Return, KeyCode.Space })
        {
            if (Input.GetKeyDown(key))
                _tabs[_activeTab].OnInput(key);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    protected override void BuildFrame(RatatuiTerminal term)
    {
        uint root = term.RootArea;

        // Main layout: tab bar at top, content below
        var main = term.Split(root, Direction.Vertical,
            Constraint.Length(3),
            Constraint.Min(0));

        if (main.Length < 2) return;

        RenderTabBar(term, main[0]);
        _tabs[_activeTab].Render(term, main[1]);
    }

    private void RenderTabBar(RatatuiTerminal term, uint area)
    {
        // Build newline-separated tab title string
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < _tabs.Length; i++)
        {
            sb.Append(_tabs[i].Title);
            if (i < _tabs.Length - 1) sb.Append('\n');
        }

        term.SetStyle(new Color(0.2f, 0.8f, 1f), Color.clear, Modifier.Bold);
        term.Tabs(area, sb.ToString(), (uint)_activeTab);
    }
}
