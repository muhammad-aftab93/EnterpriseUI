using System.ComponentModel;
using System.Drawing.Drawing2D;
using EnterpriseUI.Editors.Interactivity;
using EnterpriseUI.Layout;
using EnterpriseUI.Theming;
using Timer = System.Windows.Forms.Timer;

namespace EnterpriseUI.WinForms;

[Flags]
public enum EnterpriseTaskDialogButtons
{
    None = 0,
    OK = 1,
    Yes = 2,
    No = 4,
    Cancel = 8,
    Retry = 16,
    Close = 32
}

public sealed class EnterpriseTaskDialogCommandEventArgs : EventArgs
{
    public EnterpriseTaskDialogCommandEventArgs(string commandId) => CommandId = commandId;

    public string CommandId { get; }

    /// <summary>Set to true to keep the dialog open after the command link was chosen.</summary>
    public bool KeepOpen { get; set; }
}

public sealed class EnterpriseTaskDialogTickEventArgs : EventArgs
{
    public EnterpriseTaskDialogTickEventArgs(TimeSpan elapsed) => Elapsed = elapsed;

    public TimeSpan Elapsed { get; }

    /// <summary>Set to true to restart the elapsed time and the auto-close countdown.</summary>
    public bool ResetTimer { get; set; }
}

/// <summary>Dialog button that shows a title and an explanatory line next to an arrow, used for task dialog command links.</summary>
internal sealed class CommandLinkButton : EnterpriseControlBase
{
    private readonly string _title;
    private readonly string _note;
    private bool _pressed;

    public CommandLinkButton(string id, string title, string note)
    {
        Id = id;
        _title = title;
        _note = note;
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = title;
        AccessibleDescription = note;
    }

    public string Id { get; }

    public int MeasureHeight(int width)
    {
        var textWidth = Math.Max(40, width - S(56));
        var height = BoldFont.Height + S(16);
        if (_note.Length > 0) height += TextRenderer.MeasureText(_note, Font, new Size(textWidth, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + S(2);
        return Math.Max(S(44), height);
    }

    public void PerformClick() => OnClick(EventArgs.Empty);

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is not (Keys.Enter or Keys.Space)) return;
        e.Handled = true;
        PerformClick();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _pressed = true;
        Focus();
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var pressed = _pressed;
        _pressed = false;
        Invalidate();
        if (pressed && e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location)) PerformClick();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var c = Colors;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? c.Surface);
        var face = new Rectangle(0, 0, Width, Height);
        var back = _pressed ? c.SelectionBack : IsHovered || Focused ? c.HoverBack : c.Surface;
        Chrome.FillRounded(g, face, back, Chrome.CornerRadius);
        Chrome.DrawRoundedBorder(g, face, Focused ? c.FocusRing : IsHovered ? c.Primary : c.Border, Chrome.CornerRadius, Focused ? 2f : 1f);
        TextRenderer.DrawText(g, "➜", GlyphFonts.Get(12f), new Rectangle(S(8), 0, S(32), Height), c.Primary, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        var textX = S(48);
        var textWidth = Width - textX - S(10);
        Chrome.DrawText(g, _title, BoldFont, new Rectangle(textX, S(8), textWidth, BoldFont.Height), c.Text);
        if (_note.Length > 0) Chrome.DrawText(g, _note, Font, new Rectangle(textX, S(8) + BoldFont.Height + S(2), textWidth, Height - BoldFont.Height - S(14)), c.Muted, ContentAlignment.TopLeft, true);
    }
}

/// <summary>Task dialog window: main instruction, content with links, command links, radio buttons, a verification checkbox, expandable details, progress, footer, standard buttons and a timer; bound to <see cref="EnterpriseTaskDialog"/>.</summary>
[ToolboxItem(false)]
[DefaultEvent("CommandLinkClicked")]
public class EnterpriseTaskDialogForm : EnterpriseForm
{
    private const int ContentWidth = 452;
    private readonly FlowLayoutPanel _stack = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(24, 18, 24, 8), AutoScroll = false };
    private readonly FlowLayoutPanel _buttons = new() { Dock = DockStyle.Bottom, Height = 52, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(16, 8, 16, 8) };
    private readonly Label _footer = new() { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(24, 10, 24, 0), Visible = false };
    private readonly Label _icon = new() { TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 16, FontStyle.Bold), Size = new Size(40, 40), Margin = new Padding(0, 0, 12, 0) };
    private readonly Label _instruction = new() { AutoSize = true, MaximumSize = new Size(ContentWidth, 0), Font = new Font("Segoe UI", 12.5f), Margin = new Padding(0, 0, 0, 8) };
    private readonly EnterpriseLabelControl _content = new() { MarkupMode = LabelMarkupMode.Markdown, AutoEllipsis = false, WordWrap = true, Font = new Font("Segoe UI", 9.5f), Margin = new Padding(0, 0, 0, 8) };
    private readonly Panel _radioHost = new() { AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
    private readonly List<RadioButton> _radios = new();
    private readonly List<CommandLinkButton> _links = new();
    private readonly CheckBox _verification = new() { AutoSize = true, Margin = new Padding(0, 6, 0, 6) };
    private readonly LinkLabel _expander = new() { AutoSize = true, Margin = new Padding(0, 4, 0, 4), LinkBehavior = LinkBehavior.HoverUnderline };
    private readonly Label _expanded = new() { AutoSize = true, MaximumSize = new Size(ContentWidth, 0), Margin = new Padding(0, 0, 0, 8), Visible = false };
    private readonly EnterpriseProgressBarControl _progress = new() { Size = new Size(ContentWidth, 14), Margin = new Padding(0, 4, 0, 8), ShowText = false, Visible = false };
    private readonly Timer _timer = new() { Interval = 250 };
    private readonly List<string> _radioItems = new();
    private DateTime _started;
    private EnterpriseTaskDialog _model;
    private MessageBoxIcon _dialogIcon;
    private EnterpriseTaskDialogButtons _commonButtons = EnterpriseTaskDialogButtons.None;
    private int _autoClose;
    private bool _isExpanded;
    private bool _showProgress;
    private string _expandedText = string.Empty;
    private string _expandLabel = "Show details";
    private string _collapseLabel = "Hide details";

    public EnterpriseTaskDialogForm() : this(new EnterpriseTaskDialog())
    {
    }

    public EnterpriseTaskDialogForm(EnterpriseTaskDialog model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        AllowResize = false;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        TitleBar.ShowIcon = false;
        Controls.Add(_stack);
        Controls.Add(_footer);
        Controls.Add(_buttons);
        _progress.Style = EnterpriseProgressStyle.Determinate;
        _content.LinkClicked += (_, e) => HyperlinkClicked?.Invoke(this, e.LinkText ?? string.Empty);
        _verification.CheckedChanged += (_, _) =>
        {
            _model.IsVerificationChecked = _verification.Checked;
            VerificationChanged?.Invoke(this, _verification.Checked);
        };
        _expander.LinkClicked += (_, _) => Expanded = !Expanded;
        _timer.Tick += (_, _) => OnTimerTick();
        ApplyModel();
    }

    /// <summary>The bound model; title, instruction, content, command links, verification and details are read from it when the dialog is shown.</summary>
    [Browsable(false)]
    public EnterpriseTaskDialog Model
    {
        get => _model;
        set
        {
            _model = value ?? throw new ArgumentNullException(nameof(value));
            ApplyModel();
        }
    }

    [DefaultValue(MessageBoxIcon.None)]
    public MessageBoxIcon DialogIcon
    {
        get => _dialogIcon;
        set { _dialogIcon = value; Rebuild(); }
    }

    [DefaultValue(EnterpriseTaskDialogButtons.None)]
    public EnterpriseTaskDialogButtons CommonButtons
    {
        get => _commonButtons;
        set { _commonButtons = value; Rebuild(); }
    }

    /// <summary>Small text below a separator at the bottom of the dialog.</summary>
    [DefaultValue("")]
    public string FooterText
    {
        get => _footer.Text;
        set { _footer.Text = value ?? string.Empty; Rebuild(); }
    }

    [DefaultValue(false)]
    public bool ShowProgress
    {
        get => _showProgress;
        set { _showProgress = value; Rebuild(); }
    }

    /// <summary>0 to 100; a negative value shows a marquee.</summary>
    [DefaultValue(0)]
    public int ProgressValue
    {
        get => _progress.Style == EnterpriseProgressStyle.Indeterminate ? -1 : _progress.Value;
        set
        {
            if (value < 0) _progress.Style = EnterpriseProgressStyle.Indeterminate;
            else
            {
                _progress.Style = EnterpriseProgressStyle.Determinate;
                _progress.Value = Math.Clamp(value, _progress.Minimum, _progress.Maximum);
            }
        }
    }

    /// <summary>Texts of the radio buttons; the first is selected until the user picks another.</summary>
    [Browsable(false)]
    public IList<string> RadioButtons => _radioItems;

    [Browsable(false)]
    public int SelectedRadioIndex
    {
        get => _radios.FindIndex(r => r.Checked);
        set
        {
            if (value >= 0 && value < _radios.Count) _radios[value].Checked = true;
        }
    }

    /// <summary>Seconds after which the dialog closes with Cancel; 0 turns the countdown off.</summary>
    [DefaultValue(0)]
    public int AutoCloseSeconds
    {
        get => _autoClose;
        set => _autoClose = Math.Max(0, value);
    }

    [Browsable(false)]
    public bool Expanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value || _expandedText.Length == 0) return;
            _isExpanded = value;
            _expanded.Visible = value;
            _expander.Text = value ? _collapseLabel : _expandLabel;
            ResizeToContent();
            ExpandedChanged?.Invoke(this, value);
        }
    }

    [Browsable(false)]
    public string? SelectedCommandId => _model.SelectedCommandId;

    [Browsable(false)]
    public bool IsVerificationChecked => _verification.Checked;

    public event EventHandler<EnterpriseTaskDialogCommandEventArgs>? CommandLinkClicked;
    public event EventHandler<bool>? VerificationChanged;
    public event EventHandler<bool>? ExpandedChanged;
    public event EventHandler<string>? HyperlinkClicked;
    public event EventHandler<EnterpriseTaskDialogTickEventArgs>? Tick;

    /// <summary>Shows the dialog modally and writes the chosen command link and verification state back to the model.</summary>
    public DialogResult Execute(IWin32Window? owner = null)
    {
        Rebuild();
        var result = owner == null ? ShowDialog() : ShowDialog(owner);
        _model.IsVerificationChecked = _verification.Checked;
        return result;
    }

    /// <summary>Chooses a command link as if the user had clicked it.</summary>
    public bool ClickCommandLink(string id)
    {
        var link = _links.Find(l => l.Id == id);
        if (link == null) return false;
        link.PerformClick();
        return true;
    }

    /// <summary>Presses a standard button as if the user had clicked it.</summary>
    public bool ClickButton(EnterpriseTaskDialogButtons button)
    {
        foreach (var control in _buttons.Controls.OfType<EnterpriseButtonControl>())
        {
            if (control.Tag is EnterpriseTaskDialogButtons b && b == button)
            {
                control.PerformClick();
                return true;
            }
        }

        return false;
    }

    /// <summary>Reads text and command links from <see cref="Model"/>.</summary>
    public void ApplyModel()
    {
        Text = _model.WindowTitle;
        _expandedText = _model.ExpandedInformation;
        _verification.Text = _model.VerificationText;
        _verification.Checked = _model.IsVerificationChecked;
        Rebuild();
    }

    private void Rebuild()
    {
        var c = Colors;
        var hasIcon = _dialogIcon is MessageBoxIcon.Error or MessageBoxIcon.Warning or MessageBoxIcon.Information or MessageBoxIcon.Question;
        _stack.SuspendLayout();
        foreach (var old in _stack.Controls.Cast<Control>().ToList())
        {
            if (old is CommandLinkButton) old.Dispose();
            else if (old is FlowLayoutPanel row)
            {
                row.Controls.Clear();
                row.Dispose();
            }
        }

        _stack.Controls.Clear();

        _instruction.Text = _model.MainInstruction;
        _content.Text = _model.Content;
        if (hasIcon)
        {
            var (glyph, color) = _dialogIcon switch
            {
                MessageBoxIcon.Error => ("!", c.Danger),
                MessageBoxIcon.Warning => ("!", c.Warning),
                MessageBoxIcon.Information => ("i", c.Primary),
                _ => ("?", c.Primary)
            };
            _icon.Text = glyph;
            _icon.ForeColor = color;
            _icon.BackColor = c.Surface;
        }

        if (_instruction.Text.Length > 0)
        {
            var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0), BackColor = c.Surface };
            if (hasIcon) row.Controls.Add(_icon);
            _instruction.MaximumSize = new Size(ContentWidth - (hasIcon ? 52 : 0), 0);
            row.Controls.Add(_instruction);
            _stack.Controls.Add(row);
        }

        if (_content.Text.Length > 0)
        {
            using var layout = MarkupLayout.Create(LabelMarkup.Parse(_content.Text, LabelMarkupMode.Markdown), _content.Font, c, ContentWidth, true);
            _content.Size = new Size(ContentWidth, Math.Max(layout.Size.Height, 18) + 4);
            _stack.Controls.Add(_content);
        }

        BuildRadios();
        if (_radioItems.Count > 0) _stack.Controls.Add(_radioHost);

        _links.Clear();
        foreach (var option in _model.CommandLinks)
        {
            var link = new CommandLinkButton(option.Id, option.Title, option.Description) { Width = ContentWidth, Margin = new Padding(0, 0, 0, 6) };
            link.Height = link.MeasureHeight(ContentWidth);
            link.Click += (_, _) => OnCommandLink(link.Id);
            _links.Add(link);
            _stack.Controls.Add(link);
        }

        if (_showProgress) _stack.Controls.Add(_progress);
        _progress.Visible = _showProgress;
        if (_expandedText.Length > 0)
        {
            _expander.Text = _isExpanded ? _collapseLabel : _expandLabel;
            _expanded.Text = _expandedText;
            _expanded.Visible = _isExpanded;
            _stack.Controls.Add(_expander);
            _stack.Controls.Add(_expanded);
        }

        if (_verification.Text.Length > 0) _stack.Controls.Add(_verification);
        _stack.ResumeLayout(true);

        _footer.Visible = _footer.Text.Length > 0;
        BuildButtons();
        ResizeToContent();
    }

    private void BuildRadios()
    {
        var selected = SelectedRadioIndex;
        _radioHost.Controls.Clear();
        foreach (var old in _radios) old.Dispose();
        _radios.Clear();
        var y = 0;
        for (var i = 0; i < _radioItems.Count; i++)
        {
            var radio = new RadioButton { Text = _radioItems[i], AutoSize = true, Location = new Point(0, y), Checked = i == Math.Max(0, selected), BackColor = Colors.Surface, ForeColor = Colors.Text };
            _radios.Add(radio);
            _radioHost.Controls.Add(radio);
            y += radio.PreferredSize.Height + 4;
        }

        _radioHost.Size = new Size(ContentWidth, y);
    }

    private void BuildButtons()
    {
        _buttons.Controls.Clear();
        AcceptButton = null;
        CancelButton = null;
        var order = new[]
        {
            (EnterpriseTaskDialogButtons.Close, "Close", DialogResult.Cancel), (EnterpriseTaskDialogButtons.Cancel, "Cancel", DialogResult.Cancel),
            (EnterpriseTaskDialogButtons.Retry, "Retry", DialogResult.Retry), (EnterpriseTaskDialogButtons.No, "No", DialogResult.No),
            (EnterpriseTaskDialogButtons.Yes, "Yes", DialogResult.Yes), (EnterpriseTaskDialogButtons.OK, "OK", DialogResult.OK)
        };
        var added = new List<EnterpriseButtonControl>();
        foreach (var (flag, caption, result) in order)
        {
            if (!_commonButtons.HasFlag(flag)) continue;
            var button = new EnterpriseButtonControl { Text = caption, DialogResult = result, Size = new Size(92, 34), Margin = new Padding(8, 0, 0, 0), Tag = flag, Variant = EnterpriseButtonVariant.Outline };
            _buttons.Controls.Add(button);
            added.Add(button);
            if (result == DialogResult.Cancel) CancelButton = button;
        }

        if (added.Count > 0)
        {
            var primary = added[^1];
            primary.Variant = EnterpriseButtonVariant.Primary;
            AcceptButton = primary;
        }

        _buttons.Visible = added.Count > 0;
    }

    private void ResizeToContent()
    {
        var stackHeight = _stack.GetPreferredSize(new Size(ContentWidth + 48, 0)).Height;
        var footer = _footer.Visible ? _footer.Height : 0;
        var buttons = _buttons.Visible ? _buttons.Height : 0;
        ClientSize = new Size(ContentWidth + 48, TitleBar.Height + stackHeight + 8 + footer + buttons);
    }

    private void OnCommandLink(string id)
    {
        var args = new EnterpriseTaskDialogCommandEventArgs(id);
        _model.SelectedCommandId = id;
        CommandLinkClicked?.Invoke(this, args);
        if (args.KeepOpen) return;
        DialogResult = DialogResult.OK;
    }

    private void OnTimerTick()
    {
        var elapsed = DateTime.UtcNow - _started;
        var args = new EnterpriseTaskDialogTickEventArgs(elapsed);
        Tick?.Invoke(this, args);
        if (args.ResetTimer)
        {
            _started = DateTime.UtcNow;
            return;
        }

        if (_autoClose > 0 && elapsed.TotalSeconds >= _autoClose) DialogResult = DialogResult.Cancel;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _started = DateTime.UtcNow;
        _timer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        base.OnFormClosed(e);
    }

    protected override void OnThemeApplied()
    {
        var c = Colors;
        _stack.BackColor = c.Surface;
        _buttons.BackColor = c.Header;
        _footer.BackColor = c.Header;
        _footer.ForeColor = c.Muted;
        _instruction.BackColor = c.Surface;
        _instruction.ForeColor = c.Primary;
        _expanded.BackColor = c.Surface;
        _expanded.ForeColor = c.Muted;
        _verification.BackColor = c.Surface;
        _verification.ForeColor = c.Text;
        _expander.BackColor = c.Surface;
        _expander.LinkColor = c.Primary;
        _radioHost.BackColor = c.Surface;
        foreach (var radio in _radios)
        {
            radio.BackColor = c.Surface;
            radio.ForeColor = c.Text;
        }

        _content.Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _icon.Font.Dispose();
            _instruction.Font.Dispose();
        }

        base.Dispose(disposing);
    }
}
