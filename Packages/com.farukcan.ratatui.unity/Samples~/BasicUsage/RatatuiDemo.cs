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
            _dashboard, _servers, _colors,
            _about, _recipe, _email, _traceroute, _weather,
        };
    }

    protected override void Update()
    {
        float dt = Time.deltaTime;
        foreach (var tab in _tabs)
            tab.Update(dt);

        // base.Update() calls ProcessInput() then BuildFrame()
        base.Update();
    }

    // ── Keyboard Handler ──────────────────────────────────────────────────────

    protected override void OnTerminalKeyDown(TerminalKeyEvent e)
    {
        // Arrow keys come via GetKeyDown (Key set, Character='\0')
        // Letter keys come via inputString (Key=KeyCode.None, Character set)
        bool nextTab = e.Key == KeyCode.RightArrow
                    || e.Character == 'd' || e.Character == 'D';
        bool prevTab = e.Key == KeyCode.LeftArrow
                    || e.Character == 'a' || e.Character == 'A';

        if (nextTab) { _activeTab = (_activeTab + 1) % _tabs.Length; return; }
        if (prevTab) { _activeTab = (_activeTab + _tabs.Length - 1) % _tabs.Length; return; }

        _tabs[_activeTab].OnKeyEvent(e);
    }

    // ── Mouse Handler ─────────────────────────────────────────────────────────

    protected override void OnTerminalMouseEvent(TerminalMouseEvent e)
    {
        _tabs[_activeTab].OnMouseEvent(e);
    }

    // ── Hover Handler (optional) ──────────────────────────────────────────────

    protected override void OnTerminalHoverChanged(
        TerminalHoverState oldState, TerminalHoverState newState)
    {
        // Uncomment for debug:
        // if (newState.IsInside)
        //     Debug.Log($"Hover: cell=({newState.Col},{newState.Row}) area={newState.AreaId}");
    }

    // ── Render ────────────────────────────────────────────────────────────────

    protected override void BuildFrame(RatatuiTerminal term)
    {
        uint root = term.RootArea;

        var main = term.Split(root, Direction.Vertical,
            Constraint.Length(3),
            Constraint.Min(0));

        if (main.Length < 2) return;

        RenderTabBar(term, main[0]);
        _tabs[_activeTab].Render(term, main[1]);
    }

    private void RenderTabBar(RatatuiTerminal term, uint area)
    {
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
