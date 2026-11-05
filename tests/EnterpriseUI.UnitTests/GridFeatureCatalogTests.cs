using System;
using System.IO;
using System.Linq;
using EnterpriseUI.Grids;
using Xunit;

namespace EnterpriseUI.Tests.Unit;

public class GridFeatureCatalogTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "EnterpriseUI.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [Fact]
    public void EveryRequestedFeatureAreaIsRepresented()
    {
        var required = new[] { "Paging", "Sorting", "Filtering", "Grouping", "Hierarchy", "Columns", "Rows", "Editing", "Summaries", "Exporting", "Printing", "Selection", "Searching", "Performance", "Accessibility", "Themes" };

        var areas = GridFeatureCatalog.Areas.ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.All(required, area => Assert.Contains(area, areas));
    }

    [Fact]
    public void ImplementedFeaturesPointToADemoADocumentAndTests()
    {
        foreach (var feature in GridFeatureCatalog.All.Where(f => f.Status == GridFeatureStatus.Implemented))
        {
            Assert.False(string.IsNullOrWhiteSpace(feature.Demo), $"{feature.Name}: demo");
            Assert.False(string.IsNullOrWhiteSpace(feature.Docs), $"{feature.Name}: docs");
            Assert.False(string.IsNullOrWhiteSpace(feature.Tests), $"{feature.Name}: tests");
        }
    }

    [Fact]
    public void ReferencedDocumentationFilesExistAndFollowTheRequiredStructure()
    {
        var root = RepoRoot();
        var required = new[] { "## Overview", "## Architecture notes", "## Usage guide", "## Examples", "## Best practices", "## Performance notes", "## Accessibility notes", "## Troubleshooting", "## API reference", "## Related features", "## Live examples" };

        foreach (var doc in GridFeatureCatalog.All.Where(f => f.Docs != null).Select(f => f.Docs!).Distinct())
        {
            var path = Path.Combine(root, "docs", "Grid", doc + ".md");
            Assert.True(File.Exists(path), $"missing documentation file {path}");
            var text = File.ReadAllText(path);
            Assert.All(required, heading => Assert.Contains(heading, text));
        }
    }

    [Fact]
    public void PartialAndPlannedFeaturesExplainWhatIsMissing()
    {
        Assert.All(GridFeatureCatalog.All.Where(f => f.Status is GridFeatureStatus.Partial or GridFeatureStatus.Planned or GridFeatureStatus.OutOfScope),
            f => Assert.False(string.IsNullOrWhiteSpace(f.Notes), $"{f.Name} needs a note"));
    }

    [Fact]
    public void FeatureNamesAreUniquePerAreaAndCoverageIsComputedHonestly()
    {
        var duplicates = GridFeatureCatalog.All.GroupBy(f => (f.Area, f.Name)).Where(g => g.Count() > 1).ToList();
        Assert.Empty(duplicates);

        var implemented = GridFeatureCatalog.Count(GridFeatureStatus.Implemented);
        var inScope = GridFeatureCatalog.All.Count(f => f.Status != GridFeatureStatus.OutOfScope);
        Assert.InRange(GridFeatureCatalog.Coverage, (double)implemented / inScope, 1.0);
        Assert.True(GridFeatureCatalog.Coverage < 1.0, "the matrix must not claim complete parity while features are missing");
    }
}
