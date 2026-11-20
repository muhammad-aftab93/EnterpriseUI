using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;
using System.Globalization;
using EnterpriseUI.Editors;

namespace EnterpriseUI.WinForms;

public enum BrowseMode { OpenFile, SaveFile, Folder }

/// <summary>Path editor whose button opens the file, save or folder dialog and writes the chosen path back.</summary>
[DefaultProperty("Mode")]
[DefaultEvent("PathSelected")]
[ToolboxItem(true)]
public class EnterpriseBrowseBox : EnterpriseButtonTextBoxControl
{
    public EnterpriseBrowseBox()
    {
        LeftButtonText = string.Empty;
        RightButtonText = "Browse...";
        RightButtonClick += (_, _) => Browse();
    }

    [Category("Behavior"), DefaultValue(BrowseMode.OpenFile)] public BrowseMode Mode { get; set; }
    [Category("Behavior"), DefaultValue("All files (*.*)|*.*")] public string Filter { get; set; } = "All files (*.*)|*.*";
    [Category("Behavior"), DefaultValue("")] public string DialogTitle { get; set; } = string.Empty;
    [Category("Behavior"), DefaultValue("")] public string InitialDirectory { get; set; } = string.Empty;
    [Category("Behavior"), DefaultValue(true)] public bool CheckFileExists { get; set; } = true;

    [Category("Behavior")] public event EventHandler? PathSelected;

    /// <summary>Shows the dialog for the current <see cref="Mode"/>; returns true when a path was chosen.</summary>
    public bool Browse()
    {
        using var dialog = new EnterpriseFileDialog
        {
            Filter = Filter,
            CheckFileExists = CheckFileExists,
            InitialDirectory = ResolveStartDirectory(),
            Title = DialogTitle.Length > 0 ? DialogTitle : Mode switch { BrowseMode.SaveFile => "Save as", BrowseMode.Folder => "Select folder", _ => "Select file" },
            FolderDescription = DialogTitle.Length > 0 ? DialogTitle : "Select a folder"
        };

        string? chosen = null;
        switch (Mode)
        {
            case BrowseMode.Folder:
                if (dialog.ShowFolderDialog(FindForm()) == DialogResult.OK) chosen = dialog.FolderPath;
                break;
            case BrowseMode.SaveFile:
                dialog.FileName = Path.GetFileName(Text);
                if (dialog.ShowSaveDialog(FindForm()) == DialogResult.OK) chosen = dialog.FileName;
                break;
            default:
                if (dialog.ShowOpenDialog(FindForm()) == DialogResult.OK) chosen = dialog.FileName;
                break;
        }

        if (string.IsNullOrEmpty(chosen)) return false;
        Text = chosen;
        PathSelected?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private string ResolveStartDirectory()
    {
        try
        {
            var text = Text.Trim();
            if (text.Length > 0)
            {
                if (Directory.Exists(text)) return text;
                var parent = Path.GetDirectoryName(text);
                if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent)) return parent;
            }
        }
        catch (ArgumentException) { }
        catch (PathTooLongException) { }
        return InitialDirectory;
    }
}

/// <summary>Editor whose pop-up hosts any control; the pop-up can be resized with a grip.</summary>
[DefaultProperty("PopupContent")]
[DefaultEvent("DropDownOpening")]
[ToolboxItem(true)]
public class EnterprisePopupBox : EnterpriseDropDownEditorBase
{
    private PopupFrame? _frame;
    private Control? _content;
    private Size _dropDownSize = new(280, 200);
    private string _text = string.Empty;

    public EnterprisePopupBox()
    {
        ReadOnly = true;
        RefreshText();
    }

    [Category("Behavior")] public event CancelEventHandler? DropDownOpening;
    [Category("Behavior")] public event EventHandler? TextCommitted;

    /// <summary>The control shown in the pop-up; it is not disposed when the pop-up closes.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? PopupContent
    {
        get => _content;
        set
        {
            if (ReferenceEquals(_content, value)) return;
            if (_content is not null) _frame?.Controls.Remove(_content);
            _content = value;
            if (_frame is not null && value is not null) AttachContent(_frame, value);
        }
    }

    [Category("Layout")]
    public Size DropDownSize
    {
        get => _dropDownSize;
        set => _dropDownSize = new Size(Math.Max(80, value.Width), Math.Max(60, value.Height));
    }

    private bool ShouldSerializeDropDownSize() => _dropDownSize != new Size(280, 200);
    private void ResetDropDownSize() => _dropDownSize = new Size(280, 200);

    [Category("Behavior"), DefaultValue(true)] public bool AllowResize { get; set; } = true;

    /// <summary>The text shown in the editor; set it from the pop-up content to reflect the choice.</summary>
    [Category("Appearance"), DefaultValue(""), Localizable(true)]
    public string DisplayText
    {
        get => _text;
        set { _text = value ?? string.Empty; SetDisplayText(_text); }
    }

    protected override bool OwnsPopupContent => false;

    protected override bool CanOpenDropDown()
    {
        if (_content is null) return false;
        var args = new CancelEventArgs();
        DropDownOpening?.Invoke(this, args);
        return !args.Cancel;
    }

    protected override Control CreatePopupContent()
    {
        _frame ??= new PopupFrame();
        _frame.AllowResize = AllowResize;
        _frame.Size = _dropDownSize;
        _frame.Resized += (_, _) => _dropDownSize = _frame.Size;
        AttachContent(_frame, _content!);
        return _frame;
    }

    private static void AttachContent(PopupFrame frame, Control content)
    {
        if (content.Parent == frame) return;
        content.Dock = DockStyle.Fill;
        frame.Controls.Add(content);
    }

    protected override bool TryCommitText(string text)
    {
        _text = text;
        TextCommitted?.Invoke(this, EventArgs.Empty);
        return true;
    }

    protected override void RefreshText() => SetDisplayText(_text);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_content is not null && _frame is not null) _frame.Controls.Remove(_content);
            _frame?.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed class PopupFrame : Panel
    {
        private bool _dragging;
        private Point _dragStart;
        private Size _startSize;

        public PopupFrame()
        {
            Padding = new Padding(1, 1, 1, 1);
            DoubleBuffered = true;
        }

        public bool AllowResize { get; set; }
        public event EventHandler? Resized;

        private Rectangle Grip => new(Width - 16, Height - 16, 16, 16);

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(Color.FromArgb(120, 128, 128, 128));
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            if (!AllowResize) return;
            for (var i = 0; i < 3; i++)
                e.Graphics.DrawLine(pen, Width - 4 - i * 4, Height - 2, Width - 2, Height - 4 - i * 4);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Parent is ToolStripDropDown drop && drop.Items.Count > 0) drop.Items[0].Size = Size;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!AllowResize) return;
            if (_dragging)
            {
                var delta = new Size(Cursor.Position.X - _dragStart.X, Cursor.Position.Y - _dragStart.Y);
                Size = new Size(Math.Max(80, _startSize.Width + delta.Width), Math.Max(60, _startSize.Height + delta.Height));
                return;
            }
            Cursor = Grip.Contains(e.Location) ? Cursors.SizeNWSE : Cursors.Default;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!AllowResize || e.Button != MouseButtons.Left || !Grip.Contains(e.Location)) return;
            _dragging = true;
            _dragStart = Cursor.Position;
            _startSize = Size;
            Capture = true;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!_dragging) return;
            _dragging = false;
            Capture = false;
            Resized?.Invoke(this, EventArgs.Empty);
        }
    }
}

/// <summary>Up-down editor that steps through a list of strings.</summary>
[DefaultProperty("Items")]
[DefaultEvent("SelectedIndexChanged")]
[ToolboxItem(true)]
public class EnterpriseItemSpinner : EnterpriseControlBase
{
    private readonly ObservableCollection<string> _items = new();
    private readonly System.Windows.Forms.Timer _repeat = new() { Interval = 400 };
    private int _index = -1;
    private int _hotButton;
    private int _downButton;
    private string _typed = string.Empty;
    private long _typedAt;

    public EnterpriseItemSpinner()
    {
        Size = new Size(180, 34);
        AccessibleRole = AccessibleRole.SpinButton;
        _items.CollectionChanged += OnItemsChanged;
        _repeat.Tick += (_, _) =>
        {
            _repeat.Interval = 70;
            Step(_downButton == 1 ? 1 : -1);
        };
    }

    [Category("Property Changed")] public event EventHandler? SelectedIndexChanged;

    [Category("Data"), DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public ObservableCollection<string> Items => _items;

    /// <summary>When true, stepping past either end continues at the other end.</summary>
    [Category("Behavior"), DefaultValue(false)] public bool Wrap { get; set; }
    [Category("Behavior"), DefaultValue(false)] public bool ReadOnly { get; set; }

    [Category("Behavior"), DefaultValue(-1)]
    public int SelectedIndex
    {
        get => _index;
        set
        {
            value = Math.Clamp(value, -1, _items.Count - 1);
            if (value == _index) return;
            _index = value;
            Invalidate();
            if (IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? SelectedItem
    {
        get => _index >= 0 ? _items[_index] : null;
        set => SelectedIndex = value is null ? -1 : _items.IndexOf(value);
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [AllowNull]
    public override string Text
    {
        get => SelectedItem ?? string.Empty;
        set => SelectedItem = value;
    }

    private Rectangle UpRect => new(Width - S(26), 2, S(24), Math.Max(1, (Height - 4) / 2));
    private Rectangle DownRect => new(Width - S(26), 2 + UpRect.Height, S(24), Math.Max(1, Height - 4 - UpRect.Height));

    /// <summary>Moves to the next (+1) or previous (-1) item.</summary>
    public void Step(int direction)
    {
        if (_items.Count == 0 || ReadOnly || !Enabled) return;
        var next = _index + direction;
        if (_index < 0) next = direction > 0 ? 0 : _items.Count - 1;
        if (next >= _items.Count) next = Wrap ? 0 : _items.Count - 1;
        if (next < 0) next = Wrap ? _items.Count - 1 : 0;
        SelectedIndex = next;
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_index >= _items.Count) SelectedIndex = _items.Count - 1;
        Invalidate();
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new SpinnerAccessibleObject(this);

    private sealed class SpinnerAccessibleObject : ControlAccessibleObject
    {
        private readonly EnterpriseItemSpinner _owner;
        public SpinnerAccessibleObject(EnterpriseItemSpinner owner) : base(owner) => _owner = owner;
        public override AccessibleRole Role => AccessibleRole.SpinButton;
        public override string? Value { get => _owner.SelectedItem; set => _owner.SelectedItem = value; }
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Up: Step(1); break;
            case Keys.Down: Step(-1); break;
            case Keys.PageUp: if (!ReadOnly) SelectedIndex = Math.Min(_items.Count - 1, Math.Max(0, _index) + 5); break;
            case Keys.PageDown: if (!ReadOnly) SelectedIndex = Math.Max(0, _index - 5); break;
            case Keys.Home: if (!ReadOnly) SelectedIndex = _items.Count > 0 ? 0 : -1; break;
            case Keys.End: if (!ReadOnly) SelectedIndex = _items.Count - 1; break;
            default: return;
        }
        e.Handled = true;
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (ReadOnly || char.IsControl(e.KeyChar)) return;
        var now = Environment.TickCount64;
        _typed = now - _typedAt > 800 ? e.KeyChar.ToString() : _typed + e.KeyChar;
        _typedAt = now;
        var match = _items.ToList().FindIndex(i => i.StartsWith(_typed, StringComparison.CurrentCultureIgnoreCase));
        if (match >= 0) SelectedIndex = match;
        e.Handled = true;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Step(e.Delta > 0 ? 1 : -1);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hot = UpRect.Contains(e.Location) ? 1 : DownRect.Contains(e.Location) ? 2 : 0;
        if (hot == _hotButton) return;
        _hotButton = hot;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hotButton = 0;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button != MouseButtons.Left) return;
        _downButton = UpRect.Contains(e.Location) ? 1 : DownRect.Contains(e.Location) ? 2 : 0;
        if (_downButton == 0) return;
        Step(_downButton == 1 ? 1 : -1);
        _repeat.Interval = 400;
        _repeat.Start();
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _repeat.Stop();
        _downButton = 0;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        PaintFrame(g);
        var text = SelectedItem;
        Chrome.DrawText(g, text ?? string.Empty, Font, new Rectangle(S(10), 0, Math.Max(0, Width - S(40)), Height), Enabled ? Colors.Text : Colors.Disabled);

        foreach (var (rect, button, direction) in new[] { (UpRect, 1, ChevronDirection.Up), (DownRect, 2, ChevronDirection.Down) })
        {
            if (_downButton == button) Chrome.FillRounded(g, rect, Chrome.Blend(Colors.Surface, Colors.Primary, 0.25), Chrome.CornerRadius);
            else if (_hotButton == button) Chrome.FillRounded(g, rect, Colors.HoverBack, Chrome.CornerRadius);
            Chrome.DrawChevron(g, rect, direction, Enabled && !ReadOnly ? Colors.Muted : Colors.Disabled, 1.4f);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _repeat.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Standard calculator keypad with memory keys; expressions are evaluated by <see cref="EnterpriseCalculatorDropDown"/>.</summary>
[DefaultEvent("ValueChanged")]
[ToolboxItem(true)]
public class EnterpriseCalculatorPad : EnterpriseControlBase
{
    private static readonly string[][] Rows =
    {
        new[] { "CE", "C", "\u232B", "\u00F7" },
        new[] { "7", "8", "9", "\u00D7" },
        new[] { "4", "5", "6", "\u2212" },
        new[] { "1", "2", "3", "+" },
        new[] { "\u00B1", "0", ".", "=" }
    };

    private static readonly string[] MemoryKeys = { "MC", "MR", "M+", "M-" };
    private const int MaxDigits = 28;

    private readonly EnterpriseCalculatorDropDown _engine = new();
    private string _left = string.Empty;
    private string _entry = string.Empty;
    private bool _justEvaluated;
    private bool _error;
    private string? _hotKey;
    private string? _downKey;

    public EnterpriseCalculatorPad()
    {
        Size = new Size(260, 340);
        AccessibleRole = AccessibleRole.Client;
        AccessibleName = "Calculator";
    }

    [Category("Property Changed")] public event EventHandler? ValueChanged;

    [Browsable(false)] public EnterpriseCalculatorDropDown Engine => _engine;
    [Browsable(false)] public IReadOnlyList<string> History => _engine.CalculationHistory;
    [Category("Appearance"), DefaultValue(true)] public bool ShowMemoryKeys { get; set; } = true;

    /// <summary>The number on the display; setting it replaces the current entry.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public decimal Value
    {
        get => _error ? 0m : decimal.TryParse(_entry.Length > 0 ? _entry : "0", NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0m;
        set
        {
            _left = string.Empty;
            _entry = FormatNumber(value);
            _error = false;
            _justEvaluated = true;
            _engine.Value = value;
            Invalidate();
        }
    }

    /// <summary>Text on the main display line.</summary>
    [Browsable(false)]
    public string DisplayValue => _error ? "Error" : (_entry.Length > 0 ? _entry : "0").Replace(".", NumberFormatInfo.CurrentInfo.NumberDecimalSeparator, StringComparison.Ordinal);

    [Browsable(false)]
    public string ExpressionText => _left.Replace("*", " \u00D7 ").Replace("/", " \u00F7 ").Replace("+", " + ").Replace("-", " \u2212 ");

    private static string FormatNumber(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);

    /// <summary>Presses a key by its label: digits, ".", "+", "\u2212", "\u00D7", "\u00F7", "=", "C", "CE", "\u232B", "\u00B1", "MC", "MR", "M+", "M-".</summary>
    public void Press(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (_error) ClearAll();
        switch (key)
        {
            case { Length: 1 } when char.IsDigit(key[0]): Digit(key[0]); break;
            case ".": Decimal(); break;
            case "+" or "-" or "*" or "/" or "\u2212" or "\u00D7" or "\u00F7": Operator(key); break;
            case "=": Evaluate(); break;
            case "C": ClearAll(); break;
            case "CE": _entry = string.Empty; break;
            case "\u232B": if (_entry.Length > 0 && !_justEvaluated) _entry = _entry[..^1] is "-" ? string.Empty : _entry[..^1]; break;
            case "\u00B1": Negate(); break;
            case "MC": _engine.MemoryClear(); break;
            case "MR": _engine.MemoryRecall(); _entry = FormatNumber(_engine.Value); _justEvaluated = false; break;
            case "M+": _engine.Value = Value; _engine.MemoryAdd(); _justEvaluated = true; break;
            case "M-": _engine.Value = Value; _engine.MemorySubtract(); _justEvaluated = true; break;
            default: return;
        }
        Invalidate();
    }

    private void ClearAll()
    {
        _left = _entry = string.Empty;
        _error = _justEvaluated = false;
    }

    private void StartFresh()
    {
        if (!_justEvaluated) return;
        _left = _entry = string.Empty;
        _justEvaluated = false;
    }

    private void Digit(char digit)
    {
        StartFresh();
        if (_entry.Replace("-", string.Empty).Replace(".", string.Empty).Length >= MaxDigits) return;
        _entry = _entry is "0" ? digit.ToString() : _entry is "-0" ? "-" + digit : _entry + digit;
    }

    private void Decimal()
    {
        StartFresh();
        if (_entry.Contains('.', StringComparison.Ordinal)) return;
        _entry = (_entry.Length == 0 ? "0" : _entry == "-" ? "-0" : _entry) + ".";
    }

    private void Negate()
    {
        if (_entry.Length == 0 || _entry is "0") return;
        _entry = _entry.StartsWith('-') ? _entry[1..] : "-" + _entry;
        if (_justEvaluated) _engine.Value = Value;
    }

    private void Operator(string key)
    {
        var op = key switch { "\u2212" => "-", "\u00D7" => "*", "\u00F7" => "/", _ => key };
        if (_entry.Length == 0)
        {
            if (_left.Length == 0) _entry = "0";
            else { _left = _left[..^1] + op; return; }
        }

        _justEvaluated = false;
        var number = _entry.EndsWith('.') ? _entry[..^1] : _entry;
        _left += (number.StartsWith('-') ? "(" + number + ")" : number) + op;
        _entry = string.Empty;
    }

    private void Evaluate()
    {
        var expression = _left;
        if (_entry.Length > 0)
        {
            var number = _entry.EndsWith('.') ? _entry[..^1] : _entry;
            expression += number.StartsWith('-') ? "(" + number + ")" : number;
        }
        else if (expression.Length > 0) expression = expression[..^1];

        if (expression.Length == 0) return;
        try
        {
            var result = _engine.EvaluateExpression(expression);
            _left = string.Empty;
            _entry = FormatNumber(result);
            _justEvaluated = true;
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is DataException or OverflowException or InvalidCastException or FormatException or ArithmeticException)
        {
            _error = true;
            _left = _entry = string.Empty;
        }
    }

    private Rectangle DisplayRect => new(S(8), S(8), Width - S(16), S(76));

    private IEnumerable<(string Key, Rectangle Rect)> KeyCells()
    {
        var pad = S(8);
        var gap = S(6);
        var top = DisplayRect.Bottom + pad;
        var rows = Rows.Length + (ShowMemoryKeys ? 1 : 0);
        var rowHeight = (Height - top - pad - gap * (rows - 1)) / Math.Max(1, rows);
        var width = (Width - pad * 2 - gap * 3) / 4;
        var row = 0;
        if (ShowMemoryKeys)
        {
            for (var c = 0; c < 4; c++) yield return (MemoryKeys[c], new Rectangle(pad + c * (width + gap), top, width, rowHeight));
            row = 1;
        }

        foreach (var keys in Rows)
        {
            for (var c = 0; c < 4; c++) yield return (keys[c], new Rectangle(pad + c * (width + gap), top + row * (rowHeight + gap), width, rowHeight));
            row++;
        }
    }

    private string? KeyAt(Point p)
    {
        foreach (var (key, rect) in KeyCells()) if (rect.Contains(p)) return key;
        return null;
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Enter or Keys.Escape or Keys.Back or Keys.Delete || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Enter: Press("="); break;
            case Keys.Escape: Press("C"); break;
            case Keys.Back: Press("\u232B"); break;
            case Keys.Delete: Press("CE"); break;
            case Keys.C when e.Control: Clipboard.SetText(DisplayValue); break;
            default: return;
        }
        e.Handled = true;
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        var c = e.KeyChar;
        if (char.IsDigit(c) || c is '+' or '-' or '*' or '/') Press(c.ToString());
        else if (c is '.' or ',') Press(".");
        else if (c == '=') Press("=");
        else return;
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var key = KeyAt(e.Location);
        if (key == _hotKey) return;
        _hotKey = key;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hotKey = null;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        _downKey = e.Button == MouseButtons.Left ? KeyAt(e.Location) : null;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var key = KeyAt(e.Location);
        var pressed = _downKey;
        _downKey = null;
        if (key is not null && key == pressed) Press(key);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        PaintFrame(g);

        var display = DisplayRect;
        var expression = ExpressionText;
        if (_engine.MemoryValue != 0m) Chrome.DrawText(g, "M", Font, new Rectangle(display.X + S(4), display.Y, S(24), S(22)), Colors.Primary, ContentAlignment.MiddleLeft);
        Chrome.DrawText(g, expression, Font, new Rectangle(display.X + S(30), display.Y, display.Width - S(34), S(22)), Colors.Muted, ContentAlignment.MiddleRight);

        var text = DisplayValue;
        var size = Font.Size * 2.2f;
        Font? big = null;
        try
        {
            while (true)
            {
                big?.Dispose();
                big = new Font(Font.FontFamily, size, FontStyle.Bold);
                if (size <= 9f || Chrome.MeasureText(text, big) <= display.Width - S(8)) break;
                size -= 1f;
            }
            Chrome.DrawText(g, text, big, new Rectangle(display.X, display.Y + S(22), display.Width - S(4), display.Height - S(22)), _error ? Colors.Danger : Colors.Text, ContentAlignment.MiddleRight);
        }
        finally { big?.Dispose(); }

        foreach (var (key, rect) in KeyCells())
        {
            var isEquals = key == "=";
            var isOperator = key is "\u00F7" or "\u00D7" or "\u2212" or "+";
            var isMemory = key.StartsWith('M');
            var fill = isEquals ? Colors.Primary : isOperator || isMemory ? Colors.Header : Colors.Alt;
            if (_downKey == key) fill = Chrome.Blend(fill, Color.Black, 0.18);
            else if (_hotKey == key) fill = isEquals ? Chrome.Blend(fill, Color.White, 0.14) : Chrome.Blend(fill, Colors.Primary, 0.18);
            Chrome.FillRounded(g, rect, fill, Chrome.CornerRadius);
            Chrome.DrawText(g, key, isMemory ? Font : BoldFont, rect, isEquals ? Colors.PrimaryText : isMemory ? Colors.Muted : Colors.Text, ContentAlignment.MiddleCenter);
        }
    }
}
