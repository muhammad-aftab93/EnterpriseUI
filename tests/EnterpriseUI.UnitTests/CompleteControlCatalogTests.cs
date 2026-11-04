using System;
using System.Collections.Generic;
using System.Linq;
using EnterpriseUI.Charts;
using EnterpriseUI.Documents;
using EnterpriseUI.Editors;
using EnterpriseUI.Gauges;
using EnterpriseUI.Grids;
using EnterpriseUI.Layout;
using EnterpriseUI.Media;
using EnterpriseUI.Navigation;
using EnterpriseUI.Notifications;
using EnterpriseUI.Scheduling;
using Xunit;

namespace EnterpriseUI.Tests.Unit;

public class CompleteControlCatalogTests
{
    [Fact]
    public void MaskedTextBoxAppliesPhoneMaskAndDetectsCompletion()
    {
        var masked = new EnterpriseMaskedTextBox { Mask = "(000) 000-0000", Value = "4158902345" };
        Assert.Equal("(415) 890-2345", masked.MaskedText);
        Assert.True(masked.IsMaskCompleted);
    }

    [Fact]
    public void AutoCompleteBoxAndCheckedDropDownFilterAndCheckItems()
    {
        var ac = new EnterpriseAutoCompleteBox();
        ac.SuggestionsSource.AddRange(new[] { "DataGrid", "EnterpriseDataPager", "ChartView" });
        ac.SearchText = "Data";
        Assert.Equal(2, ac.GetFilteredSuggestions().Count);

        var cdd = new EnterpriseCheckedDropDownList<string>();
        cdd.Items.AddRange(new[] { "Alpha", "Beta", "Gamma" });
        cdd.CheckAll();
        Assert.Equal(3, cdd.CheckedItems.Count);
        cdd.UncheckAll();
        Assert.Empty(cdd.CheckedItems);
    }

    [Fact]
    public void CalculatorDropDownEvaluatesExpressionsAndMemory()
    {
        var calc = new EnterpriseCalculatorDropDown();
        decimal res = calc.EvaluateExpression("(100 + 50) * 2");
        Assert.Equal(300m, res);
        calc.MemoryAdd();
        Assert.Equal(300m, calc.MemoryValue);
    }

    [Fact]
    public void SyntaxEditorAndSpellCheckerTokenizeAndSuggest()
    {
        var editor = new EnterpriseSyntaxEditor { DocumentText = "public class Sample { // comment\n var x = 42; }" };
        var tokens = editor.Tokenize();
        Assert.Contains(tokens, t => t.Classification == "Keyword" && t.Text == "public");
        Assert.Contains(tokens, t => t.Classification == "Comment");

        var spell = new SpellChecker();
        var suggestions = spell.GetSuggestions("enterprize");
        Assert.Contains("enterprise", suggestions);
    }

    [Fact]
    public void CarouselAndBindingNavigatorProjectAndNavigate()
    {
        var carousel = new EnterpriseCarousel();
        carousel.Items.AddRange(new object[] { "A", "B", "C", "D" });
        var proj = carousel.ComputeProjections();
        Assert.Equal(4, proj.Count);

        var nav = new EnterpriseBindingNavigator<string> { NewItemFactory = () => "NewRecord" };
        nav.Items.AddRange(new[] { "Rec1", "Rec2" });
        nav.MoveLast();
        Assert.Equal("Rec2", nav.CurrentItem);
        nav.AddNew();
        Assert.Equal(3, nav.Items.Count);
    }

    [Fact]
    public void DataLayoutAndWizardControlGenerateFieldsAndAdvance()
    {
        var layout = new EnterpriseDataLayout<GanttTask>();
        Assert.NotEmpty(layout.Fields);

        var wizard = new EnterpriseWizardControl();
        wizard.Pages.Add(new EnterpriseWizardControl.WizardPage { Title = "Welcome", IsWelcomePage = true });
        wizard.Pages.Add(new EnterpriseWizardControl.WizardPage { Title = "Finish", IsCompletionPage = true });
        Assert.True(wizard.MoveNext());
        Assert.Equal(1, wizard.SelectedPageIndex);
        wizard.Finish();
        Assert.True(wizard.IsCompleted);
    }

    [Fact]
    public void VirtualGridFilterViewAndDataPagerOperateCorrectly()
    {
        var vGrid = new EnterpriseVirtualGrid { RowCount = 1_000_000, ColumnCount = 20 };
        vGrid.CellValueNeeded += (_, e) => e.Value = $"Val-{e.RowIndex}-{e.ColumnIndex}";
        Assert.Equal("Val-999999-19", vGrid.GetCellValue(999_999, 19));

        vGrid.SetCellValue(5, 5, "Override");
        Assert.Equal("Override", vGrid.GetCellValue(5, 5));

        var pager = new EnterpriseDataPager { PageSize = 10, TotalItems = 95 };
        Assert.Equal(10, pager.TotalPages);
        pager.LastPage();
        Assert.Equal(9, pager.PageIndex);
    }

    [Fact]
    public void TreeMapHeatMapBulletGraphAndGanttViewComputeMetrics()
    {
        var treeMap = new EnterpriseTreeMap();
        treeMap.Nodes.Add(new EnterpriseTreeMap.TreeMapNode("1", "Core", "Eng", 60, 1));
        treeMap.Nodes.Add(new EnterpriseTreeMap.TreeMapNode("2", "UI", "Eng", 40, 2));
        Assert.Equal(2, treeMap.ComputeNormalizedLayout().Count);

        var bullet = new EnterpriseBulletGraph { FeaturedMeasure = 88, ComparativeMeasure = 80 };
        Assert.True(bullet.IsAchievingTarget);

        var gantt = new EnterpriseGanttView();
        gantt.Tasks.Add(new GanttTask { Id = "T1", Title = "Spec", Start = DateTime.Today, End = DateTime.Today.AddDays(2), Progress = 100 });
        gantt.Tasks.Add(new GanttTask { Id = "T2", Title = "Build", Start = DateTime.Today.AddDays(2), End = DateTime.Today.AddDays(5), Progress = 50 });
        gantt.Dependencies.Add(new GanttDependency("T1", "T2"));
        Assert.Equal(2, gantt.ComputeCriticalPath().Count);
    }

    [Fact]
    public void BarcodeAndDocumentProcessingSuiteExecuteFormulasAndZip()
    {
        var barcode = new EnterpriseBarcodeGenerator { Symbology = BarcodeType.QrCode, Value = "EUI-2026" };
        var matrix = barcode.Generate2DMatrix(21);
        Assert.True(matrix[0, 0]); // Finder top-left corner

        var cells = new Dictionary<string, double> { ["A1"] = 10, ["A2"] = 20, ["A3"] = 30 };
        Assert.Equal(60, DocumentProcessingSuite.EvaluateSpreadsheetFormula("=SUM(A1,A2,A3)", cells));
        Assert.Equal(20, DocumentProcessingSuite.EvaluateSpreadsheetFormula("=AVERAGE(A1,A2,A3)", cells));

        var zip = DocumentProcessingSuite.CreateZipArchive(new Dictionary<string, string> { ["hello.txt"] = "EnterpriseUI" });
        var unzipped = DocumentProcessingSuite.ExtractZipArchive(zip);
        Assert.Equal("EnterpriseUI", unzipped["hello.txt"]);

        var cam = new EnterpriseWebCam();
        cam.StartStream();
        cam.TakeSnapshot();
        Assert.True(cam.IsStreaming);
        Assert.Equal(1, cam.CapturedSnapshotsCount);
    }
}

