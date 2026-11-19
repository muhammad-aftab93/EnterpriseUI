using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace EnterpriseUI.WinForms;

/// <summary>Editable text box with a drop-down button that opens a themed pop-up; the base of the date, time and pop-up editors.</summary>
[DefaultEvent("DropDownOpened")]
public abstract class EnterpriseDropDownEditorBase : EnterpriseControlBase
{
    private readonly TextBox _inner = new() { BorderStyle = BorderStyle.None };
    private ToolStripDropDown? _drop;
    private long _closedAt;
    private bool _hasError;
    private bool _buttonHot;
    private bool _syncing;

    protected EnterpriseDropDownEditorBase()
    {
        Size = new Size(220, 34);
        TabStop = false;
        AccessibleRole = AccessibleRole.ComboBox;
        _inner.GotFocus += (_, _) => Invalidate();
        _inner.LostFocus += (_, _) =>
        {
            if (_drop is null) CommitInnerText();
            Invalidate();
        };
        _inner.KeyDown += OnInnerKeyDown;
        _inner.MouseDown += (_, e) =>
        {
            if (_inner.ReadOnly && e.Button == MouseButtons.Left) ToggleDropDown();
        };
        _inner.TextChanged += (_, _) =>
        {
            if (!_syncing && _hasError) { _hasError = false; Invalidate(); }
        };
        Controls.Add(_inner);
        OnThemeApplied();
        LayoutInner();
    }

    [Category("Behavior")] public event EventHandler? DropDownOpened;
    [Category("Behavior")] public event EventHandler? DropDownClosed;

    [Browsable(false)] public bool IsDropDownOpen => _drop is not null;

    /// <summary>Shows the danger border; set automatically when typed text cannot be parsed.</summary>
    [Category("Appearance"), DefaultValue(false)]
    public bool HasError
    {
        get => _hasError;
        set { _hasError = value; Invalidate(); }
    }

    [Category("Behavior"), DefaultValue(false)]
    public bool ReadOnly
    {
        get => _inner.ReadOnly;
        set { _inner.ReadOnly = value; OnThemeApplied(); Invalidate(); }
    }

    [Category("Appearance"), DefaultValue(""), Localizable(true)]
    public string PlaceholderText
    {
        get => _inner.PlaceholderText;
        set => _inner.PlaceholderText = value ?? string.Empty;
    }

    protected TextBox InnerTextBox => _inner;
    protected virtual bool OwnsPopupContent => true;

    public new string? AccessibleName
    {
        get => base.AccessibleName;
        set { base.AccessibleName = value; _inner.AccessibleName = value; }
    }

    public new string? AccessibleDescription
    {
        get => base.AccessibleDescription;
        set { base.AccessibleDescription = value; _inner.AccessibleDescription = value; }
    }

    protected abstract Control CreatePopupContent();

    /// <summary>Parses the typed text; returns false when it is invalid or out of range.</summary>
    protected abstract bool TryCommitText(string text);

    /// <summary>Rewrites the text from the current value; call <see cref="SetDisplayText"/>.</summary>
    protected abstract void RefreshText();

    protected virtual void StepValue(int direction) { }

    /// <summary>Lets a derived editor veto opening the pop-up.</summary>
    protected virtual bool CanOpenDropDown() => true;

    protected void SetDisplayText(string text)
    {
        _syncing = true;
        try { _inner.Text = text; }
        finally { _syncing = false; }
        _hasError = false;
        Invalidate();
    }

    protected void CommitInnerText()
    {
        if (_inner.ReadOnly) return;
        if (TryCommitText(_inner.Text)) RefreshText();
        else { _hasError = true; Invalidate(); }
    }

    public void ShowDropDown()
    {
        if (!Enabled || _drop is not null || !CanOpenDropDown()) return;
        if (!_inner.ReadOnly && _inner.Text.Length > 0) CommitInnerText();

        var content = CreatePopupContent();
        var host = new ToolStripControlHost(content) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = content.Size };
        var drop = new ToolStripDropDown { Padding = Padding.Empty, Margin = Padding.Empty, DropShadowEnabled = true, AutoSize = true };
        drop.Items.Add(host);
        drop.Closed += (_, _) => OnPopupClosed(drop, host, content);

        _drop = drop;
        Invalidate();
        drop.Show(PointToScreen(new Point(0, Height + 1)));
        content.Focus();
        DropDownOpened?.Invoke(this, EventArgs.Empty);
    }

    public void CloseDropDown() => _drop?.Close();

    /// <summary>Opens or closes the pop-up; ignores the click that just dismissed it so the button toggles.</summary>
    private void ToggleDropDown()
    {
        if (_drop is not null) CloseDropDown();
        else if (Environment.TickCount64 - _closedAt > 250) ShowDropDown();
    }

    private void OnPopupClosed(ToolStripDropDown drop, ToolStripControlHost host, Control content)
    {
        _closedAt = Environment.TickCount64;
        _drop = null;
        Invalidate();
        DropDownClosed?.Invoke(this, EventArgs.Empty);
        if (IsDisposed || Disposing || !IsHandleCreated)
        {
            drop.Dispose();
            return;
        }

        BeginInvoke(new Action(() =>
        {
            if (!OwnsPopupContent)
            {
                // The host would dispose the content with it, so the content is detached and the host is left to the collector.
                drop.Items.Remove(host);
                content.Parent?.Controls.Remove(content);
                drop.Dispose();
                return;
            }
            drop.Dispose();
        }));
        if (!_inner.Focused) _inner.Focus();
    }

    protected Rectangle ButtonRect => new(Width - S(32), 1, S(31), Math.Max(0, Height - 2));

    protected override void OnThemeApplied()
    {
        base.OnThemeApplied();
        _inner.BackColor = _inner.ReadOnly || !Enabled ? Chrome.Blend(Colors.Surface, Colors.Header, 0.5) : Colors.Surface;
        _inner.ForeColor = Colors.Text;
        _inner.Font = Font;
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        _inner.Font = Font;
        LayoutInner();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        OnThemeApplied();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _inner.Focus();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutInner();
    }

    private void LayoutInner()
    {
        var left = S(10);
        var h = _inner.PreferredHeight;
        _inner.SetBounds(left, Math.Max(0, (Height - h) / 2), Math.Max(10, Width - left - S(34)), h);
    }

    private void OnInnerKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.F4:
            case Keys.Down when e.Alt:
                if (_drop is null) ShowDropDown();
                break;
            case Keys.Up when !e.Alt && !_inner.ReadOnly:
                StepValue(1);
                break;
            case Keys.Down when !e.Alt && !_inner.ReadOnly:
                StepValue(-1);
                break;
            case Keys.Enter or Keys.Space when _inner.ReadOnly:
                if (_drop is null) ShowDropDown();
                break;
            case Keys.Enter:
                CommitInnerText();
                break;
            case Keys.Escape:
                RefreshText();
                break;
            default:
                return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hot = ButtonRect.Contains(e.Location);
        if (hot == _buttonHot) return;
        _buttonHot = hot;
        Cursor = hot ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _buttonHot = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _inner.Focus();
        if (e.Button != MouseButtons.Left || !ButtonRect.Contains(e.Location)) return;
        ToggleDropDown();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        var rect = new Rectangle(0, 0, Width, Height);
        Chrome.FillRounded(g, rect, Enabled ? (_inner.ReadOnly ? Chrome.Blend(Colors.Surface, Colors.Header, 0.5) : Colors.Surface) : Chrome.Blend(Colors.Surface, Colors.Header, 0.5), Chrome.CornerRadius);

        if (_buttonHot || _drop is not null)
        {
            var state = g.Save();
            using (var clip = Chrome.RoundedRect(Rectangle.Inflate(rect, -1, -1), Chrome.CornerRadius - 1)) g.SetClip(clip);
            using var hover = new SolidBrush(Chrome.Blend(Colors.Surface, Colors.Primary, 0.14));
            g.FillRectangle(hover, ButtonRect);
            g.Restore(state);
        }

        var focused = _inner.Focused || _drop is not null;
        var border = _hasError ? Colors.Danger : focused ? Colors.FocusRing : IsHovered ? Chrome.Blend(Colors.Border, Colors.Primary, 0.55) : Colors.Border;
        Chrome.DrawRoundedBorder(g, rect, border, Chrome.CornerRadius, focused || _hasError ? Chrome.FocusWidth : Chrome.BorderWidth);
        Chrome.DrawChevron(g, ButtonRect, _drop is not null ? ChevronDirection.Up : ChevronDirection.Down, Enabled ? Colors.Muted : Colors.Disabled);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _drop?.Close();
            _drop = null;
        }
        base.Dispose(disposing);
    }
}
