//! Handle lifecycle, frame rendering, pixel-buffer queries, and version.

use crate::commands::render_all_commands;
use crate::ffi::util::{slice_from, state_mut, state_ref};
use crate::terminal::TerminalState;
use std::ffi::c_void;
use std::os::raw::c_char;

/// Creates a terminal instance and returns an opaque handle.
///
/// The resulting handle owns:
/// - a ratatui [`Terminal`](ratatui::Terminal) backed by
///   [`TestBackend`](ratatui::backend::TestBackend) sized `cols × rows`
///   cells,
/// - a glyph-cached `FontManager` at `font_size` pixels,
/// - a pre-allocated RGB24 pixel buffer matching `cols × rows × cell_size`.
///
/// The handle must eventually be released with [`ratatui_destroy`].
///
/// # Parameters
/// - `cols`: terminal width in character cells.
/// - `rows`: terminal height in character cells.
/// - `font_size`: glyph rasterization size in pixels (e.g. `14.0`).
#[no_mangle]
pub extern "C" fn ratatui_create(cols: u16, rows: u16, font_size: f32) -> *mut c_void {
    let state = Box::new(TerminalState::new(cols, rows, font_size));
    Box::into_raw(state) as *mut c_void
}

/// Destroys a terminal handle created by [`ratatui_create`].
///
/// After this call the handle and any previously returned pixel-buffer
/// pointers are invalid and must not be used. A null handle is a no-op.
#[no_mangle]
pub extern "C" fn ratatui_destroy(handle: *mut c_void) {
    if !handle.is_null() {
        // SAFETY: handle is non-null (checked above) and, per contract, was
        // produced by ratatui_create and not yet destroyed; reclaiming the
        // Box frees the allocation exactly once.
        unsafe { drop(Box::from_raw(handle as *mut TerminalState)); }
    }
}

/// Replaces the embedded JetBrains Mono font with custom TTF bytes.
///
/// The cell width/height are recomputed from the new font's metrics, the
/// glyph cache is dropped, and the pixel buffer is resized to the new
/// `cols × cell_size` dimensions. After a successful call the values
/// reported by [`ratatui_pixel_width`] / [`ratatui_pixel_height`] change
/// accordingly; callers must re-query them and resize any host-side
/// texture before the next `ratatui_end_frame*` call.
///
/// # Returns
/// `1` on success, `0` if `handle` is null, `font_data` is null/empty, or the
/// bytes are not a valid font.
#[no_mangle]
pub extern "C" fn ratatui_set_custom_font(
    handle: *mut c_void,
    font_data: *const u8,
    font_len: u32,
) -> u8 {
    if font_data.is_null() || font_len == 0 { return 0; }
    let Some(state) = state_mut(handle) else { return 0; };
    let bytes = slice_from(font_data, font_len as usize);
    let ok = state.font.set_custom_font(bytes);
    if ok {
        state.resync_pixel_dimensions();
    }
    u8::from(ok)
}

/// Begins a new frame.
///
/// Clears the queued widget command list, drops any in-progress builder state
/// (styled paragraph, chart, canvas), resets the pending style to default, and
/// resets the area map so that only the root area (id `0`) remains. Must be
/// called before issuing widget commands for the new frame.
#[no_mangle]
pub extern "C" fn ratatui_begin_frame(handle: *mut c_void) {
    let Some(state) = state_mut(handle) else { return; };
    state.begin_frame();
}

/// Renders all queued widget commands and rasterizes the cell buffer.
///
/// The returned pointer addresses a flat RGB24 buffer of size
/// `pixel_width * pixel_height * 3` bytes, owned by the handle. The pointer is
/// valid until the next FFI call that mutates the handle (typically the next
/// `ratatui_end_frame*` call), at which point the buffer may be overwritten.
///
/// Returns `null` only when `handle` is null.
#[no_mangle]
pub extern "C" fn ratatui_end_frame(handle: *mut c_void) -> *const u8 {
    let Some(state) = state_mut(handle) else { return std::ptr::null(); };
    render_all_commands(state);
    state.rasterize();
    state.pixel_buffer.as_ptr()
}

/// Like `ratatui_end_frame`, but skips rasterization when the cell buffer
/// is unchanged from the previous frame (hash-based dirty check).
/// Returns a valid pixel pointer when content changed, or null when unchanged.
/// The previous frame's pixel buffer remains valid when null is returned.
#[no_mangle]
pub extern "C" fn ratatui_end_frame_hashed(handle: *mut c_void) -> *const u8 {
    let Some(state) = state_mut(handle) else { return std::ptr::null(); };
    render_all_commands(state);

    let hash = {
        let buffer = state.terminal.backend().buffer();
        crate::renderer::compute_buffer_hash(buffer)
    };

    if state.last_buffer_hash == Some(hash) {
        return std::ptr::null();
    }

    state.last_buffer_hash = Some(hash);
    state.rasterize();
    state.pixel_buffer.as_ptr()
}

/// Returns the width of the pixel buffer in pixels (`cols * cell_width`).
///
/// Returns `0` if `handle` is null.
#[no_mangle]
pub extern "C" fn ratatui_pixel_width(handle: *const c_void) -> u32 {
    let Some(state) = state_ref(handle) else { return 0; };
    state.pixel_width
}

/// Returns the height of the pixel buffer in pixels (`rows * cell_height`).
///
/// Returns `0` if `handle` is null.
#[no_mangle]
pub extern "C" fn ratatui_pixel_height(handle: *const c_void) -> u32 {
    let Some(state) = state_ref(handle) else { return 0; };
    state.pixel_height
}

/// Returns the library version as a static null-terminated C string.
///
/// The returned pointer is valid for the lifetime of the process and must
/// not be freed by the caller. The value matches `CARGO_PKG_VERSION`.
#[no_mangle]
pub extern "C" fn ratatui_version() -> *const c_char {
    concat!(env!("CARGO_PKG_VERSION"), "\0").as_ptr() as *const c_char
}
