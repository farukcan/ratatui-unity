//! Layout splitting and frame-replay logic.
//!
//! The FFI layer at the crate root queues [`WidgetCommand`]s into
//! [`TerminalState::commands`] and registers areas via [`do_split`]. At
//! end-of-frame [`render_all_commands`] replays the queue into a single
//! `terminal.draw()` call, materializing each command as a concrete ratatui
//! widget. Per-widget rendering lives in the topic submodules (`table`,
//! `calendar`, `chart`, `canvas`), constraint/area math in `layout`, and the
//! packed-code decoders in `decode`.

mod calendar;
mod canvas;
mod chart;
mod decode;
mod layout;
mod table;

pub use layout::do_split;

use crate::terminal::{TerminalState, WidgetCommand};
use ratatui::style::{Color, Modifier, Style};
use ratatui::text::{Line, Span};
use ratatui::widgets::{
    Bar, BarChart, BarGroup, Block, Gauge, LineGauge, List, ListItem, ListState, Paragraph,
    Scrollbar, ScrollbarState, Sparkline, Tabs, Wrap,
};

use calendar::render_calendar;
use canvas::render_canvas;
use chart::render_chart;
use decode::{alignment_from_u8, borders_from_u8, scrollbar_orientation_from_u8};
use table::{render_table, render_table_ex};

/// Replays all queued [`WidgetCommand`]s inside a single
/// [`Terminal::draw`](ratatui::Terminal::draw) call.
///
/// The command queue and the area map are taken out of `state` to avoid
/// double-borrowing the terminal during the draw closure, then the area map
/// is restored. The queue is left empty afterwards;
/// [`crate::ratatui_begin_frame`] would clear it anyway.
///
/// Commands whose `area_id` is not present in the area map are silently
/// skipped.
///
/// # Panics
///
/// Panics if the underlying ratatui terminal draw call returns an error,
/// which on a [`TestBackend`](ratatui::backend::TestBackend) is unreachable
/// in practice.
pub fn render_all_commands(state: &mut TerminalState) {
    let commands = std::mem::take(&mut state.commands);
    let area_map = std::mem::take(&mut state.area_map);

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
                            let highlight = Style::default()
                                .fg(Color::Black)
                                .bg(style.fg.unwrap_or(Color::Cyan))
                                .add_modifier(Modifier::BOLD);
                            frame.render_widget(
                                Tabs::new(tab_lines)
                                    .select(*selected as usize)
                                    .style(*style)
                                    .highlight_style(highlight),
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
                            render_table_ex(
                                frame, area, data, col_constraints, *selected_row, *style,
                            );
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
                            render_canvas(
                                frame, area, *x_min, *x_max, *y_min, *y_max, *marker, shapes,
                            );
                        }
                    }
                }
            }
        })
        .expect("terminal draw failed");

    state.area_map = area_map;
    // commands are intentionally dropped here — begin_frame() would
    // clear them anyway
}
