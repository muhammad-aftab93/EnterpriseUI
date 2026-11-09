using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using EnterpriseUI.Data;
using EnterpriseUI.Grids;

namespace EnterpriseUI.WinForms;

/// <summary>Visual builder for nested AND/OR/NOT filter expressions over the properties of a type; results convert to filter descriptors and a readable text.</summary>
public class EnterpriseDataFilterControl : EnterpriseScrollableControl
{
    private sealed class RowInfo
    {
        public FilterNode Node = null!;
        public int Depth;
        public Rectangle Bounds;
        public Rectangle CardBounds;
        public List<(Rectangle Bounds, string Caption, Action Action, bool Active, bool Danger)> Buttons = new();
        public List<Control> Widgets = new();
        public List<(Control Control, Rectangle Bounds)> Placed = new();
    }

    private readonly List<RowInfo> _rows = new();
    private DataFilterModel<object> _model = new(typeof(object));
    private Type _itemType = typeof(object);
    private int _contentHeight;
    private int _minWidth;
    private bool _rebuilding;
    private int _hotButton = -1;
    private int _hotRow = -1;

    public EnterpriseDataFilterControl()
    {
        Size = new Size(760, 320);
        AccessibleRole = AccessibleRole.Grouping;
    }

    public event EventHandler? FilterChanged;

    public Type ItemType
    {
        get => _itemType;
        set
        {
            _itemType = value ?? typeof(object);
            _model = new DataFilterModel<object>(_itemType);
            Rebuild();
            FilterChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public DataFilterModel<object> Model => _model;

    public string FilterText => _model.ToText();
    public IReadOnlyList<string> Validate() => _model.Validate();
    public IFilterDescriptor? ToDescriptor() => _model.ToDescriptor();
    public bool Matches(object item) => _model.Matches(item);
    public IEnumerable<object> Apply(IEnumerable<object> items) => _model.Apply(items);

    private int RowPx => S(48);
    private int PreviewPx => S(44);
    protected override int LineHeight => RowPx;
    protected override Size ContentSize => new(_minWidth, _contentHeight + PreviewPx);

    public void Load(IFilterDescriptor? descriptor)
    {
        _model.Load(descriptor);
        Rebuild();
        FilterChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _model.Root.Children.Clear();
        _model.Root.Negate = false;
        Changed();
    }

    public FilterConditionNode AddCondition(FilterGroupNode? group = null)
    {
        group ??= _model.Root;
        var field = _model.Fields.FirstOrDefault() ?? string.Empty;
        var type = _model.FieldTypeOf(field);
        var op = DataFilterModel<object>.OperatorsFor(type)[0];
        var node = group.AddCondition(field, op, DefaultValue(type));
        Changed();
        return node;
    }

    public FilterGroupNode AddGroup(FilterGroupNode? parent = null)
    {
        parent ??= _model.Root;
        var group = parent.AddGroup(FilterLogicalOperator.Or);
        AddCondition(group);
        return group;
    }

    private static string DefaultValue(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(bool)) return "True";
        if (type.IsEnum) return Enum.GetNames(type).FirstOrDefault() ?? string.Empty;
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return DateTime.Today.ToString("d", CultureInfo.CurrentCulture);
        if (type == typeof(string)) return string.Empty;
        if (type == typeof(Guid)) return Guid.Empty.ToString();
        return Convert.ToString(Convert.ChangeType(0, type, CultureInfo.InvariantCulture), CultureInfo.CurrentCulture) ?? "0";
    }

    private void Changed()
    {
        Rebuild();
        _model.RaiseChanged();
        FilterChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---- rows ---------------------------------------------------------------------------

    private void Rebuild()
    {
        if (_rebuilding) return;
        _rebuilding = true;
        try
        {
            foreach (var row in _rows)
            {
                foreach (var widget in row.Widgets)
                {
                    Controls.Remove(widget);
                    widget.Dispose();
                }
            }

            _rows.Clear();
            AddRows(_model.Root, 0);
            LayoutRows();
        }
        finally
        {
            _rebuilding = false;
        }
    }

    private void AddRows(FilterNode node, int depth)
    {
        var row = new RowInfo { Node = node, Depth = depth };
        _rows.Add(row);
        if (node is FilterGroupNode group)
        {
            foreach (var child in group.Children) AddRows(child, depth + 1);
        }
        else
        {
            CreateConditionWidgets(row, (FilterConditionNode)node);
        }
    }

    private T Track<T>(RowInfo row, T control) where T : Control
    {
        row.Widgets.Add(control);
        Controls.Add(control);
        return control;
    }

    private void CreateConditionWidgets(RowInfo row, FilterConditionNode condition)
    {
        var type = _model.FieldTypeOf(condition.Field);
        var fieldDrop = Track(row, new EnterpriseDropDownControl { PlaceholderText = "Field" });
        fieldDrop.SetItems(_model.Fields);
        fieldDrop.SelectedItem = condition.Field;
        fieldDrop.SelectionChanged += (_, _) =>
        {
            if (_rebuilding || fieldDrop.SelectedItem is not { } field) return;
            var newType = _model.FieldTypeOf(field);
            condition.Field = field;
            var operators = DataFilterModel<object>.OperatorsFor(newType);
            if (!operators.Contains(condition.Operator)) condition.Operator = operators[0];
            condition.Value = DefaultValue(newType);
            condition.Value2 = DefaultValue(newType);
            Changed();
        };

        var operators = DataFilterModel<object>.OperatorsFor(type);
        var opDrop = Track(row, new EnterpriseDropDownControl { PlaceholderText = "Operator" });
        opDrop.SetItems(operators.Select(DataFilterModel<object>.Describe));
        opDrop.SelectedItem = DataFilterModel<object>.Describe(condition.Operator);
        opDrop.SelectionChanged += (_, _) =>
        {
            if (_rebuilding || opDrop.SelectedIndex < 0) return;
            condition.Operator = operators[opDrop.SelectedIndex];
            Changed();
        };

        if (!DataFilterModel<object>.NeedsValue(condition.Operator)) return;
        CreateValueEditor(row, condition, type, second: false);
        if (DataFilterModel<object>.NeedsSecondValue(condition.Operator)) CreateValueEditor(row, condition, type, second: true);
    }

    private void CreateValueEditor(RowInfo row, FilterConditionNode condition, Type type, bool second)
    {
        var current = (second ? condition.Value2 : condition.Value) ?? string.Empty;
        var core = Nullable.GetUnderlyingType(type) ?? type;
        void Set(string? text)
        {
            if (second) condition.Value2 = text;
            else condition.Value = text;
        }

        if (core == typeof(bool) || core.IsEnum)
        {
            var drop = Track(row, new EnterpriseDropDownControl { PlaceholderText = "Value" });
            drop.SetItems(core == typeof(bool) ? new[] { "True", "False" } : Enum.GetNames(core));
            drop.SelectedItem = current;
            drop.SelectionChanged += (_, _) =>
            {
                if (_rebuilding) return;
                Set(drop.SelectedItem);
                _model.RaiseChanged();
                FilterChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            };
            return;
        }

        var input = Track(row, new EnterpriseInputControl { Text = current, PlaceholderText = second ? "Second value" : "Value" });
        input.HasError = !DataFilterModel<object>.TryConvert(type, current, out _) && current.Length > 0;
        input.ValueChanged += (_, _) =>
        {
            if (_rebuilding) return;
            Set(input.Text);
            input.HasError = input.Text.Length > 0 && !DataFilterModel<object>.TryConvert(type, input.Text, out _);
            _model.RaiseChanged();
            FilterChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        };
    }

    private void LayoutRows()
    {
        var pad = S(14);
        var indent = S(28);
        var rowH = RowPx;
        var view = ViewPort;
        var width = Math.Max(view.Width, 1);
        var y = pad;
        _minWidth = 0;

        foreach (var row in _rows)
        {
            row.Buttons.Clear();
            row.Placed.Clear();
            var x = pad + row.Depth * indent;
            row.Bounds = new Rectangle(0, y, width, rowH);
            var cy = y + (rowH - S(34)) / 2;
            var h = S(34);
            var right = Math.Max(width, S(720)) - pad;

            if (row.Node is FilterGroupNode group)
            {
                var chipW = S(60);
                var and = group.Operator == FilterLogicalOperator.And;
                var cx = x + S(10);
                var op = group;
                row.Buttons.Add((new Rectangle(cx, cy, chipW, h), and ? "AND" : "OR", () => { op.Operator = and ? FilterLogicalOperator.Or : FilterLogicalOperator.And; Changed(); }, true, false));
                cx += chipW + S(8);
                row.Buttons.Add((new Rectangle(cx, cy, S(52), h), "NOT", () => { op.Negate = !op.Negate; Changed(); }, group.Negate, true));

                var rx = right;
                if (row.Depth > 0)
                {
                    rx -= S(34);
                    var parent = group.Parent!;
                    row.Buttons.Add((new Rectangle(rx, cy, S(34), h), "✕", () => { parent.Remove(op); Changed(); }, false, true));
                    rx -= S(8);
                }

                rx -= S(96);
                row.Buttons.Add((new Rectangle(rx, cy, S(96), h), "+ Group", () => AddGroup(op), false, false));
                rx -= S(8) + S(110);
                row.Buttons.Add((new Rectangle(rx, cy, S(110), h), "+ Condition", () => AddCondition(op), false, false));
                _minWidth = Math.Max(_minWidth, S(720));
            }
            else
            {
                var condition = (FilterConditionNode)row.Node;
                var rx = right;
                rx -= S(34);
                var parent = condition.Parent!;
                row.Buttons.Add((new Rectangle(rx, cy, S(34), h), "✕", () => { parent.Remove(condition); Changed(); }, false, true));
                rx -= S(8) + S(52);
                row.Buttons.Add((new Rectangle(rx, cy, S(52), h), "NOT", () => { condition.Negate = !condition.Negate; Changed(); }, condition.Negate, true));
                var valueRight = rx - S(10);

                var fieldRect = new Rectangle(x + S(10), cy, S(160), h);
                var opRect = new Rectangle(fieldRect.Right + S(8), cy, S(190), h);
                row.Placed.Add((row.Widgets[0], fieldRect));
                row.Placed.Add((row.Widgets[1], opRect));
                var vx = opRect.Right + S(8);
                var available = Math.Max(S(120), valueRight - vx);
                if (row.Widgets.Count == 3) row.Placed.Add((row.Widgets[2], new Rectangle(vx, cy, available, h)));
                else if (row.Widgets.Count == 4)
                {
                    var half = (available - S(36)) / 2;
                    row.Placed.Add((row.Widgets[2], new Rectangle(vx, cy, half, h)));
                    row.Placed.Add((row.Widgets[3], new Rectangle(vx + half + S(36), cy, half, h)));
                }

                _minWidth = Math.Max(_minWidth, vx + S(120) + S(10) + S(52) + S(8) + S(34) + pad);
            }

            y += rowH;
        }

        _contentHeight = y + pad;

        // group card bounds: header row to the last descendant row
        for (var i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Node is not FilterGroupNode) continue;
            var last = i;
            while (last + 1 < _rows.Count && _rows[last + 1].Depth > _rows[i].Depth) last++;
            var x = pad + _rows[i].Depth * indent;
            _rows[i].CardBounds = new Rectangle(x, _rows[i].Bounds.Y + S(2), Math.Max(width, _minWidth) - pad * 2 - _rows[i].Depth * indent, _rows[last].Bounds.Bottom - _rows[i].Bounds.Y - S(4));
        }

        UpdateScrollBars();
        PositionWidgets();
        Invalidate();
    }

    private Rectangle RowsArea
    {
        get
        {
            var view = ViewPort;
            return new Rectangle(view.X, view.Y, view.Width, Math.Max(0, view.Height - PreviewPx));
        }
    }

    private Point Offset => new(ViewPort.X - ScrollX, ViewPort.Y - ScrollY);

    private void PositionWidgets()
    {
        var area = RowsArea;
        foreach (var row in _rows)
        {
            foreach (var (control, local) in row.Placed)
            {
                var bounds = local;
                bounds.Offset(Offset);
                control.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                var visible = Rectangle.Intersect(bounds, area);
                control.Visible = visible.Width > 0 && visible.Height > 0;
                if (!control.Visible) continue;
                if (visible == bounds)
                {
                    control.Region = null;
                }
                else
                {
                    visible.Offset(-bounds.X, -bounds.Y);
                    control.Region = new Region(visible);
                }
            }
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_rows.Count > 0 || _model.Root.Children.Count > 0) LayoutRows();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_rows.Count == 0) Rebuild();
    }

    protected override void OnScrolled()
    {
        PositionWidgets();
        base.OnScrolled();
    }

    // ---- painting -----------------------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        PaintFrame(g, drawFocus: false);
        var view = ViewPort;
        if (view.Width <= 0 || view.Height <= 0) return;
        g.SetClip(RowsArea);
        var offset = Offset;

        foreach (var row in _rows.Where(r => r.Node is FilterGroupNode))
        {
            var card = row.CardBounds;
            card.Offset(offset);
            Chrome.FillRounded(g, card, Chrome.Blend(Colors.Surface, Colors.Header, row.Depth % 2 == 0 ? 0.35 : 0.7), S(6));
            Chrome.DrawRoundedBorder(g, card, Colors.Border, S(6));
            var group = (FilterGroupNode)row.Node;
            using var bar = new SolidBrush(group.Operator == FilterLogicalOperator.And ? Colors.Primary : Colors.Warning);
            g.FillRectangle(bar, card.X + 1, card.Y + S(8), S(3), card.Height - S(16));
        }

        for (var r = 0; r < _rows.Count; r++)
        {
            var row = _rows[r];
            var bounds = row.Bounds;
            bounds.Offset(offset.X, offset.Y);
            if (bounds.Bottom < view.Top || bounds.Top > view.Bottom) continue;

            for (var b = 0; b < row.Buttons.Count; b++)
            {
                var (rect, caption, _, active, danger) = row.Buttons[b];
                rect.Offset(offset);
                PaintButton(g, rect, caption, active, danger, r == _hotRow && b == _hotButton, row.Node is FilterGroupNode && b == 0);
            }

            if (row.Node is FilterGroupNode groupNode)
            {
                var chipsRight = row.Buttons[1].Bounds;
                var text = groupNode.Operator == FilterLogicalOperator.And ? "All of the conditions must match" : "Any of the conditions may match";
                var textBounds = new Rectangle(chipsRight.Right + offset.X + S(12), bounds.Y, Math.Max(0, row.Buttons[^1].Bounds.X + offset.X - chipsRight.Right - offset.X - S(20)), bounds.Height);
                Chrome.DrawText(g, text, Font, textBounds, Colors.Muted);
            }
            else if (row.Widgets.Count == 4)
            {
                var between = row.Placed[2].Bounds;
                var second = row.Placed[3].Bounds;
                var andBounds = new Rectangle(between.Right + offset.X, between.Y + offset.Y, second.X - between.Right, between.Height);
                Chrome.DrawText(g, "and", Font, andBounds, Colors.Muted, ContentAlignment.MiddleCenter);
            }
        }

        g.ResetClip();
        PaintPreview(g, view);
    }

    private void PaintButton(Graphics g, Rectangle rect, string caption, bool active, bool danger, bool hot, bool chip)
    {
        if (active && chip)
        {
            Chrome.FillRounded(g, rect, Colors.HighContrast ? Colors.Header : Chrome.Blend(Colors.Surface, Colors.Primary, hot ? 0.3 : 0.2), S(4));
            Chrome.DrawRoundedBorder(g, rect, Colors.Primary, S(4));
            Chrome.DrawText(g, caption, BoldFont, rect, Colors.HighContrast ? Colors.Text : Colors.Primary, ContentAlignment.MiddleCenter);
            return;
        }

        var color = active ? Colors.Danger : danger && hot ? Colors.Danger : Colors.Muted;
        var back = hot ? Chrome.Blend(Colors.Surface, danger ? Colors.Danger : Colors.Primary, 0.12) : Colors.Surface;
        Chrome.FillRounded(g, rect, active ? Chrome.Blend(Colors.Surface, Colors.Danger, 0.16) : back, S(4));
        Chrome.DrawRoundedBorder(g, rect, active ? Colors.Danger : hot ? (danger ? Colors.Danger : Colors.Primary) : Colors.Border, S(4));
        Chrome.DrawText(g, caption, Font, rect, hot && !danger ? Colors.Primary : color, ContentAlignment.MiddleCenter);
    }

    private void PaintPreview(Graphics g, Rectangle view)
    {
        var band = new Rectangle(view.X, view.Bottom - PreviewPx, view.Width, PreviewPx);
        using (var brush = new SolidBrush(Colors.Header)) g.FillRectangle(brush, band);
        using (var pen = new Pen(Colors.Border)) g.DrawLine(pen, band.X, band.Y, band.Right, band.Y);
        var errors = _model.Validate();
        var text = errors.Count > 0 ? errors[0] : _model.ToText();
        var empty = text.Length == 0;
        Chrome.DrawText(g, empty ? "No filter" : text, empty || errors.Count == 0 ? Font : Font, new Rectangle(band.X + S(14), band.Y, Math.Max(0, band.Width - S(28)), band.Height), errors.Count > 0 ? Colors.Danger : empty ? Colors.Muted : Colors.Text);
    }

    // ---- input --------------------------------------------------------------------------

    private (int Row, int Button) ButtonAt(Point location)
    {
        if (!RowsArea.Contains(location)) return (-1, -1);
        var offset = Offset;
        for (var r = 0; r < _rows.Count; r++)
        {
            for (var b = 0; b < _rows[r].Buttons.Count; b++)
            {
                var rect = _rows[r].Buttons[b].Bounds;
                rect.Offset(offset);
                if (rect.Contains(location)) return (r, b);
            }
        }

        return (-1, -1);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var (row, button) = ButtonAt(e.Location);
        if (row != _hotRow || button != _hotButton)
        {
            _hotRow = row;
            _hotButton = button;
            Cursor = row >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hotRow = _hotButton = -1;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button != MouseButtons.Left || !Enabled) return;
        var (row, button) = ButtonAt(e.Location);
        if (row < 0) return;
        _rows[row].Buttons[button].Action();
    }

    /// <summary>Runs one of the row buttons; used by tests and automation. Captions: AND, OR, NOT, + Condition, + Group, ✕.</summary>
    public bool PressButton(int row, string caption)
    {
        if (row < 0 || row >= _rows.Count) return false;
        var isOperator = caption is "AND" or "OR";
        var button = _rows[row].Buttons.FirstOrDefault(b => isOperator ? b.Caption is "AND" or "OR" : b.Caption == caption);
        if (button.Action is null) return false;
        button.Action();
        return true;
    }

    public int RowCount => _rows.Count;
}

internal sealed class EnterpriseDataFilterDialog : Form
{
    private readonly EnterpriseDataFilterControl _filter = new() { Dock = DockStyle.Fill };

    public EnterpriseDataFilterDialog(Type itemType, IFilterDescriptor? initial)
    {
        Text = "Edit filter";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(820, 440);
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        _filter.ItemType = itemType;
        _filter.Load(initial);
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 90 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90 };
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 48, Padding = new Padding(12, 8, 12, 8) };
        bar.Controls.Add(cancel);
        bar.Controls.Add(ok);
        Controls.Add(_filter);
        Controls.Add(bar);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public IFilterDescriptor? Result => _filter.ToDescriptor();
}

/// <summary>Opens the filter builder in a dialog.</summary>
public static class EnterpriseDataFilter
{
    /// <summary>Shows the builder and returns the edited filter, or <paramref name="initial"/> when cancelled.</summary>
    public static IFilterDescriptor? Edit(IWin32Window? owner, Type itemType, IFilterDescriptor? initial = null)
    {
        using var dialog = new EnterpriseDataFilterDialog(itemType, initial);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.Result : initial;
    }
}
