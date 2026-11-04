using EnterpriseUI.Data;
using EnterpriseUI.Icons;
using Xunit;

namespace EnterpriseUI.Tests.Unit;

public class IconAndVirtualizationTests
{
    [Fact]
    public void IconRegistry_LookupsBuiltInIcons()
    {
        var save = IconRegistry.GetIcon("fluent:save");
        Assert.NotNull(save);
        Assert.Equal("save", save.Name);
        Assert.Contains("<svg", save.RenderMarkup());

        var check = IconRegistry.GetIcon("material:check");
        Assert.NotNull(check);
        Assert.Equal("check", check.Name);
    }

    [Fact]
    public void VirtualizationManager_CalculatesCorrectRowRanges()
    {
        var virtualizer = new VirtualizationManager
        {
            ItemHeight = 40.0,
            BufferItems = 2
        };

        // Scroll offset = 200, Viewport = 400 (shows rows 5 to 14)
        // With buffer = 2 -> range 3 to 17
        var range = virtualizer.CalculateVisibleRows(200.0, 400.0, 1000);

        Assert.Equal(3, range.StartIndex);
        Assert.Equal(17, range.EndIndex);
        Assert.Equal(15, range.Count);
    }
}

