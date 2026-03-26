use ratatui::{
    layout::{Alignment, Constraint, Direction, Layout, Rect},
    style::{Color, Modifier, Style},
    symbols,
    text::{Line, Span},
    widgets::{
        canvas::{self, Canvas, Circle, Map, MapResolution, Rectangle},
        Axis, Bar, BarChart, BarGroup, Block, Borders, Chart, Dataset, Gauge, LineGauge,
        List, ListItem, ListState, Paragraph, Row, Cell, Scrollbar, ScrollbarOrientation,
        ScrollbarState, Sparkline, Table, TableState, Tabs, Wrap,
    },
};

use crate::terminal::{AxisInfo, CanvasShape, DatasetInfo, TerminalState, WidgetCommand};

// ─── Layout ──────────────────────────────────────────────────────────────────

const CONSTRAINT_LENGTH: u8 = 0;
const CONSTRAINT_MIN: u8 = 1;
const CONSTRAINT_MAX: u8 = 2;
const CONSTRAINT_PERCENTAGE: u8 = 3;
#[allow(dead_code)]
const CONSTRAINT_FILL: u8 = 4;

/// Splits `area_id` and writes the resulting child area IDs into `out_ids`.
pub fn do_split(
    state: &mut TerminalState,
    area_id: u32,
    direction: u8,
    constraint_types: &[u8],
    constraint_values: &[u16],
    out_ids: &mut [u32],
) -> u32 {
    let parent_rect = match state.area_map.get(&area_id).copied() {
        Some(r) => r,
        None => return 0,
    };

    let dir = if direction == 0 {
        Direction::Horizontal
    } else {
        Direction::Vertical
    };

    let count = constraint_types.len().min(constraint_values.len());
    let constraints: Vec<Constraint> = constraint_types[..count]
        .iter()
        .zip(&constraint_values[..count])
        .map(|(&t, &v)| constraint_from_bytes(t, v))
        .collect();

    let chunks = Layout::default()
        .direction(dir)
        .constraints(constraints)
        .split(parent_rect);

    let produced = chunks.len().min(out_ids.len());
    for (i, &rect) in chunks.iter().enumerate().take(produced) {
        let id = state.register_area(rect);
        out_ids[i] = id;
    }
    produced as u32
}

// ─── Frame rendering ─────────────────────────────────────────────────────────

/// Replays all queued widget commands inside a single `terminal.draw()` call.
pub fn render_all_commands(state: &mut TerminalState) {
    let commands = std::mem::take(&mut state.commands);
    let area_map = state.area_map.clone();

    state
        .terminal
        .draw(|frame| {
            for cmd in &commands {
                match cmd {
                    WidgetCommand::Block { area_id, title, borders, style } => {
                        if let Some(&area) = area_map.get(area_id) {
                            frame.render_widget(
                                Block::default()
                                    .title(title.as_str())
                                    .borders(borders_from_u8(*borders))
                                    .style(*style),
                                area,
                            );
                        }
                    }

                    WidgetCommand::Paragraph { area_id, text, alignment, wrap, style } => {
                        if let Some(&area) = area_map.get(area_id) {
                            let mut para = Paragraph::new(text.as_str())
                                .alignment(alignment_from_u8(*alignment))
                                .style(*style);
                            if *wrap {
                                para = para.wrap(Wrap { trim: false });
                            }
                            frame.render_widget(para, area);
                        }
                    }

                    WidgetCommand::StyledParagraph { area_id, alignment, wrap, lines } => {
                        if let Some(&area) = area_map.get(area_id) {
                            let ratatui_lines: Vec<Line> = lines
                                .iter()
                                .map(|spans| {
                                    Line::from(
                                        spans
                                            .iter()
                                            .map(|s| Span::styled(s.text.clone(), s.style))
                                            .collect::<Vec<_>>(),
                                    )
                                })
                                .collect();
                            let mut para = Paragraph::new(ratatui_lines)
                                .alignment(alignment_from_u8(*alignment));
                            if *wrap {
                                para = para.wrap(Wrap { trim: false });
                            }
                            frame.render_widget(para, area);
                        }
                    }

                    WidgetCommand::List { area_id, items, selected, style } => {
                        if let Some(&area) = area_map.get(area_id) {
                            let list_items: Vec<ListItem> = items
                                .lines()
                                .map(ListItem::new)
                                .collect();
                            let list = List::new(list_items)
                                .style(*style)
                                .highlight_symbol("> ")
                                .highlight_style(Style::default().add_modifier(Modifier::BOLD));

                            if *selected >= 0 {
                                let mut list_state = ListState::default()
                                    .with_selected(Some(*selected as usize));
                                frame.render_stateful_widget(list, area, &mut list_state);
                            } else {
                                frame.render_widget(list, area);
                            }
                        }
                    }

                    WidgetCommand::Gauge { area_id, ratio, label, style } => {
                        if let Some(&area) = area_map.get(area_id) {
                            frame.render_widget(
                                Gauge::default()
                                    .ratio(ratio.clamp(0.0, 1.0))
                                    .label(label.as_str())
                                    .style(*style),
                                area,
                            );
                        }
                    }

                    WidgetCommand::LineGauge { area_id, ratio, label, style } => {
                        if let Some(&area) = area_map.get(area_id) {
                            frame.render_widget(
                                LineGauge::default()
                                    .ratio(ratio.clamp(0.0, 1.0))
                                    .label(label.as_str())
                                    .style(*style),
                                area,
                            );
                        }
                    }

                    WidgetCommand::Tabs { area_id, titles, selected, style } => {
                        if let Some(&area) = area_map.get(area_id) {
                            let tab_lines: Vec<Line> = titles
                                .lines()
                                .map(|t| Line::from(t.to_owned()))
                                .collect();
                            frame.render_widget(
                                Tabs::new(tab_lines)
                                    .select(*selected as usize)
                                    .style(*style),
                                area,
                            );
                        }
                    }

                    WidgetCommand::Sparkline { area_id, data, style } => {
                        if let Some(&area) = area_map.get(area_id) {
                            frame.render_widget(
                                Sparkline::default().data(data).style(*style),
                                area,
                            );
                        }
                    }

                    WidgetCommand::BarChart { area_id, bars, bar_width, bar_gap, style } => {
                        if let Some(&area) = area_map.get(area_id) {
                            let bar_items: Vec<Bar> = bars
                                .iter()
                                .map(|(label, value)| {
                                    Bar::default().label(label.as_str()).value(*value)
                                })
                                .collect();
                            let group = BarGroup::default().bars(&bar_items);
                            frame.render_widget(
                                BarChart::default()
                                    .data(group)
                                    .bar_width(*bar_width)
                                    .bar_gap(*bar_gap)
                                    .style(*style),
                                area,
                            );
                        }
                    }

                    WidgetCommand::Table { area_id, data, style } => {
                        if let Some(&area) = area_map.get(area_id) {
                            render_table(frame, area, data, *style);
                        }
                    }

                    WidgetCommand::TableEx {
                        area_id,
                        data,
                        col_constraints,
                        selected_row,
                        style,
                    } => {
                        if let Some(&area) = area_map.get(area_id) {
                            render_table_ex(frame, area, data, col_constraints, *selected_row, *style);
                        }
                    }

                    WidgetCommand::Scrollbar {
                        area_id,
                        content_length,
                        position,
                        viewport_length,
                        orientation,
                    } => {
                        if let Some(&area) = area_map.get(area_id) {
                            let orient = scrollbar_orientation_from_u8(*orientation);
                            let mut scroll_state = ScrollbarState::default()
                                .content_length(*content_length as usize)
                                .position(*position as usize)
                                .viewport_content_length(*viewport_length as usize);
                            frame.render_stateful_widget(
                                Scrollbar::new(orient),
                                area,
                                &mut scroll_state,
                            );
                        }
                    }

                    WidgetCommand::Calendar { area_id, year, month, day } => {
                        if let Some(&area) = area_map.get(area_id) {
                            render_calendar(frame, area, *year, *month, *day);
                        }
                    }

                    WidgetCommand::Chart { area_id, x_axis, y_axis, datasets } => {
                        if let Some(&area) = area_map.get(area_id) {
                            render_chart(frame, area, x_axis, y_axis, datasets);
                        }
                    }

                    WidgetCommand::Canvas {
                        area_id,
                        x_min,
                        x_max,
                        y_min,
                        y_max,
                        marker,
                        shapes,
                    } => {
                        if let Some(&area) = area_map.get(area_id) {
                            render_canvas(frame, area, *x_min, *x_max, *y_min, *y_max, *marker, shapes);
                        }
                    }
                }
            }
        })
        .expect("terminal draw failed");

    state.commands = commands;
}

// ─── Table helpers ────────────────────────────────────────────────────────────

fn render_table(frame: &mut ratatui::Frame, area: Rect, data: &str, style: Style) {
    let mut lines = data.lines();
    let headers: Vec<&str> = lines.next().unwrap_or("").split('\t').collect();
    let col_count = headers.len().max(1);
    let header_row = Row::new(headers.iter().map(|h| Cell::from(*h)));
    let rows: Vec<Row> = lines
        .map(|line| Row::new(line.split('\t').map(Cell::from).collect::<Vec<_>>()))
        .collect();
    let equal_width = 100u16 / col_count as u16;
    let widths: Vec<Constraint> =
        (0..col_count).map(|_| Constraint::Percentage(equal_width)).collect();
    frame.render_widget(
        Table::new(rows, widths).header(header_row).style(style),
        area,
    );
}

fn render_table_ex(
    frame: &mut ratatui::Frame,
    area: Rect,
    data: &str,
    col_constraints: &[(u8, u16)],
    selected_row: i32,
    style: Style,
) {
    let mut lines = data.lines();
    let headers: Vec<&str> = lines.next().unwrap_or("").split('\t').collect();
    let col_count = headers.len().max(1);
    let rows: Vec<Row> = lines
        .map(|line| Row::new(line.split('\t').map(Cell::from).collect::<Vec<_>>()))
        .collect();
    let widths: Vec<Constraint> = if col_constraints.is_empty() {
        let eq = 100u16 / col_count as u16;
        (0..col_count).map(|_| Constraint::Percentage(eq)).collect()
    } else {
        col_constraints
            .iter()
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

// ─── Calendar ────────────────────────────────────────────────────────────────

fn render_calendar(frame: &mut ratatui::Frame, area: Rect, year: i32, month: u8, day: u8) {
    use ratatui::widgets::calendar::{CalendarEventStore, Monthly};

    let m = month_to_time(month);
    let d = day.clamp(1, 28);
    let date = time::Date::from_calendar_date(year, m, d).unwrap_or_else(|_| {
        time::Date::from_calendar_date(year, time::Month::January, 1)
            .unwrap_or_else(|_| {
                time::Date::from_calendar_date(2024, time::Month::January, 1)
                    .expect("hardcoded valid date")
            })
    });

    let calendar = Monthly::new(date, CalendarEventStore::default())
        .show_month_header(Style::default())
        .show_weekdays_header(Style::default());
    frame.render_widget(calendar, area);
}

fn month_to_time(month: u8) -> time::Month {
    match month {
        1 => time::Month::January,
        2 => time::Month::February,
        3 => time::Month::March,
        4 => time::Month::April,
        5 => time::Month::May,
        6 => time::Month::June,
        7 => time::Month::July,
        8 => time::Month::August,
        9 => time::Month::September,
        10 => time::Month::October,
        11 => time::Month::November,
        12 => time::Month::December,
        _ => time::Month::January,
    }
}

// ─── Chart ───────────────────────────────────────────────────────────────────

fn render_chart(
    frame: &mut ratatui::Frame,
    area: Rect,
    x_axis: &Option<AxisInfo>,
    y_axis: &Option<AxisInfo>,
    datasets: &[DatasetInfo],
) {
    let ratatui_datasets: Vec<Dataset> = datasets
        .iter()
        .map(|d| {
            Dataset::default()
                .name(d.name.as_str())
                .marker(marker_from_u8(d.marker))
                .style(Style::default().fg(Color::Rgb(d.r, d.g, d.b)))
                .data(d.points.as_slice())
        })
        .collect();

    let mut chart = Chart::new(ratatui_datasets);

    if let Some(ax) = x_axis {
        chart = chart.x_axis(
            Axis::default()
                .title(ax.title.as_str())
                .style(Style::default().fg(Color::Gray))
                .bounds([ax.min, ax.max]),
        );
    }
    if let Some(ay) = y_axis {
        chart = chart.y_axis(
            Axis::default()
                .title(ay.title.as_str())
                .style(Style::default().fg(Color::Gray))
                .bounds([ay.min, ay.max]),
        );
    }

    frame.render_widget(chart, area);
}

// ─── Canvas ───────────────────────────────────────────────────────────────────

fn render_canvas(
    frame: &mut ratatui::Frame,
    area: Rect,
    x_min: f64,
    x_max: f64,
    y_min: f64,
    y_max: f64,
    marker: u8,
    shapes: &[CanvasShape],
) {
    let canvas_widget = Canvas::default()
        .x_bounds([x_min, x_max])
        .y_bounds([y_min, y_max])
        .marker(marker_from_u8(marker))
        .paint(|ctx| {
            for shape in shapes {
                match shape {
                    CanvasShape::Map { resolution } => {
                        ctx.draw(&Map {
                            color: Color::White,
                            resolution: if *resolution == 0 {
                                MapResolution::Low
                            } else {
                                MapResolution::High
                            },
                        });
                    }
                    CanvasShape::Layer => {
                        ctx.layer();
                    }
                    CanvasShape::Line { x1, y1, x2, y2, r, g, b } => {
                        ctx.draw(&canvas::Line {
                            x1: *x1,
                            y1: *y1,
                            x2: *x2,
                            y2: *y2,
                            color: Color::Rgb(*r, *g, *b),
                        });
                    }
                    CanvasShape::Circle { x, y, radius, r, g, b } => {
                        ctx.draw(&Circle {
                            x: *x,
                            y: *y,
                            radius: *radius,
                            color: Color::Rgb(*r, *g, *b),
                        });
                    }
                    CanvasShape::Rectangle { x, y, w, h, r, g, b } => {
                        ctx.draw(&Rectangle {
                            x: *x,
                            y: *y,
                            width: *w,
                            height: *h,
                            color: Color::Rgb(*r, *g, *b),
                        });
                    }
                    CanvasShape::Text { x, y, text, r, g, b } => {
                        // ctx.print requires Into<Line<'static>>, so clone the String.
                        ctx.print(
                            *x,
                            *y,
                            Span::styled(
                                text.clone(),
                                Style::default().fg(Color::Rgb(*r, *g, *b)),
                            ),
                        );
                    }
                    CanvasShape::Points { coords, r, g, b } => {
                        ctx.draw(&canvas::Points {
                            coords: coords.as_slice(),
                            color: Color::Rgb(*r, *g, *b),
                        });
                    }
                }
            }
        });

    frame.render_widget(canvas_widget, area);
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

fn constraint_from_bytes(t: u8, v: u16) -> Constraint {
    match t {
        CONSTRAINT_LENGTH => Constraint::Length(v),
        CONSTRAINT_MIN => Constraint::Min(v),
        CONSTRAINT_MAX => Constraint::Max(v),
        CONSTRAINT_PERCENTAGE => Constraint::Percentage(v),
        _ => Constraint::Fill(v),
    }
}

fn borders_from_u8(b: u8) -> Borders {
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

fn alignment_from_u8(a: u8) -> Alignment {
    match a {
        1 => Alignment::Center,
        2 => Alignment::Right,
        _ => Alignment::Left,
    }
}

fn marker_from_u8(m: u8) -> symbols::Marker {
    match m {
        1 => symbols::Marker::Braille,
        2 => symbols::Marker::HalfBlock,
        3 => symbols::Marker::Block,
        _ => symbols::Marker::Dot,
    }
}

fn scrollbar_orientation_from_u8(o: u8) -> ScrollbarOrientation {
    match o {
        1 => ScrollbarOrientation::VerticalLeft,
        2 => ScrollbarOrientation::HorizontalBottom,
        3 => ScrollbarOrientation::HorizontalTop,
        _ => ScrollbarOrientation::VerticalRight,
    }
}
