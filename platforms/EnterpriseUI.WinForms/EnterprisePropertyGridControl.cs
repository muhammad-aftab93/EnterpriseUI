using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using EnterpriseUI.Grids;

namespace EnterpriseUI.WinForms;

/// <summary>Inspector for the properties of an object: categories, search, nested objects, inline editors and a description pane.</summary>
public class EnterprisePropertyGridControl : EnterpriseScrollableControl
{
    private sealed class Row
    {
        public PropertyEntry? Entry;
        public string? Category;
        public string Key = string.Empty;
        public int Depth;
        public bool Collapsed;
        public bool Expanded;
        public Rectangle Bounds;
    }

    private readonly List<Row> _rows = new();
    private readonly HashSet<string> _expanded = new();
    private readonly HashSet<string> _collapsedCategories = new();
    private readonly TextBox _search = new() { BorderStyle = BorderStyle.None, PlaceholderText = "Search properties" };
    private readonly TextBox _editor = new() { BorderStyle = BorderStyle.None, Visible = false };
    private object? _selectedObject;
    private bool _categorized = true;
    private bool _showToolbar = true;
    private bool _showDescription = true;
    private double _splitter = 0.45;
    private bool _dragSplitter;
    private string? _selectedKey;
    private string? _hotKey;
    private string? _error;
    private Row? _editing;
    private bool _committing;

    public EnterprisePropertyGridControl()
    {
        Size = new Size(380, 420);
        AccessibleRole = AccessibleRole.Table;
        _search.TextChanged += (_, _) => { EndEdit(false); Rebuild(); };
        _editor.KeyDown += OnEditorKeyDown;
        _editor.LostFocus += (_, _) => { if (_editing is not null && !_committing) CommitEdit(); };
        Controls.Add(_search);
        Controls.Add(_editor);
        OnThemeApplied();
    }

    public event EventHandler<PropertyEntry>? PropertyValueChanged;
    public event EventHandler<PropertyEntry?>? SelectedPropertyChanged;

    public object? SelectedObject
    {
        get => _selectedObject;
        set
        {
            if (ReferenceEquals(_selectedObject, value)) return;
            EndEdit(false);
            _selectedObject = value;
            _expanded.Clear();
            _selectedKey = null;
            _error = null;
            Rebuild();
        }
    }

    public bool Categorized
    {
        get => _categorized;
        set { _categorized = value; EndEdit(false); Rebuild(); }
    }

    public bool ShowToolbar
    {
        get => _showToolbar;
        set { _showToolbar = value; _search.Visible = value; Rebuild(); }
    }

    public bool ShowDescription
    {
        get => _showDescription;
        set { _showDescription = value; Rebuild(); }
    }

    public string SearchText
    {
        get => _search.Text;
        set => _search.Text = value;
    }

    public double SplitterRatio
    {
        get => _splitter;
        set { _splitter = Math.Clamp(value, 0.2, 0.8); Invalidate(); }
    }

    public PropertyEntry? SelectedProperty => _rows.FirstOrDefault(r => r.Key == _selectedKey)?.Entry;

    private int ToolbarPx => _showToolbar ? S(44) : 0;
    private int DescriptionPx => _showDescription ? S(76) : 0;
    private int RowPx => S(30);
    private int CategoryPx => S(30);

    protected override int LineHeight => RowPx;
    protected override Size ContentSize => new(0, ToolbarPx + (_rows.Count == 0 ? 0 : _rows[^1].Bounds.Bottom) + DescriptionPx);

    protected override void OnThemeApplied()
    {
        base.OnThemeApplied();
        _search.BackColor = Colors.Surface;
        _search.ForeColor = Colors.Text;
        _editor.BackColor = Colors.Surface;
        _editor.ForeColor = Colors.Text;
        _search.Font = Font;
        _editor.Font = Font;
    }

    /// <summary>Re-reads all values, for example after the object was changed in code.</summary>
    public void RefreshProperties()
    {
        EndEdit(false);
        Rebuild();
    }

    private void Rebuild()
    {
        _rows.Clear();
        if (_selectedObject is not null)
        {
            var categories = PropertyGridModel.Build(_selectedObject, _categorized, _search.Text);
            var y = 0;
            foreach (var category in categories)
            {
                if (_categorized)
                {
                    var collapsed = _collapsedCategories.Contains(category.Name);
                    _rows.Add(new Row { Category = category.Name, Key = "cat:" + category.Name, Collapsed = collapsed, Bounds = new Rectangle(0, y, 1, CategoryPx) });
                    y += CategoryPx;
                    if (collapsed) continue;
                }

                foreach (var entry in category.Entries) y = AddEntry(entry, entry.Name, 0, y);
            }
        }

        UpdateScrollBars();
        PositionControls();
        Invalidate();
    }

    private int AddEntry(PropertyEntry entry, string key, int depth, int y)
    {
        var expanded = entry.Kind == PropertyEditorKind.Nested && _expanded.Contains(key);
        _rows.Add(new Row { Entry = entry, Key = key, Depth = depth, Expanded = expanded, Bounds = new Rectangle(0, y, 1, RowPx) });
        y += RowPx;
        if (expanded)
        {
            foreach (var child in entry.Children) y = AddEntry(child, key + "." + child.Name, depth + 1, y);
        }

        return y;
    }

    // ---- geometry -----------------------------------------------------------------------

    private Rectangle RowsArea
    {
        get
        {
            var view = ViewPort;
            return new Rectangle(view.X, view.Y + ToolbarPx, view.Width, Math.Max(0, view.Height - ToolbarPx - DescriptionPx));
        }
    }

    private Rectangle RowRect(Row row)
    {
        var view = ViewPort;
        return new Rectangle(view.X, view.Y + ToolbarPx + row.Bounds.Y - ScrollY, view.Width, row.Bounds.Height);
    }

    private int NameWidth => (int)(ViewPort.Width * _splitter);
    private Rectangle NameCell(Rectangle row) => new(row.X, row.Y, NameWidth, row.Height);
    private Rectangle ValueCell(Rectangle row) => new(row.X + NameWidth, row.Y, Math.Max(0, row.Width - NameWidth), row.Height);

    private Rectangle[] ToolbarButtons()
    {
        var view = ViewPort;
        var size = S(28);
        var y = view.Y + (ToolbarPx - size) / 2;
        return new[]
        {
            new Rectangle(view.Right - S(8) - size * 2 - S(2), y, size, size),
            new Rectangle(view.Right - S(8) - size, y, size, size)
        };
    }

    private void PositionControls()
    {
        if (_showToolbar)
        {
            var view = ViewPort;
            var buttons = ToolbarButtons();
            var h = _search.PreferredHeight;
            var box = new Rectangle(view.X + S(14), view.Y + (ToolbarPx - h) / 2, Math.Max(10, buttons[0].X - view.X - S(24)), h);
            _search.SetBounds(box.X, box.Y, box.Width, box.Height);
        }

        if (_editing is not null)
        {
            var rect = ValueCell(RowRect(_editing));
            var inRows = RowsArea.IntersectsWith(rect);
            _editor.Visible = inRows;
            if (inRows)
            {
                var h = Math.Min(_editor.PreferredHeight, rect.Height - S(6));
                _editor.SetBounds(rect.X + S(8), rect.Y + (rect.Height - h) / 2, Math.Max(10, rect.Width - S(16)), h);
            }
        }
    }

    protected override void OnScrolled()
    {
        PositionControls();
        base.OnScrolled();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        PositionControls();
    }

    // ---- painting -----------------------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        PaintFrame(g);
        var view = ViewPort;
        if (view.Width <= 0 || view.Height <= 0) return;

        var rows = RowsArea;
        g.SetClip(rows);
        if (_rows.Count == 0)
        {
            Chrome.DrawText(g, _selectedObject is null ? "No object selected" : "No matching properties", Font, rows, Colors.Muted, ContentAlignment.MiddleCenter);
        }

        foreach (var row in _rows)
        {
            var rect = RowRect(row);
            if (rect.Bottom < rows.Top || rect.Top > rows.Bottom) continue;
            if (row.Entry is null) PaintCategory(g, row, rect);
            else PaintProperty(g, row, rect);
        }

        g.ResetClip();
        if (_showToolbar) PaintToolbar(g);
        if (_showDescription) PaintDescription(g);
    }

    private void PaintToolbar(Graphics g)
    {
        var view = ViewPort;
        var band = new Rectangle(view.X, view.Y, view.Width, ToolbarPx);
        using (var brush = new SolidBrush(Colors.Header)) g.FillRectangle(brush, band);
        using (var pen = new Pen(Colors.Border)) g.DrawLine(pen, band.X, band.Bottom - 1, band.Right, band.Bottom - 1);

        var searchBack = Rectangle.Inflate(_search.Bounds, S(10), S(5));
        Chrome.FillRounded(g, searchBack, Colors.Surface, S(4));
        Chrome.DrawRoundedBorder(g, searchBack, _search.Focused ? Colors.FocusRing : Colors.Border, S(4));

        var buttons = ToolbarButtons();
        PaintToolbarButton(g, buttons[0], _categorized, categorizedIcon: true);
        PaintToolbarButton(g, buttons[1], !_categorized, categorizedIcon: false);
    }

    private void PaintToolbarButton(Graphics g, Rectangle rect, bool active, bool categorizedIcon)
    {
        if (active)
        {
            Chrome.FillRounded(g, rect, Chrome.Blend(Colors.Header, Colors.Primary, 0.22), S(4));
            Chrome.DrawRoundedBorder(g, rect, Colors.Primary, S(4));
        }

        var color = active ? Colors.Primary : Colors.Muted;
        using var pen = new Pen(color, 1.6f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        var half = S(7);
        if (categorizedIcon)
        {
            g.DrawLine(pen, cx - half, cy - S(5), cx + half, cy - S(5));
            g.DrawLine(pen, cx - half + S(4), cy, cx + half, cy);
            g.DrawLine(pen, cx - half, cy + S(5), cx + half, cy + S(5));
        }
        else
        {
            Chrome.DrawText(g, "A↓", BoldFont, rect, color, ContentAlignment.MiddleCenter);
        }
    }

    private void PaintCategory(Graphics g, Row row, Rectangle rect)
    {
        using (var brush = new SolidBrush(Chrome.Blend(Colors.Surface, Colors.Header, 0.7))) g.FillRectangle(brush, rect);
        using (var pen = new Pen(Colors.Border)) g.DrawLine(pen, rect.X, rect.Bottom - 1, rect.Right, rect.Bottom - 1);
        var selected = row.Key == _selectedKey;
        Chrome.DrawChevron(g, new Rectangle(rect.X + S(6), rect.Y, S(20), rect.Height), row.Collapsed ? ChevronDirection.Right : ChevronDirection.Down, Colors.Muted);
        Chrome.DrawText(g, row.Category ?? string.Empty, BoldFont, new Rectangle(rect.X + S(30), rect.Y, Math.Max(0, rect.Width - S(36)), rect.Height), Colors.Text);
        if (selected && Focused)
        {
            using var pen = new Pen(Colors.FocusRing) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            g.DrawRectangle(pen, rect.X + 1, rect.Y + 1, rect.Width - 3, rect.Height - 3);
        }
    }

    private void PaintProperty(Graphics g, Row row, Rectangle rect)
    {
        var entry = row.Entry!;
        var selected = row.Key == _selectedKey;
        var hovered = row.Key == _hotKey && Enabled;
        using (var brush = new SolidBrush(selected ? Chrome.Blend(Colors.Surface, Colors.SelectionBack, Colors.HighContrast ? 1 : 0.7) : hovered ? Colors.HoverBack : Colors.Surface))
        {
            g.FillRectangle(brush, rect);
        }

        if (selected) Chrome.DrawSelectionBar(g, rect, Colors, S(3));
        using (var pen = new Pen(Chrome.Blend(Colors.Surface, Colors.Border, 0.6))) g.DrawLine(pen, rect.X, rect.Bottom - 1, rect.Right, rect.Bottom - 1);

        var name = NameCell(rect);
        using (var pen = new Pen(Chrome.Blend(Colors.Surface, Colors.Border, 0.6))) g.DrawLine(pen, name.Right, rect.Y, name.Right, rect.Bottom);

        var indent = S(10) + row.Depth * S(18);
        var expander = new Rectangle(name.X + indent - S(4), name.Y, S(18), name.Height);
        if (entry.Kind == PropertyEditorKind.Nested) Chrome.DrawChevron(g, expander, row.Expanded ? ChevronDirection.Down : ChevronDirection.Right, Colors.Muted);
        var nameColor = !Enabled ? Colors.Disabled : selected && Colors.HighContrast ? Colors.SelectionText : Colors.Text;
        Chrome.DrawText(g, entry.DisplayName, entry.IsModified ? BoldFont : Font, new Rectangle(name.X + indent + S(14), name.Y, Math.Max(0, name.Width - indent - S(20)), name.Height), nameColor);

        PaintValue(g, row, entry, ValueCell(rect), selected, hovered);
        if (selected && Focused)
        {
            using var pen = new Pen(Colors.FocusRing) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            g.DrawRectangle(pen, rect.X + 1, rect.Y + 1, rect.Width - 3, rect.Height - 3);
        }
    }

    private void PaintValue(Graphics g, Row row, PropertyEntry entry, Rectangle cell, bool selected, bool hovered)
    {
        if (ReferenceEquals(_editing, row)) return;
        var textBounds = new Rectangle(cell.X + S(10), cell.Y, Math.Max(0, cell.Width - S(40)), cell.Height);
        var color = !Enabled || entry.IsReadOnly ? Colors.Muted : Colors.Text;

        switch (entry.Kind)
        {
            case PropertyEditorKind.Boolean:
            {
                var on = entry.Value is true;
                var w = S(34);
                var h = S(18);
                var track = new Rectangle(cell.X + S(10), cell.Y + (cell.Height - h) / 2, w, h);
                Chrome.FillRounded(g, track, on ? (entry.IsReadOnly ? Colors.Muted : Colors.Primary) : Chrome.Blend(Colors.Surface, Colors.Muted, 0.45), h / 2);
                var knob = h - S(6);
                var kx = on ? track.Right - knob - S(3) : track.X + S(3);
                using (var brush = new SolidBrush(on ? Colors.PrimaryText : Colors.Surface)) g.FillEllipse(brush, kx, track.Y + S(3), knob, knob);
                Chrome.DrawText(g, on ? "True" : "False", Font, new Rectangle(track.Right + S(8), cell.Y, Math.Max(0, cell.Right - track.Right - S(12)), cell.Height), color);
                break;
            }
            case PropertyEditorKind.Color:
            {
                var swatch = new Rectangle(cell.X + S(10), cell.Y + (cell.Height - S(16)) / 2, S(24), S(16));
                if (entry.Value is Color value)
                {
                    Chrome.FillRounded(g, swatch, value, S(3));
                    Chrome.DrawRoundedBorder(g, swatch, Colors.Border, S(3));
                }

                Chrome.DrawText(g, entry.DisplayText, Font, new Rectangle(swatch.Right + S(8), cell.Y, Math.Max(0, cell.Right - swatch.Right - S(12)), cell.Height), color);
                break;
            }
            case PropertyEditorKind.Enum:
            case PropertyEditorKind.Choice:
                Chrome.DrawText(g, entry.DisplayText, Font, textBounds, color);
                if (selected || hovered) Chrome.DrawChevron(g, new Rectangle(cell.Right - S(28), cell.Y, S(22), cell.Height), ChevronDirection.Down, Colors.Muted);
                break;
            default:
                Chrome.DrawText(g, entry.DisplayText, Font, new Rectangle(cell.X + S(10), cell.Y, Math.Max(0, cell.Width - S(16)), cell.Height), color);
                break;
        }
    }

    private void PaintDescription(Graphics g)
    {
        var view = ViewPort;
        var pane = new Rectangle(view.X, view.Bottom - DescriptionPx, view.Width, DescriptionPx);
        using (var brush = new SolidBrush(Colors.Header)) g.FillRectangle(brush, pane);
        using (var pen = new Pen(Colors.Border)) g.DrawLine(pen, pane.X, pane.Y, pane.Right, pane.Y);

        var inner = Rectangle.Inflate(pane, -S(12), -S(8));
        if (!string.IsNullOrEmpty(_error))
        {
            Chrome.DrawText(g, _error, Font, inner, Colors.Danger, ContentAlignment.TopLeft, true);
            return;
        }

        var entry = SelectedProperty;
        if (entry is null) return;
        Chrome.DrawText(g, entry.DisplayName, BoldFont, new Rectangle(inner.X, inner.Y, inner.Width, S(20)), Colors.Text, ContentAlignment.TopLeft);
        var description = string.IsNullOrEmpty(entry.Description) ? $"{entry.PropertyType.Name} · {entry.Category}" : entry.Description;
        Chrome.DrawText(g, description, Font, new Rectangle(inner.X, inner.Y + S(20), inner.Width, Math.Max(0, inner.Height - S(20))), Colors.Muted, ContentAlignment.TopLeft, true);
    }

    // ---- editing ------------------------------------------------------------------------

    private void BeginEdit(Row row)
    {
        if (row.Entry is not { } entry || entry.IsReadOnly || !Enabled) return;
        switch (entry.Kind)
        {
            case PropertyEditorKind.Boolean:
                Commit(entry, entry.Value is not true);
                return;
            case PropertyEditorKind.Enum:
            case PropertyEditorKind.Choice:
                ShowChoices(row, entry);
                return;
            case PropertyEditorKind.Nested:
            case PropertyEditorKind.ReadOnly:
                return;
        }

        _editing = row;
        _error = null;
        _editor.Text = entry.DisplayText;
        PositionControls();
        _editor.Visible = true;
        _editor.Focus();
        _editor.SelectAll();
        Invalidate();
    }

    private void ShowChoices(Row row, PropertyEntry entry)
    {
        var cell = ValueCell(RowRect(row));
        ChoicePopup.Show(this, cell, entry.Choices, entry.DisplayText, choice =>
        {
            if (entry.TrySetText(choice, out var error)) AfterChange(entry);
            else SetError(error);
        });
    }

    private void Commit(PropertyEntry entry, object? value)
    {
        if (entry.TrySetValue(value, out var error)) AfterChange(entry);
        else SetError(error);
    }

    private void CommitEdit()
    {
        if (_editing?.Entry is not { } entry) return;
        _committing = true;
        try
        {
            if (entry.TrySetText(_editor.Text, out var error))
            {
                EndEdit(false);
                AfterChange(entry);
            }
            else
            {
                SetError(error);
                EndEdit(false);
            }
        }
        finally
        {
            _committing = false;
        }
    }

    private void SetError(string? error)
    {
        _error = error;
        Invalidate();
    }

    private void AfterChange(PropertyEntry entry)
    {
        _error = null;
        Rebuild();
        PropertyValueChanged?.Invoke(this, entry);
    }

    private void EndEdit(bool refocus)
    {
        if (_editing is null) return;
        _editing = null;
        _editor.Visible = false;
        if (refocus) Focus();
        Invalidate();
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            CommitEdit();
            Focus();
        }
        else if (e.KeyCode == Keys.Escape)
        {
            e.SuppressKeyPress = true;
            _error = null;
            EndEdit(true);
        }
    }

    // ---- selection and input ----------------------------------------------------------------

    private void SelectRow(Row? row)
    {
        EndEdit(false);
        _error = null;
        _selectedKey = row?.Key;
        if (row is not null) EnsureVisible(new Rectangle(0, row.Bounds.Y, 1, row.Bounds.Height + ToolbarPx + DescriptionPx));
        Invalidate();
        SelectedPropertyChanged?.Invoke(this, row?.Entry);
    }

    private void ToggleRow(Row row)
    {
        if (row.Entry is null)
        {
            var name = row.Category ?? string.Empty;
            if (!_collapsedCategories.Remove(name)) _collapsedCategories.Add(name);
        }
        else if (row.Entry.Kind == PropertyEditorKind.Nested)
        {
            if (!_expanded.Remove(row.Key)) _expanded.Add(row.Key);
        }

        EndEdit(false);
        Rebuild();
    }

    private Row? RowAt(Point location)
    {
        if (!RowsArea.Contains(location)) return null;
        return _rows.FirstOrDefault(r => RowRect(r).Contains(location));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragSplitter)
        {
            var view = ViewPort;
            SplitterRatio = (e.X - view.X) / (double)Math.Max(1, view.Width);
            PositionControls();
            return;
        }

        var row = RowAt(e.Location);
        var nearSplitter = row is not null && Math.Abs(e.X - (ViewPort.X + NameWidth)) <= S(3);
        Cursor = nearSplitter ? Cursors.VSplit : Cursors.Default;
        if (row?.Key != _hotKey)
        {
            _hotKey = row?.Key;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hotKey = null;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (_showToolbar && e.Y < ViewPort.Y + ToolbarPx)
        {
            var buttons = ToolbarButtons();
            if (buttons[0].Contains(e.Location)) Categorized = true;
            else if (buttons[1].Contains(e.Location)) Categorized = false;
            return;
        }

        var row = RowAt(e.Location);
        if (row is null) return;
        if (e.Button == MouseButtons.Left && Math.Abs(e.X - (ViewPort.X + NameWidth)) <= S(3))
        {
            _dragSplitter = true;
            Capture = true;
            return;
        }

        var rect = RowRect(row);
        var wasSelected = row.Key == _selectedKey;
        SelectRow(row);
        if (row.Entry is null)
        {
            ToggleRow(row);
            return;
        }

        if (row.Entry.Kind == PropertyEditorKind.Nested && NameCell(rect).Contains(e.Location) && e.X < NameCell(rect).X + S(10) + row.Depth * S(18) + S(20))
        {
            ToggleRow(row);
            return;
        }

        if (ValueCell(rect).Contains(e.Location) && e.Button == MouseButtons.Left)
        {
            if (row.Entry.Kind is PropertyEditorKind.Boolean or PropertyEditorKind.Enum or PropertyEditorKind.Choice || wasSelected) BeginEdit(row);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragSplitter = false;
        Capture = false;
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        var row = RowAt(e.Location);
        if (row is null) return;
        if (row.Entry is null || row.Entry.Kind == PropertyEditorKind.Nested) ToggleRow(row);
        else if (row.Entry.Kind == PropertyEditorKind.Color && !row.Entry.IsReadOnly && row.Entry.Value is Color color)
        {
            using var dialog = new ColorDialog { Color = color, FullOpen = true };
            if (dialog.ShowDialog(this) == DialogResult.OK) Commit(row.Entry, dialog.Color);
        }
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End or Keys.Enter or Keys.Space or Keys.F2 or Keys.Delete
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_rows.Count == 0) return;
        var index = _rows.FindIndex(r => r.Key == _selectedKey);
        var page = Math.Max(1, RowsArea.Height / RowPx - 1);
        var target = e.KeyCode switch
        {
            Keys.Down => index + 1,
            Keys.Up => Math.Max(0, index - 1),
            Keys.PageDown => Math.Max(0, index) + page,
            Keys.PageUp => Math.Max(0, index) - page,
            Keys.Home => 0,
            Keys.End => _rows.Count - 1,
            _ => int.MinValue
        };

        if (target != int.MinValue)
        {
            SelectRow(_rows[Math.Clamp(target, 0, _rows.Count - 1)]);
            e.Handled = true;
            return;
        }

        var row = index >= 0 ? _rows[index] : null;
        if (row is null) return;
        switch (e.KeyCode)
        {
            case Keys.Right when row.Entry is null && row.Collapsed:
            case Keys.Left when row.Entry is null && !row.Collapsed:
            case Keys.Right when row.Entry is { Kind: PropertyEditorKind.Nested } && !row.Expanded:
            case Keys.Left when row.Entry is { Kind: PropertyEditorKind.Nested } && row.Expanded:
                ToggleRow(row);
                e.Handled = true;
                break;
            case Keys.Enter or Keys.F2 when row.Entry is not null:
                BeginEdit(row);
                e.Handled = true;
                break;
            case Keys.Space when row.Entry is { Kind: PropertyEditorKind.Boolean }:
                BeginEdit(row);
                e.Handled = true;
                break;
            case Keys.Space or Keys.Enter when row.Entry is null:
                ToggleRow(row);
                e.Handled = true;
                break;
            case Keys.Delete when row.Entry is not null && !row.Entry.IsReadOnly:
                row.Entry.Reset();
                AfterChange(row.Entry);
                e.Handled = true;
                break;
        }
    }
}
