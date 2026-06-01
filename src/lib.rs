//! # ratatui_unity
//!
//! A C ABI wrapper around [`ratatui`] that renders terminal UIs to RGB24
//! pixel buffers, suitable for embedding in game engines (e.g. Unity).
//!
//! The crate is compiled as both `cdylib` and `staticlib` so that it can be
//! consumed from any host capable of calling C functions. All public entry
//! points are `extern "C"` and `#[no_mangle]`; there is no idiomatic Rust API.
//!
//! ## High-level flow
//!
//! 1. Create a terminal handle with [`ratatui_create`].
//! 2. For each frame:
//!    - Call [`ratatui_begin_frame`] to reset per-frame state.
//!    - Build a layout tree with [`ratatui_split`] / [`ratatui_inner`].
//!    - Optionally set a style with [`ratatui_set_style`] before any widget call.
//!    - Queue widget commands (e.g. [`ratatui_block`], [`ratatui_paragraph`],
//!      [`ratatui_chart_begin`] / [`ratatui_chart_end`], …).
//!    - Call [`ratatui_end_frame`] (or [`ratatui_end_frame_hashed`]) to draw
//!      the queue and rasterize the cell grid into an RGB24 pixel buffer.
//! 3. When done, call [`ratatui_destroy`] to release the handle.
//!
//! ## Memory & lifetime
//!
//! The handle returned by [`ratatui_create`] is an opaque pointer to a
//! heap-allocated `TerminalState`. The pixel
//! buffer pointer returned by `ratatui_end_frame*` is owned by the handle and
//! is only valid until the next FFI call that mutates the handle. The caller
//! must copy the bytes before issuing further calls if it wants to retain them.
//!
//! ## Safety
//!
//! All FFI entry points perform null-pointer checks on the handle and on any
//! pointer argument they dereference. Strings are read as null-terminated
//! `*const c_char`. Slices passed as `(ptr, len)` pairs must reference valid
//! memory for the duration of the call.

mod color;
mod commands;
mod font;
mod renderer;
mod terminal;

use crate::commands::{do_split, render_all_commands};
use crate::terminal::{
    AxisInfo, CanvasShape, DatasetInfo, PendingCanvas, PendingChart, PendingStyledParagraph,
    SpanInfo, TerminalState, WidgetCommand,
};
use ratatui::style::{Color, Modifier, Style};
use std::ffi::{c_void, CStr};
use std::os::raw::c_char;

// ─── Helpers ─────────────────────────────────────────────────────────────────

/// Reinterprets an opaque handle as a mutable reference to [`TerminalState`].
///
/// # Safety
///
/// `handle` must be a non-null pointer previously returned by
/// [`ratatui_create`] and not yet passed to [`ratatui_destroy`]. The borrow's
/// lifetime is unbounded; callers must ensure no other reference to the same
/// state is active for the duration of the returned borrow.
unsafe fn state_mut<'a>(handle: *mut c_void) -> &'a mut TerminalState {
    &mut *(handle as *mut TerminalState)
}

/// Copies a nullable null-terminated C string into an owned [`String`].
///
/// Returns an empty `String` when `ptr` is null. Invalid UTF-8 is replaced
/// using [`String::from_utf8_lossy`].
///
/// # Safety
///
/// `ptr`, if non-null, must point to a valid null-terminated byte sequence.
unsafe fn cstr_to_string(ptr: *const c_char) -> String {
    if ptr.is_null() {
        return String::new();
    }
    CStr::from_ptr(ptr).to_string_lossy().into_owned()
}

/// Builds a ratatui [`Style`] from the packed FFI representation.
///
/// `use_default_fg` / `use_default_bg`: non-zero means "leave foreground /
/// background unset so the terminal default is used"; zero means apply the
/// given RGB triple.
///
/// `modifiers` is a bit field:
/// - `0x01` Bold
/// - `0x02` Italic
/// - `0x04` Underlined
/// - `0x08` Dim
fn style_from_rgba(
    fg_r: u8, fg_g: u8, fg_b: u8, use_default_fg: u8,
    bg_r: u8, bg_g: u8, bg_b: u8, use_default_bg: u8,
    modifiers: u8,
) -> Style {
    let mut style = Style::default();
    if use_default_fg == 0 { style = style.fg(Color::Rgb(fg_r, fg_g, fg_b)); }
    if use_default_bg == 0 { style = style.bg(Color::Rgb(bg_r, bg_g, bg_b)); }
    let mut modifier = Modifier::empty();
    if modifiers & 0x01 != 0 { modifier |= Modifier::BOLD; }
    if modifiers & 0x02 != 0 { modifier |= Modifier::ITALIC; }
    if modifiers & 0x04 != 0 { modifier |= Modifier::UNDERLINED; }
    if modifiers & 0x08 != 0 { modifier |= Modifier::DIM; }
    if !modifier.is_empty() { style = style.add_modifier(modifier); }
    style
}

// ─── Background color ─────────────────────────────────────────────────────────

/// Sets the RGB background color used by the rasterizer for cells whose
/// background is [`Color::Reset`].
///
/// The value persists across frames until changed again. Setting this between
/// frames is supported; setting it mid-frame only affects subsequent calls
/// to `ratatui_end_frame*`.
#[no_mangle]
pub extern "C" fn ratatui_set_background_color(
    handle: *mut c_void,
    r: u8, g: u8, b: u8,
) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    state.background_color = [r, g, b];
}

// ─── Lifecycle ───────────────────────────────────────────────────────────────

/// Creates a terminal instance and returns an opaque handle.
///
/// The resulting handle owns:
/// - a ratatui [`Terminal`](ratatui::Terminal) backed by [`TestBackend`](ratatui::backend::TestBackend)
///   sized `cols × rows` cells,
/// - a glyph-cached `FontManager` at `font_size` pixels,
/// - a pre-allocated RGB24 pixel buffer matching `cols × rows × cell_size`.
///
/// The handle must eventually be released with [`ratatui_destroy`].
///
/// # Parameters
/// - `cols`: terminal width in character cells.
/// - `rows`: terminal height in character cells.
/// - `font_size`: glyph rasterization size in pixels (e.g. `14.0`).
#[no_mangle]
pub extern "C" fn ratatui_create(cols: u16, rows: u16, font_size: f32) -> *mut c_void {
    let state = Box::new(TerminalState::new(cols, rows, font_size));
    Box::into_raw(state) as *mut c_void
}

/// Destroys a terminal handle created by [`ratatui_create`].
///
/// After this call the handle and any previously returned pixel-buffer
/// pointers are invalid and must not be used. A null handle is a no-op.
#[no_mangle]
pub extern "C" fn ratatui_destroy(handle: *mut c_void) {
    if !handle.is_null() {
        unsafe { drop(Box::from_raw(handle as *mut TerminalState)); }
    }
}

/// Replaces the embedded JetBrains Mono font with custom TTF bytes.
///
/// The cell width/height are recomputed from the new font's metrics, and the
/// glyph cache is dropped. The pixel buffer is not resized here; callers that
/// rely on a specific pixel size should re-create the handle if the new font
/// changes cell dimensions.
///
/// # Returns
/// `1` on success, `0` if `handle` is null, `font_data` is null/empty, or the
/// bytes are not a valid font.
#[no_mangle]
pub extern "C" fn ratatui_set_custom_font(
    handle: *mut c_void,
    font_data: *const u8,
    font_len: u32,
) -> u8 {
    if handle.is_null() || font_data.is_null() || font_len == 0 { return 0; }
    let state = unsafe { state_mut(handle) };
    let bytes = unsafe { std::slice::from_raw_parts(font_data, font_len as usize) };
    u8::from(state.font.set_custom_font(bytes))
}

// ─── Frame ───────────────────────────────────────────────────────────────────

/// Begins a new frame.
///
/// Clears the queued widget command list, drops any in-progress builder state
/// (styled paragraph, chart, canvas), resets the pending style to default, and
/// resets the area map so that only the root area (id `0`) remains. Must be
/// called before issuing widget commands for the new frame.
#[no_mangle]
pub extern "C" fn ratatui_begin_frame(handle: *mut c_void) {
    if handle.is_null() { return; }
    unsafe { state_mut(handle) }.begin_frame();
}

/// Renders all queued widget commands and rasterizes the cell buffer.
///
/// The returned pointer addresses a flat RGB24 buffer of size
/// `pixel_width * pixel_height * 3` bytes, owned by the handle. The pointer is
/// valid until the next FFI call that mutates the handle (typically the next
/// `ratatui_end_frame*` call), at which point the buffer may be overwritten.
///
/// Returns `null` only when `handle` is null.
#[no_mangle]
pub extern "C" fn ratatui_end_frame(handle: *mut c_void) -> *const u8 {
    if handle.is_null() { return std::ptr::null(); }
    let state = unsafe { state_mut(handle) };
    render_all_commands(state);
    state.rasterize();
    state.pixel_buffer.as_ptr()
}

/// Like `ratatui_end_frame`, but skips rasterization when the cell buffer
/// is unchanged from the previous frame (hash-based dirty check).
/// Returns a valid pixel pointer when content changed, or null when unchanged.
/// The previous frame's pixel buffer remains valid when null is returned.
#[no_mangle]
pub extern "C" fn ratatui_end_frame_hashed(handle: *mut c_void) -> *const u8 {
    if handle.is_null() { return std::ptr::null(); }
    let state = unsafe { state_mut(handle) };
    render_all_commands(state);

    let hash = {
        let buffer = state.terminal.backend().buffer();
        crate::renderer::compute_buffer_hash(buffer)
    };

    if state.last_buffer_hash == Some(hash) {
        return std::ptr::null();
    }

    state.last_buffer_hash = Some(hash);
    state.rasterize();
    state.pixel_buffer.as_ptr()
}

/// Returns the width of the pixel buffer in pixels (`cols * cell_width`).
///
/// Returns `0` if `handle` is null.
#[no_mangle]
pub extern "C" fn ratatui_pixel_width(handle: *const c_void) -> u32 {
    if handle.is_null() { return 0; }
    unsafe { &*(handle as *const TerminalState) }.pixel_width
}

/// Returns the height of the pixel buffer in pixels (`rows * cell_height`).
///
/// Returns `0` if `handle` is null.
#[no_mangle]
pub extern "C" fn ratatui_pixel_height(handle: *const c_void) -> u32 {
    if handle.is_null() { return 0; }
    unsafe { &*(handle as *const TerminalState) }.pixel_height
}

// ─── Layout ──────────────────────────────────────────────────────────────────

/// Returns the id of the root area, which always covers the whole terminal.
///
/// The root id is the constant `0`; this getter exists for symmetry with the
/// host-side area API.
#[no_mangle]
pub extern "C" fn ratatui_root_area(_handle: *const c_void) -> u32 { 0 }

/// Splits an existing area into `count` child areas and writes their ids into
/// `out_ids`.
///
/// # Parameters
/// - `area_id`: id of the parent area to split.
/// - `direction`: `0` = horizontal split (left → right), any other value =
///   vertical split (top → bottom).
/// - `constraint_types`: array of length `count` describing each child's
///   constraint kind. Values: `0` = Length, `1` = Min, `2` = Max,
///   `3` = Percentage, `4` (or any other) = Fill.
/// - `constraint_values`: array of length `count` with the numeric value
///   matching the constraint kind (cells for Length/Min/Max, 0..=100 for
///   Percentage, weight for Fill).
/// - `count`: number of child areas requested.
/// - `out_ids`: caller-allocated buffer of length `count` that receives the
///   ids of the newly registered child areas.
///
/// # Returns
/// The number of child areas actually written. Returns `0` if any required
/// pointer is null, `count` is zero, or the parent id is unknown.
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

/// Returns a new area id covering the inner rectangle of `area_id` shrunk by
/// the given margins on each side.
///
/// # Parameters
/// - `area_id`: parent area id.
/// - `horizontal`: cells to remove from the left and right edges.
/// - `vertical`: cells to remove from the top and bottom edges.
///
/// # Returns
/// The id of the newly registered inner area, or [`u32::MAX`] if `handle` is
/// null or `area_id` is unknown.
#[no_mangle]
pub extern "C" fn ratatui_inner(
    handle: *mut c_void,
    area_id: u32,
    horizontal: u16,
    vertical: u16,
) -> u32 {
    if handle.is_null() { return u32::MAX; }
    let state = unsafe { state_mut(handle) };
    let area = match state.area_map.get(&area_id).copied() {
        Some(r) => r,
        None => return u32::MAX,
    };
    use ratatui::layout::Margin;
    let inner = area.inner(Margin { horizontal, vertical });
    state.register_area(inner)
}

// ─── Style ───────────────────────────────────────────────────────────────────

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
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    state.pending_style = style_from_rgba(
        fg_r, fg_g, fg_b, use_default_fg,
        bg_r, bg_g, bg_b, use_default_bg,
        modifiers,
    );
}

// ─── Basic widgets ────────────────────────────────────────────────────────────

/// Queues a [`Block`](ratatui::widgets::Block) widget with an optional title
/// and per-edge borders.
///
/// `borders` is a bit field — `0x01` Top, `0x02` Bottom, `0x04` Left,
/// `0x08` Right. The value `0x0F` is treated as "all borders".
///
/// The pending style (see [`ratatui_set_style`]) is consumed and applied to
/// the block.
#[no_mangle]
pub extern "C" fn ratatui_block(
    handle: *mut c_void,
    area_id: u32,
    title: *const c_char,
    borders: u8,
) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    let style = state.take_style();
    state.commands.push(WidgetCommand::Block {
        area_id,
        title: unsafe { cstr_to_string(title) },
        borders,
        style,
    });
}

/// Queues a uniformly styled [`Paragraph`](ratatui::widgets::Paragraph).
///
/// # Parameters
/// - `text`: paragraph contents. Embedded `\n` produces line breaks.
/// - `alignment`: `0` Left, `1` Center, `2` Right.
/// - `wrap`: non-zero to enable word wrapping (`trim: false`).
///
/// For multi-style text use the styled-paragraph builder
/// ([`ratatui_styled_para_begin`] / [`ratatui_styled_para_span`] /
/// [`ratatui_styled_para_newline`] / [`ratatui_styled_para_end`]).
#[no_mangle]
pub extern "C" fn ratatui_paragraph(
    handle: *mut c_void,
    area_id: u32,
    text: *const c_char,
    alignment: u8,
    wrap: u8,
) {
    if handle.is_null() { return; }
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

/// Queues a [`List`](ratatui::widgets::List) widget.
///
/// # Parameters
/// - `items`: newline-separated list entries.
/// - `selected`: zero-based index of the highlighted row, or `-1` for no
///   selection. The highlight uses `"> "` as the prefix and a bold modifier.
#[no_mangle]
pub extern "C" fn ratatui_list(
    handle: *mut c_void,
    area_id: u32,
    items: *const c_char,
    selected: i32,
) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    let style = state.take_style();
    state.commands.push(WidgetCommand::List {
        area_id,
        items: unsafe { cstr_to_string(items) },
        selected,
        style,
    });
}

/// Queues a block-style [`Gauge`](ratatui::widgets::Gauge).
///
/// # Parameters
/// - `ratio`: progress in `[0.0, 1.0]`. Values outside the range are clamped.
/// - `label`: optional text overlaid on the gauge (pass `null` or empty for none).
#[no_mangle]
pub extern "C" fn ratatui_gauge(
    handle: *mut c_void,
    area_id: u32,
    ratio: f32,
    label: *const c_char,
) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    let style = state.take_style();
    state.commands.push(WidgetCommand::Gauge {
        area_id,
        ratio: ratio as f64,
        label: unsafe { cstr_to_string(label) },
        style,
    });
}

/// Queues a [`Tabs`](ratatui::widgets::Tabs) bar.
///
/// # Parameters
/// - `titles`: newline-separated tab labels.
/// - `selected`: zero-based index of the active tab.
///
/// The pending style's foreground color (or cyan if unset) is used as the
/// highlight background of the active tab.
#[no_mangle]
pub extern "C" fn ratatui_tabs(
    handle: *mut c_void,
    area_id: u32,
    titles: *const c_char,
    selected: u32,
) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    let style = state.take_style();
    state.commands.push(WidgetCommand::Tabs {
        area_id,
        titles: unsafe { cstr_to_string(titles) },
        selected,
        style,
    });
}

/// Queues a [`Sparkline`](ratatui::widgets::Sparkline) from raw `u64` samples.
///
/// # Parameters
/// - `data`: pointer to `len` `u64` samples.
/// - `len`: number of samples.
#[no_mangle]
pub extern "C" fn ratatui_sparkline(
    handle: *mut c_void,
    area_id: u32,
    data: *const u64,
    len: u32,
) {
    if handle.is_null() || data.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    let style = state.take_style();
    let data_vec = unsafe { std::slice::from_raw_parts(data, len as usize) }.to_vec();
    state.commands.push(WidgetCommand::Sparkline { area_id, data: data_vec, style });
}

/// Queues a [`Table`](ratatui::widgets::Table) with equal-width columns.
///
/// `data` format:
/// - First line: tab-separated header cells.
/// - Subsequent lines: one row per line; cells separated by tabs.
///
/// For typed column widths and row selection use [`ratatui_table_ex`].
#[no_mangle]
pub extern "C" fn ratatui_table(
    handle: *mut c_void,
    area_id: u32,
    data: *const c_char,
) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    let style = state.take_style();
    state.commands.push(WidgetCommand::Table {
        area_id,
        data: unsafe { cstr_to_string(data) },
        style,
    });
}

// ─── New widgets: BarChart, LineGauge, Scrollbar, Calendar, TableEx ──────────

/// Queues a [`BarChart`](ratatui::widgets::BarChart).
///
/// `data` format: one bar per line, label and value separated by a tab.
/// Malformed lines (missing tab or non-numeric value) are silently skipped.
///
/// # Parameters
/// - `bar_width`: width of each bar in cells.
/// - `bar_gap`: gap between bars in cells.
#[no_mangle]
pub extern "C" fn ratatui_barchart(
    handle: *mut c_void,
    area_id: u32,
    data: *const c_char,
    bar_width: u16,
    bar_gap: u16,
) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    let style = state.take_style();
    let data_str = unsafe { cstr_to_string(data) };
    let bars: Vec<(String, u64)> = data_str
        .lines()
        .filter_map(|line| {
            let mut parts = line.splitn(2, '\t');
            let label = parts.next()?.to_string();
            let value: u64 = parts.next()?.trim().parse().ok()?;
            Some((label, value))
        })
        .collect();
    state.commands.push(WidgetCommand::BarChart { area_id, bars, bar_width, bar_gap, style });
}

/// Queues a horizontal single-line [`LineGauge`](ratatui::widgets::LineGauge).
///
/// # Parameters
/// - `ratio`: progress in `[0.0, 1.0]`; values outside the range are clamped.
/// - `label`: text shown next to the gauge (pass `null` or empty for none).
#[no_mangle]
pub extern "C" fn ratatui_line_gauge(
    handle: *mut c_void,
    area_id: u32,
    ratio: f32,
    label: *const c_char,
) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    let style = state.take_style();
    state.commands.push(WidgetCommand::LineGauge {
        area_id,
        ratio: ratio as f64,
        label: unsafe { cstr_to_string(label) },
        style,
    });
}

/// Queues a [`Scrollbar`](ratatui::widgets::Scrollbar).
///
/// # Parameters
/// - `content_length`: total scrollable length in cells.
/// - `position`: current scroll offset in cells (`0..=content_length`).
/// - `viewport_length`: visible portion of the content in cells.
/// - `orientation`: `0` VerticalRight, `1` VerticalLeft, `2` HorizontalBottom,
///   `3` HorizontalTop.
#[no_mangle]
pub extern "C" fn ratatui_scrollbar(
    handle: *mut c_void,
    area_id: u32,
    content_length: u32,
    position: u32,
    viewport_length: u32,
    orientation: u8,
) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    state.commands.push(WidgetCommand::Scrollbar {
        area_id,
        content_length,
        position,
        viewport_length,
        orientation,
    });
}

/// Queues a monthly calendar
/// ([`Monthly`](ratatui::widgets::calendar::Monthly)).
///
/// Invalid dates fall back to January 1 of `year`, and if that also fails,
/// to 2024-01-01. The `widget-calendar` Cargo feature must be enabled
/// (it is, by default, in this crate).
///
/// # Parameters
/// - `year`: full year (e.g. `2026`).
/// - `month`: `1..=12`.
/// - `day`: `1..=28` (later days are clamped to `28` to avoid month overflow).
#[no_mangle]
pub extern "C" fn ratatui_calendar(
    handle: *mut c_void,
    area_id: u32,
    year: i32,
    month: u8,
    day: u8,
) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    state.commands.push(WidgetCommand::Calendar { area_id, year, month, day });
}

/// Queues an extended [`Table`](ratatui::widgets::Table) with typed column
/// widths and optional row highlighting.
///
/// `data` follows the same format as [`ratatui_table`] (first line headers,
/// subsequent lines rows; tab-separated cells).
///
/// # Parameters
/// - `col_types` / `col_values`: parallel arrays of length `col_count`
///   describing each column's constraint kind and value. Same encoding as
///   [`ratatui_split`]. Pass `null` (or `col_count == 0`) for equal-width
///   distribution.
/// - `selected_row`: zero-based index of the highlighted row, or `-1` for
///   no selection. The highlight uses a bold modifier.
#[no_mangle]
pub extern "C" fn ratatui_table_ex(
    handle: *mut c_void,
    area_id: u32,
    data: *const c_char,
    col_types: *const u8,
    col_values: *const u16,
    col_count: u32,
    selected_row: i32,
) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    let style = state.take_style();
    let col_constraints: Vec<(u8, u16)> =
        if col_types.is_null() || col_values.is_null() || col_count == 0 {
            Vec::new()
        } else {
            let types = unsafe { std::slice::from_raw_parts(col_types, col_count as usize) };
            let values = unsafe { std::slice::from_raw_parts(col_values, col_count as usize) };
            types.iter().zip(values.iter()).map(|(&t, &v)| (t, v)).collect()
        };
    state.commands.push(WidgetCommand::TableEx {
        area_id,
        data: unsafe { cstr_to_string(data) },
        col_constraints,
        selected_row,
        style,
    });
}

// ─── StyledParagraph builder ─────────────────────────────────────────────────

/// Starts a multi-style paragraph builder.
///
/// Builder lifecycle:
/// 1. [`ratatui_styled_para_begin`] — open the builder for `area_id`.
/// 2. Zero or more [`ratatui_styled_para_span`] calls — append styled spans
///    to the current line.
/// 3. Zero or more [`ratatui_styled_para_newline`] calls — start a new line.
/// 4. [`ratatui_styled_para_end`] — flush the builder into the command queue.
///
/// Only one styled-paragraph builder may be active at a time per handle.
/// Beginning a new one before `_end` discards the previous one.
///
/// # Parameters
/// - `alignment`: `0` Left, `1` Center, `2` Right.
/// - `wrap`: non-zero to enable word wrapping (`trim: false`).
#[no_mangle]
pub extern "C" fn ratatui_styled_para_begin(
    handle: *mut c_void,
    area_id: u32,
    alignment: u8,
    wrap: u8,
) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    state.pending_styled_para = Some(PendingStyledParagraph {
        area_id,
        alignment,
        wrap: wrap != 0,
        lines: vec![vec![]],
    });
}

/// Appends a styled [`Span`](ratatui::text::Span) to the current line of the
/// pending styled paragraph.
///
/// Does nothing if no builder is active. Style parameters follow the same
/// encoding as [`ratatui_set_style`].
#[no_mangle]
pub extern "C" fn ratatui_styled_para_span(
    handle: *mut c_void,
    text: *const c_char,
    fg_r: u8, fg_g: u8, fg_b: u8, use_default_fg: u8,
    bg_r: u8, bg_g: u8, bg_b: u8, use_default_bg: u8,
    modifiers: u8,
) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    if let Some(ref mut pending) = state.pending_styled_para {
        let style = style_from_rgba(
            fg_r, fg_g, fg_b, use_default_fg,
            bg_r, bg_g, bg_b, use_default_bg,
            modifiers,
        );
        let span = SpanInfo { text: unsafe { cstr_to_string(text) }, style };
        if let Some(last_line) = pending.lines.last_mut() {
            last_line.push(span);
        }
    }
}

/// Starts a new line in the pending styled paragraph.
///
/// Does nothing if no builder is active.
#[no_mangle]
pub extern "C" fn ratatui_styled_para_newline(handle: *mut c_void) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    if let Some(ref mut pending) = state.pending_styled_para {
        pending.lines.push(vec![]);
    }
}

/// Finalizes the pending styled paragraph and queues it for rendering.
///
/// Does nothing if no builder is active.
#[no_mangle]
pub extern "C" fn ratatui_styled_para_end(handle: *mut c_void) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    if let Some(pending) = state.pending_styled_para.take() {
        state.commands.push(WidgetCommand::StyledParagraph {
            area_id: pending.area_id,
            alignment: pending.alignment,
            wrap: pending.wrap,
            lines: pending.lines,
        });
    }
}

// ─── Chart builder ────────────────────────────────────────────────────────────

/// Starts a [`Chart`](ratatui::widgets::Chart) builder.
///
/// Builder lifecycle:
/// 1. [`ratatui_chart_begin`] — open the builder for `area_id`.
/// 2. Optionally [`ratatui_chart_x_axis`] and/or [`ratatui_chart_y_axis`] —
///    set axis titles and bounds.
/// 3. Zero or more [`ratatui_chart_dataset`] calls — add datasets.
/// 4. [`ratatui_chart_end`] — flush the builder into the command queue.
///
/// Only one chart builder may be active at a time per handle.
#[no_mangle]
pub extern "C" fn ratatui_chart_begin(handle: *mut c_void, area_id: u32) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    state.pending_chart = Some(PendingChart {
        area_id,
        x_axis: None,
        y_axis: None,
        datasets: Vec::new(),
    });
}

/// Sets the X axis title and `[min, max]` data bounds of the pending chart.
///
/// Does nothing if no chart builder is active.
#[no_mangle]
pub extern "C" fn ratatui_chart_x_axis(
    handle: *mut c_void,
    title: *const c_char,
    min: f64,
    max: f64,
) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    if let Some(ref mut pending) = state.pending_chart {
        pending.x_axis = Some(AxisInfo { title: unsafe { cstr_to_string(title) }, min, max });
    }
}

/// Sets the Y axis title and `[min, max]` data bounds of the pending chart.
///
/// Does nothing if no chart builder is active.
#[no_mangle]
pub extern "C" fn ratatui_chart_y_axis(
    handle: *mut c_void,
    title: *const c_char,
    min: f64,
    max: f64,
) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    if let Some(ref mut pending) = state.pending_chart {
        pending.y_axis = Some(AxisInfo { title: unsafe { cstr_to_string(title) }, min, max });
    }
}

/// Adds a [`Dataset`](ratatui::widgets::Dataset) to the pending chart.
///
/// # Parameters
/// - `name`: dataset legend label.
/// - `marker`: `0` Dot, `1` Braille, `2` HalfBlock, `3` Block.
/// - `r`, `g`, `b`: dataset color.
/// - `data`: pointer to `point_count * 2` `f64` values, interleaved as
///   `[x0, y0, x1, y1, …]`.
/// - `point_count`: number of `(x, y)` pairs.
///
/// Does nothing if no chart builder is active or `data` is null.
#[no_mangle]
pub extern "C" fn ratatui_chart_dataset(
    handle: *mut c_void,
    name: *const c_char,
    marker: u8,
    r: u8, g: u8, b: u8,
    data: *const f64,
    point_count: u32,
) {
    if handle.is_null() || data.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    if let Some(ref mut pending) = state.pending_chart {
        let raw = unsafe { std::slice::from_raw_parts(data, (point_count * 2) as usize) };
        let points: Vec<(f64, f64)> = raw.chunks(2).map(|c| (c[0], c[1])).collect();
        pending.datasets.push(DatasetInfo {
            name: unsafe { cstr_to_string(name) },
            marker,
            r, g, b,
            points,
        });
    }
}

/// Finalizes the pending chart and queues it for rendering.
///
/// Does nothing if no chart builder is active.
#[no_mangle]
pub extern "C" fn ratatui_chart_end(handle: *mut c_void) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    if let Some(pending) = state.pending_chart.take() {
        state.commands.push(WidgetCommand::Chart {
            area_id: pending.area_id,
            x_axis: pending.x_axis,
            y_axis: pending.y_axis,
            datasets: pending.datasets,
        });
    }
}

// ─── Canvas builder ───────────────────────────────────────────────────────────

/// Starts a [`Canvas`](ratatui::widgets::canvas::Canvas) builder.
///
/// Builder lifecycle:
/// 1. [`ratatui_canvas_begin`] — open the builder for `area_id` with the
///    given data-space bounds and marker style.
/// 2. Zero or more shape calls — [`ratatui_canvas_map`],
///    [`ratatui_canvas_line`], [`ratatui_canvas_circle`],
///    [`ratatui_canvas_rectangle`], [`ratatui_canvas_text`],
///    [`ratatui_canvas_points`], [`ratatui_canvas_layer`].
/// 3. [`ratatui_canvas_end`] — flush the builder into the command queue.
///
/// Only one canvas builder may be active at a time per handle.
///
/// # Parameters
/// - `x_min`, `x_max`, `y_min`, `y_max`: data-space bounds mapped onto the
///   area.
/// - `marker`: `0` Dot, `1` Braille, `2` HalfBlock, `3` Block.
#[no_mangle]
pub extern "C" fn ratatui_canvas_begin(
    handle: *mut c_void,
    area_id: u32,
    x_min: f64, x_max: f64,
    y_min: f64, y_max: f64,
    marker: u8,
) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    state.pending_canvas = Some(PendingCanvas {
        area_id,
        x_min, x_max, y_min, y_max,
        marker,
        shapes: Vec::new(),
    });
}

/// Draws the world map on the pending canvas.
///
/// # Parameters
/// - `resolution`: `0` Low, any other value High.
///
/// Does nothing if no canvas builder is active.
#[no_mangle]
pub extern "C" fn ratatui_canvas_map(handle: *mut c_void, resolution: u8) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    if let Some(ref mut p) = state.pending_canvas {
        p.shapes.push(CanvasShape::Map { resolution });
    }
}

/// Flushes the current canvas layer.
///
/// Subsequent shapes are drawn on a new layer on top of all previously drawn
/// content. Does nothing if no canvas builder is active.
#[no_mangle]
pub extern "C" fn ratatui_canvas_layer(handle: *mut c_void) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    if let Some(ref mut p) = state.pending_canvas { p.shapes.push(CanvasShape::Layer); }
}

/// Draws a colored line from `(x1, y1)` to `(x2, y2)` on the pending canvas.
///
/// Coordinates are in data space (see [`ratatui_canvas_begin`]).
#[no_mangle]
pub extern "C" fn ratatui_canvas_line(
    handle: *mut c_void,
    x1: f64, y1: f64, x2: f64, y2: f64,
    r: u8, g: u8, b: u8,
) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    if let Some(ref mut p) = state.pending_canvas {
        p.shapes.push(CanvasShape::Line { x1, y1, x2, y2, r, g, b });
    }
}

/// Draws a colored circle centered at `(x, y)` with the given `radius`.
///
/// Coordinates are in data space (see [`ratatui_canvas_begin`]).
#[no_mangle]
pub extern "C" fn ratatui_canvas_circle(
    handle: *mut c_void,
    x: f64, y: f64, radius: f64,
    r: u8, g: u8, b: u8,
) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    if let Some(ref mut p) = state.pending_canvas {
        p.shapes.push(CanvasShape::Circle { x, y, radius, r, g, b });
    }
}

/// Draws a colored rectangle outline anchored at `(x, y)` with size `(w, h)`.
///
/// Coordinates are in data space (see [`ratatui_canvas_begin`]).
#[no_mangle]
pub extern "C" fn ratatui_canvas_rectangle(
    handle: *mut c_void,
    x: f64, y: f64, w: f64, h: f64,
    r: u8, g: u8, b: u8,
) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    if let Some(ref mut p) = state.pending_canvas {
        p.shapes.push(CanvasShape::Rectangle { x, y, w, h, r, g, b });
    }
}

/// Draws colored text anchored at `(x, y)` on the pending canvas.
///
/// Coordinates are in data space (see [`ratatui_canvas_begin`]).
#[no_mangle]
pub extern "C" fn ratatui_canvas_text(
    handle: *mut c_void,
    x: f64, y: f64,
    text: *const c_char,
    r: u8, g: u8, b: u8,
) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    if let Some(ref mut p) = state.pending_canvas {
        p.shapes.push(CanvasShape::Text { x, y, text: unsafe { cstr_to_string(text) }, r, g, b });
    }
}

/// Draws a colored point cloud on the pending canvas.
///
/// # Parameters
/// - `coords`: pointer to `count * 2` `f64` values, interleaved as
///   `[x0, y0, x1, y1, …]`.
/// - `count`: number of `(x, y)` pairs.
///
/// Coordinates are in data space (see [`ratatui_canvas_begin`]). Does nothing
/// if no canvas builder is active or `coords` is null.
#[no_mangle]
pub extern "C" fn ratatui_canvas_points(
    handle: *mut c_void,
    coords: *const f64,
    count: u32,
    r: u8, g: u8, b: u8,
) {
    if handle.is_null() || coords.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    if let Some(ref mut p) = state.pending_canvas {
        let raw = unsafe { std::slice::from_raw_parts(coords, (count * 2) as usize) };
        let pts: Vec<(f64, f64)> = raw.chunks(2).map(|c| (c[0], c[1])).collect();
        p.shapes.push(CanvasShape::Points { coords: pts, r, g, b });
    }
}

/// Finalizes the pending canvas and queues it for rendering.
///
/// Does nothing if no canvas builder is active.
#[no_mangle]
pub extern "C" fn ratatui_canvas_end(handle: *mut c_void) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    if let Some(pending) = state.pending_canvas.take() {
        state.commands.push(WidgetCommand::Canvas {
            area_id: pending.area_id,
            x_min: pending.x_min,
            x_max: pending.x_max,
            y_min: pending.y_min,
            y_max: pending.y_max,
            marker: pending.marker,
            shapes: pending.shapes,
        });
    }
}

// ─── Input / Hit-Testing ─────────────────────────────────────────────────────

/// Returns the most specific area id covering the given terminal cell.
///
/// When several registered areas contain `(col, row)` the one with the
/// smallest cell count (the most deeply nested) wins. Returns `0` (root) when
/// no registered area matches.
///
/// Useful for mapping pointer input back into the layout tree.
#[no_mangle]
pub extern "C" fn ratatui_hit_test(
    handle: *mut c_void,
    col: u16,
    row: u16,
) -> u32 {
    if handle.is_null() { return 0; }
    let state = unsafe { state_mut(handle) };
    let mut best_id = 0u32;
    let mut best_area = u32::MAX;

    for (&id, &rect) in &state.area_map {
        if col >= rect.x && col < rect.x + rect.width
            && row >= rect.y && row < rect.y + rect.height
        {
            let area = (rect.width as u32) * (rect.height as u32);
            if area < best_area {
                best_area = area;
                best_id = id;
            }
        }
    }
    best_id
}

/// Returns the cell-space rectangle of the given area as a packed `u64`.
///
/// The four `u16` fields are packed little-endian:
///
/// ```text
/// bits  0..16  -> x
/// bits 16..32  -> y
/// bits 32..48  -> width
/// bits 48..64  -> height
/// ```
///
/// Returns `0` if `handle` is null or the area id is unknown.
#[no_mangle]
pub extern "C" fn ratatui_get_area_rect(
    handle: *const c_void,
    area_id: u32,
) -> u64 {
    if handle.is_null() { return 0; }
    let state = unsafe { &*(handle as *const TerminalState) };
    match state.area_map.get(&area_id) {
        Some(rect) => {
            (rect.x as u64)
                | ((rect.y as u64) << 16)
                | ((rect.width as u64) << 32)
                | ((rect.height as u64) << 48)
        }
        None => 0,
    }
}

// ─── Utility ─────────────────────────────────────────────────────────────────

/// Returns the library version as a static null-terminated C string.
///
/// The returned pointer is valid for the lifetime of the process and must
/// not be freed by the caller. The value matches `CARGO_PKG_VERSION`.
#[no_mangle]
pub extern "C" fn ratatui_version() -> *const c_char {
    concat!(env!("CARGO_PKG_VERSION"), "\0").as_ptr() as *const c_char
}
