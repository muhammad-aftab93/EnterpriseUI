using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace EnterpriseUI.WinForms;

/// <summary>Standard push button; supports <see cref="DialogResult"/> and acts as a form's accept or cancel button.</summary>
[ToolboxItem(true)]
public class EnterpriseButtonControl : EnterpriseButtonBase, IButtonControl
{
    private bool _isDefault;

    /// <summary>The dialog result a click sets on the parent form.</summary>
    [Category("Behavior"), Description("The dialog result a click sets on the parent form.")]
    [DefaultValue(DialogResult.None)]
    public DialogResult DialogResult { get; set; }

    /// <summary>True while the form uses this button as its accept button.</summary>
    [Browsable(false)]
    public bool IsDefault => _isDefault;

    /// <summary>Called by the form when this button becomes or stops being its accept button.</summary>
    public void NotifyDefault(bool value)
    {
        _isDefault = value;
        Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        if (FindForm() is { } form && DialogResult != DialogResult.None) form.DialogResult = DialogResult;
        base.OnClick(e);
    }

    protected override void PaintFaceDecoration(Graphics g, Rectangle face, Color fill, bool outline)
    {
        if (_isDefault && !outline && Enabled) Chrome.DrawRoundedBorder(g, Rectangle.Inflate(face, -2, -2), Color.FromArgb(150, FormChrome.TextOn(fill)), ScaledRadius, 1f);
    }

    protected override AccessibleStates StateFlags() => _isDefault ? base.StateFlags() | AccessibleStates.Default : base.StateFlags();
}

/// <summary>Latching button: two-state (or three-state) with an optional exclusive group.</summary>
public class EnterpriseToggleButtonControl : EnterpriseButtonBase
{
    private CheckState _state;

    public EnterpriseToggleButtonControl() => AccessibleRole = AccessibleRole.CheckButton;

    /// <summary>Adds the indeterminate state to the cycle of clicks.</summary>
    [Category("Behavior"), Description("Adds the indeterminate state to the cycle of clicks.")]
    [DefaultValue(false)]
    public bool ThreeState { get; set; }

    /// <summary>Toggle buttons sharing a group name under one parent behave like radio buttons.</summary>
    [Category("Behavior"), Description("Toggle buttons with the same group name behave like radio buttons.")]
    [DefaultValue(null)]
    public string? GroupName { get; set; }

    /// <summary>Whether the button is latched on.</summary>
    [Category("Appearance"), Description("Whether the button is latched on.")]
    [DefaultValue(false)]
    public bool Checked
    {
        get => _state == CheckState.Checked;
        set => CheckState = value ? CheckState.Checked : CheckState.Unchecked;
    }

    /// <summary>Unchecked, checked or indeterminate. Setting it raises <see cref="CheckStateChanging"/> first, which can cancel the change.</summary>
    [Category("Appearance"), Description("Unchecked, checked or indeterminate.")]
    [DefaultValue(CheckState.Unchecked)]
    public CheckState CheckState
    {
        get => _state;
        set
        {
            if (_state == value) return;
            var args = new ToggleStateChangingEventArgs(_state, value);
            CheckStateChanging?.Invoke(this, args);
            if (!args.Cancel) ApplyState(value);
        }
    }

    /// <summary>Raised before the state changes; set <see cref="CancelEventArgs.Cancel"/> to keep the current state. Not raised for the buttons of the group that are unchecked because another one was checked.</summary>
    [Category("Behavior"), Description("Raised before the state changes; the change can be canceled.")]
    public event EventHandler<ToggleStateChangingEventArgs>? CheckStateChanging;

    /// <summary>Raised after <see cref="Checked"/> changed.</summary>
    [Category("Behavior"), Description("Raised after Checked changed.")]
    public event EventHandler? CheckedChanged;

    /// <summary>Raised after <see cref="CheckState"/> changed.</summary>
    [Category("Behavior"), Description("Raised after CheckState changed.")]
    public event EventHandler? CheckStateChanged;

    private void ApplyState(CheckState value)
    {
        _state = value;
        if (value == CheckState.Checked && !string.IsNullOrEmpty(GroupName) && Parent != null)
        {
            foreach (var sibling in Parent.Controls.OfType<EnterpriseToggleButtonControl>().Where(s => s != this && s.GroupName == GroupName && s._state != CheckState.Unchecked)) sibling.ApplyState(CheckState.Unchecked);
        }

        Invalidate();
        AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
        CheckStateChanged?.Invoke(this, EventArgs.Empty);
        CheckedChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Moves to the next state: on and off, or on, indeterminate and off with <see cref="ThreeState"/>; a grouped button can only be checked.</summary>
    public void Toggle()
    {
        if (!string.IsNullOrEmpty(GroupName))
        {
            if (_state != CheckState.Checked) CheckState = CheckState.Checked;
            return;
        }

        CheckState = ThreeState ? _state switch { CheckState.Unchecked => CheckState.Checked, CheckState.Checked => CheckState.Indeterminate, _ => CheckState.Unchecked }
            : _state == CheckState.Checked ? CheckState.Unchecked : CheckState.Checked;
    }

    protected override void OnClick(EventArgs e)
    {
        if (Enabled) Toggle();
        base.OnClick(e);
    }

    protected override string DefaultActionName => "Toggle";

    protected override AccessibleStates StateFlags() => _state switch { CheckState.Checked => AccessibleStates.Checked, CheckState.Indeterminate => AccessibleStates.Mixed, _ => AccessibleStates.None };

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        var face = FaceBounds;
        Color fore;
        var fill = FillFor(Variant == EnterpriseButtonVariant.Outline ? EnterpriseButtonVariant.Primary : Variant);
        if (_state == CheckState.Checked)
        {
            PaintFace(g, face, fill, false);
            fore = ForeFor(fill, false);
        }
        else
        {
            PaintFace(g, face, Colors.Surface, true, _state == CheckState.Indeterminate ? AccentColor : Colors.Border);
            fore = Fore(_state == CheckState.Indeterminate ? AccentColor : Colors.Text);
        }

        PaintContent(g, ContentArea(face, BadgeReserve), fore);
        if (_state == CheckState.Indeterminate)
        {
            using var pen = new Pen(AccentColor, S(2));
            g.DrawLine(pen, face.X + face.Width / 2 - S(8), face.Bottom - S(5), face.X + face.Width / 2 + S(8), face.Bottom - S(5));
        }

        PaintBadge(g, face, fill);
        PaintFocus(g);
    }
}

/// <summary>Button with a drop-down part: the main part runs the default action, the arrow part opens the menu.</summary>
public class EnterpriseSplitButtonControl : EnterpriseButtonBase
{
    private readonly ContextMenuStrip _menu = new();
    private readonly ValueAnimator _arrowHover = new();
    private int _hotPart;
    private int _pressedPart;
    private DateTime _menuClosedAt = DateTime.MinValue;
    private bool _menuOpen;

    public EnterpriseSplitButtonControl()
    {
        Size = new Size(175, 34);
        AccessibleRole = AccessibleRole.SplitButton;
        _menu.Opened += (_, _) => { _menuOpen = true; Invalidate(); };
        _menu.Closed += (_, _) => { _menuOpen = false; _menuClosedAt = DateTime.UtcNow; Invalidate(); DropDownClosed?.Invoke(this, EventArgs.Empty); };
        _menu.Opening += (_, _) => DropDownOpening?.Invoke(this, EventArgs.Empty);
        _arrowHover.Updated += (_, _) => Invalidate();
        ApplyMenuTheme();
    }

    /// <summary>True: the main part clicks and only the arrow opens the menu. False: the whole button opens the menu.</summary>
    [Category("Behavior"), Description("True: the main part clicks and the arrow opens the menu. False: the whole button opens the menu.")]
    [DefaultValue(true)]
    public bool IsSplitMode { get; set; } = true;

    /// <summary>The items of the drop-down menu.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ToolStripItemCollection MenuItems => _menu.Items;

    /// <summary>True while the menu is open.</summary>
    [Browsable(false)]
    public bool IsDropDownOpen => _menuOpen;

    /// <summary>Raised when the main part is clicked in split mode.</summary>
    [Category("Action"), Description("Raised when the main part is clicked.")]
    public event EventHandler? PrimaryActionClicked;

    /// <summary>Raised with the text of the chosen menu item.</summary>
    public event Action<string>? MenuItemSelected;

    /// <summary>Raised before the menu opens.</summary>
    public event EventHandler? DropDownOpening;

    /// <summary>Raised after the menu closed.</summary>
    public event EventHandler? DropDownClosed;

    /// <summary>Adds a menu item; <paramref name="onClick"/> runs when it is chosen.</summary>
    public ToolStripMenuItem AddOption(string itemText, Action? onClick = null, string? glyph = null)
    {
        var item = new ToolStripMenuItem(itemText);
        if (glyph != null) item.Text = glyph + "  " + itemText;
        item.Click += (_, _) =>
        {
            onClick?.Invoke();
            MenuItemSelected?.Invoke(itemText);
        };
        _menu.Items.Add(item);
        return item;
    }

    public void AddSeparator() => _menu.Items.Add(new ToolStripSeparator());

    public void ClearOptions() => _menu.Items.Clear();

    private int ArrowWidth => S(30);
    private Rectangle ArrowRect => new(FaceBounds.Right - ArrowWidth, FaceBounds.Y, ArrowWidth, FaceBounds.Height);

    /// <summary>The size of a plain button plus the drop-down arrow.</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var size = base.GetPreferredSize(proposedSize);
        return new Size(size.Width + ArrowWidth - S(4), size.Height);
    }

    private int PartAt(Point p) => !IsSplitMode || ArrowRect.Contains(p) || p.X >= ArrowRect.X ? 2 : 1;

    public void ShowMenu()
    {
        if (!Enabled || _menu.Items.Count == 0) return;
        _menu.MinimumSize = new Size(Width, 0);
        _menu.Show(this, new Point(0, Height));
    }

    private void ApplyMenuTheme()
    {
        _menu.Renderer = new EnterpriseMenuRenderer(Colors);
        _menu.BackColor = Colors.Surface;
        _menu.ForeColor = Colors.Text;
        _menu.Font = Font;
    }

    protected override void OnThemeApplied()
    {
        base.OnThemeApplied();
        ApplyMenuTheme();
    }

    protected override bool ShouldClick(Point location) => IsSplitMode && PartAt(location) == 1;

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (IsSplitMode) PrimaryActionClicked?.Invoke(this, EventArgs.Empty);
    }

    public override void PerformClick()
    {
        if (!Enabled) return;
        if (IsSplitMode) OnClick(EventArgs.Empty);
        else ShowMenu();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var part = PartAt(e.Location);
        if (part == _hotPart) return;
        _hotPart = part;
        _arrowHover.AnimateTo(part == 2 && IsSplitMode ? 1 : 0, 100);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hotPart = 0;
        _arrowHover.AnimateTo(0, 120);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _pressedPart = PartAt(e.Location);
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Enabled || _pressedPart != 2) return;
        if ((DateTime.UtcNow - _menuClosedAt).TotalMilliseconds > 250) ShowMenu();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Down || e.KeyCode == Keys.F4)
        {
            base.OnKeyDown(e);
            ShowMenu();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) == Keys.Down || base.IsInputKey(keyData);

    protected override AccessibleStates StateFlags() => (_menuOpen ? AccessibleStates.Expanded : AccessibleStates.Collapsed) | base.StateFlags();

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        var outline = Variant == EnterpriseButtonVariant.Outline;
        var fill = FillFor(Variant);
        var face = FaceBounds;
        PaintFace(g, face, fill, outline);
        var fore = ForeFor(fill, outline);

        var arrow = ArrowRect;
        var contentRight = IsSplitMode ? arrow.X : face.Right - arrow.Width;
        if (IsSplitMode)
        {
            if (_arrowHover.Value > 0.01 && Enabled)
            {
                var state = g.Save();
                using var clip = Chrome.RoundedRect(face, ScaledRadius);
                g.SetClip(clip);
                using var tint = new SolidBrush(Color.FromArgb((int)(40 * _arrowHover.Value), outline ? AccentColor : Color.White));
                g.FillRectangle(tint, arrow);
                g.Restore(state);
            }

            using var pen = new Pen(Color.FromArgb(Enabled ? 120 : 60, fore));
            g.DrawLine(pen, arrow.X, face.Y + S(7), arrow.X, face.Bottom - S(8));
        }

        PaintContent(g, ContentArea(face, face.Right - contentRight - S(4)), fore);
        Chrome.DrawChevron(g, arrow, _menuOpen ? ChevronDirection.Up : ChevronDirection.Down, fore, 1.8f);
        PaintFocus(g);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _arrowHover.Dispose();
            _menu.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>Button whose whole surface opens a menu.</summary>
public class EnterpriseDropDownButtonControl : EnterpriseSplitButtonControl
{
    public EnterpriseDropDownButtonControl()
    {
        IsSplitMode = false;
        Size = new Size(165, 34);
    }
}
