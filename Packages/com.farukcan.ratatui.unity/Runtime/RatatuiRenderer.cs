using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace RatatuiUnity
{
    /// <summary>
    /// MonoBehaviour that renders a Ratatui terminal to a <see cref="Texture2D"/>
    /// and optionally assigns it to a UI <see cref="RawImage"/> or a
    /// <see cref="MeshRenderer"/> material each frame.
    /// When no target is assigned, falls back to OnGUI rendering.
    /// Use <see cref="OnGuiMode.Full"/> to stretch the terminal to the entire screen,
    /// <see cref="OnGuiMode.Partial"/> to draw at native pixel size with configurable alignment,
    /// or <see cref="OnGuiMode.Window"/> for a draggable macOS-style window whose title bar
    /// shows <c>gameObject.name</c>.
    ///
    /// Override <see cref="BuildFrame"/> to define widget layout.
    /// Override <see cref="OnTerminalKeyDown"/>, <see cref="OnTerminalMouseEvent"/>,
    /// and <see cref="OnTerminalHoverChanged"/> to handle input events.
    /// </summary>
    public class RatatuiRenderer : MonoBehaviour
    {
        [Header("Terminal Settings")]
        [Tooltip("Width of the terminal in character columns. " +
                 "Overridden by FitColsAndRows when that toggle is on.")]
        [SerializeField] private int _cols = 80;

        [Tooltip("Height of the terminal in character rows. " +
                 "Overridden by FitColsAndRows when that toggle is on.")]
        [SerializeField] private int _rows = 24;

        [Tooltip("Derive cols × rows from the available pixel area so the terminal matches " +
                 "the target's aspect ratio. " +
                 "Target = RawImage RectTransform when assigned; otherwise the screen " +
                 "(OnGUI Full, OnGUI Window in fullscreen / WebGL fullscreen) or the window " +
                 "content area in normal Window mode. " +
                 "Recomputed on every refit when SizingMode is not Pixel.")]
        [FormerlySerializedAs("_fitIntoRectTransform")]
        [SerializeField] private bool _fitColsAndRows;

        [Tooltip("Font size — interpretation depends on SizingMode. " +
                 "Pixel: absolute pixels. " +
                 "Vh / Vw / Vmin / Vmax: percent of viewport height / width / min / max, " +
                 "e.g. fontSize=3 with Vh on a 1080px-tall viewport → 32.4px.")]
        [SerializeField] private float _fontSize = 14f;

        [Tooltip("How fontSize is interpreted. " +
                 "Pixel: absolute, terminal created once. " +
                 "Vh / Vw / Vmin / Vmax: CSS-style viewport-relative units; terminal is recreated " +
                 "whenever the target area or Screen.dpi changes so fontSize tracks the viewport.")]
        [SerializeField] private SizingMode _sizingMode = SizingMode.Pixel;

        [Tooltip("The background color of the terminal (alpha is ignored — texture is always opaque).")]
        [SerializeField] private Color _backgroundColor = new Color(0.102f, 0.102f, 0.18f); // dark navy

        [Header("Resolution / Readability")]
        [Tooltip("How often (in seconds) the renderer polls Screen.width/height/dpi for changes. " +
                 "RectTransform changes are detected immediately via OnRectTransformDimensionsChange — " +
                 "this poll covers OnGUI paths and DPI metadata refreshes (e.g. mobile rotation). " +
                 "Ignored in Pixel mode.")]
        [SerializeField] private float _resizePollSeconds = 0.25f;

        [Header("Target (optional)")]
        [Tooltip("Assign to a UI RawImage to display the terminal texture.")]
        [SerializeField] private RawImage _rawImage;

        [Tooltip("Assign to render the terminal texture onto a 3D mesh.")]
        [SerializeField] private Renderer _meshRenderer;

        [Header("OnGUI")]
        [Tooltip("Full: stretch to entire screen. Partial: native texture size with alignment. " +
                 "Window: draggable macOS-style window with title bar.")]
        [SerializeField] private OnGuiMode _onGuiMode = OnGuiMode.Full;

        [Tooltip("Horizontal placement when OnGUI mode is Partial.")]
        [SerializeField] private OnGuiHorizontalAlign _onGuiHorizontalAlign = OnGuiHorizontalAlign.Center;

        [Tooltip("Vertical placement when OnGUI mode is Partial.")]
        [SerializeField] private OnGuiVerticalAlign _onGuiVerticalAlign = OnGuiVerticalAlign.Center;

        [Tooltip("Title bar background color when this window is NOT focused (Window mode only).")]
        [SerializeField] private Color _windowTitleBarColor = new Color(0.09f, 0.09f, 0.09f);

        [Tooltip("Title bar background color when this window IS focused (Window mode only).")]
        [SerializeField] private Color _windowTitleBarColorFocused = new Color(0.18f, 0.18f, 0.20f);

        [Tooltip("Initial window X position in screen GUI space. -1 = center on screen.")]
        [SerializeField] private float _windowInitialX = -1f;

        [Tooltip("Initial window Y position in screen GUI space. -1 = center on screen.")]
        [SerializeField] private float _windowInitialY = -1f;

        [Tooltip("Initial window width in pixels (Window mode). " +
                 "-1 = derive from terminal texture (Pixel mode) or 70% screen (Fit mode).")]
        [SerializeField] private float _windowInitialWidth = -1f;

        [Tooltip("Initial window height in pixels, including title bar (Window mode). " +
                 "-1 = derive from terminal texture (Pixel mode) or 70% screen (Fit mode).")]
        [SerializeField] private float _windowInitialHeight = -1f;

        [Tooltip("Start maximized (fills screen) on first open in Window mode.")]
        [SerializeField] private bool _windowStartMaximized;

        [Tooltip("Font used for OnGUI window chrome (title bar, zoom/resize glyphs). " +
                 "Defaults to the bundled JetBrains Mono so non-ASCII glyphs like ↗ render " +
                 "on platforms without OS font fallback (e.g. WebGL). " +
                 "Falls back to Unity's default GUI font when null.")]
        [SerializeField] private Font _windowChromeFont;

        [Header("Input Settings")]
        [Tooltip("Enable input processing (keyboard + mouse).")]
        [SerializeField] private bool _enableInput = true;

        [Tooltip("Enable mouse input (hover, click, scroll).")]
        [SerializeField] private bool _enableMouseInput = true;

        [Tooltip("Enable keyboard input.")]
        [SerializeField] private bool _enableKeyboardInput = true;

        [Tooltip("Scroll wheel sensitivity multiplier. Increase for faster scrolling.")]
        [SerializeField] private float _scrollSensitivity = 1f;

        [Header("Performance")]
        [Tooltip("Maximum terminal render FPS. 0 = unlimited (renders every Unity frame). " +
                 "Lower values reduce CPU/GPU cost. Hash-based dirty check still applies.")]
        [SerializeField] private int _maxRenderFps = 60;

        // ── Public Properties ─────────────────────────────────────────────────

        /// <summary>The rendered texture. Assign to any Unity material or UI image.</summary>
        public Texture2D Texture { get; private set; }

        /// <summary>The underlying terminal instance.</summary>
        public RatatuiTerminal Terminal { get; private set; }

        /// <summary>Current mouse hover state in terminal coordinates.</summary>
        public TerminalHoverState HoverState { get; private set; }

        /// <summary>True if this renderer is the currently focused one. See <see cref="RatatuiFocusManager"/>.</summary>
        public bool IsFocused => RatatuiFocusManager.Focused == this;

        /// <summary>Make this renderer the focused one. Equivalent to <see cref="RatatuiFocusManager.SetFocus(RatatuiRenderer)"/>.</summary>
        public void RequestFocus() => RatatuiFocusManager.SetFocus(this);

        // ── Configuration Properties ──────────────────────────────────────────
        // Expose the serialized settings so subclasses / external code can
        // configure the renderer before Awake without reflection. Changing these
        // after the terminal has been created has no effect until the next
        // terminal recreation.

        /// <summary>Terminal width in character columns. Overridden by <see cref="FitColsAndRows"/>.</summary>
        public int Cols { get => _cols; set => _cols = value; }

        /// <summary>Terminal height in character rows. Overridden by <see cref="FitColsAndRows"/>.</summary>
        public int Rows { get => _rows; set => _rows = value; }

        /// <summary>Derive cols × rows from the available pixel area (see field tooltip).</summary>
        public bool FitColsAndRows { get => _fitColsAndRows; set => _fitColsAndRows = value; }

        /// <summary>Font size — interpretation depends on <see cref="SizingMode"/>.</summary>
        public float FontSize { get => _fontSize; set => _fontSize = value; }

        /// <summary>How <see cref="FontSize"/> is interpreted (absolute pixels or viewport-relative).</summary>
        public SizingMode SizingMode { get => _sizingMode; set => _sizingMode = value; }

        /// <summary>Terminal background color (alpha is ignored — texture is always opaque).</summary>
        public Color BackgroundColor { get => _backgroundColor; set => _backgroundColor = value; }

        /// <summary>OnGUI display mode: Full, Partial, or Window.</summary>
        public OnGuiMode OnGuiDisplayMode { get => _onGuiMode; set => _onGuiMode = value; }

        /// <summary>Horizontal placement when <see cref="OnGuiDisplayMode"/> is Partial.</summary>
        public OnGuiHorizontalAlign OnGuiHorizontalAlignment { get => _onGuiHorizontalAlign; set => _onGuiHorizontalAlign = value; }

        /// <summary>Vertical placement when <see cref="OnGuiDisplayMode"/> is Partial.</summary>
        public OnGuiVerticalAlign OnGuiVerticalAlignment { get => _onGuiVerticalAlign; set => _onGuiVerticalAlign = value; }

        /// <summary>Start maximized (fills screen) on first open in Window mode.</summary>
        public bool WindowStartMaximized { get => _windowStartMaximized; set => _windowStartMaximized = value; }

        /// <summary>
        /// Initial window width in pixels (Window mode). -1 = derive from terminal texture
        /// (Pixel sizing) or 70% of screen (Fit / viewport sizing). Setting this also drives
        /// the FitColsAndRows target area on first open so the terminal grid matches.
        /// </summary>
        public float WindowInitialWidth { get => _windowInitialWidth; set => _windowInitialWidth = value; }

        /// <summary>
        /// Initial window height in pixels including the title bar (Window mode). -1 = derive
        /// from terminal texture (Pixel sizing) or 70% of screen (Fit / viewport sizing).
        /// </summary>
        public float WindowInitialHeight { get => _windowInitialHeight; set => _windowInitialHeight = value; }

        /// <summary>
        /// Font used for OnGUI window chrome (title bar + zoom / resize glyphs). Setter
        /// exists so renderers created via <c>AddComponent</c> at runtime — which skip
        /// <c>Reset</c> / <c>OnValidate</c> and therefore start with a null field — can
        /// have the bundled JetBrains Mono pushed in before <see cref="Awake"/> finishes.
        /// </summary>
        public Font WindowChromeFont { get => _windowChromeFont; set => _windowChromeFont = value; }

        /// <summary>Enable input processing (keyboard + mouse).</summary>
        public bool EnableInput { get => _enableInput; set => _enableInput = value; }

        /// <summary>Enable mouse input (hover, click, scroll).</summary>
        public bool EnableMouseInput { get => _enableMouseInput; set => _enableMouseInput = value; }

        /// <summary>Enable keyboard input.</summary>
        public bool EnableKeyboardInput { get => _enableKeyboardInput; set => _enableKeyboardInput = value; }

        /// <summary>
        /// Fires after the terminal has been recreated due to a resize / DPI change.
        /// Args: new cols, new rows, new fontSize (px). Not fired for the initial Awake construction.
        /// Subscribers that cache <see cref="Terminal"/>, <see cref="Texture"/>, PixelWidth/PixelHeight,
        /// or cell metrics must refresh from the current values.
        /// </summary>
        public event Action<int, int, float> OnTerminalResized;

        /// <summary>
        /// Fires when the user clicks the red close button in Window mode.
        /// While no subscribers are attached the button is rendered dim and ignores clicks
        /// (the default — closing an embedded window has no built-in meaning).
        /// Subscribe to enable the button: e.g. <c>OnCloseClicked += () =&gt; SetOpen(false)</c>
        /// on a <see cref="RatatuiTerminalApp"/> for toggle-style apps,
        /// or destroy / disable the GameObject for a hard close.
        /// </summary>
        public event Action OnCloseClicked;

        // ── Internal State ────────────────────────────────────────────────────

        // OnGUI fallback rect (GUI coordinates: y=0 at top)
        private Rect _onGuiRect;

        // Frame on which _onGuiRect was last refreshed (set in UpdateOnGuiRect).
        // Used by OccludesScreenPoint to ignore OnGUI renderers that have paused
        // drawing (e.g. a console closed via SetOpen) and would otherwise keep
        // occluding RawImage / Mesh renderers with a stale rect.
        private int _lastOnGuiUpdateFrame = -1;

        // Track where mouse-down happened for click detection
        private int _mouseDownCol = -1;
        private int _mouseDownRow = -1;
        private MouseButton _mouseDownButton;

        // Accumulates raw scroll delta; fires a discrete event per threshold crossed.
        // This normalizes continuous (trackpad) and discrete (mouse wheel) input.
        private float _scrollAccumulator;

        // Cached Camera.main to avoid per-frame FindGameObjectWithTag overhead
        private Camera _cachedMainCamera;

        // Render throttle: time since last actual render
        private float _renderTimer;

        // Raycast cache: skip Physics.Raycast when mouse hasn't moved (3D mesh path)
        private Vector2 _lastRayScreenPos = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        private int _lastRayCol;
        private int _lastRayRow;
        private bool _lastRayValid;

        // Key repeat state for held-down special keys
        private KeyCode _heldKey = KeyCode.None;
        private float _heldKeyTime;
        private const float KeyRepeatDelay = 0.4f;  // seconds before repeat starts
        private const float KeyRepeatRate = 0.035f; // seconds between repeats

        // Window mode state
        private Rect _windowRect;
        private Rect _windowRestoreRect;
        private bool _windowInitialized;
        private bool _isDragging;
        private Vector2 _dragStartMouse;
        private Vector2 _dragStartWindowPos;
        private bool _isMinimized;
        private bool _isMaximized;

        // Drag-resize from the titlebar's top-right square handle.
        // Pivot is the window's bottom-left corner: left edge and bottom edge stay
        // fixed; the handle pulls the right and top edges independently.
        private bool _isResizing;
        private Vector2 _resizeStartMouse;
        private Rect _resizeStartWindowRect;

        // Resize / readability state
        // Cached custom font bytes so resize-driven Terminal recreation can re-apply
        // the user's font instead of falling back to the embedded JetBrains Mono.
        private byte[] _customFontBytes;

        // Reactive resize: set by OnRectTransformDimensionsChange, consumed in Update.
        // Unity forbids destroying objects inside that callback, so we defer.
        private bool _resizeDirty;

        // Polling snapshot for OnGUI paths (no RectTransform) and DPI changes.
        private float _resizePollTimer;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private float _lastScreenDpi;

        // True once Awake has completed Terminal construction. Used to suppress
        // OnRectTransformDimensionsChange callbacks that fire before Awake.
        private bool _terminalReady;

        // Cached textures for tinted GUI fills (lazy init)
        private static Texture2D _windowFillTexture;
        private static Texture2D _windowCircleTexture;
        private static Texture2D _windowRoundedRectTexture;
        private GUIStyle _windowTitleStyle;
        private GUIStyle _windowZoomGlyphStyle;

        // macOS traffic-light colors. Close switches between the dim disabled variant
        // (no OnCloseClicked subscribers) and the full red enabled variant.
        private static readonly Color WindowMinimizeColor = new Color(0.996f, 0.737f, 0.180f);
        private static readonly Color WindowFullscreenColor = new Color(0.157f, 0.784f, 0.251f);
        private static readonly Color WindowTitleTextColor = new Color(0.85f, 0.85f, 0.87f);
        private static readonly Color WindowCloseEnabledColor = new Color(1.000f, 0.373f, 0.341f);
        private static readonly Color WindowCloseDisabledColor = new Color(0.5f, 0.186f, 0.170f);

        // Right-side zoom + resize controls (uniform blue; resize dimmed while maximized)
        private static readonly Color WindowControlBlueColor = new Color(0.10f, 0.36f, 0.68f);

        private static readonly Color WindowZoomDisabledTint = new Color(1f, 1f, 1f, 0.35f);

        // Window chrome layout constants
        // Title bar height, title font size and traffic-light button size are all
        // derived from max(Screen.width, Screen.height) * WindowVMaxPercent so the
        // chrome scales with the viewport (vmax units, CSS-style).
        private const float WindowVMaxPercent = 0.0175f;
        private const float WindowTitleBarFactor = 1.6f;   // titlebar = vmax * factor → padding around buttons
        private const float WindowButtonPadding = 8f;
        private const float WindowButtonSpacing = 8f;
        private const float WindowMinVisible = 80f;
        private const float WindowZoomStep = 1.10f;
        private const float WindowFontSizeMin = 1f;
        private const float WindowFontSizeMax = 200f;
        private const float WindowResizeMargin = 3f;

        private static float WindowVMax => Mathf.Max(Screen.width, Screen.height) * WindowVMaxPercent;
        // Snap to whole pixels so the titlebar's bottom edge and the content
        // rect's top edge land on the same raster line — otherwise a fractional
        // height (e.g. 25.7) leaves a 1-pixel gap that shows the desktop behind.
        private static float WindowTitleBarHeight => Mathf.Round(WindowVMax * WindowTitleBarFactor);
        private static float WindowButtonSize => WindowVMax;
        private static int WindowTitleFontSize => Mathf.Max(1, Mathf.RoundToInt(WindowVMax));

        // Non-character keys polled with GetKeyDown each frame
        private static readonly KeyCode[] TrackedKeys =
        {
            KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
            KeyCode.Return, KeyCode.KeypadEnter,
            KeyCode.Escape,
            KeyCode.Tab,
            KeyCode.Backspace, KeyCode.Delete,
            KeyCode.Space,
            KeyCode.Home, KeyCode.End,
            KeyCode.PageUp, KeyCode.PageDown,
            KeyCode.F1,  KeyCode.F2,  KeyCode.F3,  KeyCode.F4,
            KeyCode.F5,  KeyCode.F6,  KeyCode.F7,  KeyCode.F8,
            KeyCode.F9,  KeyCode.F10, KeyCode.F11, KeyCode.F12,
            // Shortcut letters — needed because Input.inputString does NOT emit a
            // printable char when Cmd (macOS) or Ctrl is held, so widgets relying
            // on the key event for Copy/Cut/Paste/SelectAll/Undo/Redo would never
            // see the keystroke without these tracked KeyCodes.
            KeyCode.A, KeyCode.C, KeyCode.V, KeyCode.X, KeyCode.Y, KeyCode.Z,
        };

        // ── Unity Lifecycle ───────────────────────────────────────────────────

#if UNITY_EDITOR
        // Auto-populate _windowChromeFont from the package's bundled JetBrains Mono
        // so the inspector is never empty. WebGL (and any platform without OS font
        // fallback) needs an explicit font for non-ASCII chrome glyphs like ↗ and −.
        private const string BundledChromeFontPath =
            "Packages/com.farukcan.ratatui.unity/Runtime/Fonts/JetBrainsMono-Regular.ttf";

        protected virtual void Reset()
        {
            if (_windowChromeFont == null)
                _windowChromeFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(BundledChromeFontPath);
        }

        protected virtual void OnValidate()
        {
            if (_windowChromeFont == null)
                _windowChromeFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(BundledChromeFontPath);
        }
#endif

        protected virtual void Awake()
        {
            ReinitializeTerminal(firstTime: true);
            _cachedMainCamera = Camera.main;
            ValidateInputRequirements();
            SnapshotScreenMetrics();
            WarnIfChromeFontMissing();
        }

        // Diagnostic for the WebGL chrome-glyph regression: Unity's default GUI font
        // (Arial) lacks ↗ and −, so when _windowChromeFont is null on a platform without
        // OS font fallback the buttons render blank. Surface that explicitly instead of
        // failing silently.
        private void WarnIfChromeFontMissing()
        {
            if (_onGuiMode != OnGuiMode.Window) return;
            if (_windowChromeFont != null) return;
            Debug.LogWarning(
                $"[RatatuiRenderer] '{name}': _windowChromeFont is not assigned. " +
                "OnGUI Window chrome glyphs (↗, −) will not render on platforms without " +
                "OS font fallback (e.g. WebGL). Assign the bundled JetBrainsMono-Regular " +
                "font in the inspector.", this);
        }

        /// <summary>
        /// Called by Unity whenever this GameObject's RectTransform (or a parent's)
        /// changes size. Used to mark the terminal for refit when sizingMode is
        /// a viewport-relative mode (Vh / Vw / Vmin / Vmax). Recreate is deferred to
        /// <see cref="Update"/> because Unity disallows Destroy() from this callback.
        /// </summary>
        protected virtual void OnRectTransformDimensionsChange()
        {
            if (!_terminalReady) return;
            if (_sizingMode == SizingMode.Pixel) return;
            _resizeDirty = true;
        }

        protected virtual void OnEnable()
        {
            RatatuiFocusManager.Register(this);
        }

        protected virtual void OnDisable()
        {
            RatatuiFocusManager.Unregister(this);
        }

        protected virtual void Update()
        {
            // Reactive refit: poll Screen metrics + drain RectTransform dirty flag.
            // Both feed the same ReinitializeTerminal() path. Pixel mode skips
            // polling (absolute fontSize, no viewport tracking) but still drains
            // an explicitly-set dirty flag so font-zoom clicks refit the terminal.
            if (_sizingMode != SizingMode.Pixel)
                CheckForResize();
            else if (_resizeDirty)
            {
                _resizeDirty = false;
                ReinitializeTerminal(firstTime: false);
            }

            // Update OnGUI rect before input so mouse coordinates are correct
            if (_rawImage == null && _meshRenderer == null)
                UpdateOnGuiRect();

            // Window mode: when minimized, skip terminal input and render pipeline.
            // The title bar (and its drag/button handling) remains active via OnGUI.
            bool windowMinimized = _onGuiMode == OnGuiMode.Window
                && _rawImage == null && _meshRenderer == null
                && _isMinimized;

            // Input runs before BuildFrame so state changes are reflected in the same frame
            if (_enableInput && !windowMinimized) ProcessInput();

            if (windowMinimized) return;

            // FPS throttle: skip entire render pipeline when interval hasn't elapsed
            if (_maxRenderFps > 0)
            {
                _renderTimer += Time.unscaledDeltaTime;
                float interval = 1f / _maxRenderFps;
                if (_renderTimer < interval)
                    return;
                _renderTimer = Mathf.Min(_renderTimer - interval, interval);
            }

            Terminal.BeginFrame();
            BuildFrame(Terminal);

            // Hash-based dirty check: EndFrameRawIfDirty returns null when
            // the cell buffer is unchanged, skipping pixel rasterization + GPU upload.
            IntPtr ptr = Terminal.EndFrameRawIfDirty();
            if (ptr != IntPtr.Zero)
            {
                int byteCount = Terminal.PixelWidth * Terminal.PixelHeight * RatatuiTerminal.BytesPerPixel;
                Texture.LoadRawTextureData(ptr, byteCount);
                Texture.Apply(updateMipmaps: false);
            }
        }

        protected virtual void OnGUI()
        {
            if (_rawImage != null || _meshRenderer != null) return;
            if (Texture == null) return;

            // Lower GUI.depth draws on top. Mode is the primary order (Window > Partial > Full),
            // focus is the tiebreaker within a mode. Matches the mouse-routing arbitration so
            // the visually-top window is also the one that receives input.
            int subLayer = GetOnGuiSubLayer();   // Window=2, Partial=1, Full=0
            GUI.depth = -subLayer * 10 + (IsFocused ? -1 : 0);

            if (_onGuiMode == OnGuiMode.Window)
            {
                EnsureWindowInitialized();
                DrawWindowChrome();
                if (!_isMinimized)
                {
                    float titleBarH = WindowTitleBarHeight;
                    Rect contentRect = new Rect(
                        _windowRect.x,
                        _windowRect.y + titleBarH,
                        _windowRect.width,
                        _windowRect.height - titleBarH);
                    GUI.DrawTextureWithTexCoords(
                        contentRect, Texture, new Rect(0f, 1f, 1f, -1f), false);
                }
                return;
            }

            GUI.DrawTextureWithTexCoords(
                _onGuiRect, Texture, new Rect(0f, 1f, 1f, -1f), false);
        }

        protected virtual void OnDestroy()
        {
            Terminal?.Dispose();
            if (Texture != null)
                Destroy(Texture);
        }

        // ── Override Points ───────────────────────────────────────────────────

        /// <summary>
        /// Called every frame between BeginFrame and EndFrame.
        /// Override to define the terminal layout and widgets.
        /// </summary>
        protected virtual void BuildFrame(RatatuiTerminal term) { }

        /// <summary>
        /// Called for each keyboard event. Override to handle terminal key input.
        /// </summary>
        protected virtual void OnTerminalKeyDown(TerminalKeyEvent e) { }

        /// <summary>
        /// Called for mouse events (move, down, up, click, scroll).
        /// Override to handle terminal mouse input.
        /// </summary>
        protected virtual void OnTerminalMouseEvent(TerminalMouseEvent e) { }

        /// <summary>
        /// Called when the hover state changes (mouse enters/exits areas or the terminal).
        /// </summary>
        protected virtual void OnTerminalHoverChanged(
            TerminalHoverState oldState, TerminalHoverState newState)
        { }

        /// <summary>
        /// Called when this renderer gains or loses scene focus.
        /// Override to react (e.g. update a visual). If overriding, call <c>base.OnFocusChanged(isFocused)</c>
        /// to keep the held-key reset that prevents stale key-repeat after focus loss.
        /// </summary>
        protected virtual void OnFocusChanged(bool isFocused)
        {
            if (!isFocused)
            {
                // Reset held input state so regained focus doesn't replay stale events.
                _heldKey = KeyCode.None;
                _heldKeyTime = 0f;
                _scrollAccumulator = 0f;
            }
        }

        // Invoked by RatatuiFocusManager. Not part of the public surface.
        internal void InvokeFocusChanged(bool isFocused) => OnFocusChanged(isFocused);

        // ── Input Processing ──────────────────────────────────────────────────

        private void ProcessInput()
        {
            var mods = GetCurrentModifiers();
            // Keyboard goes only to the focused renderer (multi-terminal arbitration).
            // Mouse runs everywhere; TryGetTerminalCell already filters by raycast/rect hit.
            if (_enableKeyboardInput && IsFocused) ProcessKeyboard(mods);
            if (_enableMouseInput) ProcessMouse(mods);
        }

        private KeyModifiers GetCurrentModifiers()
        {
            var mods = KeyModifiers.None;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                mods |= KeyModifiers.Shift;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                mods |= KeyModifiers.Ctrl;
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                mods |= KeyModifiers.Alt;
            // macOS Command key only. LeftApple/RightApple are the legacy KeyCodes
            // that alias LeftCommand/RightCommand — check both for compatibility.
            // The Windows Super key is deliberately NOT mapped to Cmd: Win+letter
            // combos are reserved by the OS and would conflict with Copy/Paste.
            if (Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand)
             || Input.GetKey(KeyCode.LeftApple)   || Input.GetKey(KeyCode.RightApple))
                mods |= KeyModifiers.Cmd;
            return mods;
        }

        private void ProcessKeyboard(KeyModifiers mods)
        {
            // Character input — skip control chars and macOS function key codes
            foreach (char c in Input.inputString)
            {
                if (char.IsControl(c)) continue;
                // macOS injects Private Use Area chars (U+E000–U+F8FF) for function/arrow keys
                if (c >= '\uE000' && c <= '\uF8FF') continue;
                OnTerminalKeyDown(new TerminalKeyEvent(KeyCode.None, c, mods));
            }

            // Special non-character keys — with key repeat for held keys
            float dt = Time.unscaledDeltaTime;

            // Check if held key was released
            if (_heldKey != KeyCode.None && !Input.GetKey(_heldKey))
                _heldKey = KeyCode.None;

            foreach (var key in TrackedKeys)
            {
                if (Input.GetKeyDown(key))
                {
                    OnTerminalKeyDown(new TerminalKeyEvent(key, '\0', mods));
                    _heldKey = key;
                    _heldKeyTime = 0f;
                }
                else if (key == _heldKey && Input.GetKey(key))
                {
                    _heldKeyTime += dt;
                    if (_heldKeyTime >= KeyRepeatDelay)
                    {
                        _heldKeyTime -= KeyRepeatRate;
                        OnTerminalKeyDown(new TerminalKeyEvent(key, '\0', mods));
                    }
                }
            }
        }

        private void ProcessMouse(KeyModifiers mods)
        {
            Vector2 screenPos = Input.mousePosition;

            if (!TryGetTerminalCell(screenPos, out int col, out int row))
            {
                // Mouse is outside the terminal (or occluded by a higher-Z renderer).
                if (HoverState.IsInside)
                {
                    var outside = TerminalHoverState.Outside;
                    OnTerminalHoverChanged(HoverState, outside);
                    HoverState = outside;
                }

                // If a button was pressed on this renderer and an overlay then covered us
                // (or the cursor left the surface entirely), synthesize an Up at the last
                // known down-cell so drag/release handlers can finalize cleanly.
                if (_mouseDownCol >= 0 && _mouseDownRow >= 0)
                {
                    OnTerminalMouseEvent(new TerminalMouseEvent(
                        MouseEventType.Up, _mouseDownCol, _mouseDownRow, 0,
                        _mouseDownButton, 0f, mods));
                    _mouseDownCol = -1;
                    _mouseDownRow = -1;
                }
                return;
            }

            // Hit-test uses the previous frame's area_map
            uint areaId = Terminal.HitTest(col, row);

            // Update hover state
            var currentHover = new TerminalHoverState(col, row, areaId, true);
            bool hoverChanged = currentHover.Col != HoverState.Col
                             || currentHover.Row != HoverState.Row
                             || currentHover.AreaId != HoverState.AreaId
                             || currentHover.IsInside != HoverState.IsInside;

            if (hoverChanged)
            {
                OnTerminalHoverChanged(HoverState, currentHover);

                if (currentHover.Col != HoverState.Col || currentHover.Row != HoverState.Row)
                {
                    OnTerminalMouseEvent(new TerminalMouseEvent(
                        MouseEventType.Move, col, row, areaId,
                        MouseButton.Left, 0f, mods));
                }

                HoverState = currentHover;
            }

            // Mouse button events (Left, Right, Middle)
            for (int btn = 0; btn < 3; btn++)
            {
                var mouseBtn = (MouseButton)btn;

                if (Input.GetMouseButtonDown(btn))
                {
                    // Click-to-focus: any mouse button down on this renderer's surface
                    // transfers scene focus before the Down event is delivered.
                    if (!IsFocused) RatatuiFocusManager.SetFocus(this);

                    _mouseDownCol = col;
                    _mouseDownRow = row;
                    _mouseDownButton = mouseBtn;

                    OnTerminalMouseEvent(new TerminalMouseEvent(
                        MouseEventType.Down, col, row, areaId,
                        mouseBtn, 0f, mods));
                }

                if (Input.GetMouseButtonUp(btn))
                {
                    OnTerminalMouseEvent(new TerminalMouseEvent(
                        MouseEventType.Up, col, row, areaId,
                        mouseBtn, 0f, mods));

                    // Click = Down and Up on the same cell
                    if (_mouseDownCol == col && _mouseDownRow == row
                        && _mouseDownButton == mouseBtn)
                    {
                        OnTerminalMouseEvent(new TerminalMouseEvent(
                            MouseEventType.Click, col, row, areaId,
                            mouseBtn, 0f, mods));
                    }

                    _mouseDownCol = -1;
                    _mouseDownRow = -1;
                }
            }

            // Scroll wheel — accumulate raw delta, fire one discrete event per threshold.
            // This converts smooth/continuous trackpad input into predictable discrete steps.
            float rawScroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(rawScroll) > 0.001f)
            {
                _scrollAccumulator += rawScroll;
                float threshold = 1f / Mathf.Max(0.01f, _scrollSensitivity);
                while (Mathf.Abs(_scrollAccumulator) >= threshold)
                {
                    float dir = Mathf.Sign(_scrollAccumulator);
                    _scrollAccumulator -= dir * threshold;
                    OnTerminalMouseEvent(new TerminalMouseEvent(
                        MouseEventType.Scroll, col, row, areaId,
                        MouseButton.Left, dir, mods));
                }
            }
        }

        // ── Coordinate Conversion ─────────────────────────────────────────────

        /// <summary>
        /// Converts a screen-space pixel position to terminal cell coordinates.
        /// Supports RawImage (UI), MeshRenderer (3D), and OnGUI fallback targets.
        /// </summary>
        protected bool TryGetTerminalCell(Vector2 screenPos, out int col, out int row)
        {
            col = row = 0;

            // Z-order arbitration across all RatatuiRenderers in the scene.
            // Priority: OnGUI > RawImage > MeshRenderer. Within the same layer,
            // OnGUI defers to the focused window; RawImage defers to higher Canvas sortingOrder.
            // Mesh path also busts its raycast cache so a moving overlay re-evaluates.
            if (IsScreenPointObscuredByOverlay(this, screenPos))
            {
                if (_meshRenderer != null)
                {
                    _lastRayScreenPos = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                    _lastRayValid = false;
                }
                return false;
            }

            if (_rawImage != null)
            {
                Camera cam = null;
                var canvas = _rawImage.canvas;
                if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    cam = canvas.worldCamera;

                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rawImage.rectTransform, screenPos, cam, out Vector2 local))
                    return false;

                Rect rect = _rawImage.rectTransform.rect;
                float normalizedX = (local.x - rect.x) / rect.width;
                float normalizedY = (local.y - rect.y) / rect.height;

                if (normalizedX < 0f || normalizedX > 1f ||
                    normalizedY < 0f || normalizedY > 1f)
                    return false;

                col = Mathf.Clamp((int)(normalizedX * Terminal.Cols), 0, Terminal.Cols - 1);
                // Y-flip: Unity UI y=0 at bottom, terminal y=0 at top
                row = Mathf.Clamp(
                    Terminal.Rows - 1 - (int)(normalizedY * Terminal.Rows),
                    0, Terminal.Rows - 1);
                return true;
            }

            if (_meshRenderer != null)
            {
                // Return cached result when mouse hasn't moved
                if (screenPos == _lastRayScreenPos)
                {
                    col = _lastRayCol;
                    row = _lastRayRow;
                    return _lastRayValid;
                }
                _lastRayScreenPos = screenPos;

                Camera cam = _cachedMainCamera;
                if (cam == null)
                {
                    _cachedMainCamera = Camera.main;
                    cam = _cachedMainCamera;
                }
                if (cam == null)
                {
                    _lastRayValid = false;
                    return false;
                }

                Ray ray = cam.ScreenPointToRay(screenPos);
                if (!Physics.Raycast(ray, out RaycastHit hit)
                    || hit.collider == null
                    || hit.collider.gameObject != _meshRenderer.gameObject)
                {
                    _lastRayValid = false;
                    return false;
                }

                col = Mathf.Clamp(
                    (int)(hit.textureCoord.x * Terminal.Cols), 0, Terminal.Cols - 1);
                // Y-flip: UV y=0 at bottom, terminal y=0 at top
                row = Mathf.Clamp(
                    Terminal.Rows - 1 - (int)(hit.textureCoord.y * Terminal.Rows),
                    0, Terminal.Rows - 1);

                _lastRayCol = col;
                _lastRayRow = row;
                _lastRayValid = true;
                return true;
            }

            // OnGUI fallback: convert Input.mousePosition (y=0 bottom) to GUI space (y=0 top)
            if (_onGuiRect.width > 0f)
            {
                float guiMouseY = Screen.height - screenPos.y;
                float normalizedX = (screenPos.x - _onGuiRect.x) / _onGuiRect.width;
                float normalizedY = (guiMouseY - _onGuiRect.y) / _onGuiRect.height;

                if (normalizedX < 0f || normalizedX > 1f ||
                    normalizedY < 0f || normalizedY > 1f)
                    return false;

                col = Mathf.Clamp((int)(normalizedX * Terminal.Cols), 0, Terminal.Cols - 1);
                // No Y-flip: both GUI and terminal have y=0 at top
                row = Mathf.Clamp((int)(normalizedY * Terminal.Rows), 0, Terminal.Rows - 1);
                return true;
            }

            return false;
        }

        // ── Overlay / Z-order Arbitration ─────────────────────────────────────

        // Render-target categories used to order input precedence across renderers.
        // Higher value = drawn on top = grabs input first.
        private const int InputLayerMesh = 0;
        private const int InputLayerRawImage = 1;
        private const int InputLayerOnGui = 2;

        private int GetInputLayer()
        {
            if (_rawImage != null) return InputLayerRawImage;
            if (_meshRenderer != null) return InputLayerMesh;
            return InputLayerOnGui;
        }

        // Sub-priority within the OnGUI input layer. Higher = drawn on top of the others.
        // Window is draggable foreground; Partial is a fixed foreground rect;
        // Full covers the screen and acts as a background canvas.
        private int GetOnGuiSubLayer()
        {
            switch (_onGuiMode)
            {
                case OnGuiMode.Window: return 2;
                case OnGuiMode.Partial: return 1;
                default: /* Full */     return 0;
            }
        }

        // True when the given screen point falls within this renderer's visible surface.
        // OnGUI rects are stored in GUI space (y=0 top); screenPos uses Input.mousePosition (y=0 bottom).
        internal bool OccludesScreenPoint(Vector2 screenPos)
        {
            if (_rawImage != null)
            {
                if (!_rawImage.isActiveAndEnabled) return false;
                // Respect Unity's click-through convention.
                if (!_rawImage.raycastTarget) return false;
                Camera cam = null;
                var canvas = _rawImage.canvas;
                if (canvas == null) return false;
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    cam = canvas.worldCamera;
                return RectTransformUtility.RectangleContainsScreenPoint(
                    _rawImage.rectTransform, screenPos, cam);
            }

            if (_meshRenderer != null)
            {
                // 3D meshes don't act as screen-space overlays; Physics.Raycast handles
                // mesh-vs-mesh depth, so report false here.
                return false;
            }

            // OnGUI fallback target.
            // A renderer that hasn't refreshed its OnGUI rect this frame (or last frame,
            // to stay robust against script execution order) is not currently presenting
            // — e.g. a console closed via SetOpen that skips base.Update / OnGUI. Such a
            // renderer must not occlude RawImage / Mesh terminals with its stale rect.
            if (Time.frameCount - _lastOnGuiUpdateFrame > 1)
                return false;

            float guiY = Screen.height - screenPos.y;
            var guiPoint = new Vector2(screenPos.x, guiY);

            if (_onGuiMode == OnGuiMode.Window)
            {
                // _windowRect is populated by EnsureWindowInitialized inside OnGUI;
                // before the first OnGUI tick it is (0,0,0,0).
                if (!_windowInitialized) return false;
                if (_isMinimized)
                {
                    var bar = new Rect(
                        _windowRect.x, _windowRect.y,
                        _windowRect.width, WindowTitleBarHeight);
                    return bar.Contains(guiPoint);
                }
                return _windowRect.Contains(guiPoint);
            }

            if (_onGuiRect.width > 0f)
                return _onGuiRect.Contains(guiPoint);

            return false;
        }

        // Returns true if another RatatuiRenderer's surface sits above this one at screenPos.
        // Cross-layer: OnGUI > RawImage > Mesh.
        // Same-layer tiebreakers: OnGUI focused wins; RawImage higher canvas sortingOrder wins.
        private static bool IsScreenPointObscuredByOverlay(RatatuiRenderer requester, Vector2 screenPos)
        {
            if (requester == null) return false;
            int myLayer = requester.GetInputLayer();
            var list = RatatuiFocusManager.Registered;
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                var other = list[i];
                if (other == null || other == requester) continue;
                if (!other.isActiveAndEnabled) continue;
                if (!other.OccludesScreenPoint(screenPos)) continue;

                int otherLayer = other.GetInputLayer();
                if (otherLayer > myLayer) return true;
                if (otherLayer < myLayer) continue;

                // Same-layer arbitration
                if (myLayer == InputLayerOnGui)
                {
                    // Within OnGUI: mode-based sub-priority first (Window > Partial > Full).
                    // A Full-mode terminal covers the entire screen but acts as background,
                    // so a Window or Partial above it must not be occluded by it.
                    int mySub = requester.GetOnGuiSubLayer();
                    int otherSub = other.GetOnGuiSubLayer();
                    if (otherSub > mySub) return true;
                    if (otherSub < mySub) continue;
                    // Equal sub-layer: focused wins.
                    if (other.IsFocused && !requester.IsFocused) return true;
                }
                else if (myLayer == InputLayerRawImage)
                {
                    int myOrder = requester._rawImage.canvas != null
                        ? requester._rawImage.canvas.sortingOrder : 0;
                    int otherOrder = other._rawImage.canvas != null
                        ? other._rawImage.canvas.sortingOrder : 0;
                    if (otherOrder > myOrder) return true;
                }
                // Mesh-vs-mesh depth is decided by Physics.Raycast.
            }
            return false;
        }

        // ── Validation ────────────────────────────────────────────────────────

        private void ValidateInputRequirements()
        {
            if (!_enableInput || !_enableMouseInput) return;
            if (_meshRenderer == null) return;

            var meshGo = _meshRenderer.gameObject;

            if (meshGo.GetComponent<Collider>() == null)
            {
                Debug.LogWarning(
                    $"[RatatuiRenderer] Mouse input is enabled but '{meshGo.name}' " +
                    "has no Collider. Add a MeshCollider (non-convex) for mouse hit-testing.",
                    this);
            }
            else
            {
                var mc = meshGo.GetComponent<MeshCollider>();
                if (mc != null && mc.convex)
                {
                    Debug.LogWarning(
                        $"[RatatuiRenderer] MeshCollider on '{meshGo.name}' is convex. " +
                        "UV-based hit coordinates (textureCoord) require a non-convex MeshCollider.",
                        this);
                }
            }

            if (_cachedMainCamera == null)
            {
                Debug.LogWarning(
                    "[RatatuiRenderer] Camera.main is null. Ensure a camera is tagged 'MainCamera' " +
                    "for MeshRenderer mouse input to work.",
                    this);
            }
        }

        // ── OnGUI Helpers ─────────────────────────────────────────────────

        private void UpdateOnGuiRect()
        {
            if (Texture == null) return;

            // Mark this renderer as actively presenting its OnGUI surface this frame.
            // OccludesScreenPoint uses this to skip renderers that have stopped drawing.
            _lastOnGuiUpdateFrame = Time.frameCount;

            if (_onGuiMode == OnGuiMode.Full)
            {
                _onGuiRect = new Rect(0f, 0f, Screen.width, Screen.height);
                return;
            }

            if (_onGuiMode == OnGuiMode.Window)
            {
                EnsureWindowInitialized();
                // Terminal hit-testing uses _onGuiRect, so expose the content area
                // (window rect minus title bar). Mouse over the title bar falls outside,
                // so terminal mouse events do not fire while interacting with chrome.
                float titleBarH = WindowTitleBarHeight;
                _onGuiRect = new Rect(
                    _windowRect.x,
                    _windowRect.y + titleBarH,
                    _windowRect.width,
                    _windowRect.height - titleBarH);
                return;
            }

            float w = Texture.width;
            float h = Texture.height;

            float x = _onGuiHorizontalAlign switch
            {
                OnGuiHorizontalAlign.Left => 0f,
                OnGuiHorizontalAlign.Right => Screen.width - w,
                _ => (Screen.width - w) * 0.5f,
            };

            float y = _onGuiVerticalAlign switch
            {
                OnGuiVerticalAlign.Top => 0f,
                OnGuiVerticalAlign.Bottom => Screen.height - h,
                _ => (Screen.height - h) * 0.5f,
            };

            _onGuiRect = new Rect(x, y, w, h);
        }

        // ── Window Mode Helpers ───────────────────────────────────────────────

        private void EnsureWindowInitialized()
        {
            if (_windowInitialized || Texture == null) return;

            float w = _windowInitialWidth > 0f ? _windowInitialWidth : Texture.width;
            float h = _windowInitialHeight > 0f ? _windowInitialHeight : Texture.height + WindowTitleBarHeight;
            float x = _windowInitialX < 0f ? (Screen.width - w) * 0.5f : _windowInitialX;
            float y = _windowInitialY < 0f ? (Screen.height - h) * 0.5f : _windowInitialY;
            _windowRect = new Rect(x, y, w, h);
            _windowInitialized = true;

            if (_windowStartMaximized)
                ToggleMaximized();
        }

        private static Texture2D GetFillTexture()
        {
            if (_windowFillTexture == null)
            {
                _windowFillTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _windowFillTexture.SetPixel(0, 0, Color.white);
                _windowFillTexture.Apply();
            }
            return _windowFillTexture;
        }

        private static void FillRect(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, GetFillTexture());
            GUI.color = prev;
        }

        private static Texture2D GetCircleTexture()
        {
            if (_windowCircleTexture != null) return _windowCircleTexture;

            const int size = 32;
            _windowCircleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                    _windowCircleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            _windowCircleTexture.Apply();
            return _windowCircleTexture;
        }

        private static void FillCircle(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, GetCircleTexture(), ScaleMode.StretchToFill, true);
            GUI.color = prev;
        }

        private static Texture2D GetRoundedRectTexture()
        {
            if (_windowRoundedRectTexture != null) return _windowRoundedRectTexture;

            // Square SDF for a rounded rect. Buttons that use this are square, so
            // StretchToFill keeps corner curvature uniform without 9-slicing.
            const int size = 64;
            const float radius = 14f;   // ~22% of size — subtle iOS-style rounding
            _windowRoundedRectTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance from pixel to the inner rect (shrunk by `radius`).
                    // Inside the inner rect → dist=0 → fully opaque.
                    // Beyond it → dist measures how far into the corner zone we are.
                    float dx = Mathf.Max(radius - x, x - (size - 1 - radius), 0f);
                    float dy = Mathf.Max(radius - y, y - (size - 1 - radius), 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                    _windowRoundedRectTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            _windowRoundedRectTexture.Apply();
            return _windowRoundedRectTexture;
        }

        private static void FillRoundedRect(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, GetRoundedRectTexture(), ScaleMode.StretchToFill, true);
            GUI.color = prev;
        }

        private GUIStyle GetWindowTitleStyle()
        {
            if (_windowTitleStyle == null)
            {
                _windowTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip,
                    wordWrap = false,
                };
                _windowTitleStyle.normal.textColor = WindowTitleTextColor;
            }
            // Refresh on every call so screen resizes update the title size
            // and font swaps at runtime take effect.
            _windowTitleStyle.font = _windowChromeFont;
            _windowTitleStyle.fontSize = WindowTitleFontSize;
            return _windowTitleStyle;
        }

        private void DrawWindowTitle(Rect titleBarRect)
        {
            GUI.Label(titleBarRect, gameObject.name, GetWindowTitleStyle());
        }

        private void DrawWindowChrome()
        {
            // Title bar background
            float titleBarH = WindowTitleBarHeight;
            Rect titleBarRect = new Rect(
                _windowRect.x, _windowRect.y,
                _windowRect.width, titleBarH);
            FillRect(titleBarRect, IsFocused ? _windowTitleBarColorFocused : _windowTitleBarColor);
            DrawWindowTitle(titleBarRect);

            // Uniform button size for both left traffic-lights and right square controls,
            // vertically centered in the (taller) title bar.
            float btnSize = WindowButtonSize;
            float btnY = _windowRect.y + (titleBarH - btnSize) * 0.5f;
            float btnX = _windowRect.x + WindowButtonPadding;

            Rect closeRect = new Rect(btnX, btnY, btnSize, btnSize);
            Rect minimizeRect = new Rect(btnX + (btnSize + WindowButtonSpacing),
                                           btnY, btnSize, btnSize);
            Rect fullscreenRect = new Rect(btnX + (btnSize + WindowButtonSpacing) * 2f,
                                           btnY, btnSize, btnSize);

            Rect closeHit = closeRect;
            Rect minimizeHit = minimizeRect;
            Rect fullscreenHit = fullscreenRect;

            // Close: active iff OnCloseClicked has subscribers. Otherwise dim & inert.
            bool closeEnabled = OnCloseClicked != null;
            FillCircle(closeRect, closeEnabled ? WindowCloseEnabledColor : WindowCloseDisabledColor);

            // Right-side controls (all squares, same size as the traffic-lights).
            // Laid out from the far-right corner inward:
            //   [resize handle] [−] [+]
            float resizeX = _windowRect.x + _windowRect.width - WindowResizeMargin - btnSize;
            float zoomMinusX = resizeX - WindowButtonSpacing - btnSize;
            float zoomPlusX = zoomMinusX - WindowButtonSpacing - btnSize;

            Rect resizeHandleRect = new Rect(resizeX, btnY, btnSize, btnSize);
            Rect zoomMinusRect = new Rect(zoomMinusX, btnY, btnSize, btnSize);
            Rect zoomPlusRect = new Rect(zoomPlusX, btnY, btnSize, btnSize);

            HandleWindowResize(resizeHandleRect);
            HandleWindowDrag(titleBarRect, closeHit, minimizeHit, fullscreenHit,
                             zoomPlusRect, zoomMinusRect, resizeHandleRect);

            // Close click → fire callback when enabled. Drag handler already early-returns
            // on closeHit so we never compete with window drag.
            if (closeEnabled
                && Event.current.type == EventType.MouseDown
                && Event.current.button == 0
                && closeHit.Contains(Event.current.mousePosition))
            {
                OnCloseClicked.Invoke();
                Event.current.Use();
            }

            // Minimize: yellow toggle
            FillCircle(minimizeRect, WindowMinimizeColor);
            if (Event.current.type == EventType.MouseDown
                && Event.current.button == 0
                && minimizeHit.Contains(Event.current.mousePosition))
            {
                _isMinimized = !_isMinimized;
                Event.current.Use();
            }

            // Fullscreen: green toggle
            FillCircle(fullscreenRect, WindowFullscreenColor);
            if (Event.current.type == EventType.MouseDown
                && Event.current.button == 0
                && fullscreenHit.Contains(Event.current.mousePosition))
            {
                ToggleMaximized();
                Event.current.Use();
            }

            // Zoom: blue rounded squares (sized like the resize handle). Active in all states (incl. maximized).
            FillRoundedRect(zoomPlusRect, WindowControlBlueColor);
            FillRoundedRect(zoomMinusRect, WindowControlBlueColor);
            DrawZoomGlyph(zoomPlusRect, "+");
            DrawZoomGlyph(zoomMinusRect, "−");

            if (Event.current.type == EventType.MouseDown
                && Event.current.button == 0)
            {
                if (zoomPlusRect.Contains(Event.current.mousePosition))
                {
                    ApplyFontZoom(WindowZoomStep);
                    Event.current.Use();
                }
                else if (zoomMinusRect.Contains(Event.current.mousePosition))
                {
                    ApplyFontZoom(1f / WindowZoomStep);
                    Event.current.Use();
                }
            }

            // Resize handle: blue rounded square with ↗ glyph, far-right. Dimmed while maximized.
            // ↗ (U+2197) chosen over ◥ (U+25E5) because JetBrains Mono Regular contains the
            // arrow but not the upper-right triangle. The triangle would tofu on WebGL where
            // OS font fallback is unavailable.
            Color handleColor = _isMaximized
                ? WindowControlBlueColor * WindowZoomDisabledTint
                : WindowControlBlueColor;
            FillRoundedRect(resizeHandleRect, handleColor);
            DrawZoomGlyph(resizeHandleRect, "↗");
        }

        private void ToggleMaximized()
        {
            if (_isMaximized)
            {
                _windowRect = _windowRestoreRect;
                _isMaximized = false;
            }
            else
            {
                _windowRestoreRect = _windowRect;
                _windowRect = new Rect(0f, 0f, Screen.width, Screen.height);
                _isMaximized = true;
            }

            // Viewport-relative sizing: switching maximize state changes the target area,
            // so the terminal needs a refit. Deferred to Update — Unity disallows native
            // object destruction inside an OnGUI event flow.
            if (_sizingMode != SizingMode.Pixel)
                _resizeDirty = true;
        }

        private void HandleWindowDrag(Rect titleBarRect, Rect closeHit, Rect minimizeHit, Rect fullscreenHit,
                                      Rect zoomPlusRect, Rect zoomMinusRect, Rect resizeHandleRect)
        {
            // Disable drag while maximized — restoring via fullscreen toggle keeps semantics simple.
            if (_isMaximized) return;
            // Resize already owns the event flow — don't compete for the same gesture.
            if (_isResizing) return;

            Event e = Event.current;
            Vector2 mouse = e.mousePosition;

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button != 0) return;
                    if (!titleBarRect.Contains(mouse)) return;
                    // Title bar click (anywhere, including the traffic-light buttons) focuses the window.
                    if (!IsFocused) RatatuiFocusManager.SetFocus(this);
                    if (closeHit.Contains(mouse)
                        || minimizeHit.Contains(mouse)
                        || fullscreenHit.Contains(mouse)
                        || zoomPlusRect.Contains(mouse)
                        || zoomMinusRect.Contains(mouse)
                        || resizeHandleRect.Contains(mouse)) return;
                    _isDragging = true;
                    _dragStartMouse = mouse;
                    _dragStartWindowPos = _windowRect.position;
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (!_isDragging) return;
                    _windowRect.position = _dragStartWindowPos + (mouse - _dragStartMouse);
                    ClampWindowPositionOnScreen();
                    e.Use();
                    break;

                case EventType.MouseUp:
                    if (_isDragging)
                    {
                        _isDragging = false;
                        e.Use();
                    }
                    break;
            }
        }

        // Keep at least WindowMinVisible of the title bar reachable so drag + zoom stay usable.
        private void ClampWindowPositionOnScreen()
        {
            float maxX = Screen.width - WindowMinVisible;
            float minX = WindowMinVisible - _windowRect.width;
            float maxY = Screen.height - WindowTitleBarHeight;
            float minY = 0f;
            _windowRect.x = Mathf.Clamp(_windowRect.x, minX, maxX);
            _windowRect.y = Mathf.Clamp(_windowRect.y, minY, maxY);
        }

        // Drag-resize from the titlebar's top-right square handle. Bottom-left
        // pivot: left edge and bottom edge stay anchored; the right and top
        // edges follow the mouse independently (no aspect lock), so the handle
        // tracks the cursor naturally during the drag. fontSize is untouched;
        // the existing refit pipeline (CalculateColsAndRows when _fitColsAndRows=true)
        // re-derives the grid for the new content area on mouse-up.
        // NOTE: this differs from SyncWindowRectToTexture's top-right pivot —
        // refits (zoom/maximize-restore) keep the right edge fixed so the
        // right-side controls don't jump; manual drag-resize follows the cursor.
        private void HandleWindowResize(Rect handleRect)
        {
            if (_isMaximized) return;

            Event e = Event.current;
            Vector2 mouse = e.mousePosition;

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button != 0) return;
                    if (!handleRect.Contains(mouse)) return;
                    if (!IsFocused) RatatuiFocusManager.SetFocus(this);
                    _isResizing = true;
                    _resizeStartMouse = mouse;
                    _resizeStartWindowRect = _windowRect;
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (!_isResizing) return;
                    float dx = mouse.x - _resizeStartMouse.x;
                    float dy = mouse.y - _resizeStartMouse.y;

                    // Min width must fit: left traffic-lights + right (zoom pair + resize handle).
                    // All buttons share WindowButtonSize.
                    float btnSize = WindowButtonSize;
                    float minWidth = WindowButtonPadding + 3f * btnSize + 2f * WindowButtonSpacing  // left chrome
                                     + 3f * btnSize + 2f * WindowButtonSpacing                      // right squares
                                     + WindowResizeMargin;                                          // far-right margin
                    // Min height: titlebar + a sliver of content (reuse WindowMinVisible).
                    float minHeight = WindowTitleBarHeight + WindowMinVisible;

                    float bottomY = _resizeStartWindowRect.y + _resizeStartWindowRect.height;
                    float maxWidth = Screen.width;
                    // Top edge cannot leave the screen — height capped so newY >= 0.
                    // Floor at minHeight so Mathf.Clamp's invariant (min <= max) holds
                    // even when the window starts partly off-screen (bottomY < minHeight).
                    float maxHeight = Mathf.Max(minHeight, Mathf.Min(Screen.height, bottomY));

                    // Mouse moving right (dx>0) grows width (left edge pinned, right edge follows mouse).
                    // Mouse moving down  (dy>0) shrinks height (bottom edge pinned, top edge follows mouse).
                    float newWidth = Mathf.Clamp(_resizeStartWindowRect.width + dx, minWidth, maxWidth);
                    float newHeight = Mathf.Clamp(_resizeStartWindowRect.height - dy, minHeight, maxHeight);

                    _windowRect.x = _resizeStartWindowRect.x;       // left edge pinned
                    _windowRect.width = newWidth;
                    _windowRect.height = newHeight;
                    _windowRect.y = bottomY - newHeight;            // bottom edge pinned
                    ClampWindowPositionOnScreen();
                    e.Use();
                    break;

                case EventType.MouseUp:
                    if (!_isResizing) return;
                    _isResizing = false;
                    // Refit terminal grid to the new content area (only meaningful when
                    // _fitColsAndRows=true; otherwise SyncWindowRectToTexture snaps back
                    // to the fixed _cols×_rows texture, which is the documented contract).
                    _resizeDirty = true;
                    e.Use();
                    break;
            }
        }

        private void ApplyFontZoom(float factor)
        {
            if (!_terminalReady) return;

            float newFontSize = Mathf.Clamp(_fontSize * factor, WindowFontSizeMin, WindowFontSizeMax);
            if (Mathf.Approximately(newFontSize, _fontSize)) return;

            _fontSize = newFontSize;

            // Defer refit: ReinitializeTerminal destroys the native terminal,
            // which Unity forbids inside an OnGUI event flow.
            _resizeDirty = true;
        }

        private void DrawZoomGlyph(Rect rect, string text)
        {
            GUI.Label(rect, text, GetZoomGlyphStyle());
        }

        private GUIStyle GetZoomGlyphStyle()
        {
            if (_windowZoomGlyphStyle == null)
            {
                _windowZoomGlyphStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    // FontStyle.Bold avoided on purpose: the bundled JetBrains Mono is the
                    // Regular cut and Unity's runtime bold-simulate has been observed to drop
                    // non-ASCII glyphs (↗, −) on WebGL. Leave style Normal.
                    fontStyle = FontStyle.Normal,
                    clipping = TextClipping.Clip,
                    wordWrap = false,
                };
                _windowZoomGlyphStyle.normal.textColor = WindowTitleTextColor;
            }
            // Glyph sits inside the button with a small margin so it stays clear of
            // the rounded corners. Refreshed every call so screen resizes track and
            // runtime font swaps take effect. Non-ASCII glyphs (e.g. ↗, −) need the
            // bundled chrome font on platforms without OS font fallback.
            _windowZoomGlyphStyle.font = _windowChromeFont;
            _windowZoomGlyphStyle.fontSize = Mathf.Max(1, Mathf.RoundToInt(WindowButtonSize * 0.65f));
            return _windowZoomGlyphStyle;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void ApplyTextureTarget()
        {
            // Native pixel buffer is top-to-bottom (no vertical flip in Rust).
            // Each target needs a UV flip to display correctly.

            if (_rawImage != null)
            {
                _rawImage.texture = Texture;
                _rawImage.uvRect = new Rect(0f, 1f, 1f, -1f);
                if (!_fitColsAndRows)
                    FitRawImageToTexture();
            }

            if (_meshRenderer != null && _meshRenderer.material != null)
            {
                var mat = _meshRenderer.material;
                mat.mainTexture = Texture;
                mat.mainTextureScale = new Vector2(1f, -1f);
                mat.mainTextureOffset = new Vector2(0f, 1f);
            }
        }

        /// <summary>
        /// Resizes the assigned RawImage's RectTransform to exactly match the
        /// terminal texture dimensions (1 texture pixel = 1 screen pixel at 100% scale).
        /// No-op when sizingMode is viewport-relative (Vh / Vw / Vmin / Vmax), since
        /// in those modes the RectTransform is the input to sizing and overwriting
        /// it would create a feedback loop.
        /// </summary>
        public void FitRawImageToTexture()
        {
            if (_rawImage == null || Terminal == null) return;
            if (_sizingMode != SizingMode.Pixel) return;
            var rt = _rawImage.rectTransform;
            rt.sizeDelta = new Vector2(Terminal.PixelWidth, Terminal.PixelHeight);
        }

        /// <summary>
        /// Force an immediate refit + terminal recreation. Useful after manually
        /// changing the RawImage RectTransform from code, or after switching sizingMode.
        /// </summary>
        public void ForceRefit()
        {
            if (!_terminalReady) return;
            ReinitializeTerminal(firstTime: false);
        }

        /// <summary>
        /// Replace the embedded JetBrains Mono font with a custom TTF font.
        /// Caches the bytes so subsequent terminal recreations (resize / DPI change)
        /// keep the user's font instead of falling back to the embedded default.
        /// </summary>
        /// <returns>True if the font was loaded successfully.</returns>
        public bool SetCustomFont(byte[] ttfBytes)
        {
            if (Terminal == null) return false;
            bool ok = Terminal.SetCustomFont(ttfBytes);
            if (ok)
                _customFontBytes = ttfBytes;
            return ok;
        }

        // ── Refit Pipeline ────────────────────────────────────────────────────

        private void CheckForResize()
        {
            _resizePollTimer += Time.unscaledDeltaTime;
            bool poll = _resizePollSeconds <= 0f || _resizePollTimer >= _resizePollSeconds;

            if (poll)
            {
                _resizePollTimer = 0f;
                if (Screen.width != _lastScreenWidth
                    || Screen.height != _lastScreenHeight
                    || !Mathf.Approximately(Screen.dpi, _lastScreenDpi))
                {
                    _resizeDirty = true;
                }
            }

            if (_resizeDirty)
            {
                _resizeDirty = false;
                ReinitializeTerminal(firstTime: false);
                SnapshotScreenMetrics();
            }
        }

        private void SnapshotScreenMetrics()
        {
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            _lastScreenDpi = Screen.dpi;
        }

        private void ReinitializeTerminal(bool firstTime)
        {
            float fontSize = ComputeFontSize();

            int targetCols = _cols;
            int targetRows = _rows;
            if (_fitColsAndRows)
                CalculateColsAndRows(fontSize, out targetCols, out targetRows);

            // Tear down the old handle + GPU texture before creating the new ones.
            // Order matters: dispose first to release the native pixel buffer,
            // then Destroy the Texture2D so its size can change.
            if (Terminal != null)
            {
                Terminal.Dispose();
                Terminal = null;
            }
            if (Texture != null)
            {
                Destroy(Texture);
                Texture = null;
            }

            Terminal = new RatatuiTerminal(targetCols, targetRows, fontSize);

            // Re-apply state that lives on the native handle.
            if (_customFontBytes != null && _customFontBytes.Length > 0)
                Terminal.SetCustomFont(_customFontBytes);
            Terminal.SetBackgroundColor(_backgroundColor);

            Texture = new Texture2D(
                Terminal.PixelWidth,
                Terminal.PixelHeight,
                TextureFormat.RGB24,
                mipChain: false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            ApplyTextureTarget();
            SyncWindowRectToTexture();

            _terminalReady = true;

            if (!firstTime)
                OnTerminalResized?.Invoke(Terminal.Cols, Terminal.Rows, fontSize);
        }

        /// <summary>
        /// Resolve <see cref="_fontSize"/> to absolute pixels according to the current
        /// <see cref="SizingMode"/>. In <c>Pixel</c> mode this is identity; in viewport-
        /// relative modes it multiplies by the corresponding viewport dimension / 100.
        /// </summary>
        private float ComputeFontSize()
        {
            if (_sizingMode == SizingMode.Pixel)
                return Mathf.Max(1f, _fontSize);

            GetTargetPixelRect(out float width, out float height, fontSizeViewport: true);
            float basis = _sizingMode switch
            {
                SizingMode.Vh => height,
                SizingMode.Vw => width,
                SizingMode.Vmin => Mathf.Min(width, height),
                SizingMode.Vmax => Mathf.Max(width, height),
                _ => 0f,
            };
            float fontSize = _fontSize * basis / 100f;
            return Mathf.Max(1f, fontSize);
        }

        /// <param name="fontSizeViewport">
        /// When <c>true</c>, the caller wants a *stable* viewport basis for fontSize
        /// scaling rather than the live fill target. This matters only in OnGUI Window
        /// normal mode: the fill target (<c>_windowRect</c>) is itself derived from the
        /// texture, which is derived from fontSize — using it as the fontSize basis would
        /// be self-referential and compound across refits. So fontSize always scales
        /// against the screen (CSS-style viewport), while FitColsAndRows keeps filling
        /// the actual window content area.
        /// </param>
        private void GetTargetPixelRect(out float width, out float height, bool fontSizeViewport = false)
        {
            if (_rawImage != null)
            {
                Canvas.ForceUpdateCanvases();
                Rect rect = _rawImage.rectTransform.rect;
                width = rect.width;
                height = rect.height;
                return;
            }

            // OnGUI Window mode: target = the window's content area (title bar excluded).
            // Recreate keeps the window the same logical size; the terminal grid stays
            // fixed and fontSize adapts. When maximized, the window covers the screen.
            if (_meshRenderer == null && _onGuiMode == OnGuiMode.Window)
            {
                // fontSize basis is always the screen (stable, non-circular).
                bool useScreen = fontSizeViewport
                    || _isMaximized
                    || (!_windowInitialized && _windowStartMaximized);
                if (useScreen)
                {
                    width = Screen.width;
                    height = Mathf.Max(1f, Screen.height - WindowTitleBarHeight);
                    return;
                }
                if (_windowInitialized)
                {
                    width = _windowRect.width;
                    height = Mathf.Max(1f, _windowRect.height - WindowTitleBarHeight);
                    return;
                }
                // First call before EnsureWindowInitialized — honor the configured
                // initial size if set, otherwise fall back to ~70% of screen so the
                // very first auto fontSize lands somewhere usable.
                if (_windowInitialWidth > 0f && _windowInitialHeight > 0f)
                {
                    width = _windowInitialWidth;
                    height = Mathf.Max(1f, _windowInitialHeight - WindowTitleBarHeight);
                    return;
                }
                width = Screen.width * 0.7f;
                height = Mathf.Max(1f, Screen.height * 0.7f - WindowTitleBarHeight);
                return;
            }

            // OnGUI Full / Partial / mesh fallback: fit to the full screen.
            width = Screen.width;
            height = Screen.height;
        }

        /// <summary>
        /// After a recreate in a viewport-relative mode, snap the Window rect to the
        /// new texture dimensions so the chrome wraps the terminal exactly. When
        /// maximized, the window covers the screen and the terminal content fits inside.
        /// </summary>
        private void SyncWindowRectToTexture()
        {
            if (_meshRenderer != null) return;
            if (_rawImage != null) return;
            if (_onGuiMode != OnGuiMode.Window) return;
            if (Terminal == null) return;

            float w = Terminal.PixelWidth;
            float h = Terminal.PixelHeight + WindowTitleBarHeight;

            if (!_windowInitialized)
            {
                // Configured initial pixel size overrides texture-derived size so the
                // chrome opens at the user's requested rect even when FitColsAndRows
                // is on (the grid was already fit to this same rect via GetTargetPixelRect).
                float initW = _windowInitialWidth > 0f ? _windowInitialWidth : w;
                float initH = _windowInitialHeight > 0f ? _windowInitialHeight : h;
                float x = _windowInitialX < 0f ? (Screen.width - initW) * 0.5f : _windowInitialX;
                float y = _windowInitialY < 0f ? (Screen.height - initH) * 0.5f : _windowInitialY;
                _windowRect = new Rect(x, y, initW, initH);
                _windowInitialized = true;
                if (_windowStartMaximized && !_isMaximized)
                    ToggleMaximized();
                return;
            }

            if (_isMaximized)
            {
                // Maximized window already covers the screen — keep the rect at full
                // screen so the new terminal (sized to Screen - titleBar) fits exactly.
                // Leave _windowRestoreRect untouched: it holds the pre-maximize window
                // size captured in ToggleMaximized, and un-maximizing must return to
                // exactly that size. The restore refit recomputes the grid/texture to fit.
                _windowRect = new Rect(0f, 0f, Screen.width, Screen.height);
            }
            else
            {
                // Top-right pivot: right edge and top edge stay anchored across
                // refits, so the right-side controls (zoom + resize handle) keep
                // their on-screen position when the texture grows or shrinks.
                float anchorRight = _windowRect.x + _windowRect.width;
                _windowRect = new Rect(anchorRight - w, _windowRect.y, w, h);
                ClampWindowPositionOnScreen();
            }
        }

        /// <summary>
        /// Compute cols × rows so the terminal grid covers the target pixel area at
        /// the given fontSize, matching its aspect ratio. The target is whatever
        /// <see cref="GetTargetPixelRect"/> resolves to — RawImage rect, window content,
        /// or screen — so the same call works for RawImage, OnGUI Full, OnGUI Window
        /// (normal + fullscreen), and WebGL fullscreen.
        /// </summary>
        private void CalculateColsAndRows(float fontSize, out int cols, out int rows)
        {
            cols = _cols;
            rows = _rows;

            GetTargetPixelRect(out float w, out float h);
            if (w <= 0f || h <= 0f)
            {
                Debug.LogWarning(
                    "[RatatuiRenderer] FitColsAndRows: target area has zero size, " +
                    "falling back to inspector cols × rows.", this);
                return;
            }

            using (var probe = new RatatuiTerminal(1, 1, fontSize))
            {
                if (_customFontBytes != null && _customFontBytes.Length > 0)
                    probe.SetCustomFont(_customFontBytes);

                int cellWidth = probe.CellWidth;
                int cellHeight = probe.CellHeight;
                if (cellWidth <= 0 || cellHeight <= 0) return;

                // Round rather than floor so fractional cells don't accumulate as
                // window shrinkage across successive zoom refits. Texture may
                // overshoot the content area by ≤ half a cell — visually negligible.
                cols = Mathf.Max(1, Mathf.RoundToInt(w / cellWidth));
                rows = Mathf.Max(1, Mathf.RoundToInt(h / cellHeight));
            }
        }
    }
}
