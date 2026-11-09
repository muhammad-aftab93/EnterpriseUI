using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EnterpriseUI.WinForms;

/// <summary>Single or multi line text input drawn with the shared frame; hosts a borderless text box.</summary>
public class EnterpriseInputControl : EnterpriseControlBase
{
    private readonly TextBox _text = new() { BorderStyle = BorderStyle.None };
    private bool _hasError;

    public EnterpriseInputControl()
    {
        Size = new Size(260, 34);
        TabStop = false;
        _text.TextChanged += (_, e) => { OnTextChanged(e); ValueChanged?.Invoke(this, e); };
        _text.GotFocus += (_, _) => Invalidate();
        _text.LostFocus += (_, _) => Invalidate();
        _text.MouseEnter += (_, _) => OnMouseEnter(EventArgs.Empty);
        _text.MouseLeave += (_, _) => OnMouseLeave(EventArgs.Empty);
        Controls.Add(_text);
        OnThemeApplied();
        LayoutText();
    }

    public event EventHandler? ValueChanged;

    [AllowNull]
    public override string Text
    {
        get => _text.Text;
        set => _text.Text = value ?? string.Empty;
    }

    public string PlaceholderText { get => _text.PlaceholderText; set => _text.PlaceholderText = value; }
    public bool ReadOnly { get => _text.ReadOnly; set { _text.ReadOnly = value; Invalidate(); } }
    public int MaxLength { get => _text.MaxLength; set => _text.MaxLength = value; }
    public bool UsePasswordChar { get => _text.UseSystemPasswordChar; set => _text.UseSystemPasswordChar = value; }

    public bool Multiline
    {
        get => _text.Multiline;
        set { _text.Multiline = value; LayoutText(); }
    }

    public bool HasError
    {
        get => _hasError;
        set { _hasError = value; Invalidate(); }
    }

    public TextBox InnerTextBox => _text;

    protected override void OnThemeApplied()
    {
        base.OnThemeApplied();
        _text.BackColor = _text.ReadOnly ? Chrome.Blend(Colors.Surface, Colors.Header, 0.5) : Colors.Surface;
        _text.ForeColor = Colors.Text;
        _text.Font = Font;
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        _text.Font = Font;
        LayoutText();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _text.Focus();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutText();
    }

    private void LayoutText()
    {
        var pad = S(10);
        if (_text.Multiline)
        {
            _text.SetBounds(pad, S(8), Math.Max(10, Width - pad * 2), Math.Max(10, Height - S(16)));
            return;
        }

        var h = _text.PreferredHeight;
        _text.SetBounds(pad, Math.Max(0, (Height - h) / 2), Math.Max(10, Width - pad * 2), h);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        var rect = new Rectangle(0, 0, Width, Height);
        var back = _text.ReadOnly || !Enabled ? Chrome.Blend(Colors.Surface, Colors.Header, 0.5) : Colors.Surface;
        Chrome.FillRounded(g, rect, back, Chrome.CornerRadius);
        var focused = _text.Focused;
        var border = _hasError ? Colors.Danger : focused ? Colors.FocusRing : IsHovered ? Chrome.Blend(Colors.Border, Colors.Primary, 0.55) : Colors.Border;
        Chrome.DrawRoundedBorder(g, rect, border, Chrome.CornerRadius, focused || _hasError ? Chrome.FocusWidth : Chrome.BorderWidth);
    }
}

/// <summary>Drop-down that picks one value from a list; the list opens as a themed pop-up.</summary>
public class EnterpriseDropDownControl : EnterpriseControlBase
{
    private readonly List<string> _items = new();
    private int _selected = -1;
    private bool _hasError;

    public EnterpriseDropDownControl()
    {
        Size = new Size(260, 34);
        AccessibleRole = AccessibleRole.ComboBox;
    }

    public event EventHandler? SelectionChanged;

    public IReadOnlyList<string> Items => _items;
    public string PlaceholderText { get; set; } = "Select...";

    public bool HasError
    {
        get => _hasError;
        set { _hasError = value; Invalidate(); }
    }

    public void SetItems(IEnumerable<string> items)
    {
        _items.Clear();
        _items.AddRange(items);
        _selected = Math.Min(_selected, _items.Count - 1);
        Invalidate();
    }

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            value = Math.Clamp(value, -1, _items.Count - 1);
            if (value == _selected) return;
            _selected = value;
            Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string? SelectedItem
    {
        get => _selected >= 0 ? _items[_selected] : null;
        set => SelectedIndex = value is null ? -1 : _items.FindIndex(i => string.Equals(i, value, StringComparison.CurrentCultureIgnoreCase));
    }

    [AllowNull]
    public override string Text
    {
        get => SelectedItem ?? string.Empty;
        set => SelectedItem = value;
    }

    public void ShowDropDown()
    {
        if (!Enabled || _items.Count == 0) return;
        ChoicePopup.Show(this, new Rectangle(0, 0, Width, Height), _items, SelectedItem, choice => SelectedItem = choice);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button == MouseButtons.Left) ShowDropDown();
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Down when e.Alt:
            case Keys.Space:
            case Keys.Enter:
                ShowDropDown();
                break;
            case Keys.Down:
                SelectedIndex = Math.Min(_items.Count - 1, _selected + 1);
                break;
            case Keys.Up:
                SelectedIndex = Math.Max(0, _selected - 1);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        var rect = new Rectangle(0, 0, Width, Height);
        Chrome.FillRounded(g, rect, Enabled ? Colors.Surface : Chrome.Blend(Colors.Surface, Colors.Header, 0.5), Chrome.CornerRadius);
        var border = _hasError ? Colors.Danger : Focused ? Colors.FocusRing : IsHovered ? Chrome.Blend(Colors.Border, Colors.Primary, 0.55) : Colors.Border;
        Chrome.DrawRoundedBorder(g, rect, border, Chrome.CornerRadius, Focused || _hasError ? Chrome.FocusWidth : Chrome.BorderWidth);

        var text = SelectedItem;
        Chrome.DrawText(g, text ?? PlaceholderText, Font, new Rectangle(S(10), 0, Math.Max(0, Width - S(40)), Height), text is null || !Enabled ? Colors.Muted : Colors.Text);
        Chrome.DrawChevron(g, new Rectangle(Width - S(30), 0, S(24), Height), ChevronDirection.Down, Colors.Muted);
    }
}

/// <summary>Check box with a label, drawn with the shared check mark.</summary>
public class EnterpriseCheckControl : EnterpriseControlBase
{
    private bool _checked;

    public EnterpriseCheckControl()
    {
        Size = new Size(200, 28);
        AccessibleRole = AccessibleRole.CheckButton;
    }

    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool ReadOnly { get; set; }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button == MouseButtons.Left && !ReadOnly && Enabled) Checked = !Checked;
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) == Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode != Keys.Space || ReadOnly || !Enabled) return;
        Checked = !Checked;
        e.Handled = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        var size = S(18);
        var box = new Rectangle(S(2), (Height - size) / 2, size, size);
        if (Focused)
        {
            using var path = Chrome.RoundedRect(Rectangle.Inflate(box, S(3), S(3)), S(5));
            using var pen = new Pen(Colors.FocusRing, 1.5f);
            g.DrawPath(pen, path);
        }

        Chrome.DrawCheckBox(g, Colors, box, _checked ? CheckState.Checked : CheckState.Unchecked, Enabled && !ReadOnly, IsHovered);
        Chrome.DrawText(g, Text, Font, new Rectangle(box.Right + S(10), 0, Math.Max(0, Width - box.Right - S(12)), Height), Enabled ? Colors.Text : Colors.Disabled);
    }
}
