use fontdue::{Font, FontSettings, Metrics};
use std::collections::HashMap;

static DEFAULT_FONT_BYTES: &[u8] =
    include_bytes!("../fonts/JetBrainsMono-Regular.ttf");

pub struct FontManager {
    font: Font,
    font_size: f32,
    glyph_cache: HashMap<char, (Metrics, Vec<u8>)>,
    pub cell_width: u32,
    pub cell_height: u32,
    pub baseline: u32,
}

impl FontManager {
    pub fn new(font_size: f32) -> Self {
        let font = Font::from_bytes(DEFAULT_FONT_BYTES, FontSettings::default())
            .expect("Failed to load embedded JetBrains Mono font");
        Self::from_font(font, font_size)
    }

    /// Replace the current font with custom TTF bytes. Returns false if the
    /// bytes are not a valid font.
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

    /// Returns (metrics, coverage_bitmap) for a character, using the cache.
    pub fn get_glyph(&mut self, ch: char) -> (Metrics, Vec<u8>) {
        if let Some(entry) = self.glyph_cache.get(&ch) {
            return (entry.0, entry.1.clone());
        }
        let (metrics, bitmap) = self.font.rasterize(ch, self.font_size);
        self.glyph_cache.insert(ch, (metrics, bitmap.clone()));
        (metrics, bitmap)
    }
}
