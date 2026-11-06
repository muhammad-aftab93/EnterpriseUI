using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using EnterpriseUI.Grids;

namespace EnterpriseUI.WinForms;

public partial class EnterpriseDataGrid<TItem>
{
    private static readonly TextFormatFlags LeftText = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var p = GetPalette();
        var f = ComputeFrame();
        g.Clear(p.Surface);

        DrawGroupPanel(g, f, p);
        DrawBands(g, f, p);
        DrawHeader(g, f, p);
        DrawFilterRow(g, f, p);
        DrawBody(g, f, p);
        DrawFocusCell(g, f, p);
        DrawPinDividers(g, f, p);
        DrawSummaryRows(g, f, p);
        DrawStatusBar(g, f, p);
        DrawPager(g, f, p);
        DrawDragFeedback(g, f, p);
        DrawRowDropIndicator(g, f, p);

        using var outline = new Pen(Focused ? p.FocusRing : p.Border, Focused ? Math.Max(2, S(2)) : 1);
        g.DrawRectangle(outline, 0, 0, Width - 1, Height - 1);
    }

    // ------------------------------------------------------------------
    // Group panel, bands, header, filter row
    // ------------------------------------------------------------------

    private void DrawGroupPanel(Graphics g, Frame f, Colors p)
    {
        if (f.GroupH == 0) return;

        using var bg = new SolidBrush(p.Header);
        using var line = new Pen(p.Border);
        g.FillRectangle(bg, 0, f.GroupTop, Width, f.GroupH);
        g.DrawLine(line, 0, f.GroupTop + f.GroupH - 1, Width, f.GroupTop + f.GroupH - 1);

        if (_engine.CurrentRequest.Groups.Count == 0)
        {
            TextRenderer.DrawText(g, "Drag a column header here or right-click a header and choose Group By", Font, new Rectangle(S(10), f.GroupTop, Width - S(20), f.GroupH), p.Muted, LeftText);
            return;
        }

        foreach (var chip in GetGroupChips(f))
        {
            using var chipBrush = new SolidBrush(p.Primary);
            using var path = RoundedRect(chip.Bounds, S(4));
            g.FillPath(chipBrush, path);
            TextRenderer.DrawText(g, chip.Member, HeaderFont, new Rectangle(chip.Bounds.X + S(10), chip.Bounds.Y, chip.Bounds.Width - S(30), chip.Bounds.Height), p.PrimaryText, LeftText);

            using var cross = new Pen(p.PrimaryText, 1.5f);
            var cx = chip.Close.X + chip.Close.Width / 2;
            var cy = chip.Close.Y + chip.Close.Height / 2;
            var r = S(3);
            g.DrawLine(cross, cx - r, cy - r, cx + r, cy + r);
            g.DrawLine(cross, cx - r, cy + r, cx + r, cy - r);
        }
    }

    private static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var d = Math.Max(1, radius * 2);
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void DrawBands(Graphics g, Frame f, Colors p)
    {
        if (f.BandDepth == 0) return;

        using var bandBrush = new SolidBrush(Blend(p.Surface, p.Primary, p.HighContrast ? 0 : 0.10));
        using var line = new Pen(p.Border);
        g.FillRectangle(bandBrush, 0, f.BandTop, Width, f.BandDepth * f.BandRowH);

        var scrollRegion = new Rectangle((int)f.Layout.ScrollableLeft + f.Gutter, f.BandTop, Math.Max(0, (int)(f.Layout.ScrollableRight - f.Layout.ScrollableLeft)), f.BandDepth * f.BandRowH);
        foreach (var span in f.Bands)
        {
            var state = g.Save();
            if (span.Zone == ColumnZone.Scrollable) g.SetClip(scrollRegion);

            var rect = new Rectangle((int)span.Left + f.Gutter, f.BandTop + span.Level * f.BandRowH, (int)span.Width, f.BandRowH);
            if (span.Zone != ColumnZone.Scrollable) g.FillRectangle(bandBrush, rect);
            g.DrawRectangle(line, rect);

            var textRect = span.Zone == ColumnZone.Scrollable ? Rectangle.Intersect(Rectangle.Inflate(rect, -S(6), 0), scrollRegion) : Rectangle.Inflate(rect, -S(6), 0);
            TextRenderer.DrawText(g, span.Band.Title, HeaderFont, textRect, p.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            g.Restore(state);
        }
    }

    private void DrawHeader(Graphics g, Frame f, Colors p)
    {
        using var bg = new SolidBrush(p.Header);
        using var line = new Pen(p.Border);
        g.FillRectangle(bg, 0, f.HeaderTop, Width, f.HeaderH);
        g.DrawLine(line, 0, f.HeaderTop + f.HeaderH - 1, Width, f.HeaderTop + f.HeaderH - 1);

        if (f.Gutter > 0) g.DrawLine(line, f.Gutter, f.HeaderTop, f.Gutter, f.HeaderTop + f.HeaderH);
        if (f.CheckW > 0) DrawCheckBox(g, p, HeaderCheckRect(f), HeaderCheckState(), true);

        var layout = f.Layout;
        for (var zonePass = 0; zonePass < 2; zonePass++)
        {
            var state = g.Save();
            if (zonePass == 0)
            {
                g.SetClip(new Rectangle((int)layout.ScrollableLeft + f.Gutter, f.HeaderTop, Math.Max(0, (int)(layout.ScrollableRight - layout.ScrollableLeft)), f.HeaderH));
            }

            for (var i = 0; i < layout.Slots.Count; i++)
            {
                var slot = layout.Slots[i];
                if ((slot.Zone == ColumnZone.Scrollable) != (zonePass == 0)) continue;
                DrawHeaderCell(g, f, p, slot, i);
            }
            g.Restore(state);
        }
    }

    private void DrawHeaderCell(Graphics g, Frame f, Colors p, ColumnSlot slot, int index)
    {
        var rect = new Rectangle((int)slot.Left + f.Gutter, f.HeaderTop, (int)slot.Width, f.HeaderH);
        var column = slot.Column;
        var selected = _engine.Selection.IsColumnSelected(ColumnIndexOf(f, column));
        var text = selected ? p.SelectionText : p.Text;

        var back = selected ? p.SelectionBack : index == _hoveredHeader && _drag == DragMode.None ? Blend(p.Header, p.Primary, 0.08) : p.Header;
        using var bg = new SolidBrush(back);
        using var line = new Pen(p.Border);
        g.FillRectangle(bg, rect);
        g.DrawLine(line, rect.Right - 1, rect.Y + S(6), rect.Right - 1, rect.Bottom - S(6));

        var filterable = _engine.EnableFiltering && column.Filterable;
        var reserved = S(8);
        if (filterable) reserved += S(20);
        if (column.SortDirection != null) reserved += S(column.SortIndex >= 0 && _engine.CurrentRequest.Sorts.Count > 1 ? 24 : 16);

        var title = string.IsNullOrEmpty(column.Title) ? column.Field : column.Title;
        TextRenderer.DrawText(g, title, HeaderFont, new Rectangle(rect.X + S(8), rect.Y, Math.Max(0, rect.Width - reserved - S(8)), rect.Height), text, FlagsFor(column.Alignment == ColumnTextAlignment.Right ? ColumnTextAlignment.Right : ColumnTextAlignment.Left));

        var right = rect.Right - S(8);
        if (filterable)
        {
            var icon = FilterIconRect(slot, f);
            DrawFunnel(g, icon, _engine.HasFilter(column.Field) || _engine.QuickFilters.ContainsKey(column.Field) ? (selected ? p.SelectionText : p.Primary) : p.Muted);
            right = icon.X - S(4);
        }

        if (column.SortDirection != null)
        {
            var asc = column.SortDirection == ListSortDirection.Ascending;
            var size = S(6);
            var cy = rect.Y + rect.Height / 2;
            var showIndex = column.SortIndex >= 0 && _engine.CurrentRequest.Sorts.Count > 1;
            var arrowRight = right - (showIndex ? S(10) : 0);
            var pts = asc
                ? new[] { new Point(arrowRight - size * 2, cy + size / 2), new Point(arrowRight, cy + size / 2), new Point(arrowRight - size, cy - size / 2 - 1) }
                : new[] { new Point(arrowRight - size * 2, cy - size / 2), new Point(arrowRight, cy - size / 2), new Point(arrowRight - size, cy + size / 2 + 1) };
            using var arrow = new SolidBrush(selected ? p.SelectionText : p.Primary);
            g.FillPolygon(arrow, pts);

            if (showIndex)
            {
                TextRenderer.DrawText(g, (column.SortIndex + 1).ToString(CultureInfo.CurrentCulture), SmallFont, new Rectangle(arrowRight + S(2), rect.Y, S(10), rect.Height), p.Primary, LeftText);
            }
        }
    }

    private static void DrawFunnel(Graphics g, Rectangle r, Color color)
    {
        using var brush = new SolidBrush(color);
        var pts = new[]
        {
            new PointF(r.X, r.Y + r.Height * 0.15f),
            new PointF(r.Right, r.Y + r.Height * 0.15f),
            new PointF(r.X + r.Width * 0.62f, r.Y + r.Height * 0.55f),
            new PointF(r.X + r.Width * 0.62f, r.Bottom - r.Height * 0.1f),
            new PointF(r.X + r.Width * 0.38f, r.Bottom - r.Height * 0.25f),
            new PointF(r.X + r.Width * 0.38f, r.Y + r.Height * 0.55f)
        };
        g.FillPolygon(brush, pts);
    }

    private void DrawFilterRow(Graphics g, Frame f, Colors p)
    {
        if (f.FilterH == 0) return;

        using var bg = new SolidBrush(Blend(p.Surface, p.Primary, p.HighContrast ? 0 : 0.04));
        using var line = new Pen(p.Border);
        g.FillRectangle(bg, 0, f.FilterTop, Width, f.FilterH);
        g.DrawLine(line, 0, f.FilterTop + f.FilterH - 1, Width, f.FilterTop + f.FilterH - 1);

        var scrollRegion = new Rectangle((int)f.Layout.ScrollableLeft + f.Gutter, f.FilterTop, Math.Max(0, (int)(f.Layout.ScrollableRight - f.Layout.ScrollableLeft)), f.FilterH);
        foreach (var slot in f.Layout.Slots)
        {
            var state = g.Save();
            if (slot.Zone == ColumnZone.Scrollable) g.SetClip(scrollRegion);

            var rect = new Rectangle((int)slot.Left + f.Gutter, f.FilterTop, (int)slot.Width, f.FilterH);
            if (slot.Zone != ColumnZone.Scrollable) g.FillRectangle(bg, rect);
            g.DrawLine(line, rect.Right - 1, rect.Y, rect.Right - 1, rect.Bottom);

            if (slot.Column.Filterable)
            {
                var has = _engine.QuickFilters.TryGetValue(slot.Column.Field, out var text);
                TextRenderer.DrawText(g, has ? text! : "Filter...", Font, Rectangle.Inflate(rect, -S(8), 0), has ? p.Text : p.Muted, LeftText);
            }
            g.Restore(state);
        }
    }

    // ------------------------------------------------------------------
    // Body
    // ------------------------------------------------------------------

    private void DrawBody(Graphics g, Frame f, Colors p)
    {
        var bodyRect = new Rectangle(0, f.BodyTop, f.BodyWidth, Math.Max(0, f.BodyBottom - f.BodyTop));
        var state = g.Save();
        g.SetClip(bodyRect);

        using var grid = new Pen(p.HighContrast ? p.Border : Blend(p.Surface, p.Border, 0.7));
        var rowRules = _engine.FormatRules.Where(r => r.Enabled && r.ApplyToRow && r.RuleType == FormatRuleType.HighlightCell).ToArray();
        var cellRules = _engine.FormatRules
            .Where(r => r.Enabled && !r.ApplyToRow)
            .GroupBy(r => r.ColumnField, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.OrdinalIgnoreCase);

        var drewAny = false;
        foreach (var slot in EnumerateRows(f))
        {
            drewAny = true;
            var row = slot.Row;
            if (row == null)
            {
                TextRenderer.DrawText(g, "Loading...", Font, new Rectangle(f.Gutter + S(8), slot.Bounds.Y, slot.Bounds.Width, slot.Bounds.Height), p.Muted, LeftText);
                continue;
            }

            switch (row.Kind)
            {
                case GridRowKind.Data:
                    DrawDataRow(g, f, p, slot, row, rowRules, cellRules, grid, bodyRect);
                    break;
                case GridRowKind.GroupHeader:
                    DrawGroupHeaderRow(g, f, p, slot, row);
                    break;
                case GridRowKind.GroupFooter:
                    DrawGroupFooterRow(g, f, p, slot, row, grid);
                    break;
                case GridRowKind.DetailHeader:
                    DrawDetailHeaderRow(g, f, p, slot, row);
                    break;
                case GridRowKind.DetailData:
                    DrawDetailDataRow(g, f, p, slot, row, grid);
                    break;
                case GridRowKind.DetailSummary:
                    DrawDetailSummaryRow(g, f, p, slot, row);
                    break;
            }
        }

        if (!drewAny)
        {
            var message = _engine.LastError != null && !_engine.IsLoading ? $"Data could not be loaded: {_engine.LastError.Message}"
                : _engine.IsLoading ? "Loading..."
                : "No records to display";
            TextRenderer.DrawText(g, message, Font, bodyRect, _engine.LastError != null ? p.Danger : p.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
        }

        g.Restore(state);
    }

    private void DrawDataRow(Graphics g, Frame f, Colors p, RowSlot slot, GridRow<TItem> row, GridFormatRule[] rowRules, Dictionary<string, GridFormatRule[]> cellRules, Pen grid, Rectangle bodyRect)
    {
        var selection = _engine.Selection;
        var item = row.Item!;
        var rowSelected = !IsCellSelectionMode && selection.IsRowSelected(slot.Index);
        var style = _engine.GetRowStyle(item, slot.Index);

        var rowColor = _engine.EnableAlternateRows && slot.Index % 2 == 1 && !p.HighContrast ? Blend(p.Surface, p.Primary, 0.03) : p.Surface;
        foreach (var rule in rowRules)
        {
            if (GridFormatRuleEvaluator.Matches(rule, rule.ColumnField == null ? null : FindColumnValue(row, rule.ColumnField)))
            {
                rowColor = GetRuleColor(rule.BackgroundColorHex, Blend(p.Surface, p.Primary, 0.12));
            }
        }

        if (!string.IsNullOrWhiteSpace(style?.BackColorHex)) rowColor = GetRuleColor(style!.BackColorHex, rowColor);
        if (rowSelected) rowColor = p.SelectionBack;
        else if (slot.Index == _hoveredRow) rowColor = p.HighContrast ? p.Header : Blend(rowColor, p.HoverBack, 0.8);

        var textColor = Enabled ? (rowSelected ? p.SelectionText : GetRuleColor(style?.ForeColorHex, p.Text)) : p.Muted;
        var bold = style?.Bold == true;

        using (var rowBrush = new SolidBrush(rowColor)) g.FillRectangle(rowBrush, slot.Bounds);

        DrawRowGutter(g, f, p, slot, row, rowColor);

        var scrollable = new Rectangle((int)f.Layout.ScrollableLeft + f.Gutter, slot.Bounds.Y, Math.Max(0, (int)(f.Layout.ScrollableRight - f.Layout.ScrollableLeft)), slot.Bounds.Height);
        var rowState = g.Save();
        g.SetClip(Rectangle.Intersect(bodyRect, scrollable));
        foreach (var column in f.Layout.Slots.Where(s => s.Zone == ColumnZone.Scrollable))
        {
            DrawCell(g, f, p, column, slot, row, rowColor, textColor, bold, cellRules, grid);
        }
        g.Restore(rowState);

        foreach (var column in f.Layout.Slots.Where(s => s.Zone != ColumnZone.Scrollable))
        {
            using var pinBrush = new SolidBrush(rowColor);
            g.FillRectangle(pinBrush, (int)column.Left + f.Gutter, slot.Bounds.Y, (int)column.Width, slot.Bounds.Height);
            DrawCell(g, f, p, column, slot, row, rowColor, textColor, bold, cellRules, grid);
        }

        g.DrawLine(grid, 0, slot.Bounds.Bottom - 1, f.BodyWidth, slot.Bounds.Bottom - 1);

        if (rowSelected && !p.HighContrast)
        {
            using var accent = new SolidBrush(p.Primary);
            g.FillRectangle(accent, 0, slot.Bounds.Y, Math.Max(2, S(3)), slot.Bounds.Height);
        }
    }

    private object? FindColumnValue(GridRow<TItem> row, string field)
    {
        var column = _engine.FindColumn(field);
        return column == null ? null : CellValue(row, column);
    }

    private void DrawRowGutter(Graphics g, Frame f, Colors p, RowSlot slot, GridRow<TItem> row, Color rowColor)
    {
        if (f.Gutter == 0) return;

        if (f.ExpanderW > 0 && row.IsExpandable && !_engine.IsTreeMode)
        {
            DrawExpander(g, p, new Rectangle(slot.Bounds.X, slot.Bounds.Y, f.ExpanderW, slot.Bounds.Height), row.IsExpanded, p.Muted);
        }

        if (f.CheckW > 0)
        {
            DrawCheckBox(g, p, RowCheckRect(f, slot.Bounds), _engine.Selection.IsRowSelected(slot.Index) ? CheckState.Checked : CheckState.Unchecked, true);
        }
    }

    private void DrawExpander(Graphics g, Colors p, Rectangle area, bool expanded, Color color)
    {
        var size = S(12);
        var box = new Rectangle(area.X + (area.Width - size) / 2, area.Y + (area.Height - size) / 2, size, size);
        using var pen = new Pen(color);
        g.DrawRectangle(pen, box);
        var cx = box.X + box.Width / 2;
        var cy = box.Y + box.Height / 2;
        g.DrawLine(pen, box.X + S(3), cy, box.Right - S(3), cy);
        if (!expanded) g.DrawLine(pen, cx, box.Y + S(3), cx, box.Bottom - S(3));
    }

    private void DrawChevron(Graphics g, Rectangle area, bool expanded, Color color)
    {
        var size = S(5);
        var cx = area.X + area.Width / 2;
        var cy = area.Y + area.Height / 2;
        using var pen = new Pen(color, Math.Max(1.5f, S(1.5)));
        if (expanded)
        {
            g.DrawLines(pen, new[] { new Point(cx - size, cy - size / 2), new Point(cx, cy + size / 2), new Point(cx + size, cy - size / 2) });
        }
        else
        {
            g.DrawLines(pen, new[] { new Point(cx - size / 2, cy - size), new Point(cx + size / 2, cy), new Point(cx - size / 2, cy + size) });
        }
    }

    private void DrawCheckBox(Graphics g, Colors p, Rectangle box, CheckState state, bool enabled)
    {
        var border = enabled ? p.Text : p.Muted;
        using var pen = new Pen(border, 1.25f);
        if (state != CheckState.Unchecked)
        {
            using var fill = new SolidBrush(enabled ? p.Primary : p.Muted);
            g.FillRectangle(fill, box);
        }
        g.DrawRectangle(pen, box);

        if (state == CheckState.Unchecked) return;

        using var mark = new Pen(p.PrimaryText, Math.Max(1.75f, S(1.75)));
        if (state == CheckState.Checked)
        {
            g.DrawLines(mark, new[]
            {
                new Point(box.X + box.Width * 22 / 100, box.Y + box.Height * 52 / 100),
                new Point(box.X + box.Width * 42 / 100, box.Y + box.Height * 72 / 100),
                new Point(box.X + box.Width * 78 / 100, box.Y + box.Height * 28 / 100)
            });
        }
        else
        {
            g.DrawLine(mark, box.X + S(3), box.Y + box.Height / 2, box.Right - S(3), box.Y + box.Height / 2);
        }
    }

    private void DrawCell(Graphics g, Frame f, Colors p, ColumnSlot colSlot, RowSlot slot, GridRow<TItem> row, Color rowColor, Color textColor, bool bold, Dictionary<string, GridFormatRule[]> cellRules, Pen grid)
    {
        var rect = new Rectangle((int)colSlot.Left + f.Gutter, slot.Bounds.Y, (int)colSlot.Width, slot.Bounds.Height);
        var column = colSlot.Column;
        var item = row.Item!;
        var value = CellValue(row, column);
        var columnIndex = ColumnIndexOf(f, column);
        var selection = _engine.Selection;

        var hasError = _engine.HasCellError(item!, column.Field);
        if (hasError)
        {
            using var errorBrush = new SolidBrush(p.ErrorBack);
            g.FillRectangle(errorBrush, rect);
        }
        else if (IsCellSelectionMode && selection.IsCellSelected(slot.Index, columnIndex))
        {
            using var selBrush = new SolidBrush(p.SelectionBack);
            g.FillRectangle(selBrush, rect);
            textColor = p.SelectionText;
        }

        if (cellRules.TryGetValue(column.Field, out var rules))
        {
            foreach (var rule in rules)
            {
                if (rule.RuleType == FormatRuleType.HighlightCell && GridFormatRuleEvaluator.Matches(rule, value))
                {
                    using var highlight = new SolidBrush(GetRuleColor(rule.BackgroundColorHex, Blend(p.Surface, p.Primary, 0.18)));
                    g.FillRectangle(highlight, rect);
                    if (!string.IsNullOrWhiteSpace(rule.ForegroundColorHex)) textColor = GetRuleColor(rule.ForegroundColorHex, textColor);
                }
                else if (rule.RuleType == FormatRuleType.DataBar && ShowConditionalDataBars &&
                    GridFormatRuleEvaluator.TryGetNormalizedValue(value, rule.Value1, rule.Value2, out var ratio))
                {
                    var barWidth = (int)Math.Round((rect.Width - S(8)) * ratio);
                    if (barWidth > 0)
                    {
                        using var bar = new SolidBrush(GetRuleColor(rule.BackgroundColorHex, Color.FromArgb(96, p.Primary)));
                        g.FillRectangle(bar, rect.X + S(4), rect.Y + S(4), barWidth, rect.Height - S(8));
                    }
                }
                else if (rule.RuleType == FormatRuleType.ColorScale &&
                    GridFormatRuleEvaluator.TryGetNormalizedValue(value, rule.Value1, rule.Value2, out var scale))
                {
                    var from = GetRuleColor(rule.BackgroundColorHex, p.Header);
                    var to = GetRuleColor(rule.ForegroundColorHex, p.Primary);
                    using var scaleBrush = new SolidBrush(Blend(from, to, scale));
                    g.FillRectangle(scaleBrush, rect);
                }
            }
        }

        g.DrawLine(grid, rect.Right - 1, rect.Y, rect.Right - 1, rect.Bottom);

        var inner = Rectangle.Inflate(rect, -S(8), 0);
        if (columnIndex == 0 || (f.Ordered.Count > 0 && ReferenceEquals(column, f.Ordered[0])))
        {
            inner = DrawTreeIndent(g, p, row, inner, textColor);
        }

        var type = ResolveType(column, value);
        var font = bold ? HeaderFont : Font;
        switch (type)
        {
            case GridColumnType.Boolean:
                var box = new Rectangle(rect.X + (rect.Width - S(14)) / 2, rect.Y + (rect.Height - S(14)) / 2, S(14), S(14));
                DrawCheckBox(g, p, box, value is true ? CheckState.Checked : CheckState.Unchecked, column.IsEditable && _engine.EditMode != GridEditMode.None);
                break;

            case GridColumnType.Progress:
                DrawProgress(g, p, rect, value, _engine.FormatCellValue(column, value), textColor);
                break;

            case GridColumnType.Hyperlink:
                using (var linkFont = new Font(font, font.Style | FontStyle.Underline))
                {
                    TextRenderer.DrawText(g, _engine.FormatCellValue(column, value), linkFont, inner, p.HighContrast ? p.Text : p.Primary, FlagsFor(column.Alignment));
                }
                break;

            case GridColumnType.Command:
                DrawCommandButton(g, p, rect, string.IsNullOrEmpty(column.Title) ? column.Field : (value as string ?? column.Title));
                break;

            default:
                DrawCellText(g, p, inner, _engine.FormatCellValue(column, value), font, textColor, column, slot.Index, selection.IsCellSelected(slot.Index, columnIndex) || (!IsCellSelectionMode && selection.IsRowSelected(slot.Index)));
                break;
        }

        if (hasError)
        {
            var size = S(8);
            using var tri = new SolidBrush(p.Danger);
            g.FillPolygon(tri, new[] { new Point(rect.Right - size - 1, rect.Y + 1), new Point(rect.Right - 1, rect.Y + 1), new Point(rect.Right - 1, rect.Y + size + 1) });
        }
        else if (_engine.IsCellDirty(item!, column.Field))
        {
            var size = S(8);
            using var tri = new SolidBrush(p.Primary);
            g.FillPolygon(tri, new[] { new Point(rect.X + 1, rect.Y + 1), new Point(rect.X + size + 1, rect.Y + 1), new Point(rect.X + 1, rect.Y + size + 1) });
        }
    }

    private Rectangle DrawTreeIndent(Graphics g, Colors p, GridRow<TItem> row, Rectangle inner, Color color)
    {
        if (!_engine.IsTreeMode) return inner;

        var indent = row.Level * S(16);
        var glyph = new Rectangle(inner.X + indent, inner.Y, S(18), inner.Height);
        if (row.IsExpandable) DrawChevron(g, glyph, row.IsExpanded, color);
        var consumed = indent + S(20);
        return new Rectangle(inner.X + consumed, inner.Y, Math.Max(0, inner.Width - consumed), inner.Height);
    }

    private void DrawProgress(Graphics g, Colors p, Rectangle rect, object? value, string text, Color textColor)
    {
        var ratio = 0d;
        if (value != null && double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            ratio = Math.Clamp(number / 100d, 0, 1);
        }

        var track = new Rectangle(rect.X + S(6), rect.Y + S(5), Math.Max(0, rect.Width - S(12)), Math.Max(1, rect.Height - S(10)));
        using var trackBrush = new SolidBrush(Blend(p.Surface, p.Border, 0.35));
        g.FillRectangle(trackBrush, track);
        if (ratio > 0)
        {
            using var fill = new SolidBrush(p.HighContrast ? p.Primary : Blend(p.Surface, p.Primary, 0.55));
            g.FillRectangle(fill, track.X, track.Y, (int)Math.Round(track.Width * ratio), track.Height);
        }

        TextRenderer.DrawText(g, text, Font, track, p.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }

    private void DrawCommandButton(Graphics g, Colors p, Rectangle rect, string text)
    {
        var button = Rectangle.Inflate(rect, -S(6), -S(3));
        using var back = new SolidBrush(p.Header);
        using var border = new Pen(p.Border);
        g.FillRectangle(back, button);
        g.DrawRectangle(border, button);
        TextRenderer.DrawText(g, text, Font, button, p.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }

    /// <summary>Draws cell text, painting search highlights behind each occurrence of the search text.</summary>
    private void DrawCellText(Graphics g, Colors p, Rectangle inner, string text, Font font, Color color, GridColumn column, int rowIndex, bool selected)
    {
        var flags = FlagsFor(column.Alignment);
        var term = _engine.HighlightText;
        var ranges = term != null && column.Searchable && text.Length > 0
            ? DataGrid<TItem>.FindRanges(text, term, _engine.SearchOptions.IgnoreCase)
            : Array.Empty<(int Start, int Length)>();

        if (ranges.Count == 0)
        {
            TextRenderer.DrawText(g, text, font, inner, color, flags);
            return;
        }

        var measure = TextRenderer.MeasureText(g, "|", font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width;
        int Width(string s) => TextRenderer.MeasureText(g, s + "|", font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width - measure;

        var total = Width(text);
        var origin = column.Alignment switch
        {
            ColumnTextAlignment.Right => inner.Right - total,
            ColumnTextAlignment.Center => inner.X + (inner.Width - total) / 2,
            _ => inner.X
        };

        var current = _engine.IsCurrentMatch(rowIndex, column.Field);
        var backColor = current ? p.FocusRing : p.HighlightBack;
        var foreColor = current ? (p.HighContrast ? p.Surface : p.PrimaryText) : p.HighlightText;
        var hits = new List<Rectangle>();
        foreach (var (start, length) in ranges)
        {
            var left = start == 0 ? 0 : Width(text.Substring(0, start));
            var right = Width(text.Substring(0, start + length));
            hits.Add(Rectangle.Intersect(inner, new Rectangle(origin + left, inner.Y + S(2), Math.Max(2, right - left), Math.Max(1, inner.Height - S(4)))));
        }

        using (var back = new SolidBrush(backColor))
        {
            foreach (var hit in hits) g.FillRectangle(back, hit);
        }

        TextRenderer.DrawText(g, text, font, inner, color, flags);

        var state = g.Save();
        foreach (var hit in hits)
        {
            g.SetClip(hit);
            TextRenderer.DrawText(g, text, font, inner, foreColor, flags);
        }
        g.Restore(state);
    }
    // ------------------------------------------------------------------
    // Group, detail and footer rows
    // ------------------------------------------------------------------

    private void DrawGroupHeaderRow(Graphics g, Frame f, Colors p, RowSlot slot, GridRow<TItem> row)
    {
        var selected = _engine.Selection.IsRowSelected(slot.Index);
        var back = selected ? p.SelectionBack : slot.Index == _hoveredRow ? Blend(p.Header, p.Primary, 0.12) : Blend(p.Header, p.Primary, p.HighContrast ? 0 : 0.06);
        var fore = selected ? p.SelectionText : p.Text;
        using var brush = new SolidBrush(back);
        using var line = new Pen(p.Border);
        g.FillRectangle(brush, slot.Bounds);
        g.DrawLine(line, 0, slot.Bounds.Bottom - 1, slot.Bounds.Right, slot.Bounds.Bottom - 1);

        var indent = row.Level * S(18);
        DrawChevron(g, new Rectangle(S(6) + indent, slot.Bounds.Y, S(18), slot.Bounds.Height), row.IsExpanded, fore);

        // The header text is "Title: key (count)" optionally followed by "  -  summaries"; draw the summaries in a quieter color.
        var full = _engine.GetGroupHeaderText(row);
        var split = full.IndexOf("  \u2013  ", StringComparison.Ordinal);
        var label = split < 0 ? full : full.Substring(0, split);
        var summaries = split < 0 ? string.Empty : full.Substring(split + 5);
        var textX = S(28) + indent;
        var labelWidth = TextRenderer.MeasureText(label, HeaderFont, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
        TextRenderer.DrawText(g, label, HeaderFont, new Rectangle(textX, slot.Bounds.Y, Math.Max(0, slot.Bounds.Width - textX - S(8)), slot.Bounds.Height), fore, LeftText);

        if (summaries.Length > 0)
        {
            var x = textX + labelWidth + S(16);
            TextRenderer.DrawText(g, summaries, Font, new Rectangle(x, slot.Bounds.Y, Math.Max(0, slot.Bounds.Width - x - S(8)), slot.Bounds.Height), selected ? p.SelectionText : p.Muted, LeftText);
        }
    }

    private void DrawGroupFooterRow(Graphics g, Frame f, Colors p, RowSlot slot, GridRow<TItem> row, Pen grid)
    {
        using var brush = new SolidBrush(Blend(p.Surface, p.Header, 0.6));
        g.FillRectangle(brush, slot.Bounds);
        g.DrawLine(grid, 0, slot.Bounds.Bottom - 1, slot.Bounds.Right, slot.Bounds.Bottom - 1);

        var indent = row.Level * S(18) + S(28);
        var parts = row.Group == null ? Array.Empty<string>() : _engine.GetGroupSummaryTexts(row.Group).ToArray();
        var text = parts.Length == 0 ? "Group total" : string.Join("   ", parts);
        TextRenderer.DrawText(g, text, HeaderFont, new Rectangle(indent, slot.Bounds.Y, Math.Max(0, slot.Bounds.Width - indent - S(8)), slot.Bounds.Height), p.Text, LeftText);
    }

    private void DrawDetailHeaderRow(Graphics g, Frame f, Colors p, RowSlot slot, GridRow<TItem> row)
    {
        using var brush = new SolidBrush(Blend(p.Header, p.Primary, p.HighContrast ? 0 : 0.04));
        using var line = new Pen(p.Border);
        g.FillRectangle(brush, slot.Bounds);
        g.DrawLine(line, 0, slot.Bounds.Bottom - 1, slot.Bounds.Right, slot.Bounds.Bottom - 1);

        var indent = f.Gutter + (row.Level + 1) * S(16);
        var title = row.Relation?.Title ?? "Details";
        if (row.IsLoading) title += " (loading...)";
        var children = row.Parent == null || row.Relation == null ? Array.Empty<object>() : _engine.GetChildren(row.Relation, row.Parent);
        var sample = children.Count == 0 ? null : children[0];
        var columns = row.Relation == null ? Array.Empty<GridColumn>() : _engine.GetDetailColumns(row.Relation, sample).ToArray();

        TextRenderer.DrawText(g, title, HeaderFont, new Rectangle(indent, slot.Bounds.Y, S(120), slot.Bounds.Height), p.Text, LeftText);
        var x = indent + S(120);
        foreach (var column in columns)
        {
            var width = (int)(GridColumnLayout.WidthOf(column) * DpiScale);
            if (x >= slot.Bounds.Right) break;
            TextRenderer.DrawText(g, string.IsNullOrEmpty(column.Title) ? column.Field : column.Title, HeaderFont, new Rectangle(x + S(6), slot.Bounds.Y, Math.Max(0, width - S(8)), slot.Bounds.Height), p.Muted, FlagsFor(column.Alignment));
            x += width;
        }
    }

    private void DrawDetailDataRow(Graphics g, Frame f, Colors p, RowSlot slot, GridRow<TItem> row, Pen grid)
    {
        var selected = _engine.Selection.IsRowSelected(slot.Index);
        using var brush = new SolidBrush(selected ? p.SelectionBack : Blend(p.Surface, p.Header, 0.35));
        g.FillRectangle(brush, slot.Bounds);
        g.DrawLine(grid, 0, slot.Bounds.Bottom - 1, slot.Bounds.Right, slot.Bounds.Bottom - 1);

        var indent = f.Gutter + (row.Level + 1) * S(16);
        if (row.IsExpandable) DrawChevron(g, new Rectangle(indent - S(18), slot.Bounds.Y, S(18), slot.Bounds.Height), row.IsExpanded, p.Muted);

        var columns = row.Relation == null ? Array.Empty<GridColumn>() : _engine.GetDetailColumns(row.Relation, row.RowObject).ToArray();
        var x = indent + S(120);
        foreach (var column in columns)
        {
            var width = (int)(GridColumnLayout.WidthOf(column) * DpiScale);
            if (x >= slot.Bounds.Right) break;
            var text = _engine.FormatCellValue(column, column.GetValue(row.RowObject));
            TextRenderer.DrawText(g, text, Font, new Rectangle(x + S(6), slot.Bounds.Y, Math.Max(0, width - S(8)), slot.Bounds.Height), selected ? p.SelectionText : p.Text, FlagsFor(column.Alignment));
            x += width;
        }
    }

    private void DrawDetailSummaryRow(Graphics g, Frame f, Colors p, RowSlot slot, GridRow<TItem> row)
    {
        using var bg = new SolidBrush(p.Header);
        using var border = new Pen(p.Border);
        var rect = Rectangle.Inflate(slot.Bounds, -S(1), 0);
        g.FillRectangle(bg, rect);
        g.DrawLine(border, rect.X, rect.Bottom - 1, rect.Right, rect.Bottom - 1);

        var item = row.Item!;
        var summary = _engine.Columns
            .Where(c => c.Visible || c.IsEditable || true)
            .Take(8)
            .Select(c => $"{(string.IsNullOrEmpty(c.Title) ? c.Field : c.Title)}: {_engine.FormatCellValue(c, c.GetValue(item))}");
        TextRenderer.DrawText(g, string.Join("   |   ", summary), Font, new Rectangle(f.Gutter + S(12), rect.Y, Math.Max(0, rect.Width - f.Gutter - S(24)), rect.Height), p.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    private void DrawFocusCell(Graphics g, Frame f, Colors p)
    {
        var selection = _engine.Selection;
        if (!Focused || selection.FocusedRow < 0 || selection.FocusedColumn < 0 || selection.FocusedColumn >= f.Ordered.Count) return;
        if (_editor != null) return;

        // Group, detail and footer rows have no cells: outline the whole row instead.
        if (_engine.TryGetRowAt(selection.FocusedRow, out var focusedRow) && focusedRow.Kind != GridRowKind.Data)
        {
            foreach (var rowSlot in EnumerateRows(f))
            {
                if (rowSlot.Index != selection.FocusedRow) continue;
                using var rowPen = new Pen(p.FocusRing, Math.Max(2, S(2)));
                g.DrawRectangle(rowPen, 1, rowSlot.Bounds.Y + 1, f.BodyWidth - 3, rowSlot.Bounds.Height - 3);
                return;
            }
            return;
        }

        var column = f.Ordered[selection.FocusedColumn];
        var colSlot = f.Layout.Slots.FirstOrDefault(s => ReferenceEquals(s.Column, column));
        if (colSlot == null) return;

        foreach (var slot in EnumerateRows(f))
        {
            if (slot.Index != selection.FocusedRow) continue;

            var rect = new Rectangle((int)colSlot.Left + f.Gutter, slot.Bounds.Y, (int)colSlot.Width, slot.Bounds.Height);
            var state = g.Save();
            g.SetClip(new Rectangle(0, f.BodyTop, f.BodyWidth, f.BodyBottom - f.BodyTop));
            if (colSlot.Zone == ColumnZone.Scrollable)
            {
                g.SetClip(new Rectangle((int)f.Layout.ScrollableLeft + f.Gutter, f.BodyTop, Math.Max(0, (int)(f.Layout.ScrollableRight - f.Layout.ScrollableLeft)), f.BodyBottom - f.BodyTop), CombineMode.Intersect);
            }
            using var pen = new Pen(p.FocusRing, Math.Max(2, S(2))) { DashStyle = DashStyle.Solid };
            g.DrawRectangle(pen, rect.X + 1, rect.Y + 1, rect.Width - 3, rect.Height - 3);
            g.Restore(state);
            return;
        }
    }

    private void DrawPinDividers(Graphics g, Frame f, Colors p)
    {
        var layout = f.Layout;
        using var pen = new Pen(Blend(p.Border, p.Primary, 0.5), Math.Max(1, S(1.5)));
        var top = f.HeaderTop;
        var bottom = f.BodyBottom;

        if (layout.Slots.Any(s => s.Zone == ColumnZone.Left))
        {
            var x = (int)layout.ScrollableLeft + f.Gutter;
            g.DrawLine(pen, x, top, x, bottom);
        }

        if (layout.Slots.Any(s => s.Zone == ColumnZone.Right))
        {
            var x = (int)layout.ScrollableRight + f.Gutter;
            g.DrawLine(pen, x, top, x, bottom);
        }
    }

    private void DrawSummaryRows(Graphics g, Frame f, Colors p)
    {
        if (f.SummaryRows == 0) return;

        using var bg = new SolidBrush(p.Header);
        using var line = new Pen(p.Border);
        g.FillRectangle(bg, 0, f.SummaryTop, f.BodyWidth, f.SummaryH);
        g.DrawLine(line, 0, f.SummaryTop, f.BodyWidth, f.SummaryTop);

        var scrollRegion = new Rectangle((int)f.Layout.ScrollableLeft + f.Gutter, f.SummaryTop, Math.Max(0, (int)(f.Layout.ScrollableRight - f.Layout.ScrollableLeft)), f.SummaryH);
        foreach (var zonePass in new[] { 0, 1 })
        {
            foreach (var slot in f.Layout.Slots)
            {
                if ((slot.Zone == ColumnZone.Scrollable) != (zonePass == 0)) continue;

                var state = g.Save();
                if (zonePass == 0) g.SetClip(scrollRegion);
                var rect = new Rectangle((int)slot.Left + f.Gutter, f.SummaryTop, (int)slot.Width, f.SummaryH);
                if (zonePass == 1) g.FillRectangle(bg, rect);

                var values = _engine.GetFooterValues(slot.Column.Field);
                for (var i = 0; i < f.SummaryRows && i < values.Count; i++)
                {
                    var cell = new Rectangle(rect.X + S(8), rect.Y + i * f.RowH, Math.Max(0, rect.Width - S(16)), f.RowH);
                    TextRenderer.DrawText(g, values[i].Text, HeaderFont, cell, p.Text, FlagsFor(slot.Column.Alignment));
                }
                g.DrawLine(line, rect.Right - 1, rect.Y, rect.Right - 1, rect.Bottom);
                g.Restore(state);
            }
        }
    }

    private void DrawStatusBar(Graphics g, Frame f, Colors p)
    {
        if (f.FooterH == 0) return;

        using var bg = new SolidBrush(p.Header);
        using var line = new Pen(p.Border);
        g.FillRectangle(bg, 0, f.StatusTop, Width, f.FooterH);
        g.DrawLine(line, 0, f.StatusTop, Width, f.StatusTop);

        var selection = _engine.Selection;
        var text = new StringBuilder($"Rows: {_engine.TotalRecordCount:N0}");
        if (selection.SelectedRows.Count > 0) text.Append($"  |  Selected: {selection.SelectedRows.Count:N0}");
        else if (selection.SelectedCellCount > 0 && selection.HasSelection) text.Append($"  |  Cells: {selection.SelectedCellCount:N0}");
        if (_engine.CurrentRequest.Groups.Count > 0) text.Append($"  |  Groups: {_engine.CurrentRequest.Groups.Count}");
        if (_engine.SearchResult is { MatchCount: > 0 } found) text.Append($"  |  Matches: {found.MatchCount:N0}");
        if (_engine.HasPendingChanges) text.Append($"  |  Pending changes: {_engine.PendingChanges.Count}");
        if (_engine.HasErrors) text.Append($"  |  Errors: {_engine.ValidationSummary.Count}");
        if (_engine.CanUndo) text.Append("  |  Ctrl+Z to undo");
        if (_engine.IsLoading) text.Append("  |  Loading...");

        TextRenderer.DrawText(g, text.ToString(), HeaderFont, new Rectangle(S(10), f.StatusTop, Width - S(20), f.FooterH), _engine.HasErrors ? p.Danger : p.Text, LeftText);
    }

    private void DrawDragFeedback(Graphics g, Frame f, Colors p)
    {
        if (_drag != DragMode.HeaderDrag || _dragColumn == null) return;

        var slots = f.Layout.Slots;
        var cursorX = PointToClient(Cursor.Position).X;
        var title = string.IsNullOrEmpty(_dragColumn.Title) ? _dragColumn.Field : _dragColumn.Title;
        var ghost = new Rectangle(cursorX - S(40), f.HeaderTop + S(2), S(96), f.HeaderH - S(4));
        using (var ghostBrush = new SolidBrush(Color.FromArgb(200, p.Primary)))
        {
            g.FillRectangle(ghostBrush, ghost);
        }
        TextRenderer.DrawText(g, title, HeaderFont, ghost, p.PrimaryText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

        if (_dropIndex >= 0 && slots.Count > 0)
        {
            var x = _dropIndex < slots.Count ? (int)slots[_dropIndex].Left : (int)slots[^1].Right;
            x += f.Gutter;
            using var pen = new Pen(p.Primary, Math.Max(2, S(2)));
            g.DrawLine(pen, x, f.HeaderTop, x, f.BodyBottom);
        }
    }
}
