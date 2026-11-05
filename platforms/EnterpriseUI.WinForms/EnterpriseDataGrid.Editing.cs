using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using EnterpriseUI.Grids;

namespace EnterpriseUI.WinForms;

public partial class EnterpriseDataGrid<TItem>
{
    private Control? _editor;
    private EditorMode _editorMode;
    private int _editingRow = -1;
    private int _editingColumnIndex = -1;
    private GridColumn? _editingColumn;
    private bool _closingEditor;

    /// <summary>True while a cell editor is open.</summary>
    public bool IsEditing => _editor != null;

    private bool CanEdit(GridColumn column) => _engine.EditMode != GridEditMode.None && column.IsEditable && column is not GridCommandColumn;

    // ------------------------------------------------------------------
    // In-cell editing
    // ------------------------------------------------------------------

    /// <summary>Starts editing the cell at a display row and visible column index; returns false when the cell is not editable.</summary>
    public bool BeginEdit(int displayRow, int columnIndex) => BeginEditAt(ComputeFrame(), displayRow, columnIndex);

    private bool BeginEditAt(Frame f, int rowIndex, int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= f.Ordered.Count) return false;
        if (!_engine.TryGetRowAt(rowIndex, out var row) || row.Kind != GridRowKind.Data) return false;

        var column = f.Ordered[columnIndex];
        if (!CanEdit(column)) return false;

        var value = CellValue(row, column);
        var type = ResolveType(column, value);
        if (type == GridColumnType.Boolean)
        {
            ToggleBoolean(row, column);
            return true;
        }

        if (type == GridColumnType.Command) return false;

        ScrollToRow(rowIndex);
        EnsureColumnVisible(columnIndex);
        f = ComputeFrame();

        var colSlot = f.Layout.Slots.FirstOrDefault(s => ReferenceEquals(s.Column, column));
        if (colSlot == null) return false;

        foreach (var slot in EnumerateRows(f))
        {
            if (slot.Index != rowIndex) continue;

            var bounds = new Rectangle((int)colSlot.Left + f.Gutter + 1, slot.Bounds.Y + 1, Math.Max(S(24), (int)colSlot.Width - 2), slot.Bounds.Height - 2);
            _engine.Selection.SetFocus(rowIndex, columnIndex);
            OpenEditor(EditorMode.Cell, column, rowIndex, columnIndex, bounds, value, type);
            return true;
        }

        return false;
    }

    private void BeginFilterRowEdit(Frame f, ColumnSlot slot)
    {
        _engine.QuickFilters.TryGetValue(slot.Column.Field, out var current);
        OpenEditor(
            EditorMode.FilterRow,
            slot.Column,
            -1,
            -1,
            new Rectangle((int)slot.Left + f.Gutter + 1, f.FilterTop + 1, (int)slot.Width - 2, f.FilterH - 2),
            current ?? string.Empty,
            GridColumnType.Text);
    }

    private void OpenEditor(EditorMode mode, GridColumn column, int row, int columnIndex, Rectangle bounds, object? value, GridColumnType type)
    {
        CloseEditor(commit: true);

        var p = GetPalette();
        _editorMode = mode;
        _editingColumn = column;
        _editingRow = row;
        _editingColumnIndex = columnIndex;

        var title = string.IsNullOrEmpty(column.Title) ? column.Field : column.Title;
        Control editor;
        if (mode == EditorMode.Cell && type == GridColumnType.ComboBox && column.ComboItems != null)
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
            foreach (var item in column.ComboItems) combo.Items.Add(item);
            var index = value == null ? -1 : column.ComboItems.ToList().FindIndex(i => Equals(i, value) || string.Equals(Convert.ToString(i, CultureInfo.CurrentCulture), Convert.ToString(value, CultureInfo.CurrentCulture), StringComparison.Ordinal));
            combo.SelectedIndex = index;
            editor = combo;
        }
        else if (mode == EditorMode.Cell && (type == GridColumnType.Date || value is DateTime))
        {
            var picker = new DateTimePicker { Format = DateTimePickerFormat.Short };
            if (value is DateTime date && date >= picker.MinDate && date <= picker.MaxDate) picker.Value = date;
            editor = picker;
        }
        else
        {
            var text = mode == EditorMode.FilterRow ? Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty : _engine.FormatCellValue(column, value);
            editor = new TextBox
            {
                Text = mode == EditorMode.Cell && value != null && !(value is string) ? Convert.ToString(value, CultureInfo.CurrentCulture) ?? text : text,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = column.Alignment == ColumnTextAlignment.Right ? HorizontalAlignment.Right : column.Alignment == ColumnTextAlignment.Center ? HorizontalAlignment.Center : HorizontalAlignment.Left
            };
        }

        editor.Bounds = bounds;
        editor.Font = Font;
        editor.BackColor = p.Surface;
        editor.ForeColor = p.Text;
        editor.AccessibleName = mode == EditorMode.FilterRow ? $"Filter {title}" : $"Edit {title}";
        editor.PreviewKeyDown += (_, pe) =>
        {
            if (pe.KeyCode == Keys.Tab) pe.IsInputKey = true;
        };
        editor.KeyDown += OnEditorKeyDown;
        editor.Leave += (_, _) =>
        {
            if (!_closingEditor && ReferenceEquals(_editor, editor)) TryCloseEditor(commit: true, keepOpenOnError: false);
        };

        _editor = editor;
        Controls.Add(editor);
        editor.Focus();
        if (editor is TextBox box) box.SelectAll();
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Enter:
                e.Handled = e.SuppressKeyPress = true;
                TryCloseEditor(commit: true, keepOpenOnError: true);
                break;

            case Keys.Escape:
                e.Handled = e.SuppressKeyPress = true;
                CloseEditor(commit: false);
                break;

            case Keys.Tab when _editorMode == EditorMode.Cell:
                e.Handled = e.SuppressKeyPress = true;
                var row = _editingRow;
                var column = _editingColumnIndex;
                if (TryCloseEditor(commit: true, keepOpenOnError: true)) MoveEditorAfterTab(row, column, e.Shift ? -1 : 1);
                break;
        }
    }

    private void MoveEditorAfterTab(int row, int column, int step)
    {
        var f = ComputeFrame();
        var count = f.Ordered.Count;
        var total = _engine.TotalItemsCount;
        var r = row;
        var c = column;
        for (var guard = 0; guard < count * 2 + 2; guard++)
        {
            c += step;
            if (c >= count) { c = 0; r++; }
            else if (c < 0) { c = count - 1; r--; }
            if (r < 0 || r >= total) return;

            if (_engine.TryGetRowAt(r, out var next) && next.Kind == GridRowKind.Data && CanEdit(f.Ordered[c]))
            {
                _engine.Selection.Click(r, c);
                BeginEditAt(f, r, c);
                return;
            }
        }
    }

    private void CloseEditor(bool commit) => TryCloseEditor(commit, keepOpenOnError: false);

    /// <summary>Closes the editor, committing when asked. With <paramref name="keepOpenOnError"/> an invalid value leaves the editor open and shows the message.</summary>
    private bool TryCloseEditor(bool commit, bool keepOpenOnError)
    {
        if (_editor == null) return true;
        if (_closingEditor) return false;

        var editor = _editor;
        var mode = _editorMode;
        var column = _editingColumn;
        var row = _editingRow;
        object? value = editor switch
        {
            ComboBox combo => combo.SelectedItem,
            DateTimePicker picker => picker.Value,
            TextBox box => box.Text,
            _ => null
        };

        if (commit && column != null && mode == EditorMode.Cell && _engine.TryGetRowAt(row, out var gridRow) && gridRow.Item != null)
        {
            var result = _engine.TryEditCell(gridRow.Item, column.Field, value is string text ? text.Trim() : value);
            if (!result.Success && keepOpenOnError)
            {
                editor.BackColor = GetPalette().ErrorBack;
                _toolTip.Show(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)), this, editor.Left, editor.Bottom + S(2), 4000);
                AccessibilityAnnounce(string.Join(". ", result.Errors.Select(error => error.Message)), assertive: true);
                return false;
            }

            if (result.Success && result.Changed)
            {
                CellEdited?.Invoke(this, $"Edited [{column.Field}] = '{value}' (Row {row})");
            }
        }

        _closingEditor = true;
        _editor = null;
        _editingColumn = null;
        _editingRow = -1;
        _editingColumnIndex = -1;
        _editorMode = EditorMode.None;

        try
        {
            _toolTip.Hide(this);
            Controls.Remove(editor);
            editor.Dispose();

            if (commit && column != null && mode == EditorMode.FilterRow)
            {
                _engine.SetQuickFilter(column.Field, (value as string ?? string.Empty).Trim());
            }
        }
        finally
        {
            _closingEditor = false;
            if (IsHandleCreated && !IsDisposed) Focus();
            Invalidate();
        }

        return true;
    }

    private void ToggleBoolean(GridRow<TItem> row, GridColumn column)
    {
        var current = CellValue(row, column) is true;
        var result = _engine.TryEditCell(row.Item!, column.Field, !current);
        if (result.Success)
        {
            CellEdited?.Invoke(this, $"Edited [{column.Field}] = '{!current}' (Row {row.Index})");
        }
        else
        {
            AccessibilityAnnounce(string.Join(". ", result.Errors.Select(error => error.Message)), assertive: true);
        }

        Invalidate();
    }

    // ------------------------------------------------------------------
    // Row edit dialog (popup modal and form edit modes)
    // ------------------------------------------------------------------

    /// <summary>Edits all editable columns of a row in a dialog. Changes reach the item atomically and form one undo step.</summary>
    public bool ShowRowEditDialog(TItem item)
    {
        var p = GetPalette();
        var session = _engine.BeginRowEdit(item);
        var columns = GridColumnLayout.OrderVisible(_engine.Columns).Where(c => CanEdit(c)).ToList();

        using var form = new Form
        {
            Text = "Edit Row",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            Font = Font,
            BackColor = p.Surface,
            ForeColor = p.Text,
            AutoScaleMode = AutoScaleMode.Dpi,
            ClientSize = new Size(S(460), S(120) + columns.Count * S(34))
        };

        var table = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, Padding = new Padding(S(12)) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        var errors = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };
        var inputs = new List<(GridColumn Column, Control Input)>();

        foreach (var column in columns)
        {
            var title = string.IsNullOrEmpty(column.Title) ? column.Field : column.Title;
            var current = column.GetValue(item);
            Control input = ResolveType(column, current) switch
            {
                GridColumnType.Boolean => new CheckBox { Checked = current is true, Text = string.Empty, AutoSize = true },
                GridColumnType.ComboBox when column.ComboItems != null => BuildCombo(column, current),
                _ when current is DateTime date => new DateTimePicker { Format = DateTimePickerFormat.Short, Value = date },
                _ => new TextBox { Text = _engine.FormatCellValue(column, current) }
            };
            input.Dock = DockStyle.Fill;
            input.AccessibleName = title;
            var label = new Label { Text = title + ":", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoSize = false, Height = S(30) };
            table.Controls.Add(label);
            table.Controls.Add(input);
            inputs.Add((column, input));
        }

        var ok = new Button { Text = "Save", Width = S(88) };
        var cancel = new Button { Text = "Cancel", Width = S(88), DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = S(48), Padding = new Padding(S(12)) };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        form.Controls.Add(table);
        form.Controls.Add(buttons);
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        ok.Click += (_, _) =>
        {
            errors.Clear();
            foreach (var (column, input) in inputs)
            {
                session.SetValue(column.Field, input switch
                {
                    CheckBox check => check.Checked,
                    ComboBox combo => combo.SelectedItem,
                    DateTimePicker picker => picker.Value,
                    TextBox box => box.Text.Trim(),
                    _ => null
                });
            }

            if (session.TryCommit(out var found))
            {
                form.DialogResult = DialogResult.OK;
                return;
            }

            foreach (var error in found)
            {
                var target = inputs.FirstOrDefault(i => string.Equals(i.Column.Field, error.Field, StringComparison.OrdinalIgnoreCase)).Input;
                if (target != null) errors.SetError(target, error.Message);
            }
            form.DialogResult = DialogResult.None;
        };

        var saved = form.ShowDialog(FindForm()) == DialogResult.OK;
        if (!saved) session.Cancel();
        else CellEdited?.Invoke(this, "Edited row via dialog");
        errors.Dispose();
        Invalidate();
        return saved;
    }

    private static ComboBox BuildCombo(GridColumn column, object? current)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var item in column.ComboItems!) combo.Items.Add(item);
        combo.SelectedIndex = current == null ? -1 : column.ComboItems!.ToList().FindIndex(i => Equals(i, current));
        return combo;
    }
}
