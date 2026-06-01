using System;
using UnityEngine;
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
        [Tooltip("Width of the terminal in character columns.")]
        [SerializeField] private int _cols = 80;

        [Tooltip("Height of the terminal in character rows.")]
        [SerializeField] private int _rows = 24;

        [Tooltip("Font size in pixels (affects texture resolution).")]
        [SerializeField] private float _fontSize = 14f;

        [Tooltip("Derive cols/rows from the RawImage RectTransform size instead of using fixed values.")]
        [SerializeField] private bool _fitIntoRectTransform;

        [Tooltip("The background color of the terminal (alpha is ignored — texture is always opaque).")]
        [SerializeField] private Color _backgroundColor = new Color(0.102f, 0.102f, 0.18f); // dark navy

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

        [Tooltip("Height of the title bar in pixels (Window mode only).")]
        [SerializeField] private float _windowTitleBarHeight = 24f;

        [Tooltip("Background color of the draggable title bar (Window mode only).")]
        [SerializeField] private Color _windowTitleBarColor = new Color(0.09f, 0.09f, 0.09f);

        [Tooltip("Initial window X position in screen GUI space. -1 = center on screen.")]
        [SerializeField] private float _windowInitialX = -1f;

        [Tooltip("Initial window Y position in screen GUI space. -1 = center on screen.")]
        [SerializeField] private float _windowInitialY = -1f;

        [Tooltip("Start maximized (fills screen) on first open in Window mode.")]
        [SerializeField] private bool _windowStartMaximized;

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
        [SerializeField] private int _maxRenderFps;

        // ── Public Properties ─────────────────────────────────────────────────

        /// <summary>The rendered texture. Assign to any Unity material or UI image.</summary>
        public Texture2D Texture { get; private set; }

        /// <summary>The underlying terminal instance.</summary>
        public RatatuiTerminal Terminal { get; private set; }

        /// <summary>Current mouse hover state in terminal coordinates.</summary>
        public TerminalHoverState HoverState { get; private set; }

        // ── Internal State ────────────────────────────────────────────────────

        // OnGUI fallback rect (GUI coordinates: y=0 at top)
        private Rect _onGuiRect;

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

        // Cached textures for tinted GUI fills (lazy init)
        private static Texture2D _windowFillTexture;
        private static Texture2D _windowCircleTexture;
        private GUIStyle _windowTitleStyle;

        // macOS traffic-light colors (close is permanently disabled → dim variant only)
        private static readonly Color WindowMinimizeColor      = new Color(0.996f, 0.737f, 0.180f);
        private static readonly Color WindowFullscreenColor    = new Color(0.157f, 0.784f, 0.251f);
        private static readonly Color WindowTitleTextColor     = new Color(0.85f, 0.85f, 0.87f);
        private static readonly Color WindowCloseDisabledColor = new Color(0.5f, 0.186f, 0.170f);

        // Window chrome layout constants
        private const float WindowButtonSize    = 12f;
        private const float WindowButtonPadding = 8f;
        private const float WindowButtonSpacing = 8f;
        private const float WindowMinVisible    = 80f;

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
        };

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        protected virtual void Awake()
        {
            if (_fitIntoRectTransform)
                CalculateColsRowsFromRectTransform();

            Terminal = new RatatuiTerminal(_cols, _rows, _fontSize);
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
            _cachedMainCamera = Camera.main;
            ValidateInputRequirements();
        }

        protected virtual void Update()
        {
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

            if (_onGuiMode == OnGuiMode.Window)
            {
                EnsureWindowInitialized();
                DrawWindowChrome();
                if (!_isMinimized)
                {
                    Rect contentRect = new Rect(
                        _windowRect.x,
                        _windowRect.y + _windowTitleBarHeight,
                        _windowRect.width,
                        _windowRect.height - _windowTitleBarHeight);
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

        // ── Input Processing ──────────────────────────────────────────────────

        private void ProcessInput()
        {
            var mods = GetCurrentModifiers();
            if (_enableKeyboardInput) ProcessKeyboard(mods);
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
                // Mouse is outside the terminal
                if (HoverState.IsInside)
                {
                    var outside = TerminalHoverState.Outside;
                    OnTerminalHoverChanged(HoverState, outside);
                    HoverState = outside;
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
                _onGuiRect = new Rect(
                    _windowRect.x,
                    _windowRect.y + _windowTitleBarHeight,
                    _windowRect.width,
                    _windowRect.height - _windowTitleBarHeight);
                return;
            }

            float w = Texture.width;
            float h = Texture.height;

            float x = _onGuiHorizontalAlign switch
            {
                OnGuiHorizontalAlign.Left   => 0f,
                OnGuiHorizontalAlign.Right  => Screen.width - w,
                _                           => (Screen.width - w) * 0.5f,
            };

            float y = _onGuiVerticalAlign switch
            {
                OnGuiVerticalAlign.Top    => 0f,
                OnGuiVerticalAlign.Bottom => Screen.height - h,
                _                         => (Screen.height - h) * 0.5f,
            };

            _onGuiRect = new Rect(x, y, w, h);
        }

        // ── Window Mode Helpers ───────────────────────────────────────────────

        private void EnsureWindowInitialized()
        {
            if (_windowInitialized || Texture == null) return;

            float w = Texture.width;
            float h = Texture.height + _windowTitleBarHeight;
            float x = _windowInitialX < 0f ? (Screen.width  - w) * 0.5f : _windowInitialX;
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

        private GUIStyle GetWindowTitleStyle()
        {
            if (_windowTitleStyle == null)
            {
                _windowTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    clipping = TextClipping.Clip,
                    wordWrap = false,
                };
                _windowTitleStyle.normal.textColor = WindowTitleTextColor;
            }
            return _windowTitleStyle;
        }

        private void DrawWindowTitle(Rect titleBarRect)
        {
            GUI.Label(titleBarRect, gameObject.name, GetWindowTitleStyle());
        }

        private void DrawWindowChrome()
        {
            // Title bar background
            Rect titleBarRect = new Rect(
                _windowRect.x, _windowRect.y,
                _windowRect.width, _windowTitleBarHeight);
            FillRect(titleBarRect, _windowTitleBarColor);
            DrawWindowTitle(titleBarRect);

            // Traffic-light buttons (left side, vertically centered)
            float btnY = _windowRect.y + (_windowTitleBarHeight - WindowButtonSize) * 0.5f;
            float btnX = _windowRect.x + WindowButtonPadding;

            Rect closeRect      = new Rect(btnX, btnY, WindowButtonSize, WindowButtonSize);
            Rect minimizeRect   = new Rect(btnX + (WindowButtonSize + WindowButtonSpacing),
                                           btnY, WindowButtonSize, WindowButtonSize);
            Rect fullscreenRect = new Rect(btnX + (WindowButtonSize + WindowButtonSpacing) * 2f,
                                           btnY, WindowButtonSize, WindowButtonSize);

            // Close: disabled, visually dim — no click handling
            FillCircle(closeRect, WindowCloseDisabledColor);

            HandleWindowDrag(titleBarRect, closeRect, minimizeRect, fullscreenRect);

            // Minimize: yellow toggle
            FillCircle(minimizeRect, WindowMinimizeColor);
            if (Event.current.type == EventType.MouseDown
                && Event.current.button == 0
                && minimizeRect.Contains(Event.current.mousePosition))
            {
                _isMinimized = !_isMinimized;
                Event.current.Use();
            }

            // Fullscreen: green toggle
            FillCircle(fullscreenRect, WindowFullscreenColor);
            if (Event.current.type == EventType.MouseDown
                && Event.current.button == 0
                && fullscreenRect.Contains(Event.current.mousePosition))
            {
                ToggleMaximized();
                Event.current.Use();
            }
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
        }

        private void HandleWindowDrag(Rect titleBarRect, Rect closeRect, Rect minimizeRect, Rect fullscreenRect)
        {
            // Disable drag while maximized — restoring via fullscreen toggle keeps semantics simple.
            if (_isMaximized) return;

            Event e = Event.current;
            Vector2 mouse = e.mousePosition;

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button != 0) return;
                    if (!titleBarRect.Contains(mouse)) return;
                    if (closeRect.Contains(mouse)
                        || minimizeRect.Contains(mouse)
                        || fullscreenRect.Contains(mouse)) return;
                    _isDragging = true;
                    _dragStartMouse = mouse;
                    _dragStartWindowPos = _windowRect.position;
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (!_isDragging) return;
                    Vector2 newPos = _dragStartWindowPos + (mouse - _dragStartMouse);
                    // Keep at least WindowMinVisible of the title bar visible on screen.
                    float maxX = Screen.width  - WindowMinVisible;
                    float minX = WindowMinVisible - _windowRect.width;
                    float maxY = Screen.height - _windowTitleBarHeight;
                    float minY = 0f;
                    newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
                    newPos.y = Mathf.Clamp(newPos.y, minY, maxY);
                    _windowRect.position = newPos;
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

        // ── Helpers ───────────────────────────────────────────────────────────

        private void ApplyTextureTarget()
        {
            // Native pixel buffer is top-to-bottom (no vertical flip in Rust).
            // Each target needs a UV flip to display correctly.

            if (_rawImage != null)
            {
                _rawImage.texture = Texture;
                _rawImage.uvRect = new Rect(0f, 1f, 1f, -1f);
                if (!_fitIntoRectTransform)
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
        /// </summary>
        public void FitRawImageToTexture()
        {
            if (_rawImage == null || Terminal == null) return;
            var rt = _rawImage.rectTransform;
            rt.sizeDelta = new Vector2(Terminal.PixelWidth, Terminal.PixelHeight);
        }

        private void CalculateColsRowsFromRectTransform()
        {
            if (_rawImage == null)
            {
                Debug.LogWarning(
                    "[RatatuiRenderer] FitIntoRectTransform requires a RawImage target.", this);
                return;
            }

            Canvas.ForceUpdateCanvases();
            Rect rect = _rawImage.rectTransform.rect;

            if (rect.width <= 0f || rect.height <= 0f)
            {
                Debug.LogWarning(
                    "[RatatuiRenderer] RectTransform has zero size, cannot fit terminal.", this);
                return;
            }

            // Create a probe terminal to get cell dimensions for this font size
            using (var probe = new RatatuiTerminal(1, 1, _fontSize))
            {
                int cellWidth = probe.CellWidth;
                int cellHeight = probe.CellHeight;
                if (cellWidth <= 0 || cellHeight <= 0) return;

                _cols = Mathf.Max(1, Mathf.FloorToInt(rect.width / cellWidth));
                _rows = Mathf.Max(1, Mathf.FloorToInt(rect.height / cellHeight));
            }
        }
    }
}
