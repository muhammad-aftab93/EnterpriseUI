using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using EnterpriseUI.Core.Mvvm;
using EnterpriseUI.Icons;
using EnterpriseUI.Theming;
using EnterpriseUI.WinForms;

namespace EnterpriseUI.WinFormsDemo.Modules;

/// <summary>Dedicated workshop page for the button family: every feature of the button in ten sections, with live logging, runtime theme switching and accessibility read-outs.</summary>
public static class ButtonShowcase
{
    private const int ContentWidth = 940;

    public static GridScenario Create() => new()
    {
        Id = "interactive-button-showcase",
        Title = "Button Showcase - Every Feature of the Button",
        Description = "Ten sections cover the whole button: basic variants, icons and glyphs, SVG vector icons, toggle buttons, disabled and command-driven states, custom styling and painting, runtime theme switching, accessibility, high DPI behaviour and the event model.",
        Features = new[]
        {
            "Variants, corner radius, wrapping and auto size", "Glyph, bitmap, image list and SVG with five arrangements", "Vector SVG tinted by state and theme",
            "Toggle, three-state, exclusive groups and cancelable changes", "ICommand with CanExecute", "Custom colors and owner painting",
            "Runtime theme switching", "Access keys, tooltips, accessible names", "DPI-aware metrics", "Click modes and the full event model"
        },
        Documentation = "EnterpriseButtonControl, EnterpriseToggleButtonControl, EnterpriseSplitButtonControl and EnterpriseDropDownButtonControl share EnterpriseButtonBase. The base paints the face, arranges image and text with one layout routine used by painting and by GetPreferredSize, and raises FacePainting and ContentPainting so applications can replace either part. SvgImage is painted in the text color of the current state, so a button follows the theme, stays sharp at every DPI and dims when disabled.",
        Usage = "var save = new EnterpriseButtonControl { Text = \"&Save\", SvgImage = EnterpriseSvgImage.FromIcon(FluentIconPack.Save), ToolTipText = \"Save the document (Alt+S)\" };\nsave.Command = new RelayCommand(_ => Save(), _ => document.IsDirty);\nvar delete = new EnterpriseButtonControl { Variant = EnterpriseButtonVariant.Danger, DisplayStyle = ButtonDisplayStyle.Image, SvgImage = EnterpriseSvgImage.FromIcon(FluentIconPack.Delete), ToolTipText = \"Delete\" };\nsave.FacePainting += (_, e) => { /* draw e.Bounds yourself, then */ e.Handled = true; };",
        Api = "EnterpriseButtonControl -> Text, Variant, Glyph, Image, SvgImage, ImageList, ImageIndex, ImageKey, ImageSize, DisplayStyle, ContentLayout, TextImageRelation, TextAlignment, ImageAlignment, TextWrap, UseMnemonic, BadgeText, BadgeCount, CornerRadius, FillColor, BorderColor, TextColor, ShowFocusRing, AnimateStates, ClickMode, Command, CommandParameter, ToolTipText, ShowToolTips, AutoSize, AutoSizeMode, DialogResult, VisualState, IsPressed, PerformClick, GetPreferredSize, FacePainting, ContentPainting, ToolTipTextNeeded, VisualStateChanged",
        Architecture = "Pipeline: input handlers -> state (pressed, hot, keyboard focus) -> ValueAnimator tints -> PaintFace (FacePainting) -> ButtonLayoutEngine -> PaintContent (ContentPainting) -> badge -> focus ring. The engine is a pure function of sizes and alignments, so painting and preferred size can never disagree.",
        Performance = "Text is parsed and measured once per text or font change and cached; hover and press are animated with one shared timer pattern; the tooltip and its timer are created on first use. Buttons do not allocate during a normal paint.",
        Accessibility = "Role PushButton, CheckButton or SplitButton; the name is the text without the access key, or the tooltip for image-only buttons; Alt+letter is exposed as keyboard shortcut; the default action really clicks; toggle state is Checked or Mixed. Enter and Space click once per key press (holding does not repeat); Tab shows the focus ring, a mouse click does not.",
        BestPractices = "Give every icon-only button a ToolTipText (it becomes the accessible name). Prefer SvgImage to bitmaps. Bind actions through Command so enabled state follows the model. Use Variant for meaning (Danger) and FillColor only for brand colors.",
        Troubleshooting = "A color override has no effect in Windows high contrast mode on purpose. An SVG that uses several colors is painted in one color; split it into several buttons or use Image. Setting Command takes over the Enabled property until the command is removed.",
        Related = "EnterpriseToggleButtonControl, EnterpriseSplitButtonControl, EnterpriseDropDownButtonControl, EnterpriseSvgImage, ThemeManager",
        Build = Build
    };

    private static Control Build(ScenarioHost host)
    {
        var root = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(16, 4, 16, 16) };
        var disposables = new List<IDisposable>();
        root.Disposed += (_, _) => disposables.ForEach(d => d.Dispose());

        Basic(host, root);
        Icons(host, root, disposables);
        Svg(host, root);
        Toggles(host, root);
        Disabled(host, root);
        Styling(host, root);
        ThemeSupport(host, root);
        Accessibility(host, root);
        HighDpi(host, root, disposables);
        Events(host, root);

        host.Metric("Hover, press, Tab and use Alt+letter; every action is logged in the event list.");
        return root;
    }

    // ---- helpers ----

    private static FlowLayoutPanel Section(FlowLayoutPanel root, string title, string description)
    {
        root.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font("Segoe UI", 11.5f, FontStyle.Bold), Margin = new Padding(0, 16, 0, 0) });
        root.Controls.Add(new Label { Text = description, AutoSize = true, MaximumSize = new Size(ContentWidth, 0), ForeColor = SystemColors.GrayText, Margin = new Padding(0, 2, 0, 4) });
        var panel = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, MaximumSize = new Size(ContentWidth, 0), Margin = new Padding(0, 2, 0, 0) };
        root.Controls.Add(panel);
        return panel;
    }

    private static T Put<T>(FlowLayoutPanel panel, T control, bool breakAfter = false) where T : Control
    {
        control.Margin = new Padding(0, 0, 12, 10);
        panel.Controls.Add(control);
        if (breakAfter) panel.SetFlowBreak(control, true);
        return control;
    }

    private static EnterpriseButtonControl Button(ScenarioHost host, FlowLayoutPanel panel, string text, Action<EnterpriseButtonControl>? configure = null, int width = 130, int height = 36)
    {
        var button = new EnterpriseButtonControl { Text = text, Size = new Size(width, height) };
        configure?.Invoke(button);
        button.Click += (_, _) => host.Log("Button.Click", text.Replace("&", string.Empty));
        return Put(panel, button);
    }

    private static Bitmap MakeBitmap(Color color)
    {
        var bitmap = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new LinearGradientBrush(new Rectangle(0, 0, 16, 16), Color.White, color, 45f);
        g.FillEllipse(brush, 1, 1, 14, 14);
        using var pen = new Pen(color, 1.5f);
        g.DrawEllipse(pen, 1, 1, 13, 13);
        return bitmap;
    }

    // ---- 1. basic ----

    private static void Basic(ScenarioHost host, FlowLayoutPanel root)
    {
        var panel = Section(root, "1. Basic buttons", "Five variants, adjustable corner radius, wrapped text and auto size. All buttons animate hover and press and honor the system animation setting.");
        foreach (var variant in Enum.GetValues<EnterpriseButtonVariant>()) Button(host, panel, variant.ToString(), b => b.Variant = variant);
        panel.SetFlowBreak(panel.Controls[^1], true);
        var radii = new List<EnterpriseButtonControl>();
        foreach (var radius in new[] { 0, 4, 10, 18 }) radii.Add(Button(host, panel, $"Radius {radius}", b => b.CornerRadius = radius));
        Button(host, panel, "A long caption that wraps onto several lines", b => b.TextWrap = true, 150, 64);
        var auto = Button(host, panel, "Auto-sized to content", b => { b.AutoSize = true; b.AutoSizeMode = AutoSizeMode.GrowAndShrink; });
        host.Button("Change auto-size text", () => auto.Text = auto.Text.Length > 12 ? "Short" : "Auto-sized to its content, wider now");
        host.Combo("Corner radius:", new[] { "0", "4", "10", "18", "30" }, "4", v => radii.ForEach(b => b.CornerRadius = int.Parse(v)));
    }

    // ---- 2. icons ----

    private static void Icons(ScenarioHost host, FlowLayoutPanel root, List<IDisposable> disposables)
    {
        var panel = Section(root, "2. Icons and images", "A glyph, a bitmap or an image from an image list, arranged before, after, above or below the text, or laid over it with independent alignment. Image-only buttons use their text as tooltip and accessible name.");
        Button(host, panel, "Glyph", b => b.Glyph = "★");
        Button(host, panel, "Before", b => { b.Glyph = "➤"; b.ContentLayout = ButtonContentLayout.ImageBeforeText; });
        Button(host, panel, "After", b => { b.Glyph = "➤"; b.ContentLayout = ButtonContentLayout.ImageAfterText; });
        Button(host, panel, "Above", b => { b.Glyph = "☁"; b.ContentLayout = ButtonContentLayout.ImageAboveText; }, 90, 64);
        Button(host, panel, "Below", b => { b.Glyph = "☁"; b.ContentLayout = ButtonContentLayout.TextAboveImage; }, 90, 64);
        Button(host, panel, "Overlay", b => { b.Glyph = "⚑"; b.ContentLayout = ButtonContentLayout.Overlay; b.ImageAlignment = ContentAlignment.TopLeft; b.TextAlignment = ContentAlignment.BottomRight; }, 130, 64);
        panel.SetFlowBreak(panel.Controls[^1], true);

        var teal = MakeBitmap(Color.Teal);
        var orange = MakeBitmap(Color.OrangeRed);
        var list = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
        list.Images.Add("teal", teal);
        list.Images.Add("orange", orange);
        disposables.Add(list);
        disposables.Add(teal);
        disposables.Add(orange);
        Button(host, panel, "Bitmap", b => b.Image = teal);
        Button(host, panel, "ImageList key", b => { b.ImageList = list; b.ImageKey = "orange"; }, 140);
        Button(host, panel, "ImageList index", b => { b.ImageList = list; b.ImageIndex = 0; }, 150);
        Button(host, panel, "Search", b => { b.Glyph = "⌕"; b.DisplayStyle = ButtonDisplayStyle.Image; b.ToolTipText = null; }, 40);
        Button(host, panel, "&Inbox", b => { b.Glyph = "✉"; b.BadgeCount = 7; });
        Button(host, panel, "Text only", b => { b.Glyph = "★"; b.DisplayStyle = ButtonDisplayStyle.Text; });
        host.Combo("Content alignment:", new[] { "MiddleCenter", "MiddleLeft", "MiddleRight", "TopLeft", "BottomRight" }, "MiddleCenter", v =>
        {
            var alignment = Enum.Parse<ContentAlignment>(v);
            foreach (var b in panel.Controls.OfType<EnterpriseButtonControl>().Where(b => b.ContentLayout != ButtonContentLayout.Overlay)) b.TextAlignment = alignment;
        });
    }

    // ---- 3. svg ----

    private static void Svg(ScenarioHost host, FlowLayoutPanel root)
    {
        var panel = Section(root, "3. SVG buttons", "Vector icons from the icon registry or from SVG markup. They are painted in the current text color, so they follow the theme, stay sharp at every DPI and dim when the button is disabled.");
        var buttons = new List<EnterpriseButtonControl>();
        var icons = new[] { FluentIconPack.Save, FluentIconPack.Edit, FluentIconPack.Delete, FluentIconPack.Search, FluentIconPack.Refresh, FluentIconPack.Calendar, MaterialIconPack.Settings, MaterialIconPack.Download, MaterialIconPack.Print };
        foreach (var icon in icons)
        {
            var captured = icon;
            buttons.Add(Button(host, panel, char.ToUpperInvariant(captured.Name[0]) + captured.Name[1..], b => { b.SvgImage = EnterpriseSvgImage.FromIcon(captured); b.Variant = EnterpriseButtonVariant.Outline; }));
        }

        panel.SetFlowBreak(panel.Controls[^1], true);
        var clock = EnterpriseSvgImage.FromMarkup("<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><circle cx='12' cy='12' r='9'/><path d='M12 7v5l3 3'/></svg>");
        buttons.Add(Button(host, panel, "Markup clock", b => { b.SvgImage = clock; b.Variant = EnterpriseButtonVariant.Secondary; }, 150));
        foreach (var (icon, tip, variant) in new[] { (FluentIconPack.Add, "Add item", EnterpriseButtonVariant.Primary), (FluentIconPack.Delete, "Delete item", EnterpriseButtonVariant.Danger), (MaterialIconPack.Check, "Approve", EnterpriseButtonVariant.Success), (MaterialIconPack.MoreVert, "More actions", EnterpriseButtonVariant.Outline) })
        {
            var iconButton = Button(host, panel, tip, b => { b.SvgImage = EnterpriseSvgImage.FromIcon(icon); b.DisplayStyle = ButtonDisplayStyle.Image; b.Variant = variant; b.ToolTipText = tip; }, 40);
            buttons.Add(iconButton);
        }

        host.Combo("SVG size:", new[] { "12", "18", "24", "32" }, "18", v => buttons.ForEach(b => b.ImageSize = new Size(int.Parse(v), int.Parse(v))));
        host.Toggle("Enabled SVG buttons", true, v => buttons.ForEach(b => b.Enabled = v));
    }

    // ---- 4. toggles ----

    private static void Toggles(ScenarioHost host, FlowLayoutPanel root)
    {
        var panel = Section(root, "4. Toggle buttons", "Two-state, three-state and exclusive groups. The guarded toggle asks CheckStateChanging for permission: lock it and the change is canceled before it happens.");
        var bold = Put(panel, new EnterpriseToggleButtonControl { Text = "Bold", Glyph = "B", Size = new Size(100, 36) });
        var tri = Put(panel, new EnterpriseToggleButtonControl { Text = "Tri-state", ThreeState = true, Size = new Size(110, 36) });
        bold.CheckedChanged += (_, _) => host.Log("Toggle.Checked", $"Bold = {bold.Checked}");
        tri.CheckStateChanged += (_, _) => host.Log("Toggle.CheckState", tri.CheckState.ToString());
        foreach (var name in new[] { "Left", "Center", "Right" })
        {
            var t = Put(panel, new EnterpriseToggleButtonControl { Text = name, GroupName = "align", Checked = name == "Left", Size = new Size(90, 36) });
            t.CheckedChanged += (_, _) => { if (t.Checked) host.Metric($"Alignment: {name}"); };
        }

        var locked = Put(panel, new EnterpriseToggleButtonControl { Text = "Lock", Glyph = "🔒", Size = new Size(100, 36) });
        var guarded = Put(panel, new EnterpriseToggleButtonControl { Text = "Guarded", Variant = EnterpriseButtonVariant.Success, Size = new Size(110, 36) });
        guarded.CheckStateChanging += (_, e) =>
        {
            e.Cancel = locked.Checked;
            host.Log("Toggle.Changing", $"{e.OldState} -> {e.NewState}{(e.Cancel ? " (canceled)" : string.Empty)}");
        };
        host.Metric("Toggle the groups with the mouse, Space or Enter; lock 'Guarded' to see a canceled change.");
    }

    // ---- 5. disabled and commands ----

    private static void Disabled(ScenarioHost host, FlowLayoutPanel root)
    {
        var panel = Section(root, "5. Disabled states and commands", "Disabled buttons ignore mouse, keyboard and access keys and are dimmed. A button with a Command is enabled exactly while CanExecute allows it, also when the command changes on another thread.");
        var disabled = new List<Control>();
        foreach (var variant in Enum.GetValues<EnterpriseButtonVariant>()) disabled.Add(Button(host, panel, variant.ToString(), b => { b.Variant = variant; b.Glyph = "★"; b.Enabled = false; }));
        panel.SetFlowBreak(panel.Controls[^1], true);

        var allowed = false;
        var runs = 0;
        var command = new RelayCommand(p => host.Log("Command.Execute", $"run {++runs}, parameter = {p}"), _ => allowed);
        var run = Put(panel, new EnterpriseButtonControl { Text = "Run command", Glyph = "▶", Command = command, CommandParameter = "demo", Size = new Size(160, 36) });
        run.Click += (_, _) => host.Log("Command.Click", "Click event runs before the command");
        var async = new AsyncRelayCommand(async ct => { host.Metric("Async command running; the button is disabled meanwhile."); await System.Threading.Tasks.Task.Delay(1500, ct); host.Metric("Async command finished."); });
        Put(panel, new EnterpriseButtonControl { Text = "Async command", Command = async, Variant = EnterpriseButtonVariant.Secondary, Size = new Size(160, 36) });
        host.Toggle("Command allowed", false, v => { allowed = v; command.RaiseCanExecuteChanged(); });
        host.Toggle("Enable disabled row", false, v => disabled.ForEach(c => c.Enabled = v));
    }

    // ---- 6. custom styling ----

    private static void Styling(ScenarioHost host, FlowLayoutPanel root)
    {
        var panel = Section(root, "6. Custom styling and painting", "Color overrides replace the theme colors of one button (empty follows the theme). FacePainting and ContentPainting hand the drawing over to your code while the button keeps handling input, states and accessibility.");
        Button(host, panel, "Brand fill", b => { b.FillColor = Color.FromArgb(124, 58, 237); b.TextColor = Color.White; });
        Button(host, panel, "Brand outline", b => { b.Variant = EnterpriseButtonVariant.Outline; b.FillColor = Color.FromArgb(13, 148, 136); });
        Button(host, panel, "Custom border", b => { b.Variant = EnterpriseButtonVariant.Outline; b.BorderColor = Color.Goldenrod; b.TextColor = Color.Goldenrod; });
        Button(host, panel, "Gradient face", b =>
        {
            b.TextColor = Color.White;
            b.CornerRadius = 10;
            b.FacePainting += (_, e) =>
            {
                var start = e.State == ButtonVisualState.Pressed ? Color.FromArgb(200, 70, 0) : Color.FromArgb(255, 106, 0);
                using var brush = new LinearGradientBrush(e.Bounds, start, Color.FromArgb(238, 9, 121), LinearGradientMode.Horizontal);
                using var path = Chrome.RoundedRect(e.Bounds, b.CornerRadius);
                e.Graphics.FillPath(brush, path);
                e.Handled = true;
            };
        }, 150);
        Button(host, panel, "Custom content", b =>
        {
            b.ContentPainting += (_, e) =>
            {
                using var big = new Font("Segoe UI", 13f, FontStyle.Bold);
                using var small = new Font("Segoe UI", 8f);
                var half = e.Bounds.Width / 2;
                TextRenderer.DrawText(e.Graphics, "4.8", big, new Rectangle(e.Bounds.X, e.Bounds.Y, half, e.Bounds.Height), e.ForeColor, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(e.Graphics, " / 5 stars", small, new Rectangle(e.Bounds.X + half, e.Bounds.Y, half, e.Bounds.Height), e.ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                e.Handled = true;
            };
        }, 160);
        Button(host, panel, "No animation", b => b.AnimateStates = false);
        host.Button("Reset colors", () => panel.Controls.OfType<EnterpriseButtonControl>().ToList().ForEach(b => { b.FillColor = Color.Empty; b.BorderColor = Color.Empty; b.TextColor = Color.Empty; }));
    }

    // ---- 7. themes ----

    private static void ThemeSupport(ScenarioHost host, FlowLayoutPanel root)
    {
        var panel = Section(root, "7. Theme support", "Buttons take every color from the active theme (light, dark, high contrast) and repaint when it changes. Pick a theme below; in Windows high contrast mode the system colors are used and custom colors are ignored.");
        foreach (var variant in Enum.GetValues<EnterpriseButtonVariant>()) Button(host, panel, variant.ToString(), b => { b.Variant = variant; b.SvgImage = EnterpriseSvgImage.FromIcon(MaterialIconPack.Check); });
        panel.SetFlowBreak(panel.Controls[^1], true);
        Put(panel, new EnterpriseToggleButtonControl { Text = "Toggle", Checked = true, Size = new Size(110, 36) });
        var split = Put(panel, new EnterpriseSplitButtonControl { Text = "Split", Size = new Size(140, 36) });
        split.AddOption("First");
        split.AddOption("Second");
        var original = ThemeManager.Instance.CurrentTheme.Name;
        var names = ThemeManager.Instance.AvailableThemes.Select(t => t.Name).ToArray();
        host.Combo("Theme:", names, names.Contains(original) ? original : names[0], name => ThemeManager.Instance.ApplyTheme(name));
        host.Button("Restore theme", () => ThemeManager.Instance.ApplyTheme(original));
    }

    // ---- 8. accessibility ----

    private static void Accessibility(ScenarioHost host, FlowLayoutPanel root)
    {
        var panel = Section(root, "8. Accessibility", "Access keys (Alt+S, Alt+D, Alt+X), tooltips that double as accessible names for icon-only buttons, explicit accessible names and descriptions. Focus a button to see what a screen reader receives.");
        var buttons = new List<EnterpriseButtonControl>();
        void Describe(EnterpriseButtonControl b) =>
            host.Metric($"Name: '{b.AccessibilityObject.Name}' | Role: {b.AccessibilityObject.Role} | Shortcut: {b.AccessibilityObject.KeyboardShortcut ?? "none"} | Action: {b.AccessibilityObject.DefaultAction} | Description: {b.AccessibleDescription ?? "none"}");

        foreach (var (text, glyph, description) in new[] { ("&Save", "✔", "Saves the open document"), ("&Delete", "✖", "Deletes the selected item"), ("E&xport", "⇩", "Exports the data as a file") })
        {
            var b = Button(host, panel, text, x => { x.Glyph = glyph; x.AccessibleDescription = description; x.Variant = text == "&Delete" ? EnterpriseButtonVariant.Danger : EnterpriseButtonVariant.Primary; });
            b.Enter += (_, _) => Describe(b);
            b.MouseEnter += (_, _) => Describe(b);
            buttons.Add(b);
        }

        var iconOnly = Button(host, panel, string.Empty, x => { x.SvgImage = EnterpriseSvgImage.FromIcon(MaterialIconPack.Close); x.ToolTipText = "Close the window"; x.DisplayStyle = ButtonDisplayStyle.Image; x.Variant = EnterpriseButtonVariant.Outline; }, 40);
        iconOnly.Enter += (_, _) => Describe(iconOnly);
        iconOnly.MouseEnter += (_, _) => Describe(iconOnly);
        var named = Button(host, panel, "OK", x => { x.AccessibleName = "Confirm and close the dialog"; x.AccessibleDescription = "Confirms the choice"; x.Variant = EnterpriseButtonVariant.Success; }, 90);
        named.Enter += (_, _) => Describe(named);
        buttons.Add(named);
        host.Toggle("Access keys", true, v => buttons.ForEach(b => b.UseMnemonic = v));
        host.Toggle("Focus ring", true, v => { buttons.ForEach(b => b.ShowFocusRing = v); iconOnly.ShowFocusRing = v; });
        host.Toggle("Tooltips", true, v => { iconOnly.ShowToolTips = v; });
    }

    // ---- 9. high dpi ----

    private static void HighDpi(ScenarioHost host, FlowLayoutPanel root, List<IDisposable> disposables)
    {
        var panel = Section(root, "9. High DPI", "Sizes, padding, corner radius, borders and image boxes are logical pixels multiplied by the monitor scale, and they are recalculated when the window moves to another monitor. SVG icons stay crisp; the 16 px bitmap is resampled and softens as the scale grows.");
        var info = new Label { AutoSize = true, Margin = new Padding(0, 0, 0, 8), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        panel.Controls.Add(info);
        panel.SetFlowBreak(info, true);
        root.HandleCreated += (_, _) => info.Text = $"This window runs at {root.DeviceDpi} DPI (scale {root.DeviceDpi / 96.0:P0}); a 18 px icon box is {(int)Math.Round(18 * root.DeviceDpi / 96.0)} device pixels.";
        var bitmap = MakeBitmap(Color.SteelBlue);
        disposables.Add(bitmap);
        var svg = EnterpriseSvgImage.FromIcon(FluentIconPack.Calendar);
        foreach (var size in new[] { 16, 24, 32, 48 })
        {
            Button(host, panel, $"SVG {size}", b => { b.SvgImage = svg; b.ImageSize = new Size(size, size); b.Variant = EnterpriseButtonVariant.Outline; }, 110 + size, 40 + size / 2);
            Button(host, panel, $"Bitmap {size}", b => { b.Image = bitmap; b.ImageSize = new Size(size, size); b.Variant = EnterpriseButtonVariant.Outline; }, 130 + size, 40 + size / 2);
        }
    }

    // ---- 10. events ----

    private static void Events(ScenarioHost host, FlowLayoutPanel root)
    {
        var panel = Section(root, "10. Event model", "Click, VisualStateChanged, ToolTipTextNeeded and the toggle events are logged. ClickMode decides whether the click is raised on release, on press or when the pointer enters.");
        var clicks = 0;
        var button = Put(panel, new EnterpriseButtonControl { Text = "Watch my events", Size = new Size(190, 40) });
        button.Click += (_, _) => { clicks++; host.Log("Click", $"#{clicks}"); host.Metric($"Clicked {clicks} times"); };
        button.VisualStateChanged += (_, _) => host.Log("VisualStateChanged", button.VisualState.ToString());
        button.ToolTipTextNeeded += (_, e) => { e.ToolTipText = $"Clicked {clicks} times so far (ToolTipTextNeeded)"; host.Log("ToolTipTextNeeded", e.ToolTipText); };
        button.MouseEnter += (_, _) => host.Log("MouseEnter", "pointer entered");
        host.Combo("Click mode:", Enum.GetNames<ButtonClickMode>(), "Release", v => button.ClickMode = Enum.Parse<ButtonClickMode>(v));
        var repeat = Put(panel, new EnterpriseButtonControl { Text = "Hold Space (one click)", Variant = EnterpriseButtonVariant.Secondary, Size = new Size(190, 40) });
        var held = 0;
        repeat.Click += (_, _) => { held++; host.Metric($"Key clicks: {held} - holding Space or Enter does not repeat the click"); };
        host.Button("PerformClick()", button.PerformClick);
    }
}
