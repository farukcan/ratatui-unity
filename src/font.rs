//! Font loading, glyph rasterization, and cell-metric computation.
//!
//! [`FontManager`] wraps a [`fontdue::Font`] and lazily caches rasterized
//! glyphs. It exposes the cell dimensions used by the layout (`cell_width`,
//! `cell_height`, `baseline`) so the rest of the crate can think purely in
//! cell coordinates and let the manager translate to pixels.
//!
//! The default font is JetBrains Mono Regular, embedded into the binary at
//! compile time. Hosts can substitute another TTF via
//! [`crate::ratatui_set_custom_font`].

use fontdue::{Font, FontSettings, Metrics};
use std::collections::HashMap;

/// Embedded JetBrains Mono Regular TTF bytes, used as the default font.
static DEFAULT_FONT_BYTES: &[u8] =
    include_bytes!("../fonts/JetBrainsMono-Regular.ttf");

/// Owns the active font, its glyph cache, and the per-cell pixel metrics
/// derived from it.
///
/// The manager rasterizes glyphs on first use and caches them by `char`. The
/// cell dimensions (`cell_width`, `cell_height`, `baseline`) are computed
/// once when the font is loaded and remain constant until the font is
/// replaced via [`Self::set_custom_font`].
pub struct FontManager {
    /// Active fontdue font.
    font: Font,
    /// Rasterization size in pixels.
    font_size: f32,
    /// Cache from `char` to `(metrics, coverage bitmap)`.
    glyph_cache: HashMap<char, (Metrics, Vec<u8>)>,
    /// Width of one cell in pixels (advance width of the space glyph).
    pub cell_width: u32,
    /// Height of one cell in pixels (`ascent + descent`).
    pub cell_height: u32,
    /// Distance from the top of the cell to the font baseline, in pixels.
    pub baseline: u32,
}

impl FontManager {
    /// Loads the embedded JetBrains Mono font at `font_size` pixels.
    ///
    /// # Panics
    ///
    /// Panics if the embedded font fails to load, which would indicate a
    /// build-time corruption rather than a runtime condition.
    pub fn new(font_size: f32) -> Self {
        let font = Font::from_bytes(DEFAULT_FONT_BYTES, FontSettings::default())
            .expect("Failed to load embedded JetBrains Mono font");
        Self::from_font(font, font_size)
    }

    /// Replaces the active font with custom TTF bytes.
    ///
    /// On success the glyph cache is dropped and the cell metrics are
    /// recomputed from the new font at the existing `font_size`.
    ///
    /// Returns `false` if the bytes do not represent a valid font; in that
    /// case the manager is left unchanged.
    pub fn set_custom_font(&mut self, font_data: &[u8]) -> bool {
        match Font::from_bytes(font_data, FontSettings::default()) {
            Ok(font) => {
                let new = Self::from_font(font, self.font_size);
                *self = new;
                true
            }
            Err(_) => false,
        }
    }

    /// Builds a [`FontManager`] from an already-loaded fontdue [`Font`].
    ///
    /// Cell dimensions are derived from the space glyph (advance width) and
    /// the font's horizontal line metrics (ascent + descent).
    ///
    /// # Panics
    ///
    /// Panics if the font reports no horizontal line metrics, which is
    /// effectively impossible for fonts produced by mainstream toolchains.
    fn from_font(font: Font, font_size: f32) -> Self {
        let (space_metrics, _) = font.rasterize(' ', font_size);
        let line_metrics = font
            .horizontal_line_metrics(font_size)
            .expect("Font has no horizontal line metrics");

        let cell_width = space_metrics.advance_width.ceil() as u32;
        let ascent = line_metrics.ascent.ceil() as u32;
        let descent = (-line_metrics.descent).ceil() as u32;
        let cell_height = ascent + descent;
        let baseline = ascent;

        Self {
            font,
            font_size,
            glyph_cache: HashMap::new(),
            cell_width,
            cell_height,
            baseline,
        }
    }

    /// Returns whether the active font actually contains a glyph for `ch`.
    ///
    /// Glyph index `0` is `.notdef` (the placeholder "missing" box), which
    /// this function treats as "no glyph available". Used by the renderer to
    /// silently skip unsupported characters instead of drawing tofu boxes.
    pub fn has_glyph(&self, ch: char) -> bool {
        self.font.lookup_glyph_index(ch) != 0
    }

    /// Returns `(metrics, coverage_bitmap)` for `ch`, rasterizing it on first
    /// access and caching the result for subsequent calls.
    ///
    /// The coverage bitmap is row-major with one alpha byte per pixel
    /// (`0` = transparent, `255` = fully opaque).
    pub fn get_glyph(&mut self, ch: char) -> &(Metrics, Vec<u8>) {
        if !self.glyph_cache.contains_key(&ch) {
            let (metrics, bitmap) = self.font.rasterize(ch, self.font_size);
            self.glyph_cache.insert(ch, (metrics, bitmap));
        }
        self.glyph_cache.get(&ch).unwrap()
    }
}
