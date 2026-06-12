//! Per-handle terminal state and queued-command data model.
//!
//! This module owns the data that survives between FFI calls on a single
//! handle: the ratatui [`Terminal`] backed by [`TestBackend`], the
//! [`FontManager`], a map from area id to [`Rect`], the per-frame command
//! queue, the pending builder state, and the rasterized pixel buffer.
//!
//! The types here have no behavior beyond plain field access; the actual
//! widget rendering lives in [`crate::commands`] and the pixel-level
//! rasterization in [`crate::renderer`].

use crate::font::FontManager;
use ratatui::{backend::TestBackend, layout::Rect, style::Style, Terminal};
use std::collections::HashMap;

// ─── Shared data types (used by WidgetCommand variants and pending builders) ──

/// Description of a chart axis (title and `[min, max]` data bounds).
pub struct AxisInfo {
    /// Axis title rendered alongside the axis.
    pub title: String,
    /// Lower bound of the data range mapped onto the axis.
    pub min: f64,
    /// Upper bound of the data range mapped onto the axis.
    pub max: f64,
}

/// One dataset within a [`WidgetCommand::Chart`].
pub struct DatasetInfo {
    /// Legend label.
    pub name: String,
    /// Marker style: `0` Dot, `1` Braille, `2` HalfBlock, `3` Block.
    pub marker: u8,
    /// Red component of the dataset color.
    pub r: u8,
    /// Green component of the dataset color.
    pub g: u8,
    /// Blue component of the dataset color.
    pub b: u8,
    /// Plotted points as `(x, y)` pairs.
    pub points: Vec<(f64, f64)>,
}

/// A single styled run of text inside a styled paragraph.
pub struct SpanInfo {
    /// Run contents.
    pub text: String,
    /// Style applied to this run.
    pub style: Style,
}

/// One drawing primitive queued into a [`WidgetCommand::Canvas`].
///
/// Coordinates are in data space; the canvas widget maps them onto the
/// allocated area based on its `[x_min, x_max] × [y_min, y_max]` bounds.
pub enum CanvasShape {
    /// World map. `resolution`: `0` Low, any other value High.
    Map { resolution: u8 },
    /// Flushes the current layer so following shapes draw on top.
    Layer,
    /// Line segment from `(x1, y1)` to `(x2, y2)`.
    Line      { x1: f64, y1: f64, x2: f64, y2: f64, r: u8, g: u8, b: u8 },
    /// Circle centered at `(x, y)` with `radius`.
    Circle    { x: f64, y: f64, radius: f64, r: u8, g: u8, b: u8 },
    /// Rectangle outline anchored at `(x, y)` with size `(w, h)`.
    Rectangle { x: f64, y: f64, w: f64, h: f64, r: u8, g: u8, b: u8 },
    /// Text label anchored at `(x, y)`.
    Text      { x: f64, y: f64, text: String, r: u8, g: u8, b: u8 },
    /// Point cloud rendered with the canvas marker style.
    Points    { coords: Vec<(f64, f64)>, r: u8, g: u8, b: u8 },
}

// ─── Widget command queue ─────────────────────────────────────────────────────

/// A single widget request queued during a frame.
///
/// Commands are appended by the FFI layer (see [`crate::lib`]) and replayed
/// inside one `terminal.draw()` by [`crate::commands::render_all_commands`].
/// Each variant carries the target `area_id` plus the parameters needed to
/// build the corresponding ratatui widget.
pub enum WidgetCommand {
    /// A [`Block`](ratatui::widgets::Block) with an optional title and a
    /// per-edge border bit field (see [`crate::ratatui_block`]).
    Block {
        area_id: u32,
        title: String,
        borders: u8,
        style: Style,
    },
    /// A uniformly styled [`Paragraph`](ratatui::widgets::Paragraph).
    Paragraph {
        area_id: u32,
        text: String,
        alignment: u8,
        wrap: bool,
        style: Style,
    },
    /// A multi-style [`Paragraph`](ratatui::widgets::Paragraph) built from
    /// per-line span lists.
    StyledParagraph {
        area_id: u32,
        alignment: u8,
        wrap: bool,
        /// One inner [`Vec`] per line; each [`SpanInfo`] is one styled run.
        lines: Vec<Vec<SpanInfo>>,
    },
    /// A [`List`](ratatui::widgets::List) with an optional highlighted row.
    List {
        area_id: u32,
        /// Newline-separated items.
        items: String,
        /// Zero-based highlighted index, or `-1` for no selection.
        selected: i32,
        style: Style,
    },
    /// A block-style [`Gauge`](ratatui::widgets::Gauge).
    Gauge {
        area_id: u32,
        ratio: f64,
        label: String,
        style: Style,
    },
    /// A horizontal single-line [`LineGauge`](ratatui::widgets::LineGauge).
    LineGauge {
        area_id: u32,
        ratio: f64,
        label: String,
        style: Style,
    },
    /// A [`Tabs`](ratatui::widgets::Tabs) bar with one highlighted entry.
    Tabs {
        area_id: u32,
        /// Newline-separated tab titles.
        titles: String,
        /// Zero-based index of the active tab.
        selected: u32,
        style: Style,
    },
    /// A [`Sparkline`](ratatui::widgets::Sparkline) of raw `u64` samples.
    Sparkline {
        area_id: u32,
        data: Vec<u64>,
        style: Style,
    },
    /// A [`BarChart`](ratatui::widgets::BarChart) with fixed bar/gap widths.
    BarChart {
        area_id: u32,
        /// `(label, value)` pairs, one per bar.
        bars: Vec<(String, u64)>,
        bar_width: u16,
        bar_gap: u16,
        style: Style,
    },
    /// A [`Table`](ratatui::widgets::Table) with equal-width columns.
    Table {
        area_id: u32,
        /// First line = tab-separated headers, subsequent lines = tab-separated rows.
        data: String,
        style: Style,
    },
    /// A [`Table`](ratatui::widgets::Table) with typed column widths and
    /// optional row highlighting.
    TableEx {
        area_id: u32,
        data: String,
        /// `(constraint_type, value)` pairs for column widths. Empty means
        /// equal distribution. The constraint encoding is the same as
        /// [`crate::ratatui_split`].
        col_constraints: Vec<(u8, u16)>,
        /// Zero-based highlighted row, or `-1` for none.
        selected_row: i32,
        style: Style,
    },
    /// A [`Scrollbar`](ratatui::widgets::Scrollbar) showing scroll position.
    Scrollbar {
        area_id: u32,
        content_length: u32,
        position: u32,
        viewport_length: u32,
        /// `0` VerticalRight, `1` VerticalLeft, `2` HorizontalBottom,
        /// `3` HorizontalTop.
        orientation: u8,
    },
    /// A monthly calendar
    /// ([`Monthly`](ratatui::widgets::calendar::Monthly)).
    Calendar {
        area_id: u32,
        year: i32,
        /// `1..=12`.
        month: u8,
        /// `1..=28` (clamped at the FFI boundary).
        day: u8,
    },
    /// A [`Chart`](ratatui::widgets::Chart) with optional axes and one or
    /// more datasets.
    Chart {
        area_id: u32,
        x_axis: Option<AxisInfo>,
        y_axis: Option<AxisInfo>,
        datasets: Vec<DatasetInfo>,
    },
    /// A [`Canvas`](ratatui::widgets::canvas::Canvas) painted from a queued
    /// list of shape primitives.
    Canvas {
        area_id: u32,
        x_min: f64,
        x_max: f64,
        y_min: f64,
        y_max: f64,
        /// `0` Dot, `1` Braille, `2` HalfBlock, `3` Block.
        marker: u8,
        shapes: Vec<CanvasShape>,
    },
}

// ─── Pending builder state ────────────────────────────────────────────────────

/// Accumulated state for a styled paragraph being constructed over multiple
/// FFI calls (see [`crate::ratatui_styled_para_begin`]).
pub struct PendingStyledParagraph {
    pub area_id: u32,
    pub alignment: u8,
    pub wrap: bool,
    /// Each inner [`Vec`] is one line; each [`SpanInfo`] is one styled span.
    pub lines: Vec<Vec<SpanInfo>>,
}

/// Accumulated state for a chart being constructed over multiple FFI calls
/// (see [`crate::ratatui_chart_begin`]).
pub struct PendingChart {
    pub area_id: u32,
    pub x_axis: Option<AxisInfo>,
    pub y_axis: Option<AxisInfo>,
    pub datasets: Vec<DatasetInfo>,
}

/// Accumulated state for a canvas being constructed over multiple FFI calls
/// (see [`crate::ratatui_canvas_begin`]).
pub struct PendingCanvas {
    pub area_id: u32,
    pub x_min: f64,
    pub x_max: f64,
    pub y_min: f64,
    pub y_max: f64,
    pub marker: u8,
    pub shapes: Vec<CanvasShape>,
}

// ─── Terminal state ───────────────────────────────────────────────────────────

/// Everything an FFI handle owns: ratatui terminal, font, layout map,
/// per-frame command queue, builder state, and the rasterized pixel buffer.
///
/// Instances are heap-allocated, leaked via [`Box::into_raw`] in
/// [`crate::ratatui_create`], and reclaimed in
/// [`crate::ratatui_destroy`].
pub struct TerminalState {
    /// Ratatui terminal driving an in-memory [`TestBackend`].
    pub terminal: Terminal<TestBackend>,
    /// Glyph cache and metrics for the active font.
    pub font: FontManager,
    /// Maps area id → [`Rect`] in terminal cell coordinates. The root area
    /// (id `0`) always covers the full terminal.
    pub area_map: HashMap<u32, Rect>,
    /// Next id to hand out from [`Self::register_area`].
    pub next_area_id: u32,
    /// Widget commands queued for the current frame, replayed at end-of-frame.
    pub commands: Vec<WidgetCommand>,
    /// Style applied to (and consumed by) the next style-accepting widget call.
    pub pending_style: Style,
    /// Last rasterized RGB24 pixel buffer (`pixel_width * pixel_height * 3` bytes).
    pub pixel_buffer: Vec<u8>,
    /// Pixel-buffer width in pixels.
    pub pixel_width: u32,
    /// Pixel-buffer height in pixels.
    pub pixel_height: u32,
    // ── Builder state (accumulated across FFI calls, flushed on _end) ──
    /// Active styled-paragraph builder, if any.
    pub pending_styled_para: Option<PendingStyledParagraph>,
    /// Active chart builder, if any.
    pub pending_chart: Option<PendingChart>,
    /// Active canvas builder, if any.
    pub pending_canvas: Option<PendingCanvas>,
    /// RGB background color used during rasterization for cells whose
    /// background is [`ratatui::style::Color::Reset`]. Defaults to
    /// [`crate::color::DEFAULT_BG`].
    pub background_color: [u8; 3],
    /// Hash of the ratatui [`Buffer`](ratatui::buffer::Buffer) from the
    /// previous frame, used by [`crate::ratatui_end_frame_hashed`] to
    /// skip rasterization on unchanged frames. `None` on the first frame to
    /// guarantee the initial render runs.
    pub last_buffer_hash: Option<u64>,
}

impl TerminalState {
    /// Builds a fresh terminal state with the given cell grid and font size.
    ///
    /// The pixel buffer is pre-allocated to
    /// `cols * cell_width * rows * cell_height * 3` bytes, and the root area
    /// (id `0`) is registered to cover the whole grid.
    ///
    /// # Panics
    ///
    /// Panics if the embedded font fails to load or has no horizontal line
    /// metrics. Both are unreachable in practice because the font is
    /// compile-time embedded.
    pub fn new(cols: u16, rows: u16, font_size: f32) -> Self {
        let backend = TestBackend::new(cols, rows);
        let terminal = Terminal::new(backend).expect("Failed to create terminal");
        let font = FontManager::new(font_size);

        let pixel_width = cols as u32 * font.cell_width;
        let pixel_height = rows as u32 * font.cell_height;

        let mut area_map = HashMap::new();
        area_map.insert(0u32, Rect::new(0, 0, cols, rows));

        Self {
            terminal,
            font,
            area_map,
            next_area_id: 1,
            commands: Vec::new(),
            pending_style: Style::default(),
            // Multiply in usize: the u32 product can overflow for large
            // cols × rows × font_size combinations.
            pixel_buffer: vec![0u8; pixel_width as usize * pixel_height as usize * 3],
            pixel_width,
            pixel_height,
            pending_styled_para: None,
            pending_chart: None,
            pending_canvas: None,
            background_color: crate::color::DEFAULT_BG,
            last_buffer_hash: None,
        }
    }

    /// Resets per-frame state.
    ///
    /// Clears the command queue, drops in-progress builders, resets the
    /// pending style, and shrinks the area map back to just the root area
    /// (id `0`). Persistent state — font, pixel buffer, background color,
    /// and the dirty-check hash — is preserved.
    pub fn begin_frame(&mut self) {
        self.commands.clear();
        let root = self.area_map[&0];
        self.area_map.clear();
        self.area_map.insert(0, root);
        self.next_area_id = 1;
        self.pending_style = Style::default();
        self.pending_styled_para = None;
        self.pending_chart = None;
        self.pending_canvas = None;
    }

    /// Assigns the next sequential id to `rect`, inserts it into the area
    /// map, and returns the new id.
    pub fn register_area(&mut self, rect: Rect) -> u32 {
        let id = self.next_area_id;
        self.area_map.insert(id, rect);
        self.next_area_id += 1;
        id
    }

    /// Returns the current [`Self::pending_style`] and resets it to
    /// [`Style::default`].
    pub fn take_style(&mut self) -> Style {
        std::mem::replace(&mut self.pending_style, Style::default())
    }

    /// Recomputes [`Self::pixel_width`] / [`Self::pixel_height`] from the
    /// current font metrics and resizes the pixel buffer to match. Must be
    /// called whenever the font (and thus the cell dimensions) changes so
    /// the reported dimensions stay consistent with the buffer contents.
    ///
    /// Resets the dirty-check hash so the next `ratatui_end_frame_hashed`
    /// call rasterizes with the new metrics.
    pub fn resync_pixel_dimensions(&mut self) {
        let area = *self.terminal.backend().buffer().area();
        self.pixel_width = area.width as u32 * self.font.cell_width;
        self.pixel_height = area.height as u32 * self.font.cell_height;
        self.pixel_buffer =
            vec![0u8; self.pixel_width as usize * self.pixel_height as usize * 3];
        self.last_buffer_hash = None;
    }

    /// Rasterizes the current ratatui [`Buffer`](ratatui::buffer::Buffer)
    /// into [`Self::pixel_buffer`] using
    /// [`crate::renderer::render_buffer_to_pixels`].
    ///
    /// Uses disjoint field borrows so the cell buffer is not cloned.
    pub fn rasterize(&mut self) {
        let buffer = self.terminal.backend().buffer();
        crate::renderer::render_buffer_to_pixels(
            buffer,
            &mut self.font,
            &mut self.pixel_buffer,
            self.background_color,
        );
    }
}
