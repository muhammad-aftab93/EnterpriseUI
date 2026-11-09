using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace EnterpriseUI.WinForms;

public sealed class CardField
{
    public CardField(string field, string? caption = null, string? format = null)
    {
        Field = field;
        Caption = caption ?? field;
        Format = format;
    }

    public string Field { get; }
    public string Caption { get; set; }
    public string? Format { get; set; }
}

public sealed class CardFormattingEventArgs : EventArgs
{
    public CardFormattingEventArgs(object item, string title, string subtitle, string badge)
    {
        Item = item;
        Title = title;
        Subtitle = subtitle;
        Badge = badge;
    }

    public object Item { get; }
    public string Title { get; set; }
    public string Subtitle { get; set; }
    public string Badge { get; set; }
    public Color? Accent { get; set; }
    public Image? Image { get; set; }
}

/// <summary>Wrapping, virtualized cards with title, subtitle, badge and detail lines; supports grouping, sorting, filtering, selection and check boxes.</summary>
public class EnterpriseCardViewControl : EnterpriseScrollableControl
{
    private sealed class Entry
    {
        public object? Item;
        public string? Group;
        public int GroupCount;
        public bool Collapsed;
        public Rectangle Bounds;
        public int Order = -1;
    }

    private readonly List<object> _source = new();
    private readonly List<Entry> _entries = new();
    private readonly List<object> _order = new();
    private readonly HashSet<object> _selected = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> _checked = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<string> _collapsed = new();
    private readonly List<CardField> _fields = new();
    private object? _dataSource;
    private bool _dirty = true;
    private int _layoutWidth = -1;
    private int _contentHeight;
    private int _columns = 1;
    private int _cardWidth = 260;
    private int _cardHeight = 150;
    private string _titleField = string.Empty;
    private string _subtitleField = string.Empty;
    private string _badgeField = string.Empty;
    private string _groupField = string.Empty;
    private string _sortField = string.Empty;
    private bool _sortDescending;
    private string _filterText = string.Empty;
    private bool _checkBoxes;
    private ListSelectionMode _selectionMode = ListSelectionMode.One;
    private object? _current;
    private object? _anchor;
    private object? _hot;

    public EnterpriseCardViewControl()
    {
        Size = new Size(720, 420);
        AccessibleRole = AccessibleRole.List;
    }

    public event EventHandler? SelectionChanged;
    public event EventHandler<object>? CardActivate;
    public event EventHandler<object>? CardChecked;
    public event EventHandler<CardFormattingEventArgs>? CardFormatting;

    public string EmptyText { get; set; } = "No cards";

    public object? DataSource
    {
        get => _dataSource;
        set
        {
            if (_dataSource is INotifyCollectionChanged old) old.CollectionChanged -= OnSourceChanged;
            _dataSource = value;
            if (value is INotifyCollectionChanged notify) notify.CollectionChanged += OnSourceChanged;
            Reload();
        }
    }

    public IList<CardField> Fields => _fields;

    public string TitleField { get => _titleField; set { _titleField = value; Relayout(); } }
    public string SubtitleField { get => _subtitleField; set { _subtitleField = value; Relayout(); } }
    public string BadgeField { get => _badgeField; set { _badgeField = value; Relayout(); } }
    public string GroupField { get => _groupField; set { _groupField = value; Relayout(); } }
    public string SortField { get => _sortField; set { _sortField = value; Relayout(); } }
    public bool SortDescending { get => _sortDescending; set { _sortDescending = value; Relayout(); } }
    public string FilterText { get => _filterText; set { _filterText = value ?? string.Empty; Relayout(); } }
    public int CardWidth { get => _cardWidth; set { _cardWidth = Math.Max(160, value); Relayout(); } }
    public int CardHeight { get => _cardHeight; set { _cardHeight = Math.Max(90, value); Relayout(); } }
    public bool CheckBoxes { get => _checkBoxes; set { _checkBoxes = value; Invalidate(); } }
    public bool StretchCards { get; set; } = true;

    public ListSelectionMode SelectionMode
    {
        get => _selectionMode;
        set { _selectionMode = value; ClearSelection(); }
    }

    public int Count => _source.Count;
    public int VisibleCount => _order.Count;
    public IReadOnlyList<object> SelectedItems => _source.Where(_selected.Contains).ToList();
    public object? SelectedItem => SelectedItems.FirstOrDefault();
    public IReadOnlyList<object> CheckedItems => _source.Where(_checked.Contains).ToList();
    public int Columns => _columns;

    protected override int LineHeight => S(_cardHeight) / 2;

    protected override Size ContentSize
    {
        get
        {
            var inner = InnerBounds;
            EnsureLayout(inner.Width);
            if (_contentHeight > inner.Height) EnsureLayout(inner.Width - ScrollBarSize);
            return new Size(0, _contentHeight);
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (IsDisposed) return;
        if (IsHandleCreated && InvokeRequired) BeginInvoke(new Action(Reload));
        else Reload();
    }

    private void Reload()
    {
        _source.Clear();
        if (_dataSource is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is not null) _source.Add(item);
            }
        }

        _selected.RemoveWhere(i => !_source.Contains(i));
        _checked.RemoveWhere(i => !_source.Contains(i));
        if (_current is not null && !_source.Contains(_current)) _current = _anchor = null;
        Relayout();
    }

    public void Relayout()
    {
        _dirty = true;
        if (IsDisposed) return;
        UpdateScrollBars();
    }

    // ---- layout -------------------------------------------------------------------------

    private string TextOf(object item, string field) => MemberAccess.Text(item, field);

    private string FieldText(object item, CardField field)
    {
        var value = MemberAccess.Read(item, field.Field);
        if (value is null) return string.Empty;
        if (!string.IsNullOrEmpty(field.Format) && value is IFormattable formattable) return formattable.ToString(field.Format, CultureInfo.CurrentCulture);
        return Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
    }

    private bool Matches(object item)
    {
        if (_filterText.Length == 0) return true;
        bool Has(string text) => text.Contains(_filterText, StringComparison.CurrentCultureIgnoreCase);
        if (_titleField.Length > 0 && Has(TextOf(item, _titleField))) return true;
        if (_subtitleField.Length > 0 && Has(TextOf(item, _subtitleField))) return true;
        if (_badgeField.Length > 0 && Has(TextOf(item, _badgeField))) return true;
        return _fields.Any(f => Has(FieldText(item, f)));
    }

    private void EnsureLayout(int width)
    {
        if (!_dirty && _layoutWidth == width) return;
        _dirty = false;
        _layoutWidth = width;
        _entries.Clear();
        _order.Clear();

        var items = _source.Where(Matches).ToList();
        if (_sortField.Length > 0)
        {
            var sign = _sortDescending ? -1 : 1;
            items = items.OrderBy(i => i, Comparer<object>.Create((a, b) => sign * CompareValues(MemberAccess.Read(a, _sortField), MemberAccess.Read(b, _sortField)))).ToList();
        }

        var pad = S(14);
        var gap = S(12);
        var cardW = S(_cardWidth);
        var cardH = S(_cardHeight);
        _columns = Math.Max(1, (width - pad * 2 + gap) / (cardW + gap));
        if (StretchCards) cardW = (width - pad * 2 - gap * (_columns - 1)) / _columns;
        var y = pad;

        void Place(IEnumerable<object> group)
        {
            var index = 0;
            foreach (var item in group)
            {
                var col = index % _columns;
                var row = index / _columns;
                _entries.Add(new Entry { Item = item, Order = _order.Count, Bounds = new Rectangle(pad + col * (cardW + gap), y + row * (cardH + gap), cardW, cardH) });
                _order.Add(item);
                index++;
            }

            if (index > 0) y += ((index + _columns - 1) / _columns) * (cardH + gap);
        }

        if (_groupField.Length == 0)
        {
            Place(items);
        }
        else
        {
            foreach (var group in items.GroupBy(i => TextOf(i, _groupField)).OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase))
            {
                var collapsed = _collapsed.Contains(group.Key);
                _entries.Add(new Entry { Group = group.Key, GroupCount = group.Count(), Collapsed = collapsed, Bounds = new Rectangle(pad, y, width - pad * 2, S(34)) });
                y += S(34) + gap / 2;
                if (!collapsed) Place(group);
            }
        }

        _contentHeight = y + pad - gap;
    }

    private static int CompareValues(object? a, object? b)
    {
        if (a is IComparable ca && b is not null && a.GetType() == b.GetType()) return ca.CompareTo(b);
        return string.Compare(Convert.ToString(a, CultureInfo.CurrentCulture), Convert.ToString(b, CultureInfo.CurrentCulture), StringComparison.CurrentCultureIgnoreCase);
    }

    public void ToggleGroup(string group)
    {
        if (!_collapsed.Remove(group)) _collapsed.Add(group);
        Relayout();
    }

    private Rectangle ToControl(Rectangle content)
    {
        var view = ViewPort;
        return new Rectangle(view.X + content.X, view.Y + content.Y - ScrollY, content.Width, content.Height);
    }

    public void EnsureCardVisible(object item)
    {
        EnsureLayout(ViewPort.Width);
        var entry = _entries.FirstOrDefault(e => ReferenceEquals(e.Item, item));
        if (entry is not null) EnsureVisible(new Rectangle(entry.Bounds.X, entry.Bounds.Y - S(4), 1, entry.Bounds.Height + S(8)));
    }

    public object? CardAt(Point location)
    {
        EnsureLayout(ViewPort.Width);
        return _entries.FirstOrDefault(e => e.Item is not null && ToControl(e.Bounds).Contains(location))?.Item;
    }

    // ---- selection ----------------------------------------------------------------------

    public void ClearSelection()
    {
        if (_selected.Count == 0) return;
        _selected.Clear();
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectCard(object item)
    {
        if (_selectionMode == ListSelectionMode.None) return;
        _selected.Clear();
        _selected.Add(item);
        _current = _anchor = item;
        EnsureCardVisible(item);
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectAll()
    {
        if (_selectionMode is not (ListSelectionMode.MultiSimple or ListSelectionMode.MultiExtended)) return;
        EnsureLayout(ViewPort.Width);
        foreach (var item in _order) _selected.Add(item);
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool IsChecked(object item) => _checked.Contains(item);

    public void SetChecked(object item, bool value)
    {
        if (value ? !_checked.Add(item) : !_checked.Remove(item)) return;
        Invalidate();
        CardChecked?.Invoke(this, item);
    }

    private void Apply(object item, bool extend, bool toggle)
    {
        if (_selectionMode == ListSelectionMode.None) return;
        var before = _selected.ToArray();
        if (_selectionMode == ListSelectionMode.One || (!extend && !toggle && _selectionMode != ListSelectionMode.MultiSimple))
        {
            _selected.Clear();
            _selected.Add(item);
            _anchor = item;
        }
        else if (_selectionMode == ListSelectionMode.MultiSimple || toggle)
        {
            if (!_selected.Remove(item)) _selected.Add(item);
            _anchor = item;
        }
        else if (extend && _anchor is not null)
        {
            var a = _order.IndexOf(_anchor);
            var b = _order.IndexOf(item);
            if (a < 0) a = b;
            _selected.Clear();
            for (var i = Math.Min(a, b); i <= Math.Max(a, b); i++) _selected.Add(_order[i]);
        }

        _current = item;
        EnsureCardVisible(item);
        Invalidate();
        if (before.Length != _selected.Count || !before.All(_selected.Contains)) SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---- painting -----------------------------------------------------------------------

    private CardFormattingEventArgs Format(object item) => RaiseFormatting(new CardFormattingEventArgs(
        item,
        _titleField.Length > 0 ? TextOf(item, _titleField) : Convert.ToString(item, CultureInfo.CurrentCulture) ?? string.Empty,
        _subtitleField.Length > 0 ? TextOf(item, _subtitleField) : string.Empty,
        _badgeField.Length > 0 ? TextOf(item, _badgeField) : string.Empty));

    private CardFormattingEventArgs RaiseFormatting(CardFormattingEventArgs args)
    {
        CardFormatting?.Invoke(this, args);
        return args;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        PaintFrame(g);
        var view = ViewPort;
        if (view.Width <= 0 || view.Height <= 0) return;
        EnsureLayout(view.Width);
        g.SetClip(view);
        g.Clear(Chrome.Blend(Colors.Surface, Colors.Header, 0.4));
        Chrome.DrawRoundedBorder(g, new Rectangle(0, 0, Width, Height), Focused ? Colors.FocusRing : Colors.Border, Chrome.CornerRadius, Focused ? Chrome.FocusWidth : 1f);
        g.SetClip(view);

        if (_entries.Count == 0) Chrome.DrawText(g, EmptyText, Font, view, Colors.Muted, ContentAlignment.MiddleCenter);

        foreach (var entry in _entries)
        {
            var bounds = ToControl(entry.Bounds);
            if (bounds.Bottom < view.Top || bounds.Top > view.Bottom) continue;
            if (entry.Item is null) PaintGroup(g, entry, bounds);
            else PaintCard(g, entry.Item, bounds);
        }
    }

    private void PaintGroup(Graphics g, Entry entry, Rectangle b)
    {
        Chrome.DrawChevron(g, new Rectangle(b.X, b.Y, S(22), b.Height), entry.Collapsed ? ChevronDirection.Right : ChevronDirection.Down, Colors.Muted);
        Chrome.DrawText(g, entry.Group ?? string.Empty, BoldFont, new Rectangle(b.X + S(24), b.Y, Math.Max(0, b.Width - S(24)), b.Height), Colors.Text);
        var x = b.X + S(24) + Chrome.MeasureText(entry.Group ?? string.Empty, BoldFont) + S(8);
        Chrome.DrawText(g, $"({entry.GroupCount})", Font, new Rectangle(x, b.Y, S(60), b.Height), Colors.Muted);
        using var pen = new Pen(Colors.Border);
        g.DrawLine(pen, b.X, b.Bottom - 1, b.Right, b.Bottom - 1);
    }

    private void PaintCard(Graphics g, object item, Rectangle b)
    {
        var selected = _selected.Contains(item);
        var hovered = ReferenceEquals(item, _hot) && Enabled;
        var format = Format(item);
        var radius = S(6);

        if (!Colors.HighContrast)
        {
            var shadow = new Rectangle(b.X, b.Y + S(hovered ? 3 : 2), b.Width, b.Height);
            Chrome.FillRounded(g, shadow, Color.FromArgb(hovered ? 34 : 20, 0, 0, 0), radius);
        }

        Chrome.FillRounded(g, b, selected ? Chrome.Blend(Colors.Surface, Colors.SelectionBack, Colors.HighContrast ? 1 : 0.55) : Colors.Surface, radius);
        var border = selected ? Colors.Primary : hovered ? Chrome.Blend(Colors.Border, Colors.Primary, 0.6) : Colors.Border;
        Chrome.DrawRoundedBorder(g, b, border, radius, selected ? 2f : 1f);

        if (ReferenceEquals(item, _current) && Focused)
        {
            using var path = Chrome.RoundedRect(Rectangle.Inflate(b, -S(3), -S(3)), radius);
            using var pen = new Pen(Colors.FocusRing) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            g.DrawPath(pen, path);
        }

        var pad = S(14);
        var accent = format.Accent ?? Colors.Primary;
        var avatar = new Rectangle(b.X + pad, b.Y + pad, S(38), S(38));
        if (format.Image is { } image) g.DrawImage(image, avatar);
        else
        {
            Chrome.FillRounded(g, avatar, Colors.HighContrast ? Colors.Header : Chrome.Blend(Colors.Surface, accent, 0.2), avatar.Width / 2);
            var initial = format.Title.Length > 0 ? format.Title[..1].ToUpper(CultureInfo.CurrentCulture) : "?";
            Chrome.DrawText(g, initial, BoldFont, avatar, Colors.HighContrast ? Colors.Text : accent, ContentAlignment.MiddleCenter);
        }

        var textX = avatar.Right + S(10);
        var rightEdge = b.Right - pad - (_checkBoxes ? S(26) : 0);
        var badgeWidth = 0;
        if (format.Badge.Length > 0)
        {
            badgeWidth = Math.Min(S(110), Chrome.MeasureText(format.Badge, Font) + S(18));
            var badge = new Rectangle(rightEdge - badgeWidth, b.Y + pad + S(2), badgeWidth, S(22));
            Chrome.FillRounded(g, badge, Colors.HighContrast ? Colors.Header : Chrome.Blend(Colors.Surface, accent, 0.18), badge.Height / 2);
            Chrome.DrawText(g, format.Badge, Font, badge, Colors.HighContrast ? Colors.Text : accent, ContentAlignment.MiddleCenter);
            badgeWidth += S(8);
        }

        var titleBounds = new Rectangle(textX, avatar.Y - S(1), Math.Max(0, rightEdge - textX - badgeWidth), S(20));
        Chrome.DrawText(g, format.Title, BoldFont, titleBounds, Colors.Text);
        Chrome.DrawText(g, format.Subtitle, Font, new Rectangle(textX, titleBounds.Bottom, Math.Max(0, rightEdge - textX), S(18)), Colors.Muted);

        if (_checkBoxes)
        {
            var box = new Rectangle(b.Right - pad - S(16), b.Y + pad + S(3), S(16), S(16));
            Chrome.DrawCheckBox(g, Colors, box, _checked.Contains(item) ? CheckState.Checked : CheckState.Unchecked, Enabled, hovered);
        }

        var y = avatar.Bottom + S(10);
        using (var pen = new Pen(Chrome.Blend(Colors.Surface, Colors.Border, 0.7))) g.DrawLine(pen, b.X + pad, y, b.Right - pad, y);
        y += S(8);
        var lineHeight = S(22);
        foreach (var field in _fields)
        {
            if (y + lineHeight > b.Bottom - S(6)) break;
            var captionWidth = Math.Min((b.Width - pad * 2) * 2 / 5, S(110));
            Chrome.DrawText(g, field.Caption, Font, new Rectangle(b.X + pad, y, captionWidth, lineHeight), Colors.Muted);
            Chrome.DrawText(g, FieldText(item, field), Font, new Rectangle(b.X + pad + captionWidth, y, Math.Max(0, b.Width - pad * 2 - captionWidth), lineHeight), Colors.Text);
            y += lineHeight;
        }
    }

    // ---- mouse and keyboard ---------------------------------------------------------------

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var item = CardAt(e.Location);
        if (!ReferenceEquals(item, _hot))
        {
            _hot = item;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hot is not null)
        {
            _hot = null;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (!Enabled) return;
        EnsureLayout(ViewPort.Width);
        var entry = _entries.FirstOrDefault(x => ToControl(x.Bounds).Contains(e.Location));
        if (entry is null) return;
        if (entry.Item is null)
        {
            ToggleGroup(entry.Group ?? string.Empty);
            return;
        }

        var b = ToControl(entry.Bounds);
        if (_checkBoxes && e.Button == MouseButtons.Left && Rectangle.Inflate(new Rectangle(b.Right - S(14) - S(16), b.Y + S(17), S(16), S(16)), S(5), S(5)).Contains(e.Location))
        {
            SetChecked(entry.Item, !_checked.Contains(entry.Item));
            _current = entry.Item;
            return;
        }

        if (e.Button == MouseButtons.Right && _selected.Contains(entry.Item)) return;
        Apply(entry.Item, (ModifierKeys & Keys.Shift) == Keys.Shift, (ModifierKeys & Keys.Control) == Keys.Control);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button == MouseButtons.Left && CardAt(e.Location) is { } item) CardActivate?.Invoke(this, item);
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End or Keys.Enter or Keys.Space
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        EnsureLayout(ViewPort.Width);
        if (_order.Count == 0) return;
        var index = _current is null ? -1 : _order.IndexOf(_current);
        var perPage = Math.Max(1, ViewPort.Height / Math.Max(1, S(_cardHeight) + S(12))) * _columns;
        var target = e.KeyCode switch
        {
            Keys.Right => index + 1,
            Keys.Left => index - 1,
            Keys.Down => index < 0 ? 0 : index + _columns,
            Keys.Up => index < 0 ? 0 : index - _columns,
            Keys.PageDown => Math.Max(0, index) + perPage,
            Keys.PageUp => Math.Max(0, index) - perPage,
            Keys.Home => 0,
            Keys.End => _order.Count - 1,
            _ => int.MinValue
        };

        if (target != int.MinValue)
        {
            Apply(_order[Math.Clamp(target, 0, _order.Count - 1)], e.Shift, false);
            e.Handled = true;
            return;
        }

        switch (e.KeyCode)
        {
            case Keys.Space when _current is not null:
                if (_checkBoxes) SetChecked(_current, !_checked.Contains(_current));
                else Apply(_current, false, true);
                e.Handled = true;
                break;
            case Keys.Enter when _current is not null:
                CardActivate?.Invoke(this, _current);
                e.Handled = true;
                break;
            case Keys.A when e.Control:
                SelectAll();
                e.Handled = true;
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _dataSource is INotifyCollectionChanged notify) notify.CollectionChanged -= OnSourceChanged;
        base.Dispose(disposing);
    }
}
