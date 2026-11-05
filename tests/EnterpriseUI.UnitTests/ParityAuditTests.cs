using EnterpriseUI.CodeGenerators.Parity;
using EnterpriseUI.Grids;
using Xunit;

namespace EnterpriseUI.Tests.Unit;

public class ParityAuditTests
{
    [Fact]
    public void VendorEntriesAreClassifiedAndSourcedWithoutClaimingCompleteInventory()
    {
        Assert.NotEmpty(VendorInventory.Controls);
        Assert.All(VendorInventory.Controls, control =>
        {
            Assert.False(string.IsNullOrWhiteSpace(control.Category));
            Assert.True(Uri.IsWellFormedUriString(control.Source, UriKind.Absolute));
        });
        Assert.Contains(VendorInventory.Themes, theme => theme.Evidence.StartsWith("Candidate", StringComparison.Ordinal));
        var model = Assert.Single(ParityAudit.InspectTypes(new[] { typeof(DataGrid<>) }));
        Assert.Contains("not a WinForms control", model.Kind);
    }

    [Fact]
    public void GeneratorProducesTwelveReportsAndRetainsEvidenceGaps()
    {
        var root = Path.Combine(Path.GetTempPath(), "enterpriseui-parity-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "docs"));
            Directory.CreateDirectory(Path.Combine(root, "samples", "EnterpriseUI.WinFormsDemo"));
            var output = Path.Combine(root, "reports");
            ParityAudit.Generate(root, output, ParityAudit.InspectTypes(new[] { typeof(DataGrid<>) }), new { Name = "Test" }, GridFeatureCatalog.All);
            Assert.Equal(12, Directory.GetFiles(output, "*.md").Count(path => Path.GetFileName(path) != "README.md"));
            using var coverage = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "Coverage.json")));
            Assert.False(coverage.RootElement.GetProperty("InventoryComplete").GetBoolean());
            Assert.False(coverage.RootElement.GetProperty("ParityCertified").GetBoolean());
            Assert.Contains("Model/service only", File.ReadAllText(Path.Combine(output, "05-ControlCoverageMatrix.md")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}