//! Pending-style and background-color state setters.

use crate::ffi::util::{state_mut, style_from_rgba};
use std::ffi::c_void;

/// Sets the RGB background color used by the rasterizer for cells whose
/// background is [`Color::Reset`](ratatui::style::Color::Reset).
///
/// The value persists across frames until changed again. Setting this between
/// frames is supported; setting it mid-frame only affects subsequent calls
/// to `ratatui_end_frame*`.
#[no_mangle]
pub extern "C" fn ratatui_set_background_color(
    handle: *mut c_void,
    r: u8, g: u8, b: u8,
) {
    let Some(state) = state_mut(handle) else { return; };
    state.background_color = [r, g, b];
}

/// Sets the pending style consumed by the next widget-producing FFI call.
///
/// The pending style is reset to default after each widget call and at the
/// start of every frame. Widgets that do not accept a style (e.g. scrollbar,
/// calendar, chart, canvas) ignore the pending style.
///
/// # Parameters
/// - `fg_r`, `fg_g`, `fg_b`: foreground RGB components.
/// - `use_default_fg`: non-zero to leave the foreground unset (terminal
///   default); zero to apply the given RGB triple.
/// - `bg_r`, `bg_g`, `bg_b`: background RGB components.
/// - `use_default_bg`: non-zero to leave the background unset; zero to apply
///   the given RGB triple.
/// - `modifiers`: bit field — `0x01` Bold, `0x02` Italic, `0x04` Underlined,
///   `0x08` Dim.
#[no_mangle]
pub extern "C" fn ratatui_set_style(
    handle: *mut c_void,
    fg_r: u8, fg_g: u8, fg_b: u8, use_default_fg: u8,
    bg_r: u8, bg_g: u8, bg_b: u8, use_default_bg: u8,
    modifiers: u8,
) {
    let Some(state) = state_mut(handle) else { return; };
    state.pending_style = style_from_rgba(
        fg_r, fg_g, fg_b, use_default_fg,
        bg_r, bg_g, bg_b, use_default_bg,
        modifiers,
    );
}
