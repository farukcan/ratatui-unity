use crate::font::FontManager;
use ratatui::{backend::TestBackend, layout::Rect, style::Style, Terminal};
use std::collections::HashMap;

// ─── Shared data types (used by WidgetCommand variants and pending builders) ──

pub struct AxisInfo {
    pub title: String,
    pub min: f64,
    pub max: f64,
}

pub struct DatasetInfo {
    pub name: String,
    pub marker: u8,
    pub r: u8,
    pub g: u8,
    pub b: u8,
    /// Interleaved (x, y) pairs.
    pub points: Vec<(f64, f64)>,
}

pub struct SpanInfo {
    pub text: String,
    pub style: Style,
}

pub enum CanvasShape {
    Map { resolution: u8 },
    Layer,
    Line      { x1: f64, y1: f64, x2: f64, y2: f64, r: u8, g: u8, b: u8 },
    Circle    { x: f64, y: f64, radius: f64, r: u8, g: u8, b: u8 },
    Rectangle { x: f64, y: f64, w: f64, h: f64, r: u8, g: u8, b: u8 },
    Text      { x: f64, y: f64, text: String, r: u8, g: u8, b: u8 },
    Points    { coords: Vec<(f64, f64)>, r: u8, g: u8, b: u8 },
}

// ─── Widget command queue ─────────────────────────────────────────────────────

pub enum WidgetCommand {
    Block {
        area_id: u32,
        title: String,
        borders: u8,
        style: Style,
    },
    Paragraph {
        area_id: u32,
        text: String,
        alignment: u8,
        wrap: bool,
        style: Style,
    },
    StyledParagraph {
        area_id: u32,
        alignment: u8,
        wrap: bool,
        lines: Vec<Vec<SpanInfo>>,
    },
    List {
        area_id: u32,
        /// Newline-separated items.
        items: String,
        selected: i32,
        style: Style,
    },
    Gauge {
        area_id: u32,
        ratio: f64,
        label: String,
        style: Style,
    },
    LineGauge {
        area_id: u32,
        ratio: f64,
        label: String,
        style: Style,
    },
    Tabs {
        area_id: u32,
        /// Newline-separated tab titles.
        titles: String,
        selected: u32,
        style: Style,
    },
    Sparkline {
        area_id: u32,
        data: Vec<u64>,
        style: Style,
    },
    BarChart {
        area_id: u32,
        /// (label, value) pairs.
        bars: Vec<(String, u64)>,
        bar_width: u16,
        bar_gap: u16,
        style: Style,
    },
    Table {
        area_id: u32,
        /// First line = tab-separated headers, subsequent lines = tab-separated rows.
        data: String,
        style: Style,
    },
    TableEx {
        area_id: u32,
        data: String,
        /// (constraint_type, value) pairs for column widths.
        col_constraints: Vec<(u8, u16)>,
        selected_row: i32,
        style: Style,
    },
    Scrollbar {
        area_id: u32,
        content_length: u32,
        position: u32,
        viewport_length: u32,
        /// 0=VerticalRight, 1=VerticalLeft, 2=HorizontalBottom, 3=HorizontalTop
        orientation: u8,
    },
    Calendar {
        area_id: u32,
        year: i32,
        month: u8,
        day: u8,
    },
    Chart {
        area_id: u32,
        x_axis: Option<AxisInfo>,
        y_axis: Option<AxisInfo>,
        datasets: Vec<DatasetInfo>,
    },
    Canvas {
        area_id: u32,
        x_min: f64,
        x_max: f64,
        y_min: f64,
        y_max: f64,
        /// 0=Dot, 1=Braille, 2=HalfBlock, 3=Block
        marker: u8,
        shapes: Vec<CanvasShape>,
    },
}

// ─── Pending builder state ────────────────────────────────────────────────────

pub struct PendingStyledParagraph {
    pub area_id: u32,
    pub alignment: u8,
    pub wrap: bool,
    /// Each inner Vec is one line; each SpanInfo is one styled span.
    pub lines: Vec<Vec<SpanInfo>>,
}

pub struct PendingChart {
    pub area_id: u32,
    pub x_axis: Option<AxisInfo>,
    pub y_axis: Option<AxisInfo>,
    pub datasets: Vec<DatasetInfo>,
}

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

pub struct TerminalState {
    pub terminal: Terminal<TestBackend>,
    pub font: FontManager,
    /// Maps area ID → Rect in terminal cell coordinates.
    pub area_map: HashMap<u32, Rect>,
    pub next_area_id: u32,
    /// Queued widget commands for the current frame.
    pub commands: Vec<WidgetCommand>,
    /// Style applied to the next widget command.
    pub pending_style: Style,
    /// Last computed pixel buffer (RGBA32).
    pub pixel_buffer: Vec<u8>,
    pub pixel_width: u32,
    pub pixel_height: u32,
    // ── Builder state (accumulated across FFI calls, flushed on _end) ──
    pub pending_styled_para: Option<PendingStyledParagraph>,
    pub pending_chart: Option<PendingChart>,
    pub pending_canvas: Option<PendingCanvas>,
}

impl TerminalState {
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
            pixel_buffer: vec![0u8; (pixel_width * pixel_height * 4) as usize],
            pixel_width,
            pixel_height,
            pending_styled_para: None,
            pending_chart: None,
            pending_canvas: None,
        }
    }

    /// Reset per-frame state: clear command queue and area map (keep root area).
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

    /// Assign the next sequential ID to a new area rect and store it.
    pub fn register_area(&mut self, rect: Rect) -> u32 {
        let id = self.next_area_id;
        self.area_map.insert(id, rect);
        self.next_area_id += 1;
        id
    }

    /// Consume the pending style (reset to default after use).
    pub fn take_style(&mut self) -> Style {
        std::mem::replace(&mut self.pending_style, Style::default())
    }
}
