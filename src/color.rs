use ratatui::style::Color;

/// Default foreground color (light gray on dark terminal)
pub const DEFAULT_FG: [u8; 3] = [0xCC, 0xCC, 0xCC];
/// Default background color (dark navy)
pub const DEFAULT_BG: [u8; 3] = [0x1A, 0x1A, 0x2E];

pub fn color_to_rgb(color: Color, is_fg: bool) -> [u8; 3] {
    match color {
        Color::Reset => {
            if is_fg {
                DEFAULT_FG
            } else {
                DEFAULT_BG
            }
        }
        Color::Black => [0x00, 0x00, 0x00],
        Color::Red => [0xAA, 0x00, 0x00],
        Color::Green => [0x00, 0xAA, 0x00],
        Color::Yellow => [0xAA, 0x55, 0x00],
        Color::Blue => [0x00, 0x00, 0xAA],
        Color::Magenta => [0xAA, 0x00, 0xAA],
        Color::Cyan => [0x00, 0xAA, 0xAA],
        Color::Gray => [0xAA, 0xAA, 0xAA],
        Color::DarkGray => [0x55, 0x55, 0x55],
        Color::LightRed => [0xFF, 0x55, 0x55],
        Color::LightGreen => [0x55, 0xFF, 0x55],
        Color::LightYellow => [0xFF, 0xFF, 0x55],
        Color::LightBlue => [0x55, 0x55, 0xFF],
        Color::LightMagenta => [0xFF, 0x55, 0xFF],
        Color::LightCyan => [0x55, 0xFF, 0xFF],
        Color::White => [0xFF, 0xFF, 0xFF],
        Color::Rgb(r, g, b) => [r, g, b],
        Color::Indexed(n) => indexed_color(n),
    }
}

fn indexed_color(n: u8) -> [u8; 3] {
    match n {
        0..=15 => ansi_color(n),
        16..=231 => {
            let n = n - 16;
            let b = n % 6;
            let g = (n / 6) % 6;
            let r = n / 36;
            let scale = |v: u8| if v == 0 { 0u8 } else { v * 40 + 55 };
            [scale(r), scale(g), scale(b)]
        }
        232..=255 => {
            let gray = 8u8.saturating_add((n - 232).saturating_mul(10));
            [gray, gray, gray]
        }
    }
}

fn ansi_color(n: u8) -> [u8; 3] {
    const ANSI: [[u8; 3]; 16] = [
        [0x00, 0x00, 0x00], // 0  Black
        [0xAA, 0x00, 0x00], // 1  Red
        [0x00, 0xAA, 0x00], // 2  Green
        [0xAA, 0x55, 0x00], // 3  Yellow (brown)
        [0x00, 0x00, 0xAA], // 4  Blue
        [0xAA, 0x00, 0xAA], // 5  Magenta
        [0x00, 0xAA, 0xAA], // 6  Cyan
        [0xAA, 0xAA, 0xAA], // 7  Light Gray
        [0x55, 0x55, 0x55], // 8  Dark Gray
        [0xFF, 0x55, 0x55], // 9  Bright Red
        [0x55, 0xFF, 0x55], // 10 Bright Green
        [0xFF, 0xFF, 0x55], // 11 Bright Yellow
        [0x55, 0x55, 0xFF], // 12 Bright Blue
        [0xFF, 0x55, 0xFF], // 13 Bright Magenta
        [0x55, 0xFF, 0xFF], // 14 Bright Cyan
        [0xFF, 0xFF, 0xFF], // 15 White
    ];
    ANSI[n as usize]
}
