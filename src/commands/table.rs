//! Equal-width and constraint-driven table rendering.

use crate::commands::layout::constraint_from_bytes;
use ratatui::layout::{Constraint, Rect};
use ratatui::style::{Modifier, Style};
use ratatui::widgets::{Cell, Row, Table, TableState};

/// Renders a simple equal-width table from `data`.
///
/// `data` format: first line tab-separated headers, subsequent lines
/// tab-separated rows. All columns receive the same percentage width.
///
/// Columns are capped at the area width: more columns than cells cannot be
/// displayed, and an unbounded column count both overflows the u16 width
/// math and makes the layout solver pathologically slow.
pub(crate) fn render_table(frame: &mut ratatui::Frame, area: Rect, data: &str, style: Style) {
    let max_cols = (area.width as usize).max(1);
    let mut lines = data.lines();
    let headers: Vec<&str> = lines
        .next()
        .unwrap_or("")
        .split('\t')
        .take(max_cols)
        .collect();
    let col_count = headers.len().max(1);
    let header_row = Row::new(headers.iter().map(|h| Cell::from(*h)));
    let rows: Vec<Row> = lines
        .map(|line| {
            Row::new(line.split('\t').take(max_cols).map(Cell::from).collect::<Vec<_>>())
        })
        .collect();
    // Divide in usize: casting col_count to u16 first can truncate to 0
    // (e.g. 65536 columns) and panic with a division by zero.
    let equal_width = (100 / col_count) as u16;
    let widths: Vec<Constraint> =
        (0..col_count).map(|_| Constraint::Percentage(equal_width)).collect();
    frame.render_widget(
        Table::new(rows, widths).header(header_row).style(style),
        area,
    );
}

/// Renders an extended table with typed column constraints and optional row
/// highlighting.
///
/// If `col_constraints` is empty all columns get equal percentage widths.
/// A non-negative `selected_row` enables a stateful render with a bold
/// highlight on the selected row.
///
/// Columns (and explicit column constraints) are capped at the area width;
/// see `render_table` for the rationale.
pub(crate) fn render_table_ex(
    frame: &mut ratatui::Frame,
    area: Rect,
    data: &str,
    col_constraints: &[(u8, u16)],
    selected_row: i32,
    style: Style,
) {
    let max_cols = (area.width as usize).max(1);
    let mut lines = data.lines();
    let headers: Vec<&str> = lines
        .next()
        .unwrap_or("")
        .split('\t')
        .take(max_cols)
        .collect();
    let col_count = headers.len().max(1);
    let rows: Vec<Row> = lines
        .map(|line| {
            Row::new(line.split('\t').take(max_cols).map(Cell::from).collect::<Vec<_>>())
        })
        .collect();
    let widths: Vec<Constraint> = if col_constraints.is_empty() {
        // Divide in usize: see render_table for the truncation hazard.
        let eq = (100 / col_count) as u16;
        (0..col_count).map(|_| Constraint::Percentage(eq)).collect()
    } else {
        col_constraints
            .iter()
            .take(max_cols)
            .map(|&(t, v)| constraint_from_bytes(t, v))
            .collect()
    };
    let header_row = Row::new(headers.iter().map(|h| Cell::from(*h)));
    let table = Table::new(rows, widths)
        .header(header_row)
        .style(style)
        .row_highlight_style(Style::default().add_modifier(Modifier::BOLD));

    if selected_row >= 0 {
        let mut ts = TableState::default().with_selected(Some(selected_row as usize));
        frame.render_stateful_widget(table, area, &mut ts);
    } else {
        frame.render_widget(table, area);
    }
}
