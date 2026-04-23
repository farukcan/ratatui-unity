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
    private int _activeTab;

    // ── Tab bar hit-testing ─────────────────────────────────────────────────
    private uint _tabBarArea;

    // ── ITab instances ────────────────────────────────────────────────────────

    private DashboardTab _dashboard;
    private ServersTab _servers;
    private ColorsTab _colors;
    private AboutTab _about;
    private RecipeTab _recipe;
    private EmailTab _email;
    private TracerouteTab _traceroute;
    private WeatherTab _weather;
    private InputTab _inputTab;

    // ─────────────────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();

        _dashboard = new DashboardTab();
        _servers = new ServersTab();
        _colors = new ColorsTab();
        _about = new AboutTab();
        _recipe = new RecipeTab();
        _email = new EmailTab();
        _traceroute = new TracerouteTab();
        _weather = new WeatherTab();
        _inputTab = new InputTab();

        _tabs = new ITab[]
        {
            _dashboard, _servers, _colors,
            _about, _recipe, _email, _traceroute, _weather, _inputTab,
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
        // A/D for tab switching (forwarded to active tab when InputTab is active)
        bool nextTab = e.Key == KeyCode.RightArrow
                    || e.Character == 'd' || e.Character == 'D';
        bool prevTab = e.Key == KeyCode.LeftArrow
                    || e.Character == 'a' || e.Character == 'A';
        bool inputTabActive = _tabs[_activeTab] == _inputTab;
        if (nextTab && !inputTabActive) { _activeTab = (_activeTab + 1) % _tabs.Length; return; }
        if (prevTab && !inputTabActive) { _activeTab = (_activeTab + _tabs.Length - 1) % _tabs.Length; return; }

        _tabs[_activeTab].OnKeyEvent(e);
    }

    // ── Mouse Handler ─────────────────────────────────────────────────────────

    protected override void OnTerminalMouseEvent(TerminalMouseEvent e)
    {
        // Tab bar: click to switch, scroll to cycle
        if (e.AreaId == _tabBarArea)
        {
            if (e.Type == MouseEventType.Click && e.Button == MouseButton.Left)
            {
                int tabIndex = GetTabIndexAtColumn(e.Col);
                if (tabIndex >= 0) _activeTab = tabIndex;
                return;
            }
            if (e.Type == MouseEventType.Scroll)
            {
                if (e.ScrollDelta > 0)
                    _activeTab = (_activeTab + _tabs.Length - 1) % _tabs.Length;
                else
                    _activeTab = (_activeTab + 1) % _tabs.Length;
                return;
            }
        }

        _tabs[_activeTab].OnMouseEvent(e);
    }

    /// <summary>
    /// Maps a terminal column to a tab index based on tab title widths.
    /// Ratatui Tabs renders: " Title │ Title2 │ ... " with 1-char padding + 3-char dividers.
    /// </summary>
    private int GetTabIndexAtColumn(int col)
    {
        if (!Terminal.TryGetAreaRect(_tabBarArea, out int ax, out int ay, out int aw, out int ah))
            return -1;

        int localCol = col - ax;
        int pos = 0;
        for (int i = 0; i < _tabs.Length; i++)
        {
            int tabWidth = _tabs[i].Title.Length + 2; // 1 space padding each side
            if (localCol >= pos && localCol < pos + tabWidth)
                return i;
            pos += tabWidth + 1; // +1 for divider character
        }
        return -1;
    }

    // ── Hover Handler ─────────────────────────────────────────────────────────

    protected override void OnTerminalHoverChanged(
        TerminalHoverState oldState, TerminalHoverState newState)
    {
        _tabs[_activeTab].OnHoverChanged(oldState, newState);
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
        _tabBarArea = area;
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
