//! Canvas rendering by replaying queued shape primitives.

use crate::commands::decode::marker_from_u8;
use crate::terminal::CanvasShape;
use ratatui::layout::Rect;
use ratatui::style::{Color, Style};
use ratatui::text::Span;
use ratatui::widgets::canvas::{self, Canvas, Circle, Map, MapResolution, Rectangle};

/// Renders a canvas widget by replaying every queued [`CanvasShape`].
///
/// `CanvasShape::Layer` triggers a `ctx.layer()` flush so subsequent shapes
/// draw on top. Text shapes clone their string because `ctx.print` requires
/// `Into<Line<'static>>`.
pub(crate) fn render_canvas(
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
