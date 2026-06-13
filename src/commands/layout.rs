//! Area splitting and constraint decoding.

use crate::terminal::TerminalState;
use ratatui::layout::{Constraint, Direction, Layout};

/// Constraint type discriminant for [`Constraint::Length`].
const CONSTRAINT_LENGTH: u8 = 0;
/// Constraint type discriminant for [`Constraint::Min`].
const CONSTRAINT_MIN: u8 = 1;
/// Constraint type discriminant for [`Constraint::Max`].
const CONSTRAINT_MAX: u8 = 2;
/// Constraint type discriminant for [`Constraint::Percentage`].
const CONSTRAINT_PERCENTAGE: u8 = 3;
/// Constraint type discriminant for [`Constraint::Fill`].
///
/// The decoder treats any value other than the named discriminants as Fill,
/// so this constant is unused at compile time but kept for documentation.
#[allow(dead_code)]
const CONSTRAINT_FILL: u8 = 4;

/// Splits an existing area into child areas and registers each child in the
/// state's area map.
///
/// # Parameters
/// - `area_id`: parent area id.
/// - `direction`: `0` horizontal, anything else vertical.
/// - `constraint_types` / `constraint_values`: parallel slices describing
///   each child's constraint kind and value. See [`constraint_from_bytes`].
/// - `out_ids`: caller-allocated buffer that receives the new area ids in
///   layout order.
///
/// Returns the number of child ids actually written, which is
/// `min(constraint_types.len(), constraint_values.len(), out_ids.len(),
/// computed_chunks.len())`. Returns `0` when the parent id is unknown.
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

/// Decodes a `(constraint_type, value)` pair into a ratatui [`Constraint`].
///
/// Mapping:
/// - `0` → [`Constraint::Length`]
/// - `1` → [`Constraint::Min`]
/// - `2` → [`Constraint::Max`]
/// - `3` → [`Constraint::Percentage`]
/// - any other → [`Constraint::Fill`]
pub(crate) fn constraint_from_bytes(t: u8, v: u16) -> Constraint {
    match t {
        CONSTRAINT_LENGTH => Constraint::Length(v),
        CONSTRAINT_MIN => Constraint::Min(v),
        CONSTRAINT_MAX => Constraint::Max(v),
        CONSTRAINT_PERCENTAGE => Constraint::Percentage(v),
        _ => Constraint::Fill(v),
    }
}
