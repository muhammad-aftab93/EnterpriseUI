using System.Diagnostics;
using EnterpriseUI.Grids;
using EnterpriseUI.Tests.Support;
using EnterpriseUI.WinForms;
using Xunit;
using Xunit.Abstractions;
using static EnterpriseUI.Tests.Support.WinFormsTestSupport;

namespace EnterpriseUI.Tests.Performance;

/// <summary>Regression guards for the data controls: large inputs must stay virtualized, thresholds are several times the measured time.</summary>
[Trait("Category", "Performance")]
[Collection("Performance")]
public class DataControlPerformanceTests
{
    private sealed record Item(int Id, string Name, string Group, decimal Value);

    private readonly ITestOutputHelper _output;

    public DataControlPerformanceTests(ITestOutputHelper output) => _output = output;

    private static List<Item> Items(int count) => Enumerable.Range(0, count).Select(i => new Item(i, $"Item {i:000000}", $"Group {i % 40}", i % 1000)).ToList();

    private static T Host<T>(T control, int width = 800, int height = 500) where T : Control
    {
        var form = new Form { ClientSize = new Size(width + 20, height + 20), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0) };
        control.Location = new Point(10, 10);
        control.Size = new Size(width, height);
        form.Controls.Add(control);
        form.Show();
        Pump(40);
        return control;
    }

    private long Time(string name, Action action)
    {
        var sw = Stopwatch.StartNew();
        action();
        _output.WriteLine($"{name}: {sw.ElapsedMilliseconds:N0} ms");
        return sw.ElapsedMilliseconds;
    }

    private static void Render(Control control)
    {
        using var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(0, 0, control.Width, control.Height));
    }

    [Fact]
    public void ListControlHandlesHalfAMillionItems()
    {
        RunSta(() =>
        {
            var data = Items(500_000);
            var list = Host(new EnterpriseListControl { DisplayMember = "Name" });
            var bind = Time("list: bind 500k", () => list.DataSource = data);
            var render = Time("list: first render", () => Render(list));
            var scroll = Time("list: scroll to end and render", () => { list.ScrollTo(0, int.MaxValue); Render(list); });
            var filter = Time("list: filter", () => list.Filter = item => ((Item)item).Value == 7);
            Assert.Equal(500, list.VisibleCount);
            Assert.True(bind < 4_000 && render < 1_500 && scroll < 1_000 && filter < 3_000, $"bind {bind}, render {render}, scroll {scroll}, filter {filter}");
            list.FindForm()!.Dispose();
        });
    }

    [Fact]
    public void TreeViewExpandsTensOfThousandsOfNodes()
    {
        RunSta(() =>
        {
            var tree = Host(new EnterpriseTreeViewControl { CheckBoxes = true });
            var build = Time("tree: build 50k nodes", () =>
            {
                tree.BeginUpdate();
                for (var g = 0; g < 500; g++)
                {
                    var parent = tree.Nodes.Add($"Group {g}");
                    for (var c = 0; c < 99; c++) parent.Nodes.Add($"Child {g}.{c}");
                }

                tree.EndUpdate();
            });
            var expand = Time("tree: expand all", tree.ExpandAll);
            var render = Time("tree: render", () => Render(tree));
            var check = Time("tree: check first group", () => tree.Nodes[0].Checked = true);
            Assert.Equal(500, tree.Nodes.Count);
            Assert.True(build < 4_000 && expand < 4_000 && render < 1_000 && check < 500, $"build {build}, expand {expand}, render {render}, check {check}");
            tree.FindForm()!.Dispose();
        });
    }

    [Fact]
    public void ListViewSortsAndRendersTensOfThousandsOfItems()
    {
        RunSta(() =>
        {
            var view = Host(new EnterpriseListViewControl());
            view.AddColumn("Name", 200, "Name");
            view.AddColumn("Group", 150, "Group");
            view.AddColumn("Value", 100, "Value");
            var data = Items(40_000);
            var bind = Time("listview: bind 40k", () => view.DataSource = data);
            var sort = Time("listview: sort", () => view.Sort(2, ListViewSortOrder.Descending));
            var render = Time("listview: render", () => Render(view));
            var group = Time("listview: group", () => { view.GroupSelector = i => i.SubItems[0]; Render(view); });
            var tiles = Time("listview: tiles", () => { view.View = ListViewMode.Tiles; Render(view); });
            Assert.True(bind < 6_000 && sort < 3_000 && render < 1_500 && group < 3_000 && tiles < 3_000, $"bind {bind}, sort {sort}, render {render}, group {group}, tiles {tiles}");
            view.FindForm()!.Dispose();
        });
    }

    [Fact]
    public void CardViewLaysOutThirtyThousandCards()
    {
        RunSta(() =>
        {
            var cards = Host(new EnterpriseCardViewControl { TitleField = "Name", SubtitleField = "Group" }, 900, 500);
            cards.Fields.Add(new CardField("Value"));
            var data = Items(30_000);
            var bind = Time("cards: bind 30k", () => cards.DataSource = data);
            var render = Time("cards: render", () => Render(cards));
            var scroll = Time("cards: scroll to end", () => { cards.ScrollTo(0, int.MaxValue); Render(cards); });
            var filter = Time("cards: filter", () => cards.FilterText = "Item 0123");
            Assert.True(bind < 6_000 && render < 2_000 && scroll < 2_000 && filter < 4_000, $"bind {bind}, render {render}, scroll {scroll}, filter {filter}");
            cards.FindForm()!.Dispose();
        });
    }

    [Fact]
    public void VirtualGridDoesNotTouchHiddenRows()
    {
        RunSta(() =>
        {
            var grid = Host(new EnterpriseVirtualGridControl { UseSampleData = false, RowCount = 20_000_000, ColumnCount = 30 });
            var requested = 0;
            grid.CellValueNeeded += (_, e) => { requested++; e.Value = e.RowIndex; };
            var jump = Time("virtualgrid: jump to row 15M", () => { grid.SelectCell(15_000_000, 12); Render(grid); });
            Assert.True(requested < 2_000, $"requested {requested} cells");
            Assert.True(jump < 1_500, $"jump {jump} ms");
            grid.FindForm()!.Dispose();
        });
    }

    [Fact]
    public void PivotAndHeatMapAggregateLargeInputs()
    {
        RunSta(() =>
        {
            var data = Enumerable.Range(0, 200_000).Select(i => new Item(i, $"Item {i}", $"Group {i % 60}", i % 997)).ToList();
            var pivot = Host(new EnterprisePivotGridControl());
            pivot.RowFields.Add("Group");
            pivot.ColumnFields.Add("Value");
            pivot.Values.Add(new PivotValueField("Id", EnterpriseUI.Data.AggregateFunction.Count));
            var compute = Time("pivot: 200k items, 60 x ~1000", () => pivot.DataSource = data.Take(50_000).ToList());
            var render = Time("pivot: render", () => Render(pivot));

            var heat = Host(new EnterpriseHeatMapControl());
            var bind = Time("heatmap: aggregate 200k", () => heat.Bind(data, "Group", "Value", "Id", HeatMapAggregate.Count));
            var heatRender = Time("heatmap: render", () => Render(heat));
            Assert.True(compute < 8_000 && render < 2_000 && bind < 6_000 && heatRender < 2_000, $"pivot {compute}/{render}, heatmap {bind}/{heatRender}");
            pivot.FindForm()!.Dispose();
            heat.FindForm()!.Dispose();
        });
    }
}
