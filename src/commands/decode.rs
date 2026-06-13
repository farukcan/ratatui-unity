//! Decoders mapping packed FFI codes to ratatui enums.

use ratatui::layout::Alignment;
use ratatui::symbols;
use ratatui::widgets::{Borders, ScrollbarOrientation};

/// Decodes a packed border bit field into [`Borders`].
///
/// Bits: `0x01` Top, `0x02` Bottom, `0x04` Left, `0x08` Right. The value
/// `0x0F` is treated as `Borders::ALL`.
pub(crate) fn borders_from_u8(b: u8) -> Borders {
    if b == 0x0F {
        return Borders::ALL;
    }
    let mut borders = Borders::NONE;
    if b & 0x01 != 0 { borders |= Borders::TOP; }
    if b & 0x02 != 0 { borders |= Borders::BOTTOM; }
    if b & 0x04 != 0 { borders |= Borders::LEFT; }
    if b & 0x08 != 0 { borders |= Borders::RIGHT; }
    borders
}

/// Maps the FFI alignment code (`0` Left, `1` Center, `2` Right) to
/// [`Alignment`]. Unknown values default to `Left`.
pub(crate) fn alignment_from_u8(a: u8) -> Alignment {
    match a {
        1 => Alignment::Center,
        2 => Alignment::Right,
        _ => Alignment::Left,
    }
}

/// Maps the FFI marker code to [`symbols::Marker`].
///
/// `0` Dot, `1` Braille, `2` HalfBlock, `3` Block. Unknown values default to
/// `Dot`.
pub(crate) fn marker_from_u8(m: u8) -> symbols::Marker {
    match m {
        1 => symbols::Marker::Braille,
        2 => symbols::Marker::HalfBlock,
        3 => symbols::Marker::Block,
        _ => symbols::Marker::Dot,
    }
}

/// Maps the FFI scrollbar orientation code to [`ScrollbarOrientation`].
///
/// `0` VerticalRight, `1` VerticalLeft, `2` HorizontalBottom,
/// `3` HorizontalTop. Unknown values default to `VerticalRight`.
pub(crate) fn scrollbar_orientation_from_u8(o: u8) -> ScrollbarOrientation {
    match o {
        1 => ScrollbarOrientation::VerticalLeft,
        2 => ScrollbarOrientation::HorizontalBottom,
        3 => ScrollbarOrientation::HorizontalTop,
        _ => ScrollbarOrientation::VerticalRight,
    }
}
