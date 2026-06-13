//! Chart rendering from queued axes and datasets.

use crate::commands::decode::marker_from_u8;
use crate::terminal::{AxisInfo, DatasetInfo};
use ratatui::layout::Rect;
use ratatui::style::{Color, Style};
use ratatui::widgets::{Axis, Chart, Dataset};

/// Builds a [`Chart`] from optional axes and datasets and renders it.
///
/// Axis titles use a gray foreground; dataset colors come from each
/// [`DatasetInfo`]. Markers map via [`marker_from_u8`].
pub(crate) fn render_chart(
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
