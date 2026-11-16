using System.ComponentModel;
using System.Runtime.InteropServices;
using EnterpriseUI.Theming;

namespace EnterpriseUI.WinForms;

[ToolboxItem(false)]
public sealed class EnterpriseMessageBox : Form
{
    private readonly Label _message = new() { AutoSize = true, MaximumSize = new Size(520, 0), Margin = new Padding(0) };
    private readonly Label _icon = new() { TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 18, FontStyle.Bold), Size = new Size(44, 44), Margin = new Padding(0, 0, 14, 0) };
    private readonly TextBox _details = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill };
    private readonly Panel _detailsHost = new() { Dock = DockStyle.Fill, Visible = false, Height = 130 };
    private readonly Button _detailsButton = new() { Text = "Show details", AutoSize = true };
    private readonly Button _copyButton = new() { Text = "Copy", AutoSize = true };
    private string _messageText = string.Empty;
    private string? _detailsText;
    private MessageBoxIcon _messageIcon;
    private bool _detailsExpanded;

    public EnterpriseMessageBox()
    {
        Text = "Message";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        MinimumSize = new Size(380, 190);
        ClientSize = new Size(520, 220);
        Font = new Font("Segoe UI", 9.5f);
        KeyPreview = true;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.Controls.Add(_icon, 0, 0);
        body.Controls.Add(_message, 1, 0);
        body.SetRowSpan(_icon, 2);
        _detailsHost.Controls.Add(_details);
        body.Controls.Add(_detailsHost, 1, 1);
        root.Controls.Add(body, 0, 0);

        var extras = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        _detailsButton.Click += (_, _) => ToggleDetails();
        _copyButton.Click += (_, _) => CopyMessageToClipboard();
        extras.Controls.Add(_detailsButton);
        extras.Controls.Add(_copyButton);
        root.Controls.Add(extras, 0, 1);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        root.Controls.Add(actions, 0, 2);
        Controls.Add(root);
        ApplyTheme();
        ConfigureIcon();
        ConfigureButtons(MessageBoxButtons.OK);
        UpdateDetailsAvailability();
    }

    public string MessageText
    {
        get => _messageText;
        set { _messageText = value ?? string.Empty; _message.Text = _messageText; }
    }

    public string? Details
    {
        get => _detailsText;
        set
        {
            _detailsText = value;
            _details.Text = value ?? string.Empty;
            UpdateDetailsAvailability();
        }
    }

    public MessageBoxIcon MessageIcon
    {
        get => _messageIcon;
        set { _messageIcon = value; ConfigureIcon(); }
    }
    public MessageBoxButtons Buttons { get; private set; } = MessageBoxButtons.OK;
    public bool DetailsExpanded => _detailsExpanded;
    public string CopyText => string.IsNullOrWhiteSpace(Details) ? MessageText : $"{MessageText}{Environment.NewLine}{Environment.NewLine}{Details}";
    public event EventHandler? DetailsExpandedChanged;

    public static DialogResult ShowMessage(string message, string caption = "Message",
        MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None,
        string? details = null, IWin32Window? owner = null)
    {
        using var dialog = new EnterpriseMessageBox
        {
            MessageText = message,
            Text = caption,
            MessageIcon = icon,
            Details = details
        };
        dialog.ConfigureButtons(buttons);
        return owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
    }

    public void ToggleDetails()
    {
        if (string.IsNullOrWhiteSpace(Details)) return;
        _detailsExpanded = !_detailsExpanded;
        _detailsHost.Visible = _detailsExpanded;
        _detailsButton.Text = _detailsExpanded ? "Hide details" : "Show details";
        ClientSize = new Size(ClientSize.Width, _detailsExpanded ? 360 : 220);
        DetailsExpandedChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ConfigureButtons(MessageBoxButtons buttons)
    {
        Buttons = buttons;
        AcceptButton = null;
        CancelButton = null;
        var actions = Controls.OfType<TableLayoutPanel>().Single().GetControlFromPosition(0, 2) as FlowLayoutPanel;
        if (actions is null) return;
        actions.Controls.Clear();
        foreach (var (caption, result) in ButtonSet(buttons))
        {
            var button = new Button { Text = caption, DialogResult = result, AutoSize = true, MinimumSize = new Size(78, 30) };
            ApplyButtonStyle(button, result == DefaultResult(buttons));
            actions.Controls.Add(button);
            if (result == DefaultResult(buttons)) AcceptButton = button;
            if (result == DialogResult.Cancel) CancelButton = button;
        }
        if (CancelButton is null && buttons is MessageBoxButtons.OK or MessageBoxButtons.OKCancel) CancelButton = AcceptButton as IButtonControl;
    }

    private void ApplyTheme()
    {
        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = EnterpriseThemeEngine.GetColor(theme, DesignTokens.Surface, SystemColors.Window);
        ForeColor = EnterpriseThemeEngine.GetColor(theme, DesignTokens.TextColor, SystemColors.WindowText);
        _message.ForeColor = ForeColor;
        _details.BackColor = EnterpriseThemeEngine.GetColor(theme, DesignTokens.SurfaceAlt, SystemColors.Control);
        _details.ForeColor = ForeColor;
        _icon.BackColor = BackColor;
    }

    private void ConfigureIcon()
    {
        var (glyph, color) = MessageIcon switch
        {
            MessageBoxIcon.Error or MessageBoxIcon.Stop or MessageBoxIcon.Hand => ("!", Color.FromArgb(190, 56, 49)),
            MessageBoxIcon.Warning or MessageBoxIcon.Exclamation => ("!", Color.FromArgb(191, 117, 24)),
            MessageBoxIcon.Information or MessageBoxIcon.Asterisk => ("i", Color.FromArgb(35, 112, 145)),
            MessageBoxIcon.Question => ("?", Color.FromArgb(35, 112, 145)),
            _ => (string.Empty, ForeColor)
        };
        _icon.Text = glyph;
        _icon.ForeColor = color;
        _icon.Visible = glyph.Length > 0;
    }

    private void UpdateDetailsAvailability()
    {
        var available = !string.IsNullOrWhiteSpace(Details);
        _detailsButton.Visible = available;
        if (!available)
        {
            _detailsExpanded = false;
            _detailsHost.Visible = false;
            _detailsButton.Text = "Show details";
        }
    }

    private void CopyMessageToClipboard()
    {
        if (string.IsNullOrEmpty(CopyText)) return;
        try { Clipboard.SetText(CopyText); }
        catch (ExternalException) { }
    }

    private void ApplyButtonStyle(Button button, bool isDefault)
    {
        var primary = EnterpriseThemeEngine.GetColor(ThemeManager.Instance.CurrentTheme, DesignTokens.Primary, Color.FromArgb(39, 93, 115));
        button.BackColor = isDefault ? primary : BackColor;
        button.ForeColor = isDefault && primary.GetBrightness() < 0.55f ? Color.White : ForeColor;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = EnterpriseThemeEngine.GetColor(ThemeManager.Instance.CurrentTheme, DesignTokens.BorderColor, Color.LightGray);
    }

    private static IReadOnlyList<(string Caption, DialogResult Result)> ButtonSet(MessageBoxButtons buttons) => buttons switch
    {
        MessageBoxButtons.OK => new[] { ("OK", DialogResult.OK) },
        MessageBoxButtons.OKCancel => new[] { ("Cancel", DialogResult.Cancel), ("OK", DialogResult.OK) },
        MessageBoxButtons.YesNo => new[] { ("No", DialogResult.No), ("Yes", DialogResult.Yes) },
        MessageBoxButtons.YesNoCancel => new[] { ("Cancel", DialogResult.Cancel), ("No", DialogResult.No), ("Yes", DialogResult.Yes) },
        MessageBoxButtons.RetryCancel => new[] { ("Cancel", DialogResult.Cancel), ("Retry", DialogResult.Retry) },
        MessageBoxButtons.AbortRetryIgnore => new[] { ("Ignore", DialogResult.Ignore), ("Retry", DialogResult.Retry), ("Abort", DialogResult.Abort) },
        _ => throw new ArgumentOutOfRangeException(nameof(buttons))
    };

    private static DialogResult DefaultResult(MessageBoxButtons buttons) => buttons switch
    {
        MessageBoxButtons.YesNo => DialogResult.Yes,
        MessageBoxButtons.YesNoCancel => DialogResult.Yes,
        MessageBoxButtons.RetryCancel => DialogResult.Retry,
        MessageBoxButtons.AbortRetryIgnore => DialogResult.Retry,
        _ => DialogResult.OK
    };
}