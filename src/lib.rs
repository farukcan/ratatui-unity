mod color;
mod commands;
mod font;
mod renderer;
mod terminal;

use crate::commands::{do_split, render_all_commands, WidgetCommand};
use crate::terminal::TerminalState;
use ratatui::style::{Color, Modifier, Style};
use std::ffi::{c_void, CStr};
use std::os::raw::c_char;

// ─── Helpers ─────────────────────────────────────────────────────────────────

/// Cast an opaque handle back to `TerminalState`. Panics on null.
unsafe fn state_mut<'a>(handle: *mut c_void) -> &'a mut TerminalState {
    &mut *(handle as *mut TerminalState)
}

unsafe fn cstr_to_string(ptr: *const c_char) -> String {
    if ptr.is_null() {
        return String::new();
    }
    CStr::from_ptr(ptr).to_string_lossy().into_owned()
}

// ─── Lifecycle ───────────────────────────────────────────────────────────────

/// Create a terminal instance and return an opaque handle.
/// `cols` and `rows` are cell dimensions; `font_size` is in pixels.
#[no_mangle]
pub extern "C" fn ratatui_create(cols: u16, rows: u16, font_size: f32) -> *mut c_void {
    let state = Box::new(TerminalState::new(cols, rows, font_size));
    Box::into_raw(state) as *mut c_void
}

/// Destroy a terminal handle created by `ratatui_create`.
#[no_mangle]
pub extern "C" fn ratatui_destroy(handle: *mut c_void) {
    if !handle.is_null() {
        unsafe {
            drop(Box::from_raw(handle as *mut TerminalState));
        }
    }
}

/// Replace the embedded font with custom TTF bytes. Returns 1 on success, 0 on error.
#[no_mangle]
pub extern "C" fn ratatui_set_custom_font(
    handle: *mut c_void,
    font_data: *const u8,
    font_len: u32,
) -> u8 {
    if handle.is_null() || font_data.is_null() || font_len == 0 {
        return 0;
    }
    let state = unsafe { state_mut(handle) };
    let bytes = unsafe { std::slice::from_raw_parts(font_data, font_len as usize) };
    u8::from(state.font.set_custom_font(bytes))
}

// ─── Frame ───────────────────────────────────────────────────────────────────

/// Start a new frame: clears all queued commands and resets the area map to root.
#[no_mangle]
pub extern "C" fn ratatui_begin_frame(handle: *mut c_void) {
    if handle.is_null() {
        return;
    }
    let state = unsafe { state_mut(handle) };
    state.begin_frame();
}

/// Render all queued commands, rasterize to a pixel buffer, and return a pointer
/// to the RGBA32 data. Valid until the next call on this handle.
/// Use `ratatui_pixel_width` / `ratatui_pixel_height` for buffer dimensions.
#[no_mangle]
pub extern "C" fn ratatui_end_frame(handle: *mut c_void) -> *const u8 {
    if handle.is_null() {
        return std::ptr::null();
    }
    let state = unsafe { state_mut(handle) };
    render_all_commands(state);
    let buffer = state.terminal.backend().buffer().clone();
    state.pixel_buffer = renderer::render_buffer_to_pixels(&buffer, &mut state.font);
    state.pixel_buffer.as_ptr()
}

/// Width of the pixel buffer in pixels (cols × cell_width).
#[no_mangle]
pub extern "C" fn ratatui_pixel_width(handle: *const c_void) -> u32 {
    if handle.is_null() {
        return 0;
    }
    let state = unsafe { &*(handle as *const TerminalState) };
    state.pixel_width
}

/// Height of the pixel buffer in pixels (rows × cell_height).
#[no_mangle]
pub extern "C" fn ratatui_pixel_height(handle: *const c_void) -> u32 {
    if handle.is_null() {
        return 0;
    }
    let state = unsafe { &*(handle as *const TerminalState) };
    state.pixel_height
}

// ─── Layout ──────────────────────────────────────────────────────────────────

/// Root area ID (always 0 for the full terminal rect).
#[no_mangle]
pub extern "C" fn ratatui_root_area(_handle: *const c_void) -> u32 {
    0
}

/// Split `area_id` and write the resulting child IDs into `out_ids`.
/// `out_ids` must point to a caller-allocated buffer of at least `count` u32 values.
/// Returns the number of child areas produced (≤ count).
///
/// `constraint_types`: array of bytes (0=Length, 1=Min, 2=Max, 3=Percentage, 4=Fill)
/// `constraint_values`: array of u16 values corresponding to each type
#[no_mangle]
pub extern "C" fn ratatui_split(
    handle: *mut c_void,
    area_id: u32,
    direction: u8,
    constraint_types: *const u8,
    constraint_values: *const u16,
    count: u32,
    out_ids: *mut u32,
) -> u32 {
    if handle.is_null()
        || constraint_types.is_null()
        || constraint_values.is_null()
        || out_ids.is_null()
        || count == 0
    {
        return 0;
    }
    let state = unsafe { state_mut(handle) };
    let types = unsafe { std::slice::from_raw_parts(constraint_types, count as usize) };
    let values = unsafe { std::slice::from_raw_parts(constraint_values, count as usize) };
    let out = unsafe { std::slice::from_raw_parts_mut(out_ids, count as usize) };
    do_split(state, area_id, direction, types, values, out)
}

// ─── Style ───────────────────────────────────────────────────────────────────

/// Set a pending style that will be applied to the next widget command.
/// Pass 255 for fg/bg components to use the terminal defaults.
/// `modifiers`: bit flags — 0x01=Bold, 0x02=Italic, 0x04=Underlined, 0x08=Dim
#[no_mangle]
pub extern "C" fn ratatui_set_style(
    handle: *mut c_void,
    fg_r: u8,
    fg_g: u8,
    fg_b: u8,
    use_default_fg: u8,
    bg_r: u8,
    bg_g: u8,
    bg_b: u8,
    use_default_bg: u8,
    modifiers: u8,
) {
    if handle.is_null() {
        return;
    }
    let state = unsafe { state_mut(handle) };
    let mut style = Style::default();

    if use_default_fg == 0 {
        style = style.fg(Color::Rgb(fg_r, fg_g, fg_b));
    }
    if use_default_bg == 0 {
        style = style.bg(Color::Rgb(bg_r, bg_g, bg_b));
    }
    let mut modifier = Modifier::empty();
    if modifiers & 0x01 != 0 {
        modifier |= Modifier::BOLD;
    }
    if modifiers & 0x02 != 0 {
        modifier |= Modifier::ITALIC;
    }
    if modifiers & 0x04 != 0 {
        modifier |= Modifier::UNDERLINED;
    }
    if modifiers & 0x08 != 0 {
        modifier |= Modifier::DIM;
    }
    if !modifier.is_empty() {
        style = style.add_modifier(modifier);
    }
    state.pending_style = style;
}

// ─── Widgets ─────────────────────────────────────────────────────────────────

/// Render a `Block` widget (border + optional title) into `area_id`.
/// `borders`: bitmask — 0x01=Top, 0x02=Bottom, 0x04=Left, 0x08=Right, 0x0F=All
#[no_mangle]
pub extern "C" fn ratatui_block(
    handle: *mut c_void,
    area_id: u32,
    title: *const c_char,
    borders: u8,
) {
    if handle.is_null() {
        return;
    }
    let state = unsafe { state_mut(handle) };
    let style = state.take_style();
    state.commands.push(WidgetCommand::Block {
        area_id,
        title: unsafe { cstr_to_string(title) },
        borders,
        style,
    });
}

/// Render a `Paragraph` widget.
/// `alignment`: 0=Left, 1=Center, 2=Right
#[no_mangle]
pub extern "C" fn ratatui_paragraph(
    handle: *mut c_void,
    area_id: u32,
    text: *const c_char,
    alignment: u8,
    wrap: u8,
) {
    if handle.is_null() {
        return;
    }
    let state = unsafe { state_mut(handle) };
    let style = state.take_style();
    state.commands.push(WidgetCommand::Paragraph {
        area_id,
        text: unsafe { cstr_to_string(text) },
        alignment,
        wrap: wrap != 0,
        style,
    });
}

/// Render a `List` widget.
/// `items`: newline-separated list entries.
/// `selected`: index of the highlighted item, or -1 for none.
#[no_mangle]
pub extern "C" fn ratatui_list(
    handle: *mut c_void,
    area_id: u32,
    items: *const c_char,
    selected: i32,
) {
    if handle.is_null() {
        return;
    }
    let state = unsafe { state_mut(handle) };
    let style = state.take_style();
    state.commands.push(WidgetCommand::List {
        area_id,
        items: unsafe { cstr_to_string(items) },
        selected,
        style,
    });
}

/// Render a `Gauge` widget.
/// `ratio`: progress fraction in [0.0, 1.0].
/// `label`: text shown inside the bar (may be null for percentage).
#[no_mangle]
pub extern "C" fn ratatui_gauge(
    handle: *mut c_void,
    area_id: u32,
    ratio: f32,
    label: *const c_char,
) {
    if handle.is_null() {
        return;
    }
    let state = unsafe { state_mut(handle) };
    let style = state.take_style();
    state.commands.push(WidgetCommand::Gauge {
        area_id,
        ratio: ratio as f64,
        label: unsafe { cstr_to_string(label) },
        style,
    });
}

/// Render a `Tabs` widget.
/// `titles`: newline-separated tab titles.
/// `selected`: index of the active tab.
#[no_mangle]
pub extern "C" fn ratatui_tabs(
    handle: *mut c_void,
    area_id: u32,
    titles: *const c_char,
    selected: u32,
) {
    if handle.is_null() {
        return;
    }
    let state = unsafe { state_mut(handle) };
    let style = state.take_style();
    state.commands.push(WidgetCommand::Tabs {
        area_id,
        titles: unsafe { cstr_to_string(titles) },
        selected,
        style,
    });
}

/// Render a `Sparkline` widget.
/// `data`: pointer to an array of `len` u64 values.
#[no_mangle]
pub extern "C" fn ratatui_sparkline(
    handle: *mut c_void,
    area_id: u32,
    data: *const u64,
    len: u32,
) {
    if handle.is_null() || data.is_null() {
        return;
    }
    let state = unsafe { state_mut(handle) };
    let style = state.take_style();
    let data_vec = unsafe { std::slice::from_raw_parts(data, len as usize) }.to_vec();
    state.commands.push(WidgetCommand::Sparkline {
        area_id,
        data: data_vec,
        style,
    });
}

/// Render a `Table` widget.
/// `data`: first line = tab-separated headers; subsequent lines = tab-separated rows.
#[no_mangle]
pub extern "C" fn ratatui_table(
    handle: *mut c_void,
    area_id: u32,
    data: *const c_char,
) {
    if handle.is_null() {
        return;
    }
    let state = unsafe { state_mut(handle) };
    let style = state.take_style();
    state.commands.push(WidgetCommand::Table {
        area_id,
        data: unsafe { cstr_to_string(data) },
        style,
    });
}

/// Returns the library version string as a static C string (no allocation needed).
#[no_mangle]
pub extern "C" fn ratatui_version() -> *const c_char {
    concat!(env!("CARGO_PKG_VERSION"), "\0").as_ptr() as *const c_char
}
