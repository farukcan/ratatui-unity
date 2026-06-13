//! Monthly calendar rendering with date fallbacks.

use ratatui::layout::Rect;
use ratatui::style::Style;

/// Renders a monthly calendar for the given date.
///
/// Invalid dates fall back to January 1 of `year`; if even that fails, the
/// renderer uses 2024-01-01 as a last-resort hardcoded date.
pub(crate) fn render_calendar(
    frame: &mut ratatui::Frame,
    area: Rect,
    year: i32,
    month: u8,
    day: u8,
) {
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

/// Maps a `1..=12` month index to [`time::Month`]; out-of-range values fall
/// back to January.
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
