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
    /// Override <see cref="BuildFrame"/> in a subclass to define widget layout.
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

        // ── Public Properties ─────────────────────────────────────────────────

        /// <summary>The rendered texture. Assign to any Unity material or UI image.</summary>
        public Texture2D Texture { get; private set; }

        /// <summary>The underlying terminal instance.</summary>
        public RatatuiTerminal Terminal { get; private set; }

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
            Terminal.BeginFrame();
            BuildFrame(Terminal);

            // Use the zero-copy path: read directly from the native pixel buffer
            // pointer instead of marshalling into a managed byte[].
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

        // ── Override in Subclass ──────────────────────────────────────────────

        /// <summary>
        /// Called every frame between <c>BeginFrame</c> and <c>EndFrame</c>.
        /// Override this method to define the terminal layout and widgets.
        /// </summary>
        protected virtual void BuildFrame(RatatuiTerminal term)
        {
            // Default: empty frame (black screen)
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
        /// Call this after changing cols/rows/fontSize at runtime, or use the
        /// Inspector context menu "Fit RawImage to Texture".
        /// </summary>
        public void FitRawImageToTexture()
        {
            if (_rawImage == null || Terminal == null) return;
            var rt = _rawImage.rectTransform;
            rt.sizeDelta = new Vector2(Terminal.PixelWidth, Terminal.PixelHeight);
        }
    }
}
