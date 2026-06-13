//! Layout splitting, inner-margin areas, and input hit-testing.

use crate::commands::do_split;
use crate::ffi::util::{slice_from, slice_mut_from, state_mut, state_ref};
use std::ffi::c_void;

/// Returns the id of the root area, which always covers the whole terminal.
///
/// The root id is the constant `0`; this getter exists for symmetry with the
/// host-side area API.
#[no_mangle]
pub extern "C" fn ratatui_root_area(_handle: *const c_void) -> u32 { 0 }

/// Splits an existing area into `count` child areas and writes their ids into
/// `out_ids`.
///
/// # Parameters
/// - `area_id`: id of the parent area to split.
/// - `direction`: `0` = horizontal split (left → right), any other value =
///   vertical split (top → bottom).
/// - `constraint_types`: array of length `count` describing each child's
///   constraint kind. Values: `0` = Length, `1` = Min, `2` = Max,
///   `3` = Percentage, `4` (or any other) = Fill.
/// - `constraint_values`: array of length `count` with the numeric value
///   matching the constraint kind (cells for Length/Min/Max, 0..=100 for
///   Percentage, weight for Fill).
/// - `count`: number of child areas requested.
/// - `out_ids`: caller-allocated buffer of length `count` that receives the
///   ids of the newly registered child areas.
///
/// # Returns
/// The number of child areas actually written. Returns `0` if any required
/// pointer is null, `count` is zero, or the parent id is unknown.
#[no_mangle]
pub extern "C" fn ratatui_split(
    handle: *mut c_void,
    area_id: u32,
    direction: u8,
    constraint_types: *const u8,
    constraint_values: *const u16,
    count: u32,
    out_ids: *mut u32,
) -> u32 {
    if count == 0
        || constraint_types.is_null()
        || constraint_values.is_null()
        || out_ids.is_null()
    {
        return 0;
    }
    let Some(state) = state_mut(handle) else { return 0; };
    let n = count as usize;
    let types = slice_from(constraint_types, n);
    let values = slice_from(constraint_values, n);
    let Some(out) = slice_mut_from(out_ids, n) else { return 0; };
    do_split(state, area_id, direction, types, values, out)
}

/// Returns a new area id covering the inner rectangle of `area_id` shrunk by
/// the given margins on each side.
///
/// # Parameters
/// - `area_id`: parent area id.
/// - `horizontal`: cells to remove from the left and right edges.
/// - `vertical`: cells to remove from the top and bottom edges.
///
/// # Returns
/// The id of the newly registered inner area, or [`u32::MAX`] if `handle` is
/// null or `area_id` is unknown.
#[no_mangle]
pub extern "C" fn ratatui_inner(
    handle: *mut c_void,
    area_id: u32,
    horizontal: u16,
    vertical: u16,
) -> u32 {
    let Some(state) = state_mut(handle) else { return u32::MAX; };
    let area = match state.area_map.get(&area_id).copied() {
        Some(r) => r,
        None => return u32::MAX,
    };
    use ratatui::layout::Margin;
    let inner = area.inner(Margin { horizontal, vertical });
    state.register_area(inner)
}

/// Returns the most specific area id covering the given terminal cell.
///
/// When several registered areas contain `(col, row)` the one with the
/// smallest cell count (the most deeply nested) wins. Returns `0` (root) when
/// no registered area matches.
///
/// Useful for mapping pointer input back into the layout tree.
#[no_mangle]
pub extern "C" fn ratatui_hit_test(
    handle: *mut c_void,
    col: u16,
    row: u16,
) -> u32 {
    let Some(state) = state_mut(handle) else { return 0; };
    let mut best_id = 0u32;
    let mut best_area = u32::MAX;

    for (&id, &rect) in &state.area_map {
        if col >= rect.x && col < rect.x + rect.width
            && row >= rect.y && row < rect.y + rect.height
        {
            let area = (rect.width as u32) * (rect.height as u32);
            if area < best_area {
                best_area = area;
                best_id = id;
            }
        }
    }
    best_id
}

/// Returns the cell-space rectangle of the given area as a packed `u64`.
///
/// The four `u16` fields are packed little-endian:
///
/// ```text
/// bits  0..16  -> x
/// bits 16..32  -> y
/// bits 32..48  -> width
/// bits 48..64  -> height
/// ```
///
/// Returns `0` if `handle` is null or the area id is unknown.
#[no_mangle]
pub extern "C" fn ratatui_get_area_rect(
    handle: *const c_void,
    area_id: u32,
) -> u64 {
    let Some(state) = state_ref(handle) else { return 0; };
    match state.area_map.get(&area_id) {
        Some(rect) => {
            (rect.x as u64)
                | ((rect.y as u64) << 16)
                | ((rect.width as u64) << 32)
                | ((rect.height as u64) << 48)
        }
        None => 0,
    }
}
