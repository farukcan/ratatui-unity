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

unsafe fn state_mut<'a>(handle: *mut c_void) -> &'a mut TerminalState {
    &mut *(handle as *mut TerminalState)
}

unsafe fn cstr_to_string(ptr: *const c_char) -> String {
    if ptr.is_null() {
        return String::new();
    }
    CStr::from_ptr(ptr).to_string_lossy().into_owned()
}

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

// ─── Lifecycle ───────────────────────────────────────────────────────────────

/// Create a terminal instance and return an opaque handle.
#[no_mangle]
pub extern "C" fn ratatui_create(cols: u16, rows: u16, font_size: f32) -> *mut c_void {
    let state = Box::new(TerminalState::new(cols, rows, font_size));
    Box::into_raw(state) as *mut c_void
}

/// Destroy a terminal handle created by `ratatui_create`.
#[no_mangle]
pub extern "C" fn ratatui_destroy(handle: *mut c_void) {
    if !handle.is_null() {
        unsafe { drop(Box::from_raw(handle as *mut TerminalState)); }
    }
}

/// Replace the embedded font with custom TTF bytes. Returns 1 on success, 0 on error.
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

#[no_mangle]
pub extern "C" fn ratatui_begin_frame(handle: *mut c_void) {
    if handle.is_null() { return; }
    unsafe { state_mut(handle) }.begin_frame();
}

/// Render all queued commands, rasterize to a pixel buffer, return a pointer to
/// the RGBA32 data. Valid until the next call on this handle.
#[no_mangle]
pub extern "C" fn ratatui_end_frame(handle: *mut c_void) -> *const u8 {
    if handle.is_null() { return std::ptr::null(); }
    let state = unsafe { state_mut(handle) };
    render_all_commands(state);
    let buffer = state.terminal.backend().buffer().clone();
    state.pixel_buffer = renderer::render_buffer_to_pixels(&buffer, &mut state.font);
    state.pixel_buffer.as_ptr()
}

#[no_mangle]
pub extern "C" fn ratatui_pixel_width(handle: *const c_void) -> u32 {
    if handle.is_null() { return 0; }
    unsafe { &*(handle as *const TerminalState) }.pixel_width
}

#[no_mangle]
pub extern "C" fn ratatui_pixel_height(handle: *const c_void) -> u32 {
    if handle.is_null() { return 0; }
    unsafe { &*(handle as *const TerminalState) }.pixel_height
}

// ─── Layout ──────────────────────────────────────────────────────────────────

#[no_mangle]
pub extern "C" fn ratatui_root_area(_handle: *const c_void) -> u32 { 0 }

/// Split `area_id` and write the resulting child IDs into `out_ids`.
/// `constraint_types`: 0=Length, 1=Min, 2=Max, 3=Percentage, 4=Fill
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

/// Returns a new area ID inside `area_id` shrunk by the given margin.
/// Returns `u32::MAX` on error.
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

/// Set a pending style for the next widget command.
/// Pass use_default_fg/bg = 1 to use terminal defaults; 0 to use the RGB values.
/// `modifiers`: 0x01=Bold, 0x02=Italic, 0x04=Underlined, 0x08=Dim
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

/// `borders`: 0x01=Top, 0x02=Bottom, 0x04=Left, 0x08=Right, 0x0F=All
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

/// `alignment`: 0=Left, 1=Center, 2=Right
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

/// `items`: newline-separated. `selected`: highlighted index, or -1 for none.
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

/// `ratio`: [0.0, 1.0]
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

/// `titles`: newline-separated tab titles.
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

/// `data`: first line = tab-separated headers; subsequent lines = tab-separated rows.
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

/// `data`: newline-separated "label\tvalue" pairs.
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

/// Horizontal line gauge. `ratio`: [0.0, 1.0].
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

/// `orientation`: 0=VerticalRight, 1=VerticalLeft, 2=HorizontalBottom, 3=HorizontalTop
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

/// Render a monthly calendar. Requires the `widget-calendar` Cargo feature.
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

/// Enhanced table with constraint-based column widths and row selection.
/// `col_types`/`col_values`: constraint type+value per column (nullable for equal distribution).
/// `selected_row`: highlighted row index, or -1 for none.
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

/// Begin a styled paragraph. Subsequent `ratatui_styled_para_span` calls add spans.
/// Finalize with `ratatui_styled_para_end`.
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

/// Add a styled span to the current line of the pending styled paragraph.
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

/// Start a new line in the pending styled paragraph.
#[no_mangle]
pub extern "C" fn ratatui_styled_para_newline(handle: *mut c_void) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    if let Some(ref mut pending) = state.pending_styled_para {
        pending.lines.push(vec![]);
    }
}

/// Finalize the styled paragraph and add it to the command queue.
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

/// Begin a chart. Add axes with `ratatui_chart_x_axis`/`ratatui_chart_y_axis`,
/// datasets with `ratatui_chart_dataset`. Finalize with `ratatui_chart_end`.
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

/// Add a dataset. `data` is interleaved (x, y) f64 pairs; `point_count` is the number of pairs.
/// `marker`: 0=Dot, 1=Braille, 2=HalfBlock, 3=Block
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

/// Finalize the chart and add it to the command queue.
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

/// Begin a canvas with the given bounds and marker type.
/// Add shapes with subsequent calls. Finalize with `ratatui_canvas_end`.
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

/// Draw the world map. `resolution`: 0=Low, 1=High.
#[no_mangle]
pub extern "C" fn ratatui_canvas_map(handle: *mut c_void, resolution: u8) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    if let Some(ref mut p) = state.pending_canvas {
        p.shapes.push(CanvasShape::Map { resolution });
    }
}

/// Flush the current layer (allows drawing on top of what's been drawn so far).
#[no_mangle]
pub extern "C" fn ratatui_canvas_layer(handle: *mut c_void) {
    if handle.is_null() { return; }
    let state = unsafe { state_mut(handle) };
    if let Some(ref mut p) = state.pending_canvas { p.shapes.push(CanvasShape::Layer); }
}

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

/// `coords`: interleaved (x, y) f64 pairs; `count` is the number of pairs.
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

/// Finalize the canvas and add it to the command queue.
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

/// Returns the most specific area ID at the given terminal cell coordinates.
/// If multiple areas overlap, the one with the smallest area (most specific) is returned.
/// Returns 0 (root) if no specific area contains the cell.
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

/// Returns the cell-space rectangle of the given area_id as a packed u64.
/// Format: x | (y << 16) | (width << 32) | (height << 48)
/// Returns 0 if the area is not found.
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

/// Returns the library version string as a static C string.
#[no_mangle]
pub extern "C" fn ratatui_version() -> *const c_char {
    concat!(env!("CARGO_PKG_VERSION"), "\0").as_ptr() as *const c_char
}
