//! Multi-call builder entry points: styled paragraph, chart, and canvas.

use crate::ffi::util::{cstr_to_string, slice_from, state_mut, style_from_rgba};
use crate::terminal::{
    AxisInfo, CanvasShape, DatasetInfo, PendingCanvas, PendingChart, PendingStyledParagraph,
    SpanInfo, WidgetCommand,
};
use std::ffi::c_void;
use std::os::raw::c_char;

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
    let Some(state) = state_mut(handle) else { return; };
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
/// encoding as [`ratatui_set_style`](crate::ratatui_set_style).
#[no_mangle]
pub extern "C" fn ratatui_styled_para_span(
    handle: *mut c_void,
    text: *const c_char,
    fg_r: u8, fg_g: u8, fg_b: u8, use_default_fg: u8,
    bg_r: u8, bg_g: u8, bg_b: u8, use_default_bg: u8,
    modifiers: u8,
) {
    let Some(state) = state_mut(handle) else { return; };
    if let Some(ref mut pending) = state.pending_styled_para {
        let style = style_from_rgba(
            fg_r, fg_g, fg_b, use_default_fg,
            bg_r, bg_g, bg_b, use_default_bg,
            modifiers,
        );
        let span = SpanInfo { text: cstr_to_string(text), style };
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
    let Some(state) = state_mut(handle) else { return; };
    if let Some(ref mut pending) = state.pending_styled_para {
        pending.lines.push(vec![]);
    }
}

/// Finalizes the pending styled paragraph and queues it for rendering.
///
/// Does nothing if no builder is active.
#[no_mangle]
pub extern "C" fn ratatui_styled_para_end(handle: *mut c_void) {
    let Some(state) = state_mut(handle) else { return; };
    if let Some(pending) = state.pending_styled_para.take() {
        state.commands.push(WidgetCommand::StyledParagraph {
            area_id: pending.area_id,
            alignment: pending.alignment,
            wrap: pending.wrap,
            lines: pending.lines,
        });
    }
}

// ─── Chart builder ───────────────────────────────────────────────────────────

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
    let Some(state) = state_mut(handle) else { return; };
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
    let Some(state) = state_mut(handle) else { return; };
    if let Some(ref mut pending) = state.pending_chart {
        pending.x_axis = Some(AxisInfo { title: cstr_to_string(title), min, max });
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
    let Some(state) = state_mut(handle) else { return; };
    if let Some(ref mut pending) = state.pending_chart {
        pending.y_axis = Some(AxisInfo { title: cstr_to_string(title), min, max });
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
    if data.is_null() { return; }
    let Some(state) = state_mut(handle) else { return; };
    if let Some(ref mut pending) = state.pending_chart {
        // Multiply in usize: `point_count * 2` can overflow u32.
        let raw = slice_from(data, point_count as usize * 2);
        let points: Vec<(f64, f64)> = raw.chunks(2).map(|c| (c[0], c[1])).collect();
        pending.datasets.push(DatasetInfo {
            name: cstr_to_string(name),
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
    let Some(state) = state_mut(handle) else { return; };
    if let Some(pending) = state.pending_chart.take() {
        state.commands.push(WidgetCommand::Chart {
            area_id: pending.area_id,
            x_axis: pending.x_axis,
            y_axis: pending.y_axis,
            datasets: pending.datasets,
        });
    }
}

// ─── Canvas builder ──────────────────────────────────────────────────────────

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
    let Some(state) = state_mut(handle) else { return; };
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
    let Some(state) = state_mut(handle) else { return; };
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
    let Some(state) = state_mut(handle) else { return; };
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
    let Some(state) = state_mut(handle) else { return; };
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
    let Some(state) = state_mut(handle) else { return; };
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
    let Some(state) = state_mut(handle) else { return; };
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
    let Some(state) = state_mut(handle) else { return; };
    if let Some(ref mut p) = state.pending_canvas {
        p.shapes.push(CanvasShape::Text { x, y, text: cstr_to_string(text), r, g, b });
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
    if coords.is_null() { return; }
    let Some(state) = state_mut(handle) else { return; };
    if let Some(ref mut p) = state.pending_canvas {
        // Multiply in usize: `count * 2` can overflow u32.
        let raw = slice_from(coords, count as usize * 2);
        let pts: Vec<(f64, f64)> = raw.chunks(2).map(|c| (c[0], c[1])).collect();
        p.shapes.push(CanvasShape::Points { coords: pts, r, g, b });
    }
}

/// Finalizes the pending canvas and queues it for rendering.
///
/// Does nothing if no canvas builder is active.
#[no_mangle]
pub extern "C" fn ratatui_canvas_end(handle: *mut c_void) {
    let Some(state) = state_mut(handle) else { return; };
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
