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

  ratatui_inner: function(handle, areaId, horizontal, vertical) {
    return _ratatui_inner(handle, areaId, horizontal, vertical);
  },

  // ── New widgets ────────────────────────────────────────────────────────────

  ratatui_barchart: function(handle, areaId, data, barWidth, barGap) {
    _ratatui_barchart(handle, areaId, data, barWidth, barGap);
  },

  ratatui_line_gauge: function(handle, areaId, ratio, label) {
    _ratatui_line_gauge(handle, areaId, ratio, label);
  },

  ratatui_scrollbar: function(handle, areaId, contentLength, position, viewportLength, orientation) {
    _ratatui_scrollbar(handle, areaId, contentLength, position, viewportLength, orientation);
  },

  ratatui_calendar: function(handle, areaId, year, month, day) {
    _ratatui_calendar(handle, areaId, year, month, day);
  },

  ratatui_table_ex: function(handle, areaId, data, colTypes, colValues, colCount, selectedRow) {
    _ratatui_table_ex(handle, areaId, data, colTypes, colValues, colCount, selectedRow);
  },

  // ── StyledParagraph builder ────────────────────────────────────────────────

  ratatui_styled_para_begin: function(handle, areaId, alignment, wrap) {
    _ratatui_styled_para_begin(handle, areaId, alignment, wrap);
  },

  ratatui_styled_para_span: function(handle, text, fgR, fgG, fgB, useDefaultFg, bgR, bgG, bgB, useDefaultBg, modifiers) {
    _ratatui_styled_para_span(handle, text, fgR, fgG, fgB, useDefaultFg, bgR, bgG, bgB, useDefaultBg, modifiers);
  },

  ratatui_styled_para_newline: function(handle) {
    _ratatui_styled_para_newline(handle);
  },

  ratatui_styled_para_end: function(handle) {
    _ratatui_styled_para_end(handle);
  },

  // ── Chart builder ──────────────────────────────────────────────────────────

  ratatui_chart_begin: function(handle, areaId) {
    _ratatui_chart_begin(handle, areaId);
  },

  ratatui_chart_x_axis: function(handle, title, min, max) {
    _ratatui_chart_x_axis(handle, title, min, max);
  },

  ratatui_chart_y_axis: function(handle, title, min, max) {
    _ratatui_chart_y_axis(handle, title, min, max);
  },

  ratatui_chart_dataset: function(handle, name, marker, r, g, b, data, pointCount) {
    _ratatui_chart_dataset(handle, name, marker, r, g, b, data, pointCount);
  },

  ratatui_chart_end: function(handle) {
    _ratatui_chart_end(handle);
  },

  // ── Canvas builder ─────────────────────────────────────────────────────────

  ratatui_canvas_begin: function(handle, areaId, xMin, xMax, yMin, yMax, marker) {
    _ratatui_canvas_begin(handle, areaId, xMin, xMax, yMin, yMax, marker);
  },

  ratatui_canvas_map: function(handle, resolution) {
    _ratatui_canvas_map(handle, resolution);
  },

  ratatui_canvas_layer: function(handle) {
    _ratatui_canvas_layer(handle);
  },

  ratatui_canvas_line: function(handle, x1, y1, x2, y2, r, g, b) {
    _ratatui_canvas_line(handle, x1, y1, x2, y2, r, g, b);
  },

  ratatui_canvas_circle: function(handle, x, y, radius, r, g, b) {
    _ratatui_canvas_circle(handle, x, y, radius, r, g, b);
  },

  ratatui_canvas_rectangle: function(handle, x, y, w, h, r, g, b) {
    _ratatui_canvas_rectangle(handle, x, y, w, h, r, g, b);
  },

  ratatui_canvas_text: function(handle, x, y, text, r, g, b) {
    _ratatui_canvas_text(handle, x, y, text, r, g, b);
  },

  ratatui_canvas_points: function(handle, coords, count, r, g, b) {
    _ratatui_canvas_points(handle, coords, count, r, g, b);
  },

  ratatui_canvas_end: function(handle) {
    _ratatui_canvas_end(handle);
  },

});
