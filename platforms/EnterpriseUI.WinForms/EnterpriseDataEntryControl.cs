using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using EnterpriseUI.Grids;

namespace EnterpriseUI.WinForms;

/// <summary>Generates an edit form for an object from its properties and data annotations, with multi-column layout, validation and apply/reset.</summary>
public class EnterpriseDataEntryControl : EnterpriseScrollableControl
{
    private sealed class FieldCell
    {
        public EntityField Field = null!;
        public Control Editor = null!;
        public Rectangle Bounds;
        public int EditorTop;
        public int EditorHeight;
    }

    private sealed class GroupHeader
    {
        public string Title = string.Empty;
        public Rectangle Bounds;
    }

    private readonly List<FieldCell> _cells = new();
    private readonly List<GroupHeader> _headers = new();
    private EntityFormModel? _model;
    private object? _entity;
    private bool _loading;
    private bool _showButtons = true;
    private int _columnWidth = 340;
    private Rectangle _applyRect;
    private Rectangle _resetRect;
    private int _contentHeight;
    private string? _status;

    public EnterpriseDataEntryControl()
    {
        Size = new Size(720, 460);
        AccessibleRole = AccessibleRole.Grouping;
    }

    public event EventHandler? Applied;
    public event EventHandler<IReadOnlyDictionary<string, string>>? ValidationFailed;
    public event EventHandler<string>? FieldChanged;

    public object? Entity
    {
        get => _entity;
        set
        {
            _entity = value;
            Build();
        }
    }

    public EntityFormModel? Model => _model;

    /// <summary>Preferred width of one column in logical pixels; the number of columns follows the control width.</summary>
    public int ColumnWidth
    {
        get => _columnWidth;
        set { _columnWidth = Math.Max(180, value); LayoutFields(); }
    }

    public bool ShowButtons
    {
        get => _showButtons;
        set { _showButtons = value; LayoutFields(); }
    }

    public string ApplyText { get; set; } = "Save";
    public string ResetText { get; set; } = "Reset";

    private int ButtonBarPx => _showButtons ? S(56) : 0;
    protected override int LineHeight => S(40);
    protected override Size ContentSize => new(0, _contentHeight + ButtonBarPx);

    public Control? GetEditor(string field) => _cells.FirstOrDefault(c => string.Equals(c.Field.Name, field, StringComparison.OrdinalIgnoreCase))?.Editor;

    private void Build()
    {
        foreach (var cell in _cells)
        {
            Controls.Remove(cell.Editor);
            cell.Editor.Dispose();
        }

        _cells.Clear();
        _status = null;
        _model = _entity is null ? null : new EntityFormModel(_entity);
        if (_model is not null)
        {
            _loading = true;
            foreach (var field in _model.Fields) _cells.Add(CreateCell(field));
            _loading = false;
        }

        LayoutFields();
    }

    private FieldCell CreateCell(EntityField field)
    {
        var cell = new FieldCell { Field = field };
        var text = _model!.CurrentText(field.Name);
        switch (field.Kind)
        {
            case EntityEditorKind.Boolean:
            {
                var check = new EnterpriseCheckControl { Text = field.Label, Checked = _model.CurrentValue(field.Name) is true, ReadOnly = field.IsReadOnly, Enabled = true };
                check.CheckedChanged += (_, _) => OnValueEdited(field, null, check.Checked);
                cell.Editor = check;
                cell.EditorHeight = 28;
                break;
            }
            case EntityEditorKind.Choice:
            {
                var drop = new EnterpriseDropDownControl { Enabled = !field.IsReadOnly };
                drop.SetItems(field.AllowsNull ? new[] { string.Empty }.Concat(field.Choices) : field.Choices);
                drop.SelectedItem = text;
                if (text.Length == 0 && field.AllowsNull) drop.SelectedIndex = 0;
                drop.SelectionChanged += (_, _) => OnValueEdited(field, drop.SelectedItem ?? string.Empty, null);
                cell.Editor = drop;
                cell.EditorHeight = 34;
                break;
            }
            default:
            {
                var input = new EnterpriseInputControl
                {
                    Text = field.Kind == EntityEditorKind.Date && _model.CurrentValue(field.Name) is DateTime date ? date.ToString("d", CultureInfo.CurrentCulture) : text,
                    ReadOnly = field.IsReadOnly,
                    UsePasswordChar = field.Kind == EntityEditorKind.Password,
                    Multiline = field.Kind == EntityEditorKind.MultiLineText,
                    PlaceholderText = field.Prompt ?? string.Empty
                };
                if (field.MaxLength is > 0 and < 100000) input.MaxLength = field.MaxLength.Value;
                input.ValueChanged += (_, _) => OnValueEdited(field, input.Text, null);
                input.InnerTextBox.Leave += (_, _) => Invalidate();
                cell.Editor = input;
                cell.EditorHeight = field.Kind == EntityEditorKind.MultiLineText ? 96 : 34;
                break;
            }
        }

        Controls.Add(cell.Editor);
        cell.Editor.Enter += (_, _) => EnsureVisible(new Rectangle(0, cell.Bounds.Y, 1, cell.Bounds.Height + ButtonBarPx));
        return cell;
    }

    private void OnValueEdited(EntityField field, string? text, object? value)
    {
        if (_loading || _model is null) return;
        _status = null;
        if (value is not null) _model.SetValue(field.Name, value, out _);
        else _model.SetText(field.Name, text, out _);
        UpdateErrorMarks();
        FieldChanged?.Invoke(this, field.Name);
        Invalidate();
    }

    private void UpdateErrorMarks()
    {
        foreach (var cell in _cells)
        {
            var hasError = _model!.Errors.ContainsKey(cell.Field.Name);
            switch (cell.Editor)
            {
                case EnterpriseInputControl input: input.HasError = hasError; break;
                case EnterpriseDropDownControl drop: drop.HasError = hasError; break;
            }
        }
    }

    public bool Validate()
    {
        if (_model is null) return true;
        var errors = _model.Validate();
        UpdateErrorMarks();
        Invalidate();
        return errors.Count == 0;
    }

    /// <summary>Validates and writes the entered values to the object.</summary>
    public bool Apply()
    {
        if (_model is null) return false;
        if (_model.Apply())
        {
            UpdateErrorMarks();
            _status = "Saved";
            Invalidate();
            Applied?.Invoke(this, EventArgs.Empty);
            return true;
        }

        UpdateErrorMarks();
        _status = null;
        Invalidate();
        ValidationFailed?.Invoke(this, _model.Errors);
        return false;
    }

    /// <summary>Discards pending changes and shows the values of the object again.</summary>
    public void ResetChanges()
    {
        if (_model is null) return;
        _model.Reset();
        _loading = true;
        foreach (var cell in _cells)
        {
            var text = _model.CurrentText(cell.Field.Name);
            switch (cell.Editor)
            {
                case EnterpriseCheckControl check: check.Checked = _model.CurrentValue(cell.Field.Name) is true; break;
                case EnterpriseDropDownControl drop:
                    drop.SelectedItem = text;
                    if (text.Length == 0 && cell.Field.AllowsNull) drop.SelectedIndex = 0;
                    break;
                case EnterpriseInputControl input:
                    input.Text = cell.Field.Kind == EntityEditorKind.Date && _model.CurrentValue(cell.Field.Name) is DateTime date ? date.ToString("d", CultureInfo.CurrentCulture) : text;
                    break;
            }
        }

        _loading = false;
        _status = null;
        UpdateErrorMarks();
        Invalidate();
    }

    // ---- layout -------------------------------------------------------------------------

    private int Columns(int width)
    {
        var usable = Math.Max(1, width - S(32));
        return Math.Max(1, usable / S(_columnWidth));
    }

    private void LayoutFields()
    {
        _headers.Clear();
        var width = Math.Max(1, InnerBounds.Width - (_contentHeight > InnerBounds.Height ? ScrollBarSize : 0));
        var columns = Columns(width);
        var pad = S(16);
        var gap = S(20);
        var colWidth = Math.Max(S(120), (width - pad * 2 - gap * (columns - 1)) / columns);
        var labelPx = S(22);
        var helpPx = S(20);
        var rowGap = S(8);

        var useGroups = _cells.Any(c => !string.IsNullOrEmpty(c.Field.Group));
        var groups = _cells.GroupBy(c => string.IsNullOrEmpty(c.Field.Group) ? "General" : c.Field.Group!).ToList();
        var y = pad;
        foreach (var group in groups)
        {
            if (useGroups)
            {
                _headers.Add(new GroupHeader { Title = group.Key, Bounds = new Rectangle(pad, y, width - pad * 2, S(30)) });
                y += S(38);
            }

            var index = 0;
            var rowHeight = 0;
            var rowTop = y;
            foreach (var cell in group)
            {
                var isCheck = cell.Editor is EnterpriseCheckControl;
                var col = index % columns;
                if (col == 0 && index > 0)
                {
                    rowTop += rowHeight + rowGap;
                    rowHeight = 0;
                }

                var editorH = S(cell.EditorHeight);
                var cellH = (isCheck ? 0 : labelPx) + editorH + helpPx;
                cell.Bounds = new Rectangle(pad + col * (colWidth + gap), rowTop, colWidth, cellH);
                cell.EditorTop = isCheck ? 0 : labelPx;
                rowHeight = Math.Max(rowHeight, cellH);
                index++;
            }

            y = rowTop + rowHeight + (group.Any() ? S(16) : 0);
        }

        _contentHeight = y;
        UpdateScrollBars();
        PositionEditors();
        Invalidate();
    }

    private void PositionEditors()
    {
        var inner = InnerBounds;
        var area = new Rectangle(inner.X, inner.Y, Math.Max(0, ViewPort.Width), Math.Max(0, ViewPort.Bottom - ButtonBarPx - inner.Y));
        foreach (var cell in _cells)
        {
            var bounds = new Rectangle(cell.Bounds.X + inner.X, cell.Bounds.Y + cell.EditorTop - ScrollY + inner.Y, cell.Bounds.Width, S(cell.EditorHeight));
            cell.Editor.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            var visible = Rectangle.Intersect(bounds, area);
            cell.Editor.Visible = visible.Width > 0 && visible.Height > 0;
            if (!cell.Editor.Visible) continue;
            if (visible == bounds)
            {
                cell.Editor.Region = null;
            }
            else
            {
                visible.Offset(-bounds.X, -bounds.Y);
                cell.Editor.Region = new Region(visible);
            }
        }

        var view = ViewPort;
        var buttonsTop = view.Bottom - ButtonBarPx + S(10);
        var h = S(34);
        _applyRect = new Rectangle(view.Right - S(16) - S(100), buttonsTop, S(100), h);
        _resetRect = new Rectangle(_applyRect.X - S(8) - S(100), buttonsTop, S(100), h);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_cells.Count > 0) LayoutFields();
    }

    protected override void OnScrolled()
    {
        PositionEditors();
        base.OnScrolled();
    }

    // ---- painting -----------------------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        PaintFrame(g, drawFocus: false);
        var view = ViewPort;
        var areaBottom = view.Bottom - ButtonBarPx;
        g.SetClip(new Rectangle(view.X, view.Y, view.Width, Math.Max(0, areaBottom - view.Y)));

        if (_cells.Count == 0)
        {
            Chrome.DrawText(g, "No object to edit", Font, view, Colors.Muted, ContentAlignment.MiddleCenter);
        }

        foreach (var header in _headers)
        {
            var b = header.Bounds;
            b.Offset(view.X, view.Y - ScrollY);
            Chrome.DrawText(g, header.Title, BoldFont, new Rectangle(b.X, b.Y, b.Width, S(24)), Colors.Primary);
            using var pen = new Pen(Colors.Border);
            g.DrawLine(pen, b.X, b.Y + S(28), b.Right, b.Y + S(28));
        }

        foreach (var cell in _cells)
        {
            var b = cell.Bounds;
            b.Offset(view.X, view.Y - ScrollY);
            if (b.Bottom < view.Y || b.Top > areaBottom) continue;
            var field = cell.Field;
            if (cell.Editor is not EnterpriseCheckControl)
            {
                var labelRect = new Rectangle(b.X, b.Y, b.Width, S(22));
                Chrome.DrawText(g, field.Label, Font, labelRect, Colors.Text, ContentAlignment.TopLeft);
                if (field.IsRequired && !field.IsReadOnly)
                {
                    var star = Chrome.MeasureText(field.Label, Font) + S(3);
                    Chrome.DrawText(g, "*", Font, new Rectangle(b.X + star, b.Y, S(12), S(22)), Colors.Danger, ContentAlignment.TopLeft);
                }
            }

            var below = new Rectangle(b.X, b.Y + cell.EditorTop + S(cell.EditorHeight) + S(2), b.Width, S(18));
            if (_model is not null && _model.Errors.TryGetValue(field.Name, out var error))
            {
                Chrome.DrawText(g, error, Font, below, Colors.Danger, ContentAlignment.TopLeft);
            }
            else if (!string.IsNullOrEmpty(field.Description))
            {
                Chrome.DrawText(g, field.Description, Font, below, Colors.Muted, ContentAlignment.TopLeft);
            }
        }

        g.ResetClip();
        if (_showButtons) PaintButtonBar(g, view);
    }

    private void PaintButtonBar(Graphics g, Rectangle view)
    {
        var bar = new Rectangle(view.X, view.Bottom - ButtonBarPx, view.Width, ButtonBarPx);
        using (var brush = new SolidBrush(Colors.Header)) g.FillRectangle(brush, bar);
        using (var pen = new Pen(Colors.Border)) g.DrawLine(pen, bar.X, bar.Y, bar.Right, bar.Y);

        var hasErrors = _model is { Errors.Count: > 0 };
        if (hasErrors) Chrome.DrawText(g, $"{_model!.Errors.Count} field(s) need attention", Font, new Rectangle(bar.X + S(16), bar.Y, Math.Max(0, _resetRect.X - bar.X - S(24)), bar.Height), Colors.Danger);
        else if (_status is not null) Chrome.DrawText(g, _status, Font, new Rectangle(bar.X + S(16), bar.Y, Math.Max(0, _resetRect.X - bar.X - S(24)), bar.Height), Colors.Success);
        else if (_model is { HasChanges: true }) Chrome.DrawText(g, "Unsaved changes", Font, new Rectangle(bar.X + S(16), bar.Y, Math.Max(0, _resetRect.X - bar.X - S(24)), bar.Height), Colors.Muted);

        var pending = _model?.HasChanges == true;
        Chrome.FillRounded(g, _resetRect, Colors.Surface, Chrome.CornerRadius);
        Chrome.DrawRoundedBorder(g, _resetRect, Colors.Border, Chrome.CornerRadius);
        Chrome.DrawText(g, ResetText, Font, _resetRect, pending ? Colors.Text : Colors.Muted, ContentAlignment.MiddleCenter);
        Chrome.FillRounded(g, _applyRect, Colors.Primary, Chrome.CornerRadius);
        Chrome.DrawText(g, ApplyText, BoldFont, _applyRect, Colors.PrimaryText, ContentAlignment.MiddleCenter);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!_showButtons || e.Button != MouseButtons.Left) return;
        if (_applyRect.Contains(e.Location)) Apply();
        else if (_resetRect.Contains(e.Location)) ResetChanges();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = _showButtons && (_applyRect.Contains(e.Location) || _resetRect.Contains(e.Location)) ? Cursors.Hand : Cursors.Default;
    }
}
