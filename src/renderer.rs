use crate::color::color_to_rgba;
use crate::font::FontManager;
use fontdue::Metrics;
use ratatui::buffer::Buffer;

/// Converts a ratatui `Buffer` (cell grid) into a flat RGBA pixel buffer.
/// The returned Vec is `pixel_width * pixel_height * 4` bytes.
pub fn render_buffer_to_pixels(buffer: &Buffer, font: &mut FontManager) -> Vec<u8> {
    let area = buffer.area();
    let cols = area.width as u32;
    let rows = area.height as u32;
    let cw = font.cell_width;
    let ch = font.cell_height;
    let total_w = cols * cw;
    let total_h = rows * ch;

    let mut pixels = vec![0u8; (total_w * total_h * 4) as usize];

    for row in 0..rows {
        for col in 0..cols {
            let cell = match buffer.cell((col as u16 + area.x, row as u16 + area.y)) {
                Some(c) => c,
                None => continue,
            };

            let fg = color_to_rgba(cell.fg, true);
            let bg = color_to_rgba(cell.bg, false);
            let cell_px = col * cw;
            let cell_py = row * ch;

            fill_background(&mut pixels, cell_px, cell_py, cw, ch, &bg, total_w);

            let symbol = cell.symbol();
            if let Some(first_char) = symbol.chars().next() {
                if !first_char.is_whitespace() {
                    if is_block_element(first_char) {
                        draw_block_element(
                            &mut pixels, first_char, cell_px, cell_py, cw, ch, &fg, total_w,
                        );
                    } else {
                        let (metrics, bitmap) = font.get_glyph(first_char);
                        draw_glyph(
                            &mut pixels, &bitmap, &metrics, cell_px, cell_py, cw, ch,
                            font.baseline, &fg, total_w,
                        );
                    }
                }
            }
        }
    }

    pixels
}

// ─── Block element renderer ──────────────────────────────────────────────────

/// Returns true for Unicode "Block Elements" (U+2580–U+259F) and related
/// box-drawing / braille characters that ratatui uses in Gauge and Sparkline.
fn is_block_element(ch: char) -> bool {
    matches!(ch,
        '\u{2580}'..='\u{259F}' | // Block Elements (▀ ▁ ▂ … █)
        '\u{2500}'..='\u{257F}'   // Box Drawing (─ │ ┌ ┐ └ ┘ ├ ┤ ┬ ┴ ┼ …)
    )
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
    fg: &[u8; 4],
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
    fg: &[u8; 4],
    total_w: u32,
) {
    for py in row_start..row_end {
        for px in 0..cw {
            let idx = ((cell_py + py) * total_w + (cell_px + px)) as usize * 4;
            if let Some(dst) = pixels.get_mut(idx..idx + 4) {
                dst.copy_from_slice(fg);
            }
        }
    }
}

/// Minimal box-drawing renderer: paints centre-line horizontal/vertical segments.
fn draw_box_drawing(
    pixels: &mut [u8],
    ch: char,
    cell_px: u32,
    cell_py: u32,
    cw: u32,
    ch_h: u32,
    fg: &[u8; 4],
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
            let idx = (mid_y * total_w + x) as usize * 4;
            if let Some(dst) = pixels.get_mut(idx..idx + 4) {
                dst.copy_from_slice(fg);
            }
        }
    }

    // Vertical segment
    if up || down {
        let y_start = if up { cell_py } else { mid_y };
        let y_end = if down { cell_py + ch_h } else { mid_y + 1 };
        for y in y_start..y_end {
            let idx = (y * total_w + mid_x) as usize * 4;
            if let Some(dst) = pixels.get_mut(idx..idx + 4) {
                dst.copy_from_slice(fg);
            }
        }
    }
}

/// Returns (left, right, up, down) segment flags for common box-drawing characters.
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

fn fill_background(
    pixels: &mut [u8],
    cell_px: u32,
    cell_py: u32,
    cw: u32,
    ch: u32,
    bg: &[u8; 4],
    total_w: u32,
) {
    for py in 0..ch {
        for px in 0..cw {
            let idx = ((cell_py + py) * total_w + (cell_px + px)) as usize * 4;
            if let Some(dst) = pixels.get_mut(idx..idx + 4) {
                dst.copy_from_slice(bg);
            }
        }
    }
}

fn draw_glyph(
    pixels: &mut [u8],
    bitmap: &[u8],
    metrics: &Metrics,
    cell_px: u32,
    cell_py: u32,
    cell_w: u32,
    cell_h: u32,
    baseline: u32,
    fg: &[u8; 4],
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

            let idx = (py as u32 * total_w + px as u32) as usize * 4;
            if idx + 3 >= pixels.len() {
                continue;
            }

            // Alpha composite foreground over existing background
            let alpha = coverage as f32 / 255.0;
            let inv = 1.0 - alpha;
            pixels[idx]     = (fg[0] as f32 * alpha + pixels[idx]     as f32 * inv) as u8;
            pixels[idx + 1] = (fg[1] as f32 * alpha + pixels[idx + 1] as f32 * inv) as u8;
            pixels[idx + 2] = (fg[2] as f32 * alpha + pixels[idx + 2] as f32 * inv) as u8;
            pixels[idx + 3] = 0xFF;
        }
    }
}
