//! C ABI entry points, grouped by concern.
//!
//! Every public function in the submodules is `#[no_mangle] extern "C"`. The
//! crate root re-exports them all (`pub use ffi::*`) so the flat C symbol set
//! is unchanged regardless of how the functions are split across files.

pub(crate) mod util;

mod builders;
mod layout;
mod lifecycle;
mod style;
mod widgets;

pub use builders::*;
pub use layout::*;
pub use lifecycle::*;
pub use style::*;
pub use widgets::*;
