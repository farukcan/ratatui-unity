//! Cell-buffer → RGB24 pixel-buffer rasterization.
//!
//! This module turns a ratatui [`Buffer`] (a grid of styled glyphs) into
//! flat RGB24 pixel data. It handles three render paths:
//!
//! - **Block elements** (`U+2580..=U+259F`) — painted directly in pixel
//!   space for crisp connections between adjacent cells.
//! - **Braille patterns** (`U+2800..=U+28FF`) — painted as 2×4 dot grids.
//! - **Everything else** — rasterized through fontdue and alpha-composited
//!   over the cell background.
//!
//! It also exposes [`compute_buffer_hash`], used to short-circuit
//! rasterization when the cell buffer is unchanged frame-to-frame.

use crate::color::color_to_rgb;
use crate::font::FontManager;
use fontdue::Metrics;
use ratatui::buffer::Buffer;
use ratatui::style::Color;
use rustc_hash::FxHasher;
use std::hash::{Hash, Hasher};

/// Computes a 64-bit hash over every cell field that affects visual output
/// (symbol, foreground, background, modifier).
///
/// Used by [`crate::ratatui_end_frame_hashed`] to skip rasterization
/// when the cell buffer is identical to the previous frame.
pub fn compute_buffer_hash(buffer: &Buffer) -> u64 {
    let mut hasher = FxHasher::default();
    let area = buffer.area();
    for row in 0..area.height {
        for col in 0..area.width {
            if let Some(cell) = buffer.cell((col + area.x, row + area.y)) {
                cell.symbol().hash(&mut hasher);
                cell.fg.hash(&mut hasher);
                cell.bg.hash(&mut hasher);
                cell.modifier.hash(&mut hasher);
            }
        }
    }
    hasher.finish()
}

/// Rasterizes a ratatui [`Buffer`] (cell grid) into a flat RGB24 pixel buffer.
///
/// # Parameters
/// - `buffer`: source cell grid.
/// - `font`: font + glyph cache; glyphs missing in the font are silently
///   skipped instead of being drawn as `.notdef`.
/// - `pixels`: destination byte buffer, reused across frames and resized to
///   `cols * cell_width * rows * cell_height * 3` bytes as needed.
/// - `background_color`: RGB used in place of [`Color::Reset`] for cell
///   backgrounds.
///
/// Layout: the output is row-major, three bytes per pixel (R, G, B), with no
/// padding between rows.
pub fn render_buffer_to_pixels(
    buffer: &Buffer,
    font: &mut FontManager,
    pixels: &mut Vec<u8>,
    background_color: [u8; 3],
) {
    let area = buffer.area();
    let cols = area.width as u32;
    let rows = area.height as u32;
    let cw = font.cell_width;
    let ch = font.cell_height;
    let baseline = font.baseline;
    let total_w = cols * cw;
    let total_h = rows * ch;
    // Multiply in usize: the u32 product can overflow for large buffers.
    let required = total_w as usize * total_h as usize * 3;
    pixels.resize(required, 0);

    for row in 0..rows {
        for col in 0..cols {
            let cell = match buffer.cell((col as u16 + area.x, row as u16 + area.y)) {
                Some(c) => c,
                None => continue,
            };

            let fg = color_to_rgb(cell.fg, true);
            let bg = if cell.bg == Color::Reset {
                background_color
            } else {
                color_to_rgb(cell.bg, false)
            };
            let cell_px = col * cw;
            let cell_py = row * ch;

            fill_background(pixels, cell_px, cell_py, cw, ch, &bg, total_w);

            let symbol = cell.symbol();
            if let Some(first_char) = symbol.chars().next() {
                if !first_char.is_whitespace() {
                    if is_block_element(first_char) {
                        draw_block_element(
                            pixels, first_char, cell_px, cell_py, cw, ch, &fg, total_w,
                        );
                    } else if is_braille(first_char) {
                        draw_braille(
                            pixels, first_char, cell_px, cell_py, cw, ch, &fg, total_w,
                        );
                    } else if font.has_glyph(first_char) {
                        let (metrics, bitmap) = font.get_glyph(first_char);
                        draw_glyph(
                            pixels, bitmap, metrics, cell_px, cell_py, cw, ch,
                            baseline, &fg, total_w,
                        );
                    }
                    // Characters not in the font are silently skipped (no .notdef box).
                }
            }
        }
    }

}

// ─── Braille renderer ────────────────────────────────────────────────────────

/// Returns true for Unicode Braille Patterns (U+2800–U+28FF).
fn is_braille(ch: char) -> bool {
    matches!(ch, '\u{2800}'..='\u{28FF}')
}

/// Renders a braille character as a 2-column × 4-row dot grid.
///
/// Unicode braille bit layout (relative to U+2800):
/// - bits 0-3: left column, rows 0-3 top→bottom
/// - bits 4-7: right column, rows 0-3 top→bottom
fn draw_braille(
    pixels: &mut [u8],
    ch: char,
    cell_px: u32,
    cell_py: u32,
    cw: u32,
    ch_h: u32,
    fg: &[u8; 3],
    total_w: u32,
) {
    let bits = ch as u32 - 0x2800;
    if bits == 0 { return; }

    // Dot radius in pixels (at least 1).
    let dot_r = (cw / 6).max(1);

    // Grid: 2 columns, 4 rows.  We divide the cell into equal segments.
    let col_step = cw / 2;
    let row_step = ch_h / 4;

    for bit in 0..8u32 {
        if bits & (1 << bit) == 0 { continue; }

        let col = bit / 4;       // 0 = left, 1 = right
        let row = bit % 4;       // 0..3 top→bottom

        // Centre of this dot within the cell
        let cx = cell_px + col * col_step + col_step / 2;
        let cy = cell_py + row * row_step + row_step / 2;

        // Paint a small filled square centered on (cx, cy)
        let x0 = cx.saturating_sub(dot_r);
        let y0 = cy.saturating_sub(dot_r);
        let x1 = (cx + dot_r + 1).min(cell_px + cw);
        let y1 = (cy + dot_r + 1).min(cell_py + ch_h);

        for py in y0..y1 {
            for px in x0..x1 {
                let idx = (py * total_w + px) as usize * 3;
                if let Some(dst) = pixels.get_mut(idx..idx + 3) {
                    dst.copy_from_slice(fg);
                }
            }
        }
    }
}

// ─── Block element renderer ──────────────────────────────────────────────────

/// Returns true only for Unicode "Block Elements" (U+2580–U+259F).
/// Box-drawing characters (U+2500–U+257F) are intentionally excluded here
/// and rendered via fontdue instead, which gives correct thickness and
/// seamless connections between adjacent cells.
fn is_block_element(ch: char) -> bool {
    matches!(ch, '\u{2580}'..='\u{259F}')
}

/// Renders a block/box character by directly painting pixel rows — no font
/// metrics involved, so positioning is always exact within the cell.
fn draw_block_element(
    pixels: &mut [u8],
    ch: char,
    cell_px: u32,
    cell_py: u32,
    cw: u32,
    ch_h: u32,
    fg: &[u8; 3],
    total_w: u32,
) {
    // Fraction of the cell (from the bottom) that this block covers.
    // U+2581 LOWER ONE EIGHTH BLOCK = 1/8, …, U+2588 FULL BLOCK = 8/8.
    let fill_fraction: f32 = match ch {
        '▁' => 1.0 / 8.0,
        '▂' => 2.0 / 8.0,
        '▃' => 3.0 / 8.0,
        '▄' => 4.0 / 8.0,
        '▅' => 5.0 / 8.0,
        '▆' => 6.0 / 8.0,
        '▇' => 7.0 / 8.0,
        '█' => 1.0,
        '▀' => 0.5, // upper half block — fill from top
        _ => {
            // Box-drawing characters: paint a thin line through the centre.
            draw_box_drawing(pixels, ch, cell_px, cell_py, cw, ch_h, fg, total_w);
            return;
        }
    };

    if ch == '▀' {
        // Upper-half block: top 50 % of the cell
        let fill_rows = (ch_h as f32 * 0.5).round() as u32;
        fill_cell_rows(pixels, cell_px, cell_py, cw, 0, fill_rows, fg, total_w);
    } else {
        // Lower blocks: bottom N/8 of the cell
        let fill_rows = (ch_h as f32 * fill_fraction).round() as u32;
        let start_row = ch_h.saturating_sub(fill_rows);
        fill_cell_rows(pixels, cell_px, cell_py, cw, start_row, ch_h, fg, total_w);
    }
}

/// Fill a horizontal band of pixel rows inside a cell with `fg`.
fn fill_cell_rows(
    pixels: &mut [u8],
    cell_px: u32,
    cell_py: u32,
    cw: u32,
    row_start: u32,
    row_end: u32,
    fg: &[u8; 3],
    total_w: u32,
) {
    for py in row_start..row_end {
        for px in 0..cw {
            let idx = ((cell_py + py) * total_w + (cell_px + px)) as usize * 3;
            if let Some(dst) = pixels.get_mut(idx..idx + 3) {
                dst.copy_from_slice(fg);
            }
        }
    }
}

/// Minimal box-drawing renderer: paints centre-line horizontal/vertical
/// segments.
fn draw_box_drawing(
    pixels: &mut [u8],
    ch: char,
    cell_px: u32,
    cell_py: u32,
    cw: u32,
    ch_h: u32,
    fg: &[u8; 3],
    total_w: u32,
) {
    let mid_x = cell_px + cw / 2;
    let mid_y = cell_py + ch_h / 2;

    // Determine which segments this character draws
    let (left, right, up, down) = box_drawing_segments(ch);

    // Horizontal segment
    if left || right {
        let x_start = if left { cell_px } else { mid_x };
        let x_end = if right { cell_px + cw } else { mid_x + 1 };
        for x in x_start..x_end {
            let idx = (mid_y * total_w + x) as usize * 3;
            if let Some(dst) = pixels.get_mut(idx..idx + 3) {
                dst.copy_from_slice(fg);
            }
        }
    }

    // Vertical segment
    if up || down {
        let y_start = if up { cell_py } else { mid_y };
        let y_end = if down { cell_py + ch_h } else { mid_y + 1 };
        for y in y_start..y_end {
            let idx = (y * total_w + mid_x) as usize * 3;
            if let Some(dst) = pixels.get_mut(idx..idx + 3) {
                dst.copy_from_slice(fg);
            }
        }
    }
}

/// Returns (left, right, up, down) segment flags for common box-drawing
/// characters.
fn box_drawing_segments(ch: char) -> (bool, bool, bool, bool) {
    match ch {
        '─' | '━' | '╌' | '╍' => (true,  true,  false, false),
        '│' | '┃' | '╎' | '╏' => (false, false, true,  true ),
        '┌' | '╔' | '╒' | '╓' => (false, true,  false, true ),
        '┐' | '╗' | '╕' | '╖' => (true,  false, false, true ),
        '└' | '╚' | '╘' | '╙' => (false, true,  true,  false),
        '┘' | '╝' | '╛' | '╜' => (true,  false, true,  false),
        '├' | '╠' | '╞' | '╟' => (false, true,  true,  true ),
        '┤' | '╣' | '╡' | '╢' => (true,  false, true,  true ),
        '┬' | '╦' | '╤' | '╥' => (true,  true,  false, true ),
        '┴' | '╩' | '╧' | '╨' => (true,  true,  true,  false),
        '┼' | '╬' | '╪' | '╫' => (true,  true,  true,  true ),
        _                       => (true,  true,  true,  true ),
    }
}

// ─── Font-based glyph renderer ───────────────────────────────────────────────

/// Fills a full cell rectangle with `bg`.
fn fill_background(
    pixels: &mut [u8],
    cell_px: u32,
    cell_py: u32,
    cw: u32,
    ch: u32,
    bg: &[u8; 3],
    total_w: u32,
) {
    for py in 0..ch {
        for px in 0..cw {
            let idx = ((cell_py + py) * total_w + (cell_px + px)) as usize * 3;
            if let Some(dst) = pixels.get_mut(idx..idx + 3) {
                dst.copy_from_slice(bg);
            }
        }
    }
}

/// Alpha-composites a fontdue coverage bitmap over the cell at
/// `(cell_px, cell_py)` using `fg` as the source color.
///
/// `bitmap` is a row-major coverage map of size
/// `metrics.width × metrics.height`.
/// The glyph is positioned using `metrics.xmin` / `metrics.ymin` relative to
/// the baseline; pixels falling outside the cell are clipped.
fn draw_glyph(
    pixels: &mut [u8],
    bitmap: &[u8],
    metrics: &Metrics,
    cell_px: u32,
    cell_py: u32,
    cell_w: u32,
    cell_h: u32,
    baseline: u32,
    fg: &[u8; 3],
    total_w: u32,
) {
    if metrics.width == 0 || metrics.height == 0 {
        return;
    }

    // ymin: distance from baseline to glyph bottom edge (positive = above baseline)
    let glyph_y_top = baseline as i32 - (metrics.ymin + metrics.height as i32);
    let glyph_x_left = metrics.xmin;

    for gy in 0..metrics.height {
        for gx in 0..metrics.width {
            let px = cell_px as i32 + glyph_x_left + gx as i32;
            let py = cell_py as i32 + glyph_y_top + gy as i32;

            if px < cell_px as i32
                || py < cell_py as i32
                || px >= (cell_px + cell_w) as i32
                || py >= (cell_py + cell_h) as i32
            {
                continue;
            }

            let coverage = bitmap[gy * metrics.width + gx];
            if coverage == 0 {
                continue;
            }

            let idx = (py as u32 * total_w + px as u32) as usize * 3;
            if idx + 2 >= pixels.len() {
                continue;
            }

            // Alpha composite foreground over existing background
            let alpha = coverage as f32 / 255.0;
            let inv = 1.0 - alpha;
            pixels[idx]     = (fg[0] as f32 * alpha + pixels[idx]     as f32 * inv) as u8;
            pixels[idx + 1] = (fg[1] as f32 * alpha + pixels[idx + 1] as f32 * inv) as u8;
            pixels[idx + 2] = (fg[2] as f32 * alpha + pixels[idx + 2] as f32 * inv) as u8;
        }
    }
}
