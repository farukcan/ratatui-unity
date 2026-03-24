use ratatui::{
    layout::{Alignment, Constraint, Direction, Layout, Rect},
    style::Style,
    text::Line,
    widgets::{Block, Borders, Gauge, List, ListItem, Paragraph, Sparkline, Table, Row, Cell, Tabs, Wrap},
};

use crate::terminal::TerminalState;

// ─── Command queue ────────────────────────────────────────────────────────────

pub enum WidgetCommand {
    Block {
        area_id: u32,
        title: String,
        borders: u8,
        style: Style,
    },
    Paragraph {
        area_id: u32,
        text: String,
        alignment: u8,
        wrap: bool,
        style: Style,
    },
    List {
        area_id: u32,
        /// Newline-separated items
        items: String,
        #[allow(dead_code)]
        selected: i32,
        style: Style,
    },
    Gauge {
        area_id: u32,
        ratio: f64,
        label: String,
        style: Style,
    },
    Tabs {
        area_id: u32,
        /// Newline-separated tab titles
        titles: String,
        selected: u32,
        style: Style,
    },
    Sparkline {
        area_id: u32,
        data: Vec<u64>,
        style: Style,
    },
    Table {
        area_id: u32,
        /// First line = tab-separated headers, subsequent lines = rows
        data: String,
        style: Style,
    },
}

// ─── Layout ──────────────────────────────────────────────────────────────────

/// Constraint type byte values (must match C# Constraint.Type enum)
const CONSTRAINT_LENGTH: u8 = 0;
const CONSTRAINT_MIN: u8 = 1;
const CONSTRAINT_MAX: u8 = 2;
const CONSTRAINT_PERCENTAGE: u8 = 3;
#[allow(dead_code)]
const CONSTRAINT_FILL: u8 = 4;

/// Splits `area_id` and writes the resulting child area IDs into `out_ids`.
/// `out_ids` must point to a buffer of at least `constraint_count` u32 elements.
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
        .map(|(&t, &v)| match t {
            CONSTRAINT_LENGTH => Constraint::Length(v),
            CONSTRAINT_MIN => Constraint::Min(v),
            CONSTRAINT_MAX => Constraint::Max(v),
            CONSTRAINT_PERCENTAGE => Constraint::Percentage(v),
            _ => Constraint::Fill(v),
        })
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
                            let block = Block::default()
                                .title(title.as_str())
                                .borders(borders_from_u8(*borders))
                                .style(*style);
                            frame.render_widget(block, area);
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

                    WidgetCommand::List { area_id, items, style, .. } => {
                        if let Some(&area) = area_map.get(area_id) {
                            let list_items: Vec<ListItem> = items
                                .lines()
                                .map(|s| ListItem::new(s))
                                .collect();
                            let list = List::new(list_items).style(*style);
                            frame.render_widget(list, area);
                        }
                    }

                    WidgetCommand::Gauge { area_id, ratio, label, style } => {
                        if let Some(&area) = area_map.get(area_id) {
                            let gauge = Gauge::default()
                                .ratio(ratio.clamp(0.0, 1.0))
                                .label(label.as_str())
                                .style(*style);
                            frame.render_widget(gauge, area);
                        }
                    }

                    WidgetCommand::Tabs { area_id, titles, selected, style } => {
                        if let Some(&area) = area_map.get(area_id) {
                            let tab_lines: Vec<Line> = titles
                                .lines()
                                .map(|t| Line::from(t.to_owned()))
                                .collect();
                            let tabs = Tabs::new(tab_lines)
                                .select(*selected as usize)
                                .style(*style);
                            frame.render_widget(tabs, area);
                        }
                    }

                    WidgetCommand::Sparkline { area_id, data, style } => {
                        if let Some(&area) = area_map.get(area_id) {
                            let sparkline = Sparkline::default()
                                .data(data)
                                .style(*style);
                            frame.render_widget(sparkline, area);
                        }
                    }

                    WidgetCommand::Table { area_id, data, style } => {
                        if let Some(&area) = area_map.get(area_id) {
                            render_table(frame, area, data, *style);
                        }
                    }
                }
            }
        })
        .expect("terminal draw failed");

    state.commands = commands;
}

fn render_table(
    frame: &mut ratatui::Frame,
    area: Rect,
    data: &str,
    style: Style,
) {
    let mut lines = data.lines();
    let headers: Vec<&str> = lines
        .next()
        .unwrap_or("")
        .split('\t')
        .collect();
    let col_count = headers.len().max(1);

    let header_row = Row::new(headers.iter().map(|h| Cell::from(*h)));
    let rows: Vec<Row> = lines
        .map(|line| {
            let cells: Vec<Cell> = line.split('\t').map(|c| Cell::from(c)).collect();
            Row::new(cells)
        })
        .collect();

    let equal_width = 100u16 / col_count as u16;
    let widths: Vec<Constraint> = (0..col_count)
        .map(|_| Constraint::Percentage(equal_width))
        .collect();

    let table = Table::new(rows, widths)
        .header(header_row)
        .style(style);

    frame.render_widget(table, area);
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

fn borders_from_u8(b: u8) -> Borders {
    if b == 0x0F {
        return Borders::ALL;
    }
    let mut borders = Borders::NONE;
    if b & 0x01 != 0 {
        borders |= Borders::TOP;
    }
    if b & 0x02 != 0 {
        borders |= Borders::BOTTOM;
    }
    if b & 0x04 != 0 {
        borders |= Borders::LEFT;
    }
    if b & 0x08 != 0 {
        borders |= Borders::RIGHT;
    }
    borders
}

fn alignment_from_u8(a: u8) -> Alignment {
    match a {
        1 => Alignment::Center,
        2 => Alignment::Right,
        _ => Alignment::Left,
    }
}
