using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;
using EnterpriseUI.Editors;

namespace EnterpriseUI.WinForms;

/// <summary>Scrolling hour, minute, second and AM/PM columns shown by the time editors.</summary>
internal sealed class TimeSelectorPanel : EnterpriseControlBase
{
    private enum Part { Hour, Minute, Second, Meridiem }

    private sealed class Column
    {
        public Column(Part part, string title) { Part = part; Title = title; }
        public Part Part { get; }
        public string Title { get; }
        public List<(string Label, int Value)> Items { get; } = new();
        public float Scroll { get; set; }
    }

    private const int VisibleRows = 7;
    private readonly List<Column> _columns = new();
    private readonly Func<TimeOnly?> _current;
    private readonly Func<TimeOnly, bool> _isAllowed;
    private readonly Func<TimeOnly, bool> _apply;
    private readonly Action _done;
    private readonly bool _is24;
    private readonly TimeOnly _fallback;
    private int _active;
    private Point _hot = new(-1, -1);
    private int _hotFooter = -1;

    public TimeSelectorPanel(Func<TimeOnly?> current, Func<TimeOnly, bool> isAllowed, Func<TimeOnly, bool> apply, Action done, bool is24Hour, bool showSeconds, int minuteStep, TimeOnly fallback)
    {
        _current = current;
        _isAllowed = isAllowed;
        _apply = apply;
        _done = done;
        _is24 = is24Hour;
        _fallback = fallback;

        var hours = new Column(Part.Hour, "Hour");
        if (is24Hour) for (var h = 0; h < 24; h++) hours.Items.Add((h.ToString("00", CultureInfo.InvariantCulture), h));
        else for (var h = 1; h <= 12; h++) hours.Items.Add((h.ToString(CultureInfo.InvariantCulture), h));
        _columns.Add(hours);

        var minutes = new Column(Part.Minute, "Min");
        for (var m = 0; m < 60; m += Math.Max(1, minuteStep)) minutes.Items.Add((m.ToString("00", CultureInfo.InvariantCulture), m));
        _columns.Add(minutes);

        if (showSeconds)
        {
            var seconds = new Column(Part.Second, "Sec");
            for (var s = 0; s < 60; s++) seconds.Items.Add((s.ToString("00", CultureInfo.InvariantCulture), s));
            _columns.Add(seconds);
        }

        if (!is24Hour)
        {
            var meridiem = new Column(Part.Meridiem, string.Empty);
            var format = CultureInfo.CurrentCulture.DateTimeFormat;
            meridiem.Items.Add((string.IsNullOrEmpty(format.AMDesignator) ? "AM" : format.AMDesignator, 0));
            meridiem.Items.Add((string.IsNullOrEmpty(format.PMDesignator) ? "PM" : format.PMDesignator, 1));
            _columns.Add(meridiem);
        }

        Size = new Size(S(60) * _columns.Count + 2, HeaderHeight + RowHeight * VisibleRows + FooterHeight + 2);
        TabStop = true;
        foreach (var column in _columns) CenterOnSelection(column);
    }

    private int RowHeight => S(28);
    private int ColumnWidth => S(60);
    private int HeaderHeight => S(24);
    private int FooterHeight => S(36);
    private Rectangle ColumnRect(int index) => new(1 + index * ColumnWidth, 1, ColumnWidth, HeaderHeight + RowHeight * VisibleRows);
    private Rectangle FooterButton(int index) => new(index == 0 ? S(8) : Width - S(8) - S(70), Height - FooterHeight + S(5), S(70), FooterHeight - S(10));
    private float MaxScroll(Column column) => Math.Max(0, column.Items.Count * RowHeight - RowHeight * VisibleRows);

    private int SelectedIndex(Column column)
    {
        if (_current() is not { } time) return -1;
        var value = column.Part switch
        {
            Part.Hour => _is24 ? time.Hour : (time.Hour % 12 == 0 ? 12 : time.Hour % 12),
            Part.Minute => time.Minute,
            Part.Second => time.Second,
            _ => time.Hour >= 12 ? 1 : 0
        };
        return column.Items.FindIndex(i => i.Value == value);
    }

    private TimeOnly Compose(Part part, int value)
    {
        var basis = _current() ?? _fallback;
        int h = basis.Hour, m = basis.Minute, s = basis.Second;
        switch (part)
        {
            case Part.Hour: h = _is24 ? value : (value % 12) + (h >= 12 ? 12 : 0); break;
            case Part.Minute: m = value; break;
            case Part.Second: s = value; break;
            default: h = (h % 12) + (value == 1 ? 12 : 0); break;
        }
        return new TimeOnly(h, m, s, basis.Millisecond);
    }

    private void CenterOnSelection(Column column)
    {
        var index = SelectedIndex(column);
        column.Scroll = index < 0 ? 0 : Math.Clamp(index * RowHeight - RowHeight * (VisibleRows / 2), 0, MaxScroll(column));
    }

    private void Select(int columnIndex, int row)
    {
        var column = _columns[columnIndex];
        if (row < 0 || row >= column.Items.Count) return;
        _active = columnIndex;
        if (_apply(Compose(column.Part, column.Items[row].Value))) EnsureVisible(column, row);
        Invalidate();
    }

    private void EnsureVisible(Column column, int row)
    {
        var top = row * RowHeight;
        if (top < column.Scroll) column.Scroll = top;
        else if (top + RowHeight > column.Scroll + RowHeight * VisibleRows) column.Scroll = top + RowHeight - RowHeight * VisibleRows;
    }

    private bool HitRow(Point p, out int columnIndex, out int row)
    {
        columnIndex = row = -1;
        for (var i = 0; i < _columns.Count; i++)
        {
            var rect = ColumnRect(i);
            var list = new Rectangle(rect.X, rect.Y + HeaderHeight, rect.Width, RowHeight * VisibleRows);
            if (!list.Contains(p)) continue;
            columnIndex = i;
            row = (int)((p.Y - list.Y + _columns[i].Scroll) / RowHeight);
            return row >= 0 && row < _columns[i].Items.Count;
        }
        return false;
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Enter or Keys.Escape or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var column = _columns[_active];
        switch (e.KeyCode)
        {
            case Keys.Left: _active = Math.Max(0, _active - 1); break;
            case Keys.Right: _active = Math.Min(_columns.Count - 1, _active + 1); break;
            case Keys.Up: Select(_active, Math.Max(0, SelectedIndex(column) - 1)); break;
            case Keys.Down: Select(_active, Math.Min(column.Items.Count - 1, SelectedIndex(column) + 1)); break;
            case Keys.Enter:
            case Keys.Escape:
            case Keys.Space:
                _done();
                break;
            default:
                return;
        }

        e.Handled = true;
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        for (var i = 0; i < _columns.Count; i++)
        {
            if (!ColumnRect(i).Contains(e.Location)) continue;
            var column = _columns[i];
            column.Scroll = Math.Clamp((float)Math.Round((column.Scroll - e.Delta / 120f * RowHeight * 2) / RowHeight) * RowHeight, 0, MaxScroll(column));
            Invalidate();
            return;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hot = HitRow(e.Location, out var c, out var r) ? new Point(c, r) : new Point(-1, -1);
        var footer = FooterButton(0).Contains(e.Location) ? 0 : FooterButton(1).Contains(e.Location) ? 1 : -1;
        if (hot == _hot && footer == _hotFooter) return;
        _hot = hot;
        _hotFooter = footer;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hot = new Point(-1, -1);
        _hotFooter = -1;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button != MouseButtons.Left) return;
        if (HitRow(e.Location, out var column, out var row))
        {
            if (e.Clicks > 1) _done();
            else Select(column, row);
        }
        else if (FooterButton(0).Contains(e.Location))
        {
            var now = TimeOnly.FromDateTime(DateTime.Now);
            if (_apply(now)) foreach (var col in _columns) CenterOnSelection(col);
            Invalidate();
        }
        else if (FooterButton(1).Contains(e.Location)) _done();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Colors.Surface);
        using var borderPen = new Pen(Colors.Border);
        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

        for (var i = 0; i < _columns.Count; i++)
        {
            var column = _columns[i];
            var rect = ColumnRect(i);
            Chrome.DrawText(g, column.Title, Font, new Rectangle(rect.X, rect.Y, rect.Width, HeaderHeight), Colors.Muted, ContentAlignment.MiddleCenter);
            var list = new Rectangle(rect.X, rect.Y + HeaderHeight, rect.Width, RowHeight * VisibleRows);
            var selected = SelectedIndex(column);

            var state = g.Save();
            g.SetClip(list);
            for (var row = 0; row < column.Items.Count; row++)
            {
                var y = list.Y + row * RowHeight - (int)column.Scroll;
                if (y < list.Y || y + RowHeight > list.Bottom) continue;
                var cell = new Rectangle(list.X + S(4), y + 1, list.Width - S(8), RowHeight - 2);
                var allowed = _isAllowed(Compose(column.Part, column.Items[row].Value));
                var isSelected = row == selected;
                var hot = _hot.X == i && _hot.Y == row && allowed;
                if (isSelected) Chrome.FillRounded(g, cell, Colors.Primary, Chrome.CornerRadius);
                else if (hot) Chrome.FillRounded(g, cell, Colors.HoverBack, Chrome.CornerRadius);
                var color = isSelected ? Colors.PrimaryText : allowed ? Colors.Text : Colors.Disabled;
                Chrome.DrawText(g, column.Items[row].Label, Font, cell, color, ContentAlignment.MiddleCenter);
                if (i == _active && Focused && isSelected) Chrome.DrawFocusRing(g, cell, Colors);
            }
            g.Restore(state);

            if (i > 0) g.DrawLine(borderPen, rect.X, rect.Y + HeaderHeight, rect.X, list.Bottom);
        }

        var footerTop = Height - FooterHeight;
        g.DrawLine(borderPen, 1, footerTop, Width - 2, footerTop);
        DrawFooterButton(g, FooterButton(0), "Now", _hotFooter == 0);
        DrawFooterButton(g, FooterButton(1), "Done", _hotFooter == 1);
    }

    private void DrawFooterButton(Graphics g, Rectangle rect, string text, bool hot)
    {
        if (hot) Chrome.FillRounded(g, rect, Colors.HoverBack, Chrome.CornerRadius);
        Chrome.DrawText(g, text, Font, rect, Colors.Primary, ContentAlignment.MiddleCenter);
    }
}

/// <summary>Month grid with week numbers, range limits and keyboard navigation shown by the date editors.</summary>
internal sealed class MonthGridPanel : EnterpriseControlBase
{
    private readonly Func<DateOnly?> _selected;
    private readonly Func<DateOnly, bool> _isAllowed;
    private readonly Action<DateOnly> _pick;
    private readonly DayOfWeek _firstDay;
    private readonly bool _weekNumbers;
    private DateOnly _view;
    private DateOnly _cursor;
    private int _hot = -1;
    private int _hotChrome;

    public MonthGridPanel(Func<DateOnly?> selected, Func<DateOnly, bool> isAllowed, Action<DateOnly> pick, DayOfWeek firstDay, bool weekNumbers)
    {
        _selected = selected;
        _isAllowed = isAllowed;
        _pick = pick;
        _firstDay = firstDay;
        _weekNumbers = weekNumbers;
        _cursor = selected() ?? DateOnly.FromDateTime(DateTime.Today);
        _view = new DateOnly(_cursor.Year, _cursor.Month, 1);
        TabStop = true;
        Size = new Size(Padding2 * 2 + WeekWidth + CellWidth * 7, HeaderHeight + DayNamesHeight + RowHeight * 6 + FooterHeight + 2);
    }

    private int Padding2 => S(8);
    private int CellWidth => S(36);
    private int RowHeight => S(32);
    private int HeaderHeight => S(40);
    private int DayNamesHeight => S(26);
    private int FooterHeight => S(36);
    private int WeekWidth => _weekNumbers ? S(32) : 0;
    private int GridLeft => Padding2 + WeekWidth;
    private int GridTop => HeaderHeight + DayNamesHeight;
    private Rectangle PreviousRect => new(Padding2, 2, S(36), HeaderHeight - 4);
    private Rectangle NextRect => new(Width - Padding2 - S(36), 2, S(36), HeaderHeight - 4);
    private Rectangle TodayRect => new((Width - S(90)) / 2, Height - FooterHeight + S(5), S(90), FooterHeight - S(10));
    private Rectangle CellRect(int index) => new(GridLeft + index % 7 * CellWidth, GridTop + index / 7 * RowHeight, CellWidth, RowHeight);

    private int GridStartDayNumber => _view.DayNumber - (((int)_view.DayOfWeek - (int)_firstDay + 7) % 7);

    private bool TryCell(int index, out DateOnly date)
    {
        var number = GridStartDayNumber + index;
        if (number < DateOnly.MinValue.DayNumber || number > DateOnly.MaxValue.DayNumber) { date = default; return false; }
        date = DateOnly.FromDayNumber(number);
        return true;
    }

    private int HitCell(Point p)
    {
        for (var i = 0; i < 42; i++) if (CellRect(i).Contains(p)) return i;
        return -1;
    }

    private void MoveView(int months)
    {
        try { _view = _view.AddMonths(months); }
        catch (ArgumentOutOfRangeException) { return; }
        _cursor = new DateOnly(_view.Year, _view.Month, Math.Min(_cursor.Day, DateTime.DaysInMonth(_view.Year, _view.Month)));
        Invalidate();
    }

    private void MoveCursor(int days)
    {
        var number = Math.Clamp(_cursor.DayNumber + days, DateOnly.MinValue.DayNumber, DateOnly.MaxValue.DayNumber);
        _cursor = DateOnly.FromDayNumber(number);
        _view = new DateOnly(_cursor.Year, _cursor.Month, 1);
        Invalidate();
    }

    private void PickIfAllowed(DateOnly date)
    {
        if (_isAllowed(date)) _pick(date);
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End or Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Left: MoveCursor(-1); break;
            case Keys.Right: MoveCursor(1); break;
            case Keys.Up: MoveCursor(-7); break;
            case Keys.Down: MoveCursor(7); break;
            case Keys.PageUp: MoveView(e.Control ? -12 : -1); break;
            case Keys.PageDown: MoveView(e.Control ? 12 : 1); break;
            case Keys.Home: _cursor = new DateOnly(_view.Year, _view.Month, 1); Invalidate(); break;
            case Keys.End: _cursor = new DateOnly(_view.Year, _view.Month, DateTime.DaysInMonth(_view.Year, _view.Month)); Invalidate(); break;
            case Keys.Enter:
            case Keys.Space: PickIfAllowed(_cursor); break;
            default: return;
        }
        e.Handled = true;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        MoveView(e.Delta > 0 ? -1 : 1);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var cell = HitCell(e.Location);
        var chrome = PreviousRect.Contains(e.Location) ? 1 : NextRect.Contains(e.Location) ? 2 : TodayRect.Contains(e.Location) ? 3 : 0;
        if (cell == _hot && chrome == _hotChrome) return;
        _hot = cell;
        _hotChrome = chrome;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hot = -1;
        _hotChrome = 0;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button != MouseButtons.Left) return;
        if (PreviousRect.Contains(e.Location)) { MoveView(-1); return; }
        if (NextRect.Contains(e.Location)) { MoveView(1); return; }
        if (TodayRect.Contains(e.Location))
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (_isAllowed(today)) _pick(today);
            else { _cursor = today; _view = new DateOnly(today.Year, today.Month, 1); Invalidate(); }
            return;
        }

        var cell = HitCell(e.Location);
        if (cell >= 0 && TryCell(cell, out var date))
        {
            _cursor = date;
            if (_isAllowed(date)) _pick(date);
            else Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Colors.Surface);
        using var borderPen = new Pen(Colors.Border);
        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

        var culture = CultureInfo.CurrentCulture;
        if (_hotChrome == 1) Chrome.FillRounded(g, PreviousRect, Colors.HoverBack, Chrome.CornerRadius);
        if (_hotChrome == 2) Chrome.FillRounded(g, NextRect, Colors.HoverBack, Chrome.CornerRadius);
        Chrome.DrawChevron(g, PreviousRect, ChevronDirection.Left, Colors.Text);
        Chrome.DrawChevron(g, NextRect, ChevronDirection.Right, Colors.Text);
        var title = _view.ToString("MMMM yyyy", culture);
        TextRenderer.DrawText(g, title, BoldFont, new Rectangle(PreviousRect.Right, 0, NextRect.Left - PreviousRect.Right, HeaderHeight), Colors.Text, Chrome.TextFlags(ContentAlignment.MiddleCenter));

        for (var c = 0; c < 7; c++)
        {
            var name = culture.DateTimeFormat.GetShortestDayName((DayOfWeek)(((int)_firstDay + c) % 7));
            Chrome.DrawText(g, name, Font, new Rectangle(GridLeft + c * CellWidth, HeaderHeight, CellWidth, DayNamesHeight), Colors.Muted, ContentAlignment.MiddleCenter);
        }

        var selected = _selected();
        var today = DateOnly.FromDateTime(DateTime.Today);
        for (var i = 0; i < 42; i++)
        {
            if (_weekNumbers && i % 7 == 0 && TryCell(i, out var weekDate))
                Chrome.DrawText(g, ISOWeek.GetWeekOfYear(weekDate.ToDateTime(TimeOnly.MinValue)).ToString(CultureInfo.InvariantCulture), Font, new Rectangle(Padding2, GridTop + i / 7 * RowHeight, WeekWidth, RowHeight), Colors.Muted, ContentAlignment.MiddleCenter);

            if (!TryCell(i, out var date)) continue;
            var rect = Rectangle.Inflate(CellRect(i), -2, -2);
            var allowed = _isAllowed(date);
            var inMonth = date.Month == _view.Month;
            var isSelected = selected == date;
            if (isSelected) Chrome.FillRounded(g, rect, Colors.Primary, Chrome.CornerRadius);
            else if (_hot == i && allowed) Chrome.FillRounded(g, rect, Colors.HoverBack, Chrome.CornerRadius);
            if (date == today && !isSelected)
            {
                using var ring = new Pen(Colors.Primary, 1.5f);
                using var path = Chrome.RoundedRect(rect, Chrome.CornerRadius);
                g.DrawPath(ring, path);
            }

            var color = isSelected ? Colors.PrimaryText : !allowed ? Colors.Disabled : inMonth ? Colors.Text : Colors.Muted;
            Chrome.DrawText(g, date.Day.ToString(CultureInfo.InvariantCulture), Font, rect, color, ContentAlignment.MiddleCenter);
            if (Focused && date == _cursor) Chrome.DrawFocusRing(g, CellRect(i), Colors);
        }

        var footerTop = Height - FooterHeight;
        g.DrawLine(borderPen, 1, footerTop, Width - 2, footerTop);
        if (_hotChrome == 3) Chrome.FillRounded(g, TodayRect, Colors.HoverBack, Chrome.CornerRadius);
        Chrome.DrawText(g, "Today", Font, TodayRect, Colors.Primary, ContentAlignment.MiddleCenter);
    }
}

/// <summary>Base of the time editors: text entry plus hour, minute, second and AM/PM columns.</summary>
public abstract class EnterpriseTimeEditorBase : EnterpriseDropDownEditorBase
{
    protected abstract TimeOnly? CurrentTime { get; }
    protected abstract TimeOnly DefaultTime { get; }
    protected abstract bool Is24HourCore { get; }
    protected abstract bool ShowSecondsCore { get; }
    protected abstract int MinuteStepCore { get; }
    protected abstract bool ApplyTime(TimeOnly time);
    protected abstract bool IsTimeAllowed(TimeOnly time);

    protected override Control CreatePopupContent() =>
        new TimeSelectorPanel(() => CurrentTime, IsTimeAllowed, ApplyTime, CloseDropDown, Is24HourCore, ShowSecondsCore, MinuteStepCore, DefaultTime);
}

/// <summary>Edits the time of a <see cref="DateTime"/> (the date part is preserved).</summary>
[DefaultProperty("Value")]
[DefaultEvent("ValueChanged")]
[ToolboxItem(true)]
[Description("Drop-down editor for the time part of a DateTime.")]
public class EnterpriseTimePickerControl : EnterpriseTimeEditorBase
{
    private readonly EnterpriseTimePicker _model = new();

    public EnterpriseTimePickerControl()
    {
        _model.PropertyChanged += (_, _) =>
        {
            RefreshText();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        };
        RefreshText();
    }

    [Category("Property Changed")] public event EventHandler? ValueChanged;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EnterpriseTimePicker Model => _model;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateTime? Value { get => _model.Value; set => _model.Value = value; }

    [Category("Behavior"), DefaultValue(false)]
    public bool Is24Hour { get => _model.Is24Hour; set { _model.Is24Hour = value; RefreshText(); } }

    [Category("Behavior"), DefaultValue(false)]
    public bool ShowSeconds { get => _model.ShowSeconds; set { _model.ShowSeconds = value; RefreshText(); } }

    [Category("Behavior"), DefaultValue(1)]
    public int MinuteStep { get => _model.MinuteStep; set => _model.MinuteStep = value; }

    [Category("Behavior")] public TimeSpan Minimum { get => _model.Minimum; set => _model.Minimum = value; }
    [Category("Behavior")] public TimeSpan Maximum { get => _model.Maximum; set => _model.Maximum = value; }

    private bool ShouldSerializeMinimum() => _model.Minimum != TimeSpan.Zero;
    private void ResetMinimum() => _model.Minimum = TimeSpan.Zero;
    private bool ShouldSerializeMaximum() => _model.Maximum != new TimeSpan(23, 59, 59);
    private void ResetMaximum() => _model.Maximum = new TimeSpan(23, 59, 59);

    protected override TimeOnly? CurrentTime => _model.Value is { } v ? TimeOnly.FromDateTime(v) : null;
    protected override TimeOnly DefaultTime => TimeOnly.FromTimeSpan(_model.Minimum > new TimeSpan(12, 0, 0) ? _model.Minimum : new TimeSpan(12, 0, 0));
    protected override bool Is24HourCore => _model.Is24Hour;
    protected override bool ShowSecondsCore => _model.ShowSeconds;
    protected override int MinuteStepCore => _model.MinuteStep;
    protected override bool ApplyTime(TimeOnly time) => _model.TrySetTime(time.ToTimeSpan());
    protected override bool IsTimeAllowed(TimeOnly time) => time.ToTimeSpan() >= _model.Minimum && time.ToTimeSpan() <= _model.Maximum;
    protected override bool TryCommitText(string text) => _model.TrySetText(text, CultureInfo.CurrentCulture);
    protected override void RefreshText() => SetDisplayText(_model.DisplayText());
    protected override void StepValue(int direction) => _model.Increment(direction);
}

/// <summary>Edits a <see cref="TimeOnly"/> with minute, second or millisecond precision.</summary>
[DefaultProperty("Value")]
[DefaultEvent("ValueChanged")]
[ToolboxItem(true)]
[Description("Drop-down editor for a TimeOnly value.")]
public class EnterpriseTimeOnlyPickerControl : EnterpriseTimeEditorBase
{
    private readonly EnterpriseTimeOnlyPicker _model = new();

    public EnterpriseTimeOnlyPickerControl()
    {
        _model.PropertyChanged += (_, _) =>
        {
            RefreshText();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        };
        RefreshText();
    }

    [Category("Property Changed")] public event EventHandler? ValueChanged;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EnterpriseTimeOnlyPicker Model => _model;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TimeOnly? Value { get => _model.Value; set => _model.Value = value; }

    [Category("Behavior"), DefaultValue(TimeOnlyPrecision.Minutes)]
    public TimeOnlyPrecision Precision { get => _model.Precision; set { _model.Precision = value; RefreshText(); } }

    [Category("Behavior"), DefaultValue(false)]
    public bool Is24Hour { get => _model.Is24Hour; set { _model.Is24Hour = value; RefreshText(); } }

    [Category("Behavior"), DefaultValue(1)]
    public int MinuteStep
    {
        get => _model.MinuteStep;
        set => _model.MinuteStep = value is >= 1 and <= 30 && 60 % value == 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), "The step must divide 60 and be between 1 and 30.");
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TimeOnly Minimum { get => _model.Minimum; set => _model.Minimum = value; }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TimeOnly Maximum { get => _model.Maximum; set => _model.Maximum = value; }

    protected override TimeOnly? CurrentTime => _model.Value;
    protected override TimeOnly DefaultTime => _model.Minimum > new TimeOnly(12, 0) ? _model.Minimum : new TimeOnly(12, 0);
    protected override bool Is24HourCore => _model.Is24Hour;
    protected override bool ShowSecondsCore => _model.Precision >= TimeOnlyPrecision.Seconds;
    protected override int MinuteStepCore => _model.MinuteStep;
    protected override bool ApplyTime(TimeOnly time) => _model.TrySetValue(time);
    protected override bool IsTimeAllowed(TimeOnly time) => time >= _model.Minimum && time <= _model.Maximum;
    protected override bool TryCommitText(string text) => _model.TrySetText(text, CultureInfo.CurrentCulture);
    protected override void RefreshText() => SetDisplayText(_model.DisplayText());
    protected override void StepValue(int direction) => _model.Increment(direction);
}

/// <summary>Edits a <see cref="DateOnly"/> with a month grid pop-up.</summary>
[DefaultProperty("Value")]
[DefaultEvent("ValueChanged")]
[ToolboxItem(true)]
[Description("Drop-down editor for a DateOnly value.")]
public class EnterpriseDateOnlyPickerControl : EnterpriseDropDownEditorBase
{
    private readonly EnterpriseDateOnlyPicker _model = new();

    public EnterpriseDateOnlyPickerControl()
    {
        _model.PropertyChanged += (_, _) =>
        {
            RefreshText();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        };
        RefreshText();
    }

    [Category("Property Changed")] public event EventHandler? ValueChanged;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EnterpriseDateOnlyPicker Model => _model;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateOnly? Value { get => _model.Value; set => _model.Value = value; }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateOnly MinDate { get => _model.MinDate; set => _model.MinDate = value; }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateOnly MaxDate { get => _model.MaxDate; set => _model.MaxDate = value; }

    [Category("Appearance"), DefaultValue("d")]
    public string Format { get => _model.Format; set { _model.Format = string.IsNullOrEmpty(value) ? "d" : value; RefreshText(); } }

    [Category("Appearance"), DefaultValue(false)]
    public bool ShowWeekNumbers { get => _model.ShowWeekNumbers; set => _model.ShowWeekNumbers = value; }

    [Category("Appearance")]
    public DayOfWeek FirstDayOfWeek { get => _model.FirstDayOfWeek; set => _model.FirstDayOfWeek = value; }

    private bool ShouldSerializeFirstDayOfWeek() => _model.FirstDayOfWeek != CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
    private void ResetFirstDayOfWeek() => _model.FirstDayOfWeek = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;

    protected override Control CreatePopupContent() =>
        new MonthGridPanel(() => _model.Value, d => d >= _model.MinDate && d <= _model.MaxDate, d => { _model.TrySetValue(d); CloseDropDown(); }, _model.FirstDayOfWeek, _model.ShowWeekNumbers);

    protected override bool TryCommitText(string text) => _model.TrySetText(text, CultureInfo.CurrentCulture);
    protected override void RefreshText() => SetDisplayText(_model.DisplayText());
    protected override void StepValue(int direction) => _model.AddDays(direction);
}
