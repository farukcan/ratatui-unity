use crate::commands::WidgetCommand;
use crate::font::FontManager;
use ratatui::{backend::TestBackend, layout::Rect, style::Style, Terminal};
use std::collections::HashMap;

pub struct TerminalState {
    pub terminal: Terminal<TestBackend>,
    pub font: FontManager,
    /// Maps area ID -> Rect in terminal cell coordinates
    pub area_map: HashMap<u32, Rect>,
    pub next_area_id: u32,
    /// Queued widget commands for the current frame
    pub commands: Vec<WidgetCommand>,
    /// Style to apply to the next widget command
    pub pending_style: Style,
    /// Last computed pixel buffer (RGBA32)
    pub pixel_buffer: Vec<u8>,
    pub pixel_width: u32,
    pub pixel_height: u32,
}

impl TerminalState {
    pub fn new(cols: u16, rows: u16, font_size: f32) -> Self {
        let backend = TestBackend::new(cols, rows);
        let terminal = Terminal::new(backend).expect("Failed to create terminal");
        let font = FontManager::new(font_size);

        let pixel_width = cols as u32 * font.cell_width;
        let pixel_height = rows as u32 * font.cell_height;

        let mut area_map = HashMap::new();
        // Area ID 0 always refers to the full terminal rect
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
