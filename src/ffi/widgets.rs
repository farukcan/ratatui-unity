//! Single-call widget entry points (block, paragraph, list, gauge, tabs,
//! sparkline, tables, bar chart, line gauge, scrollbar, calendar).

use crate::ffi::util::{cstr_to_string, slice_from, state_mut};
use crate::terminal::WidgetCommand;
use std::ffi::c_void;
use std::os::raw::c_char;

/// Queues a [`Block`](ratatui::widgets::Block) widget with an optional title
/// and per-edge borders.
///
/// `borders` is a bit field — `0x01` Top, `0x02` Bottom, `0x04` Left,
/// `0x08` Right. The value `0x0F` is treated as "all borders".
///
/// The pending style (see [`ratatui_set_style`](crate::ratatui_set_style)) is
/// consumed and applied to the block.
#[no_mangle]
pub extern "C" fn ratatui_block(
    handle: *mut c_void,
    area_id: u32,
    title: *const c_char,
    borders: u8,
) {
    let Some(state) = state_mut(handle) else { return; };
    let style = state.take_style();
    state.commands.push(WidgetCommand::Block {
        area_id,
        title: cstr_to_string(title),
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
/// ([`ratatui_styled_para_begin`](crate::ratatui_styled_para_begin) /
/// [`ratatui_styled_para_span`](crate::ratatui_styled_para_span) /
/// [`ratatui_styled_para_newline`](crate::ratatui_styled_para_newline) /
/// [`ratatui_styled_para_end`](crate::ratatui_styled_para_end)).
#[no_mangle]
pub extern "C" fn ratatui_paragraph(
    handle: *mut c_void,
    area_id: u32,
    text: *const c_char,
    alignment: u8,
    wrap: u8,
) {
    let Some(state) = state_mut(handle) else { return; };
    let style = state.take_style();
    state.commands.push(WidgetCommand::Paragraph {
        area_id,
        text: cstr_to_string(text),
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
    let Some(state) = state_mut(handle) else { return; };
    let style = state.take_style();
    state.commands.push(WidgetCommand::List {
        area_id,
        items: cstr_to_string(items),
        selected,
        style,
    });
}

/// Queues a block-style [`Gauge`](ratatui::widgets::Gauge).
///
/// # Parameters
/// - `ratio`: progress in `[0.0, 1.0]`. Values outside the range are clamped.
/// - `label`: optional text overlaid on the gauge (pass `null` or empty
///   for none).
#[no_mangle]
pub extern "C" fn ratatui_gauge(
    handle: *mut c_void,
    area_id: u32,
    ratio: f32,
    label: *const c_char,
) {
    let Some(state) = state_mut(handle) else { return; };
    let style = state.take_style();
    state.commands.push(WidgetCommand::Gauge {
        area_id,
        ratio: ratio as f64,
        label: cstr_to_string(label),
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
    let Some(state) = state_mut(handle) else { return; };
    let style = state.take_style();
    state.commands.push(WidgetCommand::Tabs {
        area_id,
        titles: cstr_to_string(titles),
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
    if data.is_null() { return; }
    let Some(state) = state_mut(handle) else { return; };
    let style = state.take_style();
    let data_vec = slice_from(data, len as usize).to_vec();
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
    let Some(state) = state_mut(handle) else { return; };
    let style = state.take_style();
    state.commands.push(WidgetCommand::Table {
        area_id,
        data: cstr_to_string(data),
        style,
    });
}

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
    let Some(state) = state_mut(handle) else { return; };
    let style = state.take_style();
    let data_str = cstr_to_string(data);
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
    let Some(state) = state_mut(handle) else { return; };
    let style = state.take_style();
    state.commands.push(WidgetCommand::LineGauge {
        area_id,
        ratio: ratio as f64,
        label: cstr_to_string(label),
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
    let Some(state) = state_mut(handle) else { return; };
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
    let Some(state) = state_mut(handle) else { return; };
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
///   [`ratatui_split`](crate::ratatui_split). Pass `null` (or
///   `col_count == 0`) for equal-width distribution.
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
    let Some(state) = state_mut(handle) else { return; };
    let style = state.take_style();
    let col_constraints: Vec<(u8, u16)> =
        if col_types.is_null() || col_values.is_null() || col_count == 0 {
            Vec::new()
        } else {
            let n = col_count as usize;
            let types = slice_from(col_types, n);
            let values = slice_from(col_values, n);
            types.iter().zip(values.iter()).map(|(&t, &v)| (t, v)).collect()
        };
    state.commands.push(WidgetCommand::TableEx {
        area_id,
        data: cstr_to_string(data),
        col_constraints,
        selected_row,
        style,
    });
}
