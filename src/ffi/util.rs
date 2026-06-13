//! Shared helpers for the FFI entry points.
//!
//! These confine every raw-pointer dereference behind a small, documented
//! surface so the public `extern "C"` functions in the sibling modules stay
//! free of `unsafe`.

use crate::terminal::TerminalState;
use ratatui::style::{Color, Modifier, Style};
use std::ffi::{c_void, CStr};
use std::os::raw::c_char;

/// Borrows an opaque handle as a mutable [`TerminalState`], or `None` if null.
///
/// Centralizes the raw-pointer dereference for the mutable FFI entry points so
/// their bodies stay free of `unsafe`.
///
/// # Preconditions
///
/// A non-null `handle` must be a pointer previously returned by
/// [`ratatui_create`](crate::ratatui_create) and not yet passed to
/// [`ratatui_destroy`](crate::ratatui_destroy). The borrow's lifetime is
/// unbounded; callers must ensure no other reference to the same state is
/// active for the duration of the returned borrow.
pub(crate) fn state_mut<'a>(handle: *mut c_void) -> Option<&'a mut TerminalState> {
    if handle.is_null() {
        return None;
    }
    // SAFETY: handle is non-null (checked above) and, per the contract above,
    // points to a live TerminalState allocated by ratatui_create.
    Some(unsafe { &mut *(handle as *mut TerminalState) })
}

/// Borrows an opaque handle as a shared [`TerminalState`], or `None` if null.
///
/// The read-only counterpart of [`state_mut`], used by the getters.
///
/// # Preconditions
///
/// Same preconditions as [`state_mut`].
pub(crate) fn state_ref<'a>(handle: *const c_void) -> Option<&'a TerminalState> {
    if handle.is_null() {
        return None;
    }
    // SAFETY: handle is non-null (checked above) and points to a live
    // TerminalState allocated by ratatui_create.
    Some(unsafe { &*(handle as *const TerminalState) })
}

/// Copies a nullable null-terminated C string into an owned [`String`].
///
/// Returns an empty `String` when `ptr` is null. Invalid UTF-8 is replaced
/// using [`String::from_utf8_lossy`].
///
/// # Preconditions
///
/// `ptr`, if non-null, must point to a valid null-terminated byte sequence
/// that stays alive for the duration of the call.
pub(crate) fn cstr_to_string(ptr: *const c_char) -> String {
    if ptr.is_null() {
        return String::new();
    }
    // SAFETY: ptr is non-null (checked above) and, per the contract above,
    // references a valid null-terminated string for the duration of the call.
    unsafe { CStr::from_ptr(ptr).to_string_lossy().into_owned() }
}

/// Builds a shared slice from an FFI `(ptr, len)` pair, or an empty slice when
/// `ptr` is null.
///
/// # Preconditions
///
/// If `ptr` is non-null it must reference `len` consecutive initialized values
/// of type `T` that stay valid for the duration of the call.
pub(crate) fn slice_from<'a, T>(ptr: *const T, len: usize) -> &'a [T] {
    if ptr.is_null() {
        return &[];
    }
    // SAFETY: ptr is non-null (checked above) and, per the contract above,
    // references `len` valid elements for the duration of the call.
    unsafe { std::slice::from_raw_parts(ptr, len) }
}

/// Builds a mutable slice from an FFI `(ptr, len)` pair, or `None` when `ptr`
/// is null.
///
/// # Preconditions
///
/// If `ptr` is non-null it must uniquely reference `len` consecutive
/// initialized values of type `T`, valid for the duration of the call.
pub(crate) fn slice_mut_from<'a, T>(ptr: *mut T, len: usize) -> Option<&'a mut [T]> {
    if ptr.is_null() {
        return None;
    }
    // SAFETY: ptr is non-null (checked above) and, per the contract above,
    // uniquely references `len` valid elements for the duration of the call.
    Some(unsafe { std::slice::from_raw_parts_mut(ptr, len) })
}

/// Builds a ratatui [`Style`] from the packed FFI representation.
///
/// `use_default_fg` / `use_default_bg`: non-zero means "leave foreground /
/// background unset so the terminal default is used"; zero means apply the
/// given RGB triple.
///
/// `modifiers` is a bit field:
/// - `0x01` Bold
/// - `0x02` Italic
/// - `0x04` Underlined
/// - `0x08` Dim
pub(crate) fn style_from_rgba(
    fg_r: u8, fg_g: u8, fg_b: u8, use_default_fg: u8,
    bg_r: u8, bg_g: u8, bg_b: u8, use_default_bg: u8,
    modifiers: u8,
) -> Style {
    let mut style = Style::default();
    if use_default_fg == 0 { style = style.fg(Color::Rgb(fg_r, fg_g, fg_b)); }
    if use_default_bg == 0 { style = style.bg(Color::Rgb(bg_r, bg_g, bg_b)); }
    let mut modifier = Modifier::empty();
    if modifiers & 0x01 != 0 { modifier |= Modifier::BOLD; }
    if modifiers & 0x02 != 0 { modifier |= Modifier::ITALIC; }
    if modifiers & 0x04 != 0 { modifier |= Modifier::UNDERLINED; }
    if modifiers & 0x08 != 0 { modifier |= Modifier::DIM; }
    if !modifier.is_empty() { style = style.add_modifier(modifier); }
    style
}
