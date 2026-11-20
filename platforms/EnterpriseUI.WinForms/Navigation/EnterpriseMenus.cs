using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Navigation;
using EnterpriseUI.Theming;

namespace EnterpriseUI.WinForms;

/// <summary>Builds menu hierarchies from slash separated paths ("File/Export/PDF").</summary>
public static class EnterpriseMenuBuilder
{
    /// <summary>Adds an item at <paramref name="path"/>, creating missing parent items; "&amp;" marks the mnemonic of a segment.</summary>
    public static ToolStripMenuItem Add(ToolStripItemCollection root, string path, Action? onClick = null, Keys shortcut = Keys.None, bool isChecked = false)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A menu path is required.", nameof(path));
        var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var items = root;
        ToolStripMenuItem? current = null;
        for (var i = 0; i < segments.Length; i++)
        {
            var text = segments[i].Trim();
            var last = i == segments.Length - 1;
            var existing = last ? null : items.OfType<ToolStripMenuItem>().FirstOrDefault(m => Normalize(m.Text) == Normalize(text));
            if (existing is null)
            {
                existing = new ToolStripMenuItem(text) { Name = "menu_" + Normalize(string.Join("_", segments.Take(i + 1))) };
                items.Add(existing);
            }

            current = existing;
            items = current.DropDownItems;
        }

        var item = current!;
        if (onClick is not null) item.Click += (_, _) => onClick();
        if (shortcut != Keys.None) item.ShortcutKeys = shortcut;
        if (isChecked) item.Checked = true;
        return item;
    }

    public static ToolStripSeparator AddSeparator(ToolStripItemCollection root, string? parentPath = null)
    {
        var items = parentPath is null ? root : Find(root, parentPath)?.DropDownItems ?? root;
        var separator = new ToolStripSeparator();
        items.Add(separator);
        return separator;
    }

    public static ToolStripMenuItem? Find(ToolStripItemCollection root, string path)
    {
        var items = root;
        ToolStripMenuItem? found = null;
        foreach (var segment in path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            found = items.OfType<ToolStripMenuItem>().FirstOrDefault(m => Normalize(m.Text) == Normalize(segment));
            if (found is null) return null;
            items = found.DropDownItems;
        }

        return found;
    }

    private static string Normalize(string? text) => (text ?? string.Empty).Replace("&", string.Empty).Trim().ToUpperInvariant();
}

/// <summary>Themed menu bar with path based item creation.</summary>
[ToolboxItem(true)]
public class EnterpriseMenuBar : MenuStrip, IThemeableControl
{
    private readonly EventHandler<ITheme> _themeHandler;

    public EnterpriseMenuBar()
    {
        GripStyle = ToolStripGripStyle.Hidden;
        Padding = new Padding(4, 2, 4, 2);
        Font = new Font("Segoe UI", 10f);
        ShowItemToolTips = true;
        _themeHandler = (_, theme) => OnThemeChanged(theme);
        ThemeManager.Instance.ThemeChanged += _themeHandler;
        EnterpriseMenuRenderer.Apply(this);
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? ThemeOverride { get; set; }

    public ToolStripMenuItem AddItem(string path, Action? onClick = null, Keys shortcut = Keys.None, bool isChecked = false) =>
        EnterpriseMenuBuilder.Add(Items, path, onClick, shortcut, isChecked);

    public ToolStripMenuItem? FindItem(string path) => EnterpriseMenuBuilder.Find(Items, path);

    public ToolStripSeparator AddSeparator(string? parentPath = null) => EnterpriseMenuBuilder.AddSeparator(Items, parentPath);

    public void OnThemeChanged(ITheme newTheme)
    {
        if (IsDisposed) return;
        if (IsHandleCreated && InvokeRequired)
        {
            try { BeginInvoke(new Action(() => OnThemeChanged(newTheme))); } catch (InvalidOperationException) { }
            return;
        }

        EnterpriseMenuRenderer.Apply(this);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ThemeManager.Instance.ThemeChanged -= _themeHandler;
        base.Dispose(disposing);
    }
}

/// <summary>Themed context menu with path based item creation; attach it to any control.</summary>
[ToolboxItem(true)]
public class EnterpriseContextMenu : ContextMenuStrip, IThemeableControl
{
    private readonly EventHandler<ITheme> _themeHandler;

    public EnterpriseContextMenu()
    {
        Font = new Font("Segoe UI", 10f);
        ShowImageMargin = true;
        _themeHandler = (_, theme) => OnThemeChanged(theme);
        ThemeManager.Instance.ThemeChanged += _themeHandler;
        EnterpriseMenuRenderer.Apply(this);
    }

    public EnterpriseContextMenu(IContainer container) : this()
    {
        ArgumentNullException.ThrowIfNull(container);
        container.Add(this);
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? ThemeOverride { get; set; }

    public ToolStripMenuItem AddItem(string path, Action? onClick = null, Keys shortcut = Keys.None, bool isChecked = false) =>
        EnterpriseMenuBuilder.Add(Items, path, onClick, shortcut, isChecked);

    public ToolStripMenuItem? FindItem(string path) => EnterpriseMenuBuilder.Find(Items, path);

    public ToolStripSeparator AddSeparator(string? parentPath = null) => EnterpriseMenuBuilder.AddSeparator(Items, parentPath);

    /// <summary>Shows this menu for <paramref name="control"/> on right click and the menu key.</summary>
    public void AttachTo(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.ContextMenuStrip = this;
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        if (IsDisposed) return;
        EnterpriseMenuRenderer.Apply(this);
    }

    protected override void OnOpening(CancelEventArgs e)
    {
        EnterpriseMenuRenderer.Apply(this);
        base.OnOpening(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ThemeManager.Instance.ThemeChanged -= _themeHandler;
        base.Dispose(disposing);
    }
}

/// <summary>Command bar button whose glyph image follows the theme color.</summary>
public sealed class EnterpriseCommandBarButton : ToolStripButton
{
    public EnterpriseCommandBarButton(string text, string? glyph, Action? onClick, string? toolTip = null)
    {
        Text = text;
        Glyph = glyph;
        ToolTipText = toolTip ?? text;
        DisplayStyle = string.IsNullOrEmpty(glyph) ? ToolStripItemDisplayStyle.Text : ToolStripItemDisplayStyle.ImageAndText;
        TextImageRelation = TextImageRelation.ImageBeforeText;
        ImageScaling = ToolStripItemImageScaling.None;
        if (onClick is not null) Click += (_, _) => onClick();
    }

    public string? Glyph { get; }

    public bool ShowText { get; set; } = true;

    internal void RefreshImage(Color color, int size)
    {
        Image?.Dispose();
        Image = string.IsNullOrEmpty(Glyph) ? null : CommandGlyph.Create(Glyph!, color, size);
        DisplayStyle = Image is null ? ToolStripItemDisplayStyle.Text : ShowText ? ToolStripItemDisplayStyle.ImageAndText : ToolStripItemDisplayStyle.Image;
    }
}

internal static class CommandGlyph
{
    public static Bitmap Create(string glyph, Color color, int size)
    {
        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var font = new Font("Segoe UI Symbol", size * 0.62f, GraphicsUnit.Pixel);
        TextRenderer.DrawText(g, glyph, font, new Rectangle(0, 0, size, size), color,
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        return bitmap;
    }
}

public sealed class EnterpriseCommandEventArgs : EventArgs
{
    public EnterpriseCommandEventArgs(string row, ToolStripItem item)
    {
        Row = row;
        Item = item;
    }

    public string Row { get; }
    public ToolStripItem Item { get; }
}

/// <summary>Stack of themed command rows (tool strips) with overflow, tooltips, keyboard navigation and end-user customization of the visible commands.</summary>
[DefaultEvent(nameof(CommandInvoked))]
[ToolboxItem(true)]
public class EnterpriseCommandBarControl : EnterpriseContainerBase
{
    private readonly List<(string Name, ToolStrip Strip)> _rows = new();
    private bool _allowCustomize = true;

    public EnterpriseCommandBarControl() : this(new EnterpriseCommandBar())
    {
    }

    public EnterpriseCommandBarControl(EnterpriseCommandBar model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Dock = DockStyle.Top;
        Height = 34;
        AccessibleRole = AccessibleRole.ToolBar;
        AccessibleName = "Command bar";
        LoadModel();
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EnterpriseCommandBar Model { get; }

    /// <summary>Lets users show or hide commands from the right-click menu.</summary>
    [DefaultValue(true)]
    public bool AllowCustomize { get => _allowCustomize; set { _allowCustomize = value; Model.AllowCustomize = value; } }

    [Browsable(false)]
    public IReadOnlyList<ToolStrip> Rows => _rows.Select(r => r.Strip).ToList();

    public event EventHandler<EnterpriseCommandEventArgs>? CommandInvoked;

    /// <summary>Rebuilds the rows from <see cref="Model"/>.</summary>
    public void LoadModel()
    {
        foreach (var (_, strip) in _rows) { Controls.Remove(strip); strip.Dispose(); }
        _rows.Clear();
        foreach (var row in Model.Rows)
        {
            var strip = AddRow(row.Name);
            foreach (var item in row.Items) AddCommand(strip, item.Text, item.Icon, item.Command is null ? null : () => item.Command.Execute(null), item.ToolTip, item.IsEnabled);
        }

        ApplyRows();
    }

    public ToolStrip AddRow(string name)
    {
        var strip = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            CanOverflow = true,
            ShowItemToolTips = true,
            Padding = new Padding(4, 2, 4, 2),
            Font = Font,
            AutoSize = true,
            Name = name
        };
        _rows.Add((name, strip));
        Controls.Add(strip);
        strip.BringToFront();
        strip.MouseUp += (_, e) => { if (e.Button == MouseButtons.Right && _allowCustomize) ShowCustomizeMenu(strip, e.Location); };
        ApplyRows();
        return strip;
    }

    public EnterpriseCommandBarButton AddCommand(ToolStrip row, string text, string? glyph, Action? onClick, string? toolTip = null, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(row);
        var rowName = _rows.FirstOrDefault(r => r.Strip == row).Name ?? string.Empty;
        EnterpriseCommandBarButton? button = null;
        button = new EnterpriseCommandBarButton(text, glyph, () =>
        {
            onClick?.Invoke();
            CommandInvoked?.Invoke(this, new EnterpriseCommandEventArgs(rowName, button!));
        }, toolTip) { Enabled = enabled };
        row.Items.Add(button);
        ApplyRows();
        return button;
    }

    public EnterpriseCommandBarButton AddCommand(string text, string? glyph, Action? onClick, string? toolTip = null)
    {
        var row = _rows.Count > 0 ? _rows[0].Strip : AddRow("Main");
        return AddCommand(row, text, glyph, onClick, toolTip);
    }

    public void AddSeparator(ToolStrip? row = null)
    {
        row ??= _rows.Count > 0 ? _rows[0].Strip : AddRow("Main");
        row.Items.Add(new ToolStripSeparator());
    }

    public ToolStripItem? FindCommand(string text) =>
        _rows.SelectMany(r => r.Strip.Items.Cast<ToolStripItem>()).FirstOrDefault(i => string.Equals(i.Text, text, StringComparison.OrdinalIgnoreCase));

    protected override void OnThemeApplied()
    {
        base.OnThemeApplied();
        ApplyRows();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        foreach (var (_, strip) in _rows) strip.Font = Font;
        ApplyRows();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ApplyRows();
    }

    private void ApplyRows()
    {
        var c = Colors;
        var iconSize = Math.Max(16, S(16));
        var height = 0;
        foreach (var (_, strip) in _rows)
        {
            EnterpriseMenuRenderer.Apply(strip);
            strip.BackColor = c.Header;
            foreach (var button in strip.Items.OfType<EnterpriseCommandBarButton>()) button.RefreshImage(button.Enabled ? c.Primary : c.Disabled, iconSize);
            height += strip.GetPreferredSize(new Size(Math.Max(1, Width), 0)).Height;
        }

        if (_rows.Count > 0 && Dock is DockStyle.Top or DockStyle.Bottom) Height = Math.Max(S(28), height);
    }

    private void ShowCustomizeMenu(ToolStrip strip, Point location)
    {
        var menu = new ContextMenuStrip();
        EnterpriseMenuRenderer.Apply(menu);
        foreach (var item in strip.Items.OfType<ToolStripItem>().Where(i => i is not ToolStripSeparator && !string.IsNullOrEmpty(i.Text)).ToList())
        {
            var entry = new ToolStripMenuItem(item.Text) { Checked = item.Visible, CheckOnClick = false };
            entry.Click += (_, _) => { item.Visible = !item.Visible; ApplyRows(); };
            menu.Items.Add(entry);
        }

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Reset", null, (_, _) =>
        {
            foreach (ToolStripItem item in strip.Items) item.Visible = true;
            ApplyRows();
        }));
        menu.Closed += (_, _) => BeginInvoke(new Action(menu.Dispose));
        menu.Show(strip, location);
    }
}
