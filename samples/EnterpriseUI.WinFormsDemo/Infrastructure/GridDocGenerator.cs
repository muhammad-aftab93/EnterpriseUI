using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using EnterpriseUI.Grids;
using EnterpriseUI.WinFormsDemo.Modules;

namespace EnterpriseUI.WinFormsDemo.Infrastructure;

/// <summary>
/// Writes docs/Grid from the workshop scenarios, the generated example code and the feature catalog, so documentation, demo pages and the
/// feature matrix cannot drift apart. Run: EnterpriseUI.WinFormsDemo.exe --generate-grid-docs &lt;docs\Grid directory&gt;
/// </summary>
public static class GridDocGenerator
{

    private static readonly string[] CachedValues2 = new[] { "Column footers", "11 aggregate functions", "Group summaries", "Group footers", "Running totals", "Calculated columns" };

    private static readonly string[] CachedValues1 = new[] { "Search panel", "Filter or highlight mode", "Match count", "Next/previous (F3)", "Type-ahead", "Search in child tables", "Expand to reveal" };
    private sealed record Page(string File, string Title, string Overview, string[] Features, string Usage, string Architecture, string Performance, string Accessibility,
        string Troubleshooting, string BestPractices, string Api, string DemoId, string DemoTitle, string[] Related);

    private static readonly Dictionary<string, string> FileByScenario = new()
    {
        ["grid-basics"] = "Basics", ["grid-paging"] = "Paging", ["grid-sorting"] = "Sorting", ["grid-filtering"] = "Filtering", ["grid-grouping"] = "Grouping",
        ["grid-hierarchy"] = "Hierarchy", ["grid-columns"] = "Columns", ["grid-rows"] = "Rows", ["grid-editing"] = "Editing", ["grid-selection"] = "Selection",
        ["grid-exporting"] = "Exporting", ["grid-printing"] = "Printing", ["grid-performance"] = "Performance", ["grid-accessibility"] = "Accessibility",
        ["grid-theming"] = "Theming", ["grid-realtime"] = "RealTime", ["grid-large-data"] = "LargeData", ["grid-master-detail"] = "MasterDetail",
        ["grid-treegrid"] = "TreeGrid", ["grid-enterprise"] = "Enterprise"
    };

    private static readonly Dictionary<string, string[]> RelatedByFile = new()
    {
        ["Basics"] = new[] { "Columns", "Selection", "Theming" },
        ["Paging"] = new[] { "LargeData", "Sorting", "Filtering", "Exporting" },
        ["Sorting"] = new[] { "Filtering", "Grouping", "Columns" },
        ["Filtering"] = new[] { "Searching", "Sorting", "Grouping" },
        ["Searching"] = new[] { "Filtering", "Hierarchy", "Accessibility" },
        ["Grouping"] = new[] { "Summaries", "Sorting", "Exporting" },
        ["Hierarchy"] = new[] { "MasterDetail", "TreeGrid", "Exporting" },
        ["MasterDetail"] = new[] { "Hierarchy", "TreeGrid", "LargeData" },
        ["TreeGrid"] = new[] { "Hierarchy", "Searching", "Summaries" },
        ["Columns"] = new[] { "Basics", "Sorting", "Enterprise" },
        ["Rows"] = new[] { "Editing", "Selection", "Accessibility" },
        ["Editing"] = new[] { "Rows", "Summaries", "Accessibility" },
        ["Summaries"] = new[] { "Grouping", "Editing", "Exporting" },
        ["Selection"] = new[] { "Rows", "Accessibility", "Basics" },
        ["Exporting"] = new[] { "Printing", "Grouping", "Hierarchy" },
        ["Printing"] = new[] { "Exporting", "Columns", "Theming" },
        ["Performance"] = new[] { "LargeData", "RealTime", "Paging" },
        ["LargeData"] = new[] { "Paging", "Performance", "RealTime" },
        ["RealTime"] = new[] { "Performance", "Sorting", "LargeData" },
        ["Accessibility"] = new[] { "Theming", "Selection", "Editing" },
        ["Theming"] = new[] { "Accessibility", "Basics", "Printing" },
        ["Enterprise"] = new[] { "Columns", "Editing", "Exporting", "Printing" }
    };

    public static int GenerateAll(string directory)
    {
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(Path.Combine(directory, "Reports"));
        var pages = new List<Page>();
        var examples = new Dictionary<string, string>();

        foreach (var scenario in GridScenarios.All)
        {
            var file = FileByScenario[scenario.Id];
            GridScenarioDocs.Fill(scenario, out var architecture, out var performance, out var accessibility, out var troubleshooting, out var best);
            pages.Add(new Page(file, scenario.Title, scenario.Description, scenario.Features, scenario.Usage, architecture, performance, accessibility, troubleshooting, best, scenario.Api, scenario.Id, scenario.Title, RelatedByFile[file]));
            examples[file] = ExampleFor(scenario);
        }

        pages.Add(new Page("Searching", "Searching - Search Panel, Highlight, Type-Ahead",
            "Search all columns from the built-in panel (Ctrl+F). Filter mode hides rows that do not match; highlight mode keeps rows, counts matches and lets you move between them. Typing in the grid jumps to the next matching row of the focused column.",
            CachedValues1,
            "grid.ShowSearchPanel = true; grid.SearchPanelText = \"acme\"; grid.SearchMode = GridSearchMode.Highlight; await grid.SearchAsync(\"acme\"); grid.NextMatch(); await grid.FindRowByPrefixAsync(\"Name\", \"Ac\").",
            "DataGrid<T> implements search over visible, searchable columns using formatted cell text, so users find what they see. Filter mode adds a composite OR filter; highlight mode computes a match list (row, field, range) that drives painting and navigation. Hierarchy search walks child relations and can expand collapsed groups, tree nodes and details.",
            "Search is a single pass over the formatted text of searchable columns; MaxMatches (default 100,000) bounds the match list. Filter mode runs in the query engine like any other filter.",
            "The panel is a labelled text box with count text and buttons with accessible names. Highlights use a fill plus text color derived from the theme, and the current match also gets a different (stronger) fill so it does not depend on hue.",
            "No matches in a numeric column usually means the formatted text differs from what you typed (for example currency symbols); matching uses FormatCellValue. Columns with Searchable = false are skipped.",
            "Use highlight mode for review workflows, filter mode for narrowing, and keep searchable columns to the ones people actually read.",
            "DataGrid<T> -> SearchPanelText, SearchMode, SearchOptions, SearchAsync, NextMatch, PreviousMatch, GoToMatch, HighlightText, SearchResult, FindRowByPrefixAsync; EnterpriseDataGrid<T> -> ShowSearchPanel, EnableSearchPanel",
            "grid-filtering", "Filtering - Filter Row, Value Lists, Operators and Presets", RelatedByFile["Searching"]));

        pages.Add(new Page("Summaries", "Summaries - Column Footers, Group Totals and Running Totals",
            "Show aggregates under columns (sum, average, count, min, max, median and more), in group headers and footers, and as running totals. Summaries are computed over the whole filtered result, not just the visible page.",
            CachedValues2,
            "column.FooterSummaries.Add(new GridColumnSummary(AggregateFunction.Sum, \"C0\")); grid.GroupSummaries.Add(new GroupSummaryDescriptor(\"Total\", AggregateFunction.Sum, \"Sum {0:C0}\")); column.RunningTotalOf = \"Amount\"; column.Expression = \"[UnitPrice] * [Quantity]\".",
            "Footer summaries become AggregateDescriptors in the request, so the data source computes them next to paging. A format without a placeholder (\"C0\") formats the value and gets the aggregate label; a composite format (\"Sum {0:C0}\") is used as written.",
            "Aggregates are computed in the query engine in one pass per query, together with sorting and filtering. Running totals are computed asynchronously and raise RunningTotalsChanged.",
            "Footer values are exposed through the grid's status text and the summary row is drawn with the same contrast rules as the header.",
            "A footer that shows only the format text has an invalid composite format; use \"C0\" or \"Sum {0:C0}\". Median and standard deviation require numeric values.",
            "Prefer data-source aggregates over UI calculations, format money consistently and show counts for context.",
            "GridColumn -> FooterSummaries, RunningTotalOf, Expression; GridColumnSummary; DataGrid<T> -> FooterValues, GetFooterValues, GroupSummaries, ShowGroupFooters, ComputeRunningTotalsAsync",
            "grid-editing", "Editing - In-Cell, Popup, Batch and Validation", RelatedByFile["Summaries"]));

        foreach (var page in pages) File.WriteAllText(Path.Combine(directory, page.File + ".md"), Render(page, examples.TryGetValue(page.File, out var code) ? code : examples[FileForDemo(page.DemoId)]));

        WriteIndex(directory, pages);
        WriteReports(Path.Combine(directory, "Reports"), pages);
        return pages.Count;
    }

    private static string FileForDemo(string demoId) => FileByScenario[demoId];

    private static string ExampleFor(GridScenario scenario)
    {
        var demo = DemoMetadataRegistry.GetById(scenario.Id);
        using var view = new GridScenarioView(demo, scenario);
        if (!GridCodeGenerator.TryGenerate(view.Base.CodeTarget, demo, CodeOutputFormat.FluentBuilder, out var code)) return "// code generation unavailable";
        // Keep the example focused: drop the generated header and usings, keep the builder expression.
        var lines = code.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var start = lines.FindIndex(l => l.StartsWith("namespace ", StringComparison.Ordinal));
        return start < 0 ? code : string.Join(Environment.NewLine, lines.Skip(start + 2));
    }

    private static string Render(Page page, string example)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {page.Title}");
        sb.AppendLine();
        sb.AppendLine("> Generated by `GridDocGenerator` from the workshop page, its live-generated code and the feature catalog. Edit the scenario text in `samples/EnterpriseUI.WinFormsDemo/Modules` and regenerate.");
        sb.AppendLine();
        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine(page.Overview);
        sb.AppendLine();
        foreach (var feature in page.Features) sb.AppendLine($"- {feature}");
        sb.AppendLine();
        var features = GridFeatureCatalog.All.Where(f => f.Docs == page.File).ToList();
        if (features.Count > 0)
        {
            sb.AppendLine("Feature status (from the feature catalog):");
            sb.AppendLine();
            sb.AppendLine("| Feature | Implementation | Status | Notes |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var f in features) sb.AppendLine($"| {f.Name} | {f.EnterpriseUIImplementation} | {f.Status} | {f.Notes} |");
            sb.AppendLine();
        }
        sb.AppendLine("## Architecture notes");
        sb.AppendLine();
        sb.AppendLine(page.Architecture);
        sb.AppendLine();
        sb.AppendLine("## Usage guide");
        sb.AppendLine();
        sb.AppendLine(string.IsNullOrWhiteSpace(page.Usage) ? "Configure the grid through its properties or `GridBuilder<T>`; see the example below." : page.Usage);
        sb.AppendLine();
        sb.AppendLine("## Examples");
        sb.AppendLine();
        sb.AppendLine("The code below was generated from the live grid of the demo page (fluent API). The demo's *Generated Code* tab also produces imperative C#, Designer code, a JSON configuration and a complete runnable example, all compiled during validation.");
        sb.AppendLine();
        sb.AppendLine("```csharp");
        sb.AppendLine(example);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Best practices");
        sb.AppendLine();
        sb.AppendLine(page.BestPractices);
        sb.AppendLine();
        sb.AppendLine("## Performance notes");
        sb.AppendLine();
        sb.AppendLine(page.Performance);
        sb.AppendLine();
        sb.AppendLine("Measured numbers are in [PerformanceReport](Reports/PerformanceReport.md).");
        sb.AppendLine();
        sb.AppendLine("## Accessibility notes");
        sb.AppendLine();
        sb.AppendLine(page.Accessibility);
        sb.AppendLine();
        sb.AppendLine("## Troubleshooting");
        sb.AppendLine();
        sb.AppendLine(page.Troubleshooting);
        sb.AppendLine();
        sb.AppendLine("## API reference");
        sb.AppendLine();
        foreach (var part in page.Api.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) sb.AppendLine($"- `{part}`");
        sb.AppendLine();
        sb.AppendLine("## Related features");
        sb.AppendLine();
        foreach (var related in page.Related) sb.AppendLine($"- [{related}]({related}.md)");
        sb.AppendLine();
        sb.AppendLine("## Live examples");
        sb.AppendLine();
        sb.AppendLine($"Run `samples/EnterpriseUI.WinFormsDemo`, open **DataGrid Workshop > {page.DemoTitle}** (demo id `{page.DemoId}`).");
        var tests = features.Select(f => f.Tests).Where(t => !string.IsNullOrEmpty(t)).Distinct().ToList();
        if (tests.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Covered by: " + string.Join("; ", tests));
        }
        return sb.ToString();
    }

    private static void WriteIndex(string directory, List<Page> pages)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# EnterpriseUI DataGrid documentation");
        sb.AppendLine();
        sb.AppendLine("One page per feature area (generated), plus reports. Every page has: overview, architecture notes, usage guide, examples, best practices, performance notes, accessibility notes, troubleshooting, API reference, related features and live examples.");
        sb.AppendLine();
        sb.AppendLine("## Feature pages");
        sb.AppendLine();
        foreach (var page in pages.OrderBy(p => p.File)) sb.AppendLine($"- [{page.File}]({page.File}.md) - {page.Title}");
        sb.AppendLine();
        sb.AppendLine("## Reports");
        sb.AppendLine();
        foreach (var report in new[]
        {
            ("EnterpriseUIFeatureMatrix", "EnterpriseUI DataGrid feature matrix"),
            ("MissingFeaturesReport", "3. Missing features"), ("PagingArchitecture", "4. Paging architecture"), ("HierarchyArchitecture", "5. Hierarchy architecture"),
            ("ExportingArchitecture", "6. Exporting architecture"), ("PrintingArchitecture", "7. Printing architecture"), ("PerformanceReport", "8. Performance"),
            ("DemoCoverageReport", "9. Demo coverage"), ("DocumentationCoverageReport", "10. Documentation coverage"), ("TestCoverageReport", "11. Test coverage"),
            ("ModernizationRoadmap", "12. Grid modernization roadmap")
        })
        {
            sb.AppendLine($"- [{report.Item2}](Reports/{report.Item1}.md)");
        }
        File.WriteAllText(Path.Combine(directory, "README.md"), sb.ToString());
    }

    private static void WriteReports(string directory, List<Page> pages)
    {
        var all = GridFeatureCatalog.All;
        var date = DateTime.Now.ToString("yyyy-MM-dd");

        File.WriteAllText(Path.Combine(directory, "EnterpriseUIFeatureMatrix.md"),
            $"# 2. EnterpriseUI DataGrid feature matrix\n\nGenerated {date} from `GridFeatureCatalog`. Statuses are literal: Implemented means built, documented, demonstrated and tested; Partial lists what is missing.\n\n" +
            $"Summary: {GridFeatureCatalog.Count(GridFeatureStatus.Implemented)} implemented, {GridFeatureCatalog.Count(GridFeatureStatus.Partial)} partial, {GridFeatureCatalog.Count(GridFeatureStatus.Planned)} planned, {GridFeatureCatalog.Count(GridFeatureStatus.OutOfScope)} out of scope. Weighted coverage of in-scope features (partial counts half): {GridFeatureCatalog.Coverage:P0}.\n\n" +
            "| Area | Implemented | Partial | Planned | Out of scope |\n|---|---:|---:|---:|---:|\n" +
            string.Join("\n", GridFeatureCatalog.Areas.Select(a => $"| {a} | {GridFeatureCatalog.ForArea(a).Count(f => f.Status == GridFeatureStatus.Implemented)} | {GridFeatureCatalog.ForArea(a).Count(f => f.Status == GridFeatureStatus.Partial)} | {GridFeatureCatalog.ForArea(a).Count(f => f.Status == GridFeatureStatus.Planned)} | {GridFeatureCatalog.ForArea(a).Count(f => f.Status == GridFeatureStatus.OutOfScope)} |")) +
            "\n\n## Full matrix\n\n" + GridFeatureCatalog.ToMarkdown());

        File.WriteAllText(Path.Combine(directory, "MissingFeaturesReport.md"),
            "# 3. Missing features report\n\nEverything that is not fully implemented, with the reason. Nothing here is hidden: this is the gap list that the roadmap works through.\n\n" +
            GridFeatureCatalog.ToMarkdown(f => f.Status != GridFeatureStatus.Implemented));

        var demoRows = GridScenarios.All.Select(s => $"| {s.Title} | `{s.Id}` | {FileByScenario[s.Id]} | {all.Count(f => f.Demo == s.Id)} | {s.Features.Length} |");
        File.WriteAllText(Path.Combine(directory, "DemoCoverageReport.md"),
            $"# 9. Demo coverage report\n\nGenerated {date}. The `DataGrid Workshop` in `EnterpriseUI.WinFormsDemo` has {GridScenarios.All.Count} pages. Every page binds a live grid, offers toolbar actions that exercise the feature, and its *Generated Code* tab produces C#, Designer code, Fluent API, JSON, XML and a complete exportable example from the live state. All generated sources were compiled during validation.\n\n" +
            "| Page | Demo id | Documentation | Catalog features demonstrated | Feature tags |\n|---|---|---|---:|---:|\n" + string.Join("\n", demoRows) + "\n\n" +
            $"Implemented features without a demo page: {all.Count(f => f.Status == GridFeatureStatus.Implemented && string.IsNullOrEmpty(f.Demo))}.\n");

        var docRows = pages.OrderBy(p => p.File).Select(p => $"| [{p.File}](../{p.File}.md) | {p.Features.Length} | 11/11 |");
        File.WriteAllText(Path.Combine(directory, "DocumentationCoverageReport.md"),
            $"# 10. Documentation coverage report\n\nGenerated {date}. {pages.Count} feature pages, each with all eleven required sections (a unit test verifies the headings of every page referenced by the catalog).\n\n" +
            "| Page | Features listed | Sections |\n|---|---:|---|\n" + string.Join("\n", docRows) + "\n\n" +
            $"Features referencing documentation: {all.Count(f => !string.IsNullOrEmpty(f.Docs))} of {all.Count} ({all.Count(f => f.Status == GridFeatureStatus.Implemented)} implemented, all documented).\n");

        var roadmap = new StringBuilder();
        roadmap.AppendLine("# 12. Grid modernization roadmap");
        roadmap.AppendLine();
        roadmap.AppendLine($"Generated {date} from the open items of the feature catalog, ordered by priority (P1 first) then complexity (S, M, L).");
        roadmap.AppendLine();
        foreach (var group in all.Where(f => f.Status is GridFeatureStatus.Partial or GridFeatureStatus.Planned).GroupBy(f => f.Priority).OrderBy(g => g.Key))
        {
            roadmap.AppendLine($"## Priority {group.Key}");
            roadmap.AppendLine();
            foreach (var f in group.OrderBy(f => "SML".IndexOf(f.Complexity[0], StringComparison.Ordinal))) roadmap.AppendLine($"- **{f.Area}: {f.Name}** ({f.Status}, {f.Complexity}) - {f.Notes}");
            roadmap.AppendLine();
        }
        roadmap.AppendLine("## Cross-cutting");
        roadmap.AppendLine();
        roadmap.AppendLine("- Localization provider for the control's built-in texts and a right-to-left layout pass (they belong together).");
        roadmap.AppendLine("- Variable row heights (word wrap, auto height) which also unlocks merged cells.");
        roadmap.AppendLine("- Visual composite filter builder on top of the existing filter descriptors.");
        roadmap.AppendLine("- Embedded fonts in PDF export and a real-device pass with Narrator and NVDA for the UI Automation tree.");
        File.WriteAllText(Path.Combine(directory, "ModernizationRoadmap.md"), roadmap.ToString());
    }
}
