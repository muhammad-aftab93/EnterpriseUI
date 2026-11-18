using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace EnterpriseUI.WinForms;

public sealed class EnterpriseAboutLinkEventArgs : EventArgs
{
    public EnterpriseAboutLinkEventArgs(string url) => Url = url;

    public string Url { get; }

    /// <summary>Set to true to stop the default action of opening the address in the browser.</summary>
    public bool Handled { get; set; }
}

public sealed class EnterpriseAboutSystemInfoEventArgs : EventArgs
{
    public EnterpriseAboutSystemInfoEventArgs(string text) => Text = text;

    /// <summary>The system information shown; handlers can extend or replace it.</summary>
    public string Text { get; set; }
}

/// <summary>Standard About dialog: logo, product, version, copyright, description, a link and copyable system information.</summary>
[ToolboxItem(false)]
[DefaultEvent("LinkClicked")]
public class EnterpriseAboutBox : EnterpriseForm
{
    private readonly PictureBox _logo = new() { SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(72, 72), Margin = new Padding(0, 0, 16, 0) };
    private readonly Label _product = new() { AutoSize = true, MaximumSize = new Size(480, 0), Margin = new Padding(0) };
    private readonly Label _version = new() { AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
    private readonly Label _copyright = new() { AutoSize = true, MaximumSize = new Size(480, 0), Margin = new Padding(0, 2, 0, 0) };
    private readonly Label _description = new() { AutoSize = true, MaximumSize = new Size(470, 0), Margin = new Padding(0, 12, 0, 0) };
    private readonly LinkLabel _link = new() { AutoSize = true, Margin = new Padding(0, 8, 0, 0), LinkBehavior = LinkBehavior.HoverUnderline };
    private readonly TextBox _systemInfo = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Height = 120, Dock = DockStyle.Fill, Visible = false, BorderStyle = BorderStyle.FixedSingle };
    private readonly EnterpriseButtonControl _infoButton = new() { Text = "System info", Variant = EnterpriseButtonVariant.Outline, Size = new Size(110, 32) };
    private readonly EnterpriseButtonControl _copyButton = new() { Text = "Copy", Variant = EnterpriseButtonVariant.Outline, Size = new Size(80, 32) };
    private readonly EnterpriseButtonControl _okButton = new() { Text = "OK", DialogResult = DialogResult.OK, Size = new Size(90, 32) };
    private string _productName = string.Empty;
    private string _versionText = string.Empty;
    private string _company = string.Empty;
    private string _copyrightText = string.Empty;
    private string _descriptionText = string.Empty;
    private string _linkUrl = string.Empty;
    private bool _showSystemInfo = true;
    private bool _infoVisible;

    public EnterpriseAboutBox()
    {
        Text = "About";
        AllowResize = false;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 330);
        TitleBar.ShowIcon = false;
        _product.Font = new Font(Font.FontFamily, 16f, FontStyle.Bold);

        var head = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 0, 0, 4) };
        var texts = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
        texts.Controls.AddRange(new Control[] { _product, _version, _copyright });
        head.Controls.Add(_logo);
        head.Controls.Add(texts);

        var body = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        body.Controls.AddRange(new Control[] { _description, _link });

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 6, 0, 0) };
        buttons.Controls.Add(_okButton);
        buttons.Controls.Add(_copyButton);
        buttons.Controls.Add(_infoButton);

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 20, 24, 12) };
        content.Controls.Add(_systemInfo);
        content.Controls.Add(body);
        content.Controls.Add(head);
        content.Controls.Add(buttons);
        Controls.Add(content);
        AcceptButton = _okButton;
        CancelButton = _okButton;

        _link.LinkClicked += (_, _) => OpenLink();
        _infoButton.Click += (_, _) => ToggleSystemInfo();
        _copyButton.Click += (_, _) => CopyInfo();
        ApplyTheme();
        Refresh_();
    }

    [DefaultValue("")]
    public new string ProductName
    {
        get => _productName;
        set { _productName = value ?? string.Empty; Refresh_(); }
    }

    [DefaultValue("")]
    public string Version
    {
        get => _versionText;
        set { _versionText = value ?? string.Empty; Refresh_(); }
    }

    [DefaultValue("")]
    public string Company
    {
        get => _company;
        set { _company = value ?? string.Empty; Refresh_(); }
    }

    [DefaultValue("")]
    public string Copyright
    {
        get => _copyrightText;
        set { _copyrightText = value ?? string.Empty; Refresh_(); }
    }

    [DefaultValue("")]
    public string Description
    {
        get => _descriptionText;
        set { _descriptionText = value ?? string.Empty; Refresh_(); }
    }

    [DefaultValue(null)]
    public Image? Logo
    {
        get => _logo.Image;
        set { _logo.Image = value; Refresh_(); }
    }

    /// <summary>Web address shown as a link under the description.</summary>
    [DefaultValue("")]
    public string LinkUrl
    {
        get => _linkUrl;
        set { _linkUrl = value ?? string.Empty; Refresh_(); }
    }

    /// <summary>Whether the System info and Copy buttons are offered.</summary>
    [DefaultValue(true)]
    public bool ShowSystemInfo
    {
        get => _showSystemInfo;
        set { _showSystemInfo = value; Refresh_(); }
    }

    /// <summary>Product text, version and system details as copied by <see cref="CopyInfo"/>.</summary>
    [Browsable(false)]
    public string InfoText
    {
        get
        {
            var text = new StringBuilder();
            text.AppendLine(string.IsNullOrEmpty(_productName) ? "Application" : _productName);
            if (_versionText.Length > 0) text.AppendLine($"Version {_versionText}");
            if (_company.Length > 0) text.AppendLine(_company);
            if (_copyrightText.Length > 0) text.AppendLine(_copyrightText);
            text.AppendLine();
            text.Append(BuildSystemInfo());
            return text.ToString();
        }
    }

    public event EventHandler<EnterpriseAboutLinkEventArgs>? LinkClicked;
    public event EventHandler<EnterpriseAboutSystemInfoEventArgs>? SystemInfoRequested;

    /// <summary>Fills product, version, company, copyright and description from assembly attributes (the entry assembly by default).</summary>
    public void LoadFromAssembly(Assembly? assembly = null)
    {
        assembly ??= Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var name = assembly.GetName();
        ProductName = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product is { Length: > 0 } product ? product : name.Name ?? string.Empty;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        Version = informational is { Length: > 0 } ? informational.Split('+')[0] : name.Version?.ToString() ?? string.Empty;
        Company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty;
        Copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;
        Description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? string.Empty;
        Text = $"About {ProductName}";
    }

    /// <summary>Copies <see cref="InfoText"/> to the clipboard; returns false when the clipboard stays locked.</summary>
    public bool CopyInfo()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(InfoText, true, 3, 50);
                return true;
            }
            catch (ExternalException)
            {
            }
        }

        return false;
    }

    public static DialogResult ShowAbout(IWin32Window? owner = null, Assembly? assembly = null)
    {
        using var box = new EnterpriseAboutBox();
        box.LoadFromAssembly(assembly);
        return owner == null ? box.ShowDialog() : box.ShowDialog(owner);
    }

    private string BuildSystemInfo()
    {
        var text = new StringBuilder();
        text.AppendLine($"Operating system: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        text.AppendLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
        text.AppendLine($"Process: {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}, {Environment.ProcessorCount} logical processors");
        text.AppendLine($"Display: {DeviceDpi} DPI, {Screen.AllScreens.Length} screen(s)");
        var args = new EnterpriseAboutSystemInfoEventArgs(text.ToString().TrimEnd());
        SystemInfoRequested?.Invoke(this, args);
        return args.Text;
    }

    private void ToggleSystemInfo()
    {
        _infoVisible = !_infoVisible;
        if (_infoVisible) _systemInfo.Text = BuildSystemInfo();
        Refresh_();
    }

    private void OpenLink()
    {
        var args = new EnterpriseAboutLinkEventArgs(_linkUrl);
        LinkClicked?.Invoke(this, args);
        if (args.Handled || !Uri.TryCreate(_linkUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return;
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { }
    }

    private void Refresh_()
    {
        _product.Text = _productName;
        _version.Text = _versionText.Length > 0 ? $"Version {_versionText}" : string.Empty;
        _version.Visible = _versionText.Length > 0;
        _copyright.Text = string.Join("  ", new[] { _copyrightText, _company }.Where(s => s.Length > 0));
        _copyright.Visible = _copyright.Text.Length > 0;
        _description.Text = _descriptionText;
        _description.Visible = _descriptionText.Length > 0;
        _link.Text = _linkUrl;
        _link.Visible = _linkUrl.Length > 0;
        _logo.Visible = _logo.Image != null;
        _infoButton.Visible = _showSystemInfo;
        _copyButton.Visible = _showSystemInfo;
        _infoButton.Text = _infoVisible ? "Hide info" : "System info";
        _systemInfo.Visible = _showSystemInfo && _infoVisible;
    }

    protected override void OnThemeApplied()
    {
        var c = Colors;
        _product.ForeColor = c.Text;
        _version.ForeColor = c.Text;
        _copyright.ForeColor = c.Muted;
        _description.ForeColor = c.Text;
        _link.LinkColor = c.Primary;
        _link.ActiveLinkColor = c.Primary;
        _link.BackColor = c.Surface;
        _systemInfo.BackColor = c.Alt;
        _systemInfo.ForeColor = c.Text;
        foreach (var label in new[] { _product, _version, _copyright, _description }) label.BackColor = c.Surface;
    }
}
