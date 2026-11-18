using System.ComponentModel;
using System.Diagnostics;
using EnterpriseUI.Editors.Interactivity;

namespace EnterpriseUI.WinForms;

/// <summary>Message dialog whose body is Markdown or HTML-style markup: bold, italic, code, colors and clickable links.</summary>
[ToolboxItem(false)]
[DefaultEvent("LinkClicked")]
public class EnterpriseMarkupDialog : EnterpriseForm
{
    private const int TextWidth = 440;
    private readonly Label _icon = new() { TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 18, FontStyle.Bold), Size = new Size(44, 44), Location = new Point(24, 20) };
    private readonly EnterpriseLabelControl _label = new() { AutoEllipsis = false, WordWrap = true, MarkupMode = LabelMarkupMode.Markdown, Font = new Font("Segoe UI", 9.5f) };
    private readonly FlowLayoutPanel _buttons = new() { Dock = DockStyle.Bottom, Height = 52, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(16, 8, 16, 8) };
    private readonly Panel _content = new() { Dock = DockStyle.Fill };
    private string _markup = string.Empty;
    private LabelMarkupMode _mode = LabelMarkupMode.Markdown;
    private MessageBoxIcon _messageIcon;
    private MessageBoxButtons _buttonSet = MessageBoxButtons.OK;
    private MessageBoxDefaultButton _defaultButton = MessageBoxDefaultButton.Button1;

    public EnterpriseMarkupDialog()
    {
        Text = "Message";
        AllowResize = false;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        TitleBar.ShowIcon = false;
        _content.Controls.Add(_icon);
        _content.Controls.Add(_label);
        Controls.Add(_content);
        Controls.Add(_buttons);
        _label.LinkClicked += (_, e) => OnLinkClicked(e.LinkText ?? string.Empty);
        ConfigureIcon();
        ConfigureButtons(MessageBoxButtons.OK);
        LayoutDialog();
    }

    [DefaultValue("")]
    public string Markup
    {
        get => _markup;
        set { _markup = value ?? string.Empty; LayoutDialog(); }
    }

    [DefaultValue(LabelMarkupMode.Markdown)]
    public LabelMarkupMode MarkupMode
    {
        get => _mode;
        set { _mode = value; LayoutDialog(); }
    }

    [DefaultValue(MessageBoxIcon.None)]
    public MessageBoxIcon MessageIcon
    {
        get => _messageIcon;
        set { _messageIcon = value; ConfigureIcon(); LayoutDialog(); }
    }

    [DefaultValue(MessageBoxButtons.OK)]
    public MessageBoxButtons Buttons
    {
        get => _buttonSet;
        set => ConfigureButtons(value);
    }

    /// <summary>The button that Enter presses; Button2 makes the safer choice the default for destructive questions.</summary>
    [DefaultValue(MessageBoxDefaultButton.Button1)]
    public MessageBoxDefaultButton DefaultButton
    {
        get => _defaultButton;
        set
        {
            _defaultButton = value;
            ConfigureButtons(_buttonSet);
        }
    }

    /// <summary>The text of the body without any markup.</summary>
    [Browsable(false)]
    public string PlainText => LabelMarkup.ToPlainText(LabelMarkup.Parse(_markup, _mode));

    public event EventHandler<LinkClickedEventArgs>? LinkClicked;
    public event EventHandler<DialogResult>? ButtonClicked;

    public static DialogResult ShowMarkup(string markup, string title = "Message", MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.None, LabelMarkupMode mode = LabelMarkupMode.Markdown, IWin32Window? owner = null, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
    {
        using var dialog = new EnterpriseMarkupDialog { Text = title, MarkupMode = mode, Markup = markup, MessageIcon = icon, Buttons = buttons, DefaultButton = defaultButton };
        return owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
    }

    /// <summary>Raises <see cref="LinkClicked"/>; unhandled http and https addresses open in the browser.</summary>
    protected virtual void OnLinkClicked(string link)
    {
        LinkClicked?.Invoke(this, new LinkClickedEventArgs(link));
        if (LinkClicked != null || !Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return;
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { }
    }

    private void LayoutDialog()
    {
        _label.MarkupMode = _mode;
        _label.Text = _markup;
        var left = HasIcon ? 24 + 44 + 14 : 24;
        var width = TextWidth - (HasIcon ? 0 : 58);
        int height;
        using (var layout = MarkupLayout.Create(LabelMarkup.Parse(_markup, _mode), _label.Font, ControlColors.FromTheme(EnterpriseUI.Theming.ThemeManager.Instance.CurrentTheme), width, true))
            height = Math.Max(layout.Size.Height, 20) + 4;
        _label.SetBounds(left, 22, width, height);
        var body = Math.Max(height + 22, 44 + 20);
        ClientSize = new Size(left + width + 24, TitleBar.Height + body + 22 + _buttons.Height);
    }

    private bool HasIcon => _messageIcon is MessageBoxIcon.Error or MessageBoxIcon.Warning or MessageBoxIcon.Information or MessageBoxIcon.Question;

    private void ConfigureIcon()
    {
        var (glyph, color) = _messageIcon switch
        {
            MessageBoxIcon.Error => ("!", Colors.Danger),
            MessageBoxIcon.Warning => ("!", Colors.Warning),
            MessageBoxIcon.Information => ("i", Colors.Primary),
            MessageBoxIcon.Question => ("?", Colors.Primary),
            _ => (string.Empty, Colors.Text)
        };
        _icon.Text = glyph;
        _icon.ForeColor = color;
        _icon.Visible = glyph.Length > 0;
    }

    private void ConfigureButtons(MessageBoxButtons buttons)
    {
        _buttonSet = buttons;
        _buttons.Controls.Clear();
        AcceptButton = null;
        CancelButton = null;
        var set = buttons switch
        {
            MessageBoxButtons.OKCancel => new[] { ("Cancel", DialogResult.Cancel), ("OK", DialogResult.OK) },
            MessageBoxButtons.YesNo => new[] { ("No", DialogResult.No), ("Yes", DialogResult.Yes) },
            MessageBoxButtons.YesNoCancel => new[] { ("Cancel", DialogResult.Cancel), ("No", DialogResult.No), ("Yes", DialogResult.Yes) },
            MessageBoxButtons.RetryCancel => new[] { ("Cancel", DialogResult.Cancel), ("Retry", DialogResult.Retry) },
            MessageBoxButtons.AbortRetryIgnore => new[] { ("Ignore", DialogResult.Ignore), ("Retry", DialogResult.Retry), ("Abort", DialogResult.Abort) },
            _ => new[] { ("OK", DialogResult.OK) }
        };

        var offset = _defaultButton switch { MessageBoxDefaultButton.Button2 => 1, MessageBoxDefaultButton.Button3 => 2, _ => 0 };
        var defaultIndex = Math.Clamp(set.Length - 1 - offset, 0, set.Length - 1);
        for (var i = 0; i < set.Length; i++)
        {
            var (caption, result) = set[i];
            var isDefault = i == defaultIndex;
            var button = new EnterpriseButtonControl
            {
                Text = caption,
                DialogResult = result,
                Size = new Size(92, 34),
                Margin = new Padding(8, 0, 0, 0),
                Variant = isDefault ? EnterpriseButtonVariant.Primary : EnterpriseButtonVariant.Outline
            };
            button.Click += (_, _) => ButtonClicked?.Invoke(this, result);
            _buttons.Controls.Add(button);
            if (isDefault) AcceptButton = button;
            if (result == DialogResult.Cancel || buttons is MessageBoxButtons.OK || (buttons == MessageBoxButtons.YesNo && result == DialogResult.No)) CancelButton = button;
        }
    }

    protected override void OnThemeApplied()
    {
        var c = Colors;
        _content.BackColor = c.Surface;
        _buttons.BackColor = c.Header;
        _icon.BackColor = c.Surface;
        _label.Invalidate();
        ConfigureIcon();
        if (_label.Text.Length > 0) LayoutDialog();
    }
}
