//! # ratatui_unity
//!
//! A C ABI wrapper around [`ratatui`] that renders terminal UIs to RGB24
//! pixel buffers, suitable for embedding in game engines (e.g. Unity).
//!
//! The crate is compiled as both `cdylib` and `staticlib` so that it can be
//! consumed from any host capable of calling C functions. All public entry
//! points are `extern "C"` and `#[no_mangle]`; there is no idiomatic Rust API.
//!
//! ## High-level flow
//!
//! 1. Create a terminal handle with [`ratatui_create`].
//! 2. For each frame:
//!    - Call [`ratatui_begin_frame`] to reset per-frame state.
//!    - Build a layout tree with [`ratatui_split`] / [`ratatui_inner`].
//!    - Optionally set a style with [`ratatui_set_style`] before any
//!      widget call.
//!    - Queue widget commands (e.g. [`ratatui_block`], [`ratatui_paragraph`],
//!      [`ratatui_chart_begin`] / [`ratatui_chart_end`], …).
//!    - Call [`ratatui_end_frame`] (or [`ratatui_end_frame_hashed`]) to draw
//!      the queue and rasterize the cell grid into an RGB24 pixel buffer.
//! 3. When done, call [`ratatui_destroy`] to release the handle.
//!
//! ## Memory & lifetime
//!
//! The handle returned by [`ratatui_create`] is an opaque pointer to a
//! heap-allocated `TerminalState`. The pixel
//! buffer pointer returned by `ratatui_end_frame*` is owned by the handle and
//! is only valid until the next FFI call that mutates the handle. The caller
//! must copy the bytes before issuing further calls if it wants to retain them.
//!
//! ## Safety
//!
//! All FFI entry points perform null-pointer checks on the handle and on any
//! pointer argument they dereference. Strings are read as null-terminated
//! `*const c_char`. Slices passed as `(ptr, len)` pairs must reference valid
//! memory for the duration of the call.
//!
//! ## Source layout
//!
//! The C ABI surface lives in `ffi` (split into `lifecycle`, `layout`,
//! `style`, `widgets`, `builders`, and shared `util` helpers) and is
//! re-exported here. The non-FFI internals live in `terminal`, `commands`,
//! `renderer`, `font`, and `color`.

mod color;
mod commands;
mod ffi;
mod font;
mod renderer;
mod terminal;

pub use ffi::*;

#[cfg(test)]
mod tests {
    // Panicking via unwrap/expect is the standard way to fail a test.
    #![allow(clippy::unwrap_used)]
    use super::*;
    use crate::ffi::util::state_ref;
    use std::ffi::CString;

    /// Regression: 65536 tab-separated columns used to truncate `col_count`
    /// to 0 in the u16 cast and panic with a division by zero — and, before
    /// the column cap, stalled the layout solver for hours.
    #[test]
    fn table_with_more_than_u16_max_columns_does_not_panic() {
        let handle = ratatui_create(10, 5, 14.0);
        ratatui_begin_frame(handle);
        let data = CString::new(vec!["h"; 65536].join("\t")).unwrap();
        ratatui_table(handle, 0, data.as_ptr());
        ratatui_end_frame(handle);
        ratatui_destroy(handle);
    }

    /// Regression: invalid font bytes must be rejected without panicking
    /// (the crate is built with `panic = "abort"` in release).
    #[test]
    fn set_custom_font_with_invalid_bytes_returns_zero() {
        let handle = ratatui_create(10, 5, 14.0);
        let bytes = [0u8; 16];
        assert_eq!(ratatui_set_custom_font(handle, bytes.as_ptr(), bytes.len() as u32), 0);
        ratatui_destroy(handle);
    }

    /// Regression: after a successful font swap the reported pixel dimensions
    /// must match the rasterized buffer size.
    #[test]
    fn set_custom_font_resyncs_pixel_dimensions() {
        let handle = ratatui_create(10, 5, 14.0);
        let bytes = include_bytes!("../fonts/JetBrainsMono-Regular.ttf");
        assert_eq!(
            ratatui_set_custom_font(handle, bytes.as_ptr(), bytes.len() as u32),
            1
        );
        let w = ratatui_pixel_width(handle);
        let h = ratatui_pixel_height(handle);
        ratatui_begin_frame(handle);
        ratatui_end_frame(handle);
        let state = state_ref(handle).unwrap();
        assert_eq!(state.pixel_buffer.len(), w as usize * h as usize * 3);
        ratatui_destroy(handle);
    }
}
