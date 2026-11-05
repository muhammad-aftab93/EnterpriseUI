namespace EnterpriseUI.CodeGenerators.Parity;

public sealed record VendorControl(string Vendor, string Category, string Name, string Source, string Granularity = "Control");
public sealed record VendorTheme(string Vendor, string Name, string Family, string Variant, string Source, string Evidence)
{
    public IReadOnlyDictionary<string, string> Characteristics { get; } = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
        new Dictionary<string, string>
        {
            ["VisualCharacteristics"] = "Runtime visual observation required",
            ["ColorPalette"] = "Exact palette not published in audited sources",
            ["Typography"] = "Not measured",
            ["Density"] = "Not measured; inspect regular/compact/touch variants separately",
            ["SpacingRules"] = "Not measured",
            ["ControlChrome"] = "Not measured",
            ["BorderTreatment"] = "Not measured",
            ["SelectionTreatment"] = "Not measured",
            ["HoverTreatment"] = "Not measured",
            ["FocusTreatment"] = "Not measured",
            ["DarkLightVariants"] = "Use the variant field; palette enumeration is still required",
            ["AccessibilityCharacteristics"] = "Per-control high-contrast and assistive-technology testing required",
            ["EnterpriseUISupportStatus"] = "Exact visual equivalent not verified"
        });
}

public static class VendorInventory
{
    public const string AssessmentDate = "2026-11-05";
    public const string TelerikControls = "https://www.telerik.com/products/winforms/documentation/introduction";
    public const string TelerikThemes = "https://www.telerik.com/products/winforms/documentation/styling-and-appearance/using-default-themes";
    public const string DevExpressControls = "https://docs.devexpress.com/WindowsForms/7891/controls-and-libraries";
    public const string DevExpressThemes = "https://docs.devexpress.com/WindowsForms/2399/build-an-application/skins";

    public static IReadOnlyList<VendorControl> Controls { get; } = BuildControls();
    public static IReadOnlyList<VendorTheme> Themes { get; } = BuildThemes();

    private static IReadOnlyList<VendorControl> BuildControls()
    {
        var result = new List<VendorControl>();
        void Add(string vendor, string category, string names, string granularity = "Control")
        {
            foreach (var name in names.Split('|'))
                result.Add(new(vendor, category, name, vendor == "Telerik" ? TelerikControls : DevExpressControls, granularity));
        }
        Add("Telerik", "Data Management", "CardView|CheckedListBox|DataEntry|DataLayout|GridView|ListControl|ListView|PivotGrid|PropertyGrid|TreeView|TreeMap|HeatMap|VirtualGrid|DataFilter|FilterView");
        Add("Telerik", "Tools", "ControlSpy|Form Converter|Element Hierarchy Editor|Shape Editor|Visual Style Builder|Zip library", "Tool or library");
        Add("Telerik", "Media", "ImageEditor|WebCam|PictureBox");
        Add("Telerik", "Document Processing", "PdfProcessing|SpreadProcessing|SpreadStreamProcessing|WordsProcessing", "Library");
        Add("Telerik", "Interactivity", "AIPrompt|Buttons|Carousel|Chat UI|Label|ProgressBar|Rating|Rotator|SmartPasteButton|SpeechToTextButton|StepProgressBar|TrackBar|WaitingBar|VirtualKeyboard|Callout|NotifyIcon");
        Add("Telerik", "Forms and Dialogs", "AboutBox|ColorDialog|DesktopAlert|FileDialogs|Form|FormConverter|MarkupDialog|MessageBox|RadScrollBar|RibbonForm|ShapedForm|StatusStrip|TabbedForm|TaskDialog|ToolbarForm|TitleBar|ToastNotification|TaskbarButton|SplashScreen|Overlay|Flyout");
        Add("Telerik", "Scheduling", "Calendar|GanttView|Scheduler");
        Add("Telerik", "Editors", "AutoCompleteBox|BrowseEditor|ButtonTextBox|CalculatorDropDown|Calculator|CheckedDropDownList|Clock|ColorBox|DateOnlyPicker|DateTimePicker|DomainUpDown|DropDownList|FontDropDownList|MaskedEditBox|MultiColumnComboBox|PopupEditor|SpinEditor|Spreadsheet|TextBox|TimeOnlyPicker|TimePicker|TimeSpanPicker|SyntaxEditor");
        Add("Telerik", "Office", "PDFViewer|RichTextEditor|SpellChecker");
        Add("Telerik", "Navigation", "ApplicationMenu|BreadCrumb|CommandBar|ContextMenu|Menu|PageView|NavigationView|OfficeNavigationBar|Panorama|RibbonBar|SlideView|PipsPager");
        Add("Telerik", "Layout", "CollapsiblePanel|Dock|GroupBox|LayoutControl|Panel|ScrollablePanel|Separator|SplitContainer");
        Add("Telerik", "Framework", "UI Framework", "Framework");
        Add("Telerik", "Data Visualization", "BarcodeView|BindingNavigator|BulletGraph|ChartView|Diagram|LinearGauge|Map|RadialGauge|RangeSelector|Sparkline|Task Board");

        Add("DevExpress", "Grids and Editors", "Data Grid|Tree List|Vertical Grid|Property Grid|Gantt Control|Data Editors and Simple Controls|Code Editor", "Family");
        Add("DevExpress", "Data Visualization", "Charts|Diagrams|Gauges|Map Control|Tree Map Control|Sunburst Control|Sankey Diagram Control|Heatmap Control", "Family");
        Add("DevExpress", "Office-inspired", "Ribbon, Bars and Menu|Rich Text Editor|Scheduler|Spreadsheet|Spell Checker|Application Skins|Office File API", "Family");
        Add("DevExpress", "Reporting and Analytics", "PDF Viewer|Pivot Grid|Printing and Exporting|Reporting", "Family");
        Add("DevExpress", "Navigation and Layout", "Application UI Manager|App Navigation Controls|Docking Library|Form Layout Managers|Forms and User Controls|Messages, Notifications, Dialogs", "Family");
        Add("DevExpress", "Templates", "WinForms UI Templates", "Template family");
        return result;
    }

    private static IReadOnlyList<VendorTheme> BuildThemes()
    {
        var result = new List<VendorTheme>();
        void Add(string vendor, string name, string family, string variant, string evidence, string? source = null) =>
            result.Add(new(vendor, name, family, variant, source ?? (vendor == "Telerik" ? TelerikThemes : DevExpressThemes), evidence));
        foreach (var name in new[] { "ControlDefault", "Office2007Silver", "Office2019Light", "Office2019Gray", "Office2019Dark", "VisualStudio2022", "VisualStudio2022Dark" })
            Add("Telerik", name, "Documented built-in theme", "See name; visual verification required", "Named in the supplied theme documentation");
        Add("Telerik", "Fluent", "Fluent", "Package verification required", "FluentTheme appears in the official API namespace", "https://www.telerik.com/products/winforms/documentation/api/telerik.wincontrols.themes");
        foreach (var name in new[] { "Aqua", "Breeze", "Desert", "HighContrastBlack", "Office2007Black", "Office2007Blue", "Office2010Black", "Office2010Blue", "Office2010Silver", "Office2013Dark", "Office2013Light", "TelerikMetro", "TelerikMetroBlue", "TelerikMetroTouch", "VisualStudio2012Dark", "VisualStudio2012Light", "Windows7", "Windows8", "FluentDark", "Material", "MaterialBlueGray", "MaterialPink", "MaterialTeal" })
            Add("Telerik", name, "Candidate historical theme", "Unverified", "Candidate only: confirm shipping version and theme package before counting");
        foreach (var name in new[] { "WXI", "Basic", "The Bezier", "High Contrast", "Office 2019 Colorful", "Office 2019 Dark Gray", "DevExpress Style", "Pumpkin" })
            Add("DevExpress", name, "Skin", "Palette-dependent", "Named in the official skins documentation");
        foreach (var name in new[] { "Caramel", "Coffee", "Liquid Sky", "Stardust" })
            Add("DevExpress", name, "Legacy or bonus skin", "Runtime verification required", "Named in the official SkinManager documentation", "https://docs.devexpress.com/WindowsForms/DevExpress.Skins.SkinManager");
        Add("DevExpress", "The Bezier / Tokyo", "Vector palette", "Palette-dependent", "Named palette example in official skins documentation");
        foreach (var name in new[] { "Office 2007 Blue", "Office 2007 Black", "Office 2007 Silver", "Office 2010 Blue", "Office 2010 Black", "Office 2010 Silver", "Office 2013", "Office 2013 Dark Gray", "Office 2013 Light Gray", "Office 2016 Colorful", "Office 2016 Dark", "Visual Studio 2013 Blue", "Visual Studio 2013 Dark", "Visual Studio 2013 Light", "Seven", "Seven Classic", "Sharp", "Sharp Plus", "Metropolis", "Metropolis Dark", "DevExpress Dark Style", "Blueprint", "Darkroom", "Foggy", "Glass Oceans", "High Contrast", "Lilian", "McSkin", "Money Twins", "Springtime", "Summer", "Valentine", "Whiteprint", "Xmas 2008" })
            if (!result.Any(theme => theme.Vendor == "DevExpress" && theme.Name == name))
                Add("DevExpress", name, "Candidate historical skin", "Unverified", "Candidate only: enumerate licensed runtime skins and palettes before counting");
        return result;
    }
}