using System.ComponentModel;
using System.ComponentModel.Design;
using System.Windows.Forms.Design;
using EnterpriseUI.Navigation;

namespace EnterpriseUI.WinForms.Design;

/// <summary>Base of the smart tag panels: reads and writes component properties through <see cref="TypeDescriptor"/> so every change is undoable in the designer.</summary>
public abstract class EnterpriseActionList : DesignerActionList
{
    protected EnterpriseActionList(IComponent component) : base(component)
    {
    }

    protected T Get<T>(string name) => (T)TypeDescriptor.GetProperties(Component!)[name]!.GetValue(Component)!;

    protected void Set(string name, object? value) => TypeDescriptor.GetProperties(Component!)[name]!.SetValue(Component, value);

    protected static DesignerActionPropertyItem Item(string property, string display, string category, string description) =>
        new(property, display, category, description);
}

public sealed class NavigationPaneActions : EnterpriseActionList
{
    public NavigationPaneActions(IComponent component) : base(component) { }

    public EnterpriseNavigationDisplayMode DisplayMode { get => Get<EnterpriseNavigationDisplayMode>(nameof(DisplayMode)); set => Set(nameof(DisplayMode), value); }
    public string Title { get => Get<string>(nameof(Title)); set => Set(nameof(Title), value); }
    public int ItemHeight { get => Get<int>(nameof(ItemHeight)); set => Set(nameof(ItemHeight), value); }
    public bool ShowToggleButton { get => Get<bool>(nameof(ShowToggleButton)); set => Set(nameof(ShowToggleButton), value); }

    public override DesignerActionItemCollection GetSortedActionItems() => new()
    {
        Item(nameof(DisplayMode), "Display mode", "Behavior", "Expanded, compact icon rail, or automatic by parent width."),
        Item(nameof(Title), "Title", "Appearance", "Caption shown next to the toggle button."),
        Item(nameof(ItemHeight), "Item height", "Appearance", "Height of one item in logical pixels."),
        Item(nameof(ShowToggleButton), "Show toggle button", "Behavior", "Lets users collapse the pane to an icon rail.")
    };
}

public sealed class SurfacePanelActions : EnterpriseActionList
{
    public SurfacePanelActions(IComponent component) : base(component) { }

    public EnterpriseSurfaceStyle SurfaceStyle { get => Get<EnterpriseSurfaceStyle>(nameof(SurfaceStyle)); set => Set(nameof(SurfaceStyle), value); }
    public int CornerRadius { get => Get<int>(nameof(CornerRadius)); set => Set(nameof(CornerRadius), value); }

    public override DesignerActionItemCollection GetSortedActionItems() => new()
    {
        Item(nameof(SurfaceStyle), "Surface style", "Appearance", "Flat, outlined, filled or raised."),
        Item(nameof(CornerRadius), "Corner radius", "Appearance", "Radius of the rounded corners in logical pixels.")
    };
}

public sealed class GroupPanelActions : EnterpriseActionList
{
    public GroupPanelActions(IComponent component) : base(component) { }

    public string Text { get => Get<string>(nameof(Text)); set => Set(nameof(Text), value); }
    public EnterpriseGroupStyle GroupStyle { get => Get<EnterpriseGroupStyle>(nameof(GroupStyle)); set => Set(nameof(GroupStyle), value); }
    public HorizontalAlignment CaptionAlignment { get => Get<HorizontalAlignment>(nameof(CaptionAlignment)); set => Set(nameof(CaptionAlignment), value); }

    public override DesignerActionItemCollection GetSortedActionItems() => new()
    {
        Item(nameof(Text), "Caption", "Appearance", "Text of the group header."),
        Item(nameof(GroupStyle), "Group style", "Appearance", "Outlined, header band or underlined."),
        Item(nameof(CaptionAlignment), "Caption alignment", "Appearance", "Left, center or right.")
    };
}

public sealed class DividerActions : EnterpriseActionList
{
    public DividerActions(IComponent component) : base(component) { }

    public Orientation Orientation { get => Get<Orientation>(nameof(Orientation)); set => Set(nameof(Orientation), value); }
    public EnterpriseDividerStyle LineStyle { get => Get<EnterpriseDividerStyle>(nameof(LineStyle)); set => Set(nameof(LineStyle), value); }
    public string Text { get => Get<string>(nameof(Text)); set => Set(nameof(Text), value); }

    public override DesignerActionItemCollection GetSortedActionItems() => new()
    {
        Item(nameof(Orientation), "Orientation", "Appearance", "Horizontal or vertical rule."),
        Item(nameof(LineStyle), "Line style", "Appearance", "Solid, dashed or fading rule."),
        Item(nameof(Text), "Caption", "Appearance", "Optional text centered in the rule.")
    };
}

public sealed class PageViewActions : EnterpriseActionList
{
    private readonly EnterprisePageViewControl _view;

    public PageViewActions(IComponent component) : base(component) => _view = (EnterprisePageViewControl)component;

    public PageViewMode ViewMode { get => Get<PageViewMode>(nameof(ViewMode)); set => Set(nameof(ViewMode), value); }

    private void AddPage()
    {
        var host = (IDesignerHost?)GetService(typeof(IDesignerHost));
        if (host is null) { _view.AddPage($"Page {_view.Pages.Count + 1}"); return; }
        using var transaction = host.CreateTransaction("Add page");
        var page = (EnterprisePageViewPage)host.CreateComponent(typeof(EnterprisePageViewPage));
        page.Title = $"Page {_view.Pages.Count + 1}";
        _view.AddPage(page);
        transaction.Commit();
    }

    public override DesignerActionItemCollection GetSortedActionItems() => new()
    {
        Item(nameof(ViewMode), "View mode", "Appearance", "Tab strip, stack, Outlook bar, accordion, backstage or navigation pane."),
        new DesignerActionMethodItem(this, nameof(AddPage), "Add page", "Pages", "Adds a new page.", true)
    };
}

public sealed class SparklineActions : EnterpriseActionList
{
    public SparklineActions(IComponent component) : base(component) { }

    public EnterpriseUI.Charts.EnterpriseSparkline.SparklineKind Kind { get => Get<EnterpriseUI.Charts.EnterpriseSparkline.SparklineKind>(nameof(Kind)); set => Set(nameof(Kind), value); }
    public bool ShowHighLowIndicators { get => Get<bool>(nameof(ShowHighLowIndicators)); set => Set(nameof(ShowHighLowIndicators), value); }
    public bool ShowFirstLastIndicators { get => Get<bool>(nameof(ShowFirstLastIndicators)); set => Set(nameof(ShowFirstLastIndicators), value); }

    public override DesignerActionItemCollection GetSortedActionItems() => new()
    {
        Item(nameof(Kind), "Kind", "Appearance", "Line, area, column or win/loss."),
        Item(nameof(ShowHighLowIndicators), "Mark high and low", "Appearance", "Marks the highest and lowest points."),
        Item(nameof(ShowFirstLastIndicators), "Mark first and last", "Appearance", "Marks the first and last points.")
    };
}

/// <summary>Smart tag of the button family: the properties most often set at design time.</summary>
public sealed class ButtonActions : EnterpriseActionList
{
    public ButtonActions(IComponent component) : base(component) { }

    public string Text { get => Get<string>(nameof(Text)); set => Set(nameof(Text), value); }
    public EnterpriseButtonVariant Variant { get => Get<EnterpriseButtonVariant>(nameof(Variant)); set => Set(nameof(Variant), value); }
    public string? Glyph { get => Get<string?>(nameof(Glyph)); set => Set(nameof(Glyph), value); }
    public ButtonDisplayStyle DisplayStyle { get => Get<ButtonDisplayStyle>(nameof(DisplayStyle)); set => Set(nameof(DisplayStyle), value); }
    public ButtonContentLayout ContentLayout { get => Get<ButtonContentLayout>(nameof(ContentLayout)); set => Set(nameof(ContentLayout), value); }
    public int CornerRadius { get => Get<int>(nameof(CornerRadius)); set => Set(nameof(CornerRadius), value); }
    public ButtonClickMode ClickMode { get => Get<ButtonClickMode>(nameof(ClickMode)); set => Set(nameof(ClickMode), value); }
    public string? ToolTipText { get => Get<string?>(nameof(ToolTipText)); set => Set(nameof(ToolTipText), value); }
    public bool AutoSize { get => Get<bool>(nameof(AutoSize)); set => Set(nameof(AutoSize), value); }

    public override DesignerActionItemCollection GetSortedActionItems() => new()
    {
        Item(nameof(Text), "Text", "Appearance", "Caption of the button; put & before the access key letter."),
        Item(nameof(Variant), "Variant", "Appearance", "Primary, secondary, success, danger or outline."),
        Item(nameof(Glyph), "Glyph", "Appearance", "A symbol character drawn with the text."),
        Item(nameof(DisplayStyle), "Display style", "Appearance", "Image and text, text only, image only or nothing."),
        Item(nameof(ContentLayout), "Content layout", "Appearance", "Where the image sits relative to the text."),
        Item(nameof(CornerRadius), "Corner radius", "Appearance", "Radius of the rounded corners in logical pixels."),
        Item(nameof(ClickMode), "Click mode", "Behavior", "Click on release, on press or on hover."),
        Item(nameof(ToolTipText), "Tooltip", "Behavior", "Text shown when the pointer rests on the button."),
        Item(nameof(AutoSize), "Auto size", "Layout", "Sizes the button to its content.")
    };
}

public sealed class CustomizableLayoutActions : EnterpriseActionList
{
    public CustomizableLayoutActions(IComponent component) : base(component) { }

    public int ColumnCount { get => Get<int>(nameof(ColumnCount)); set => Set(nameof(ColumnCount), value); }
    public int RowHeight { get => Get<int>(nameof(RowHeight)); set => Set(nameof(RowHeight), value); }
    public bool IsCustomizing { get => Get<bool>(nameof(IsCustomizing)); set => Set(nameof(IsCustomizing), value); }

    public override DesignerActionItemCollection GetSortedActionItems() => new()
    {
        Item(nameof(ColumnCount), "Columns", "Layout", "Number of grid columns."),
        Item(nameof(RowHeight), "Row height", "Layout", "Height of one grid row in logical pixels."),
        Item(nameof(IsCustomizing), "Customization mode", "Behavior", "Shows the frames used to rearrange items.")
    };
}

/// <summary>Designer for single controls that adds a smart tag panel.</summary>
public sealed class EnterpriseControlDesigner<TActions> : ControlDesigner where TActions : EnterpriseActionList
{
    private DesignerActionListCollection? _lists;

    public override DesignerActionListCollection ActionLists =>
        _lists ??= new DesignerActionListCollection { (DesignerActionList)Activator.CreateInstance(typeof(TActions), Component)! };
}

/// <summary>Designer for container controls that adds a smart tag panel and keeps drag-and-drop of children.</summary>
public sealed class EnterpriseContainerDesigner<TActions> : ParentControlDesigner where TActions : EnterpriseActionList
{
    private DesignerActionListCollection? _lists;

    public override DesignerActionListCollection ActionLists =>
        _lists ??= new DesignerActionListCollection { (DesignerActionList)Activator.CreateInstance(typeof(TActions), Component)! };
}
