// WebGL JS glue layer for ratatui_unity.
//
// Unity's Emscripten pipeline links the Rust static lib (.a) directly, so all
// ratatui_* C symbols are already available in the WASM module.  This jslib is
// only needed to satisfy Unity's IL2CPP/WebGL import mechanism — it delegates
// every call to the underlying Emscripten-exported Rust functions.

mergeInto(LibraryManager.library, {

  ratatui_create: function(cols, rows, fontSize) {
    return _ratatui_create(cols, rows, fontSize);
  },

  ratatui_destroy: function(handle) {
    _ratatui_destroy(handle);
  },

  ratatui_set_custom_font: function(handle, fontData, fontLen) {
    return _ratatui_set_custom_font(handle, fontData, fontLen);
  },

  ratatui_begin_frame: function(handle) {
    _ratatui_begin_frame(handle);
  },

  ratatui_end_frame: function(handle) {
    return _ratatui_end_frame(handle);
  },

  ratatui_pixel_width: function(handle) {
    return _ratatui_pixel_width(handle);
  },

  ratatui_pixel_height: function(handle) {
    return _ratatui_pixel_height(handle);
  },

  ratatui_root_area: function(handle) {
    return _ratatui_root_area(handle);
  },

  ratatui_split: function(handle, areaId, direction, constraintTypes, constraintValues, count, outIds) {
    return _ratatui_split(handle, areaId, direction, constraintTypes, constraintValues, count, outIds);
  },

  ratatui_set_style: function(handle, fgR, fgG, fgB, useDefaultFg, bgR, bgG, bgB, useDefaultBg, modifiers) {
    _ratatui_set_style(handle, fgR, fgG, fgB, useDefaultFg, bgR, bgG, bgB, useDefaultBg, modifiers);
  },

  ratatui_block: function(handle, areaId, title, borders) {
    _ratatui_block(handle, areaId, title, borders);
  },

  ratatui_paragraph: function(handle, areaId, text, alignment, wrap) {
    _ratatui_paragraph(handle, areaId, text, alignment, wrap);
  },

  ratatui_list: function(handle, areaId, items, selected) {
    _ratatui_list(handle, areaId, items, selected);
  },

  ratatui_gauge: function(handle, areaId, ratio, label) {
    _ratatui_gauge(handle, areaId, ratio, label);
  },

  ratatui_tabs: function(handle, areaId, titles, selected) {
    _ratatui_tabs(handle, areaId, titles, selected);
  },

  ratatui_sparkline: function(handle, areaId, data, len) {
    _ratatui_sparkline(handle, areaId, data, len);
  },

  ratatui_table: function(handle, areaId, data) {
    _ratatui_table(handle, areaId, data);
  },

  ratatui_version: function() {
    return _ratatui_version();
  },

});
