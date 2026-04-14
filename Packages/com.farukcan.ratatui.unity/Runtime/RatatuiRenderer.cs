using System;
using UnityEngine;
using UnityEngine.UI;

namespace RatatuiUnity
{
    /// <summary>
    /// MonoBehaviour that renders a Ratatui terminal to a <see cref="Texture2D"/>
    /// and optionally assigns it to a UI <see cref="RawImage"/> or a
    /// <see cref="MeshRenderer"/> material each frame.
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

        [Header("Target (optional)")]
        [Tooltip("Assign to a UI RawImage to display the terminal texture.")]
        [SerializeField] private RawImage _rawImage;

        [Tooltip("Assign to render the terminal texture onto a 3D mesh.")]
        [SerializeField] private Renderer _meshRenderer;

        [Header("Input Settings")]
        [Tooltip("Enable input processing (keyboard + mouse).")]
        [SerializeField] private bool _enableInput = true;

        [Tooltip("Enable mouse input (hover, click, scroll).")]
        [SerializeField] private bool _enableMouseInput = true;

        [Tooltip("Enable keyboard input.")]
        [SerializeField] private bool _enableKeyboardInput = true;

        [Tooltip("Scroll wheel sensitivity multiplier. Increase for faster scrolling.")]
        [SerializeField] private float _scrollSensitivity = 1f;

        // ── Public Properties ─────────────────────────────────────────────────

        /// <summary>The rendered texture. Assign to any Unity material or UI image.</summary>
        public Texture2D Texture { get; private set; }

        /// <summary>The underlying terminal instance.</summary>
        public RatatuiTerminal Terminal { get; private set; }

        /// <summary>Current mouse hover state in terminal coordinates.</summary>
        public TerminalHoverState HoverState { get; private set; }

        // ── Internal State ────────────────────────────────────────────────────

        // Track where mouse-down happened for click detection
        private int _mouseDownCol = -1;
        private int _mouseDownRow = -1;
        private MouseButton _mouseDownButton;

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
            Terminal = new RatatuiTerminal(_cols, _rows, _fontSize);
            Texture  = new Texture2D(
                Terminal.PixelWidth,
                Terminal.PixelHeight,
                TextureFormat.RGBA32,
                mipChain: false)
            {
                filterMode = FilterMode.Point,
                wrapMode   = TextureWrapMode.Clamp,
            };
            ApplyTextureTarget();
        }

        protected virtual void Update()
        {
            // Input runs before BuildFrame so state changes are reflected in the same frame
            if (_enableInput) ProcessInput();

            Terminal.BeginFrame();
            BuildFrame(Terminal);

            IntPtr ptr = Terminal.EndFrameRaw();
            if (ptr != IntPtr.Zero)
            {
                int byteCount = Terminal.PixelWidth * Terminal.PixelHeight * 4;
                Texture.LoadRawTextureData(ptr, byteCount);
                Texture.Apply(updateMipmaps: false);
            }
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
            TerminalHoverState oldState, TerminalHoverState newState) { }

        // ── Input Processing ──────────────────────────────────────────────────

        private void ProcessInput()
        {
            if (_enableKeyboardInput) ProcessKeyboard();
            if (_enableMouseInput)    ProcessMouse();
        }

        private KeyModifiers GetCurrentModifiers()
        {
            var mods = KeyModifiers.None;
            if (Input.GetKey(KeyCode.LeftShift)   || Input.GetKey(KeyCode.RightShift))
                mods |= KeyModifiers.Shift;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                mods |= KeyModifiers.Ctrl;
            if (Input.GetKey(KeyCode.LeftAlt)     || Input.GetKey(KeyCode.RightAlt))
                mods |= KeyModifiers.Alt;
            return mods;
        }

        private void ProcessKeyboard()
        {
            var mods = GetCurrentModifiers();

            // Character input — skip control chars, those are handled as KeyCode below
            foreach (char c in Input.inputString)
            {
                if (char.IsControl(c)) continue;
                OnTerminalKeyDown(new TerminalKeyEvent(KeyCode.None, c, mods));
            }

            // Special non-character keys
            foreach (var key in TrackedKeys)
            {
                if (Input.GetKeyDown(key))
                    OnTerminalKeyDown(new TerminalKeyEvent(key, '\0', mods));
            }
        }

        private void ProcessMouse()
        {
            var mods = GetCurrentModifiers();
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
            bool hoverChanged = currentHover.Col    != HoverState.Col
                             || currentHover.Row    != HoverState.Row
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
                    _mouseDownCol    = col;
                    _mouseDownRow    = row;
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

            // Scroll wheel
            float scroll = Input.mouseScrollDelta.y * _scrollSensitivity;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                OnTerminalMouseEvent(new TerminalMouseEvent(
                    MouseEventType.Scroll, col, row, areaId,
                    MouseButton.Left, scroll, mods));
            }
        }

        // ── Coordinate Conversion ─────────────────────────────────────────────

        /// <summary>
        /// Converts a screen-space pixel position to terminal cell coordinates.
        /// Supports both RawImage (UI) and MeshRenderer (3D) targets.
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
                Camera cam = Camera.main;
                if (cam == null) return false;

                Ray ray = cam.ScreenPointToRay(screenPos);
                if (!Physics.Raycast(ray, out RaycastHit hit)) return false;
                if (hit.collider == null || hit.collider.gameObject != gameObject)
                    return false;

                col = Mathf.Clamp(
                    (int)(hit.textureCoord.x * Terminal.Cols), 0, Terminal.Cols - 1);
                // Y-flip: UV y=0 at bottom, terminal y=0 at top
                row = Mathf.Clamp(
                    Terminal.Rows - 1 - (int)(hit.textureCoord.y * Terminal.Rows),
                    0, Terminal.Rows - 1);
                return true;
            }

            return false;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void ApplyTextureTarget()
        {
            if (_rawImage != null)
            {
                _rawImage.texture = Texture;
                FitRawImageToTexture();
            }

            if (_meshRenderer != null && _meshRenderer.material != null)
                _meshRenderer.material.mainTexture = Texture;
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
    }
}
