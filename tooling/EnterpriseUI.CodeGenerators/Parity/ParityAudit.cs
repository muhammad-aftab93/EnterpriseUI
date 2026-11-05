using System.Reflection;
using System.Text;
using System.Text.Json;

namespace EnterpriseUI.CodeGenerators.Parity;

public sealed record ControlEvidence(string Name, string FullName, string Kind, IReadOnlyList<string> Apis, IReadOnlyList<string> Events, bool ThemeHook, bool NativeAccessibilityOverride);
public sealed record ControlCoverage(VendorControl Vendor, string Status, IReadOnlyList<string> Candidates, string MissingFeatures, string MissingApis, string MissingEvents, string MissingBehaviors, string ThemeSupport, IReadOnlyList<string> Documentation, IReadOnlyList<string> MissingDocumentationSections, IReadOnlyList<string> DemoEvidence);

public static class ParityAudit
{
    public static IReadOnlyList<ControlEvidence> InspectTypes(IEnumerable<Type> types)
    {
        return types.Where(type => type.IsPublic || type.IsNestedPublic).OrderBy(type => type.FullName).Select(type =>
        {
            var isControl = Ancestors(type).Any(parent => parent.FullName == "System.Windows.Forms.Control");
            var kind = isControl ? (type.Assembly.GetName().Name?.Contains("WinFormsDemo", StringComparison.Ordinal) == true ? "Demo-only WinForms control"
                : type.IsAbstract || type.ContainsGenericParameters ? "Abstract or generic WinForms control" : "Concrete WinForms control")
                : type.IsEnum ? "Enum" : type.IsInterface ? "Interface" : "Model, service or tooling; not a WinForms control";
            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var apis = type.GetProperties(flags).Select(property => property.Name)
                .Concat(type.GetMethods(flags).Where(method => !method.IsSpecialName).Select(method => method.ToString() ?? method.Name)).Order().ToArray();
            return new ControlEvidence(type.Name.Split('`')[0], type.FullName ?? type.Name, kind, apis,
                type.GetEvents(flags).Select(eventInfo => eventInfo.Name).Order().ToArray(),
                type.GetInterfaces().Any(contract => contract.Name == "IThemeableControl"),
                type.GetMethod("CreateAccessibilityInstance", BindingFlags.NonPublic | BindingFlags.Instance)?.DeclaringType == type);
        }).ToArray();
    }

    private static IEnumerable<Type> Ancestors(Type type)
    {
        for (var current = type; current != null; current = current.BaseType) yield return current;
    }

    private static string Normalize(string text) => new(text.Where(char.IsLetterOrDigit).ToArray());

    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GridView"] = new[] { "EnterpriseDataGrid", "DataGrid" }, ["DataGrid"] = new[] { "EnterpriseDataGrid", "DataGrid" },
        ["Buttons"] = new[] { "EnterpriseButtonControl", "EnterpriseSplitButtonControl", "EnterpriseRepeatButtonControl" },
        ["TextBox"] = new[] { "EnterpriseTextBoxControl" }, ["FontDropDownList"] = new[] { "EnterpriseFontDropDownControl" },
        ["Gauges"] = new[] { "EnterpriseBulletGraphControl" }, ["CodeEditor"] = new[] { "EnterpriseSyntaxEditorControl" },
        ["Rating"] = new[] { "EnterpriseRatingAndWaitingControl" }, ["WaitingBar"] = new[] { "EnterpriseRatingAndWaitingControl" },
        ["Rotator"] = new[] { "EnterpriseSlideRotatorPipsControl" }, ["SlideView"] = new[] { "EnterpriseSlideRotatorPipsControl" },
        ["PipsPager"] = new[] { "EnterpriseSlideRotatorPipsControl" }, ["DataEditorsandSimpleControls"] = new[] { "EnterpriseTextBoxControl", "EnterpriseButtonControl" }
    };

    public static IReadOnlyList<ControlCoverage> Reconcile(IReadOnlyList<ControlEvidence> types, string repositoryRoot)
    {
        var docs = Directory.GetFiles(Path.Combine(repositoryRoot, "docs"), "*.md", SearchOption.AllDirectories)
            .Select(path => (Path: Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'), Text: File.ReadAllText(path))).ToArray();
        var demoFiles = Directory.GetFiles(Path.Combine(repositoryRoot, "samples", "EnterpriseUI.WinFormsDemo"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .Select(path => (Path: Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'), Text: File.ReadAllText(path))).ToArray();
        var sections = new[] { "Overview", "Feature Matrix", "Architecture", "API", "Examples", "Performance", "Accessibility", "Troubleshooting", "Best Practices", "Related", "Theme", "Migration", "Enterprise" };
        return VendorInventory.Controls.Select(vendor =>
        {
            var key = Normalize(vendor.Name);
            var names = Aliases.GetValueOrDefault(key) ?? new[] { "Enterprise" + key + "Control", "Enterprise" + key, key, key + "Control" };
            var matches = types.Where(type => names.Any(name => string.Equals(name, type.Name, StringComparison.OrdinalIgnoreCase))).ToArray();
            var controls = matches.Where(type => type.Kind is "Concrete WinForms control" or "Abstract or generic WinForms control").ToArray();
            var status = controls.Length > 0 ? "WinForms implementation present; competitive behavior unverified"
                : matches.Length > 0 ? "Model/service only; WinForms adapter not evidenced" : "No equivalent evidenced; mapping review required";
            var controlNames = matches.Select(type => type.Name).Distinct().ToArray();
            var relatedDocs = docs.Where(doc => controlNames.Length > 0 && controlNames.Any(name => doc.Text.Contains(name, StringComparison.Ordinal))).ToArray();
            var missingSections = sections.Where(section => !relatedDocs.Any(doc => doc.Text.Split('\n').Any(line => line.TrimStart().StartsWith('#') && line.Contains(section, StringComparison.OrdinalIgnoreCase)))).ToArray();
            return new ControlCoverage(vendor, status, matches.Select(type => type.FullName).ToArray(),
                "Per-feature vendor behavior comparison required", "Exact vendor API comparison not completed", "Exact vendor event comparison not completed",
                "Keyboard, mouse, touch, focus, selection, editing, drag/drop, menus, states and animation require workflow tests",
                controls.Any(type => type.ThemeHook) ? "Theme hook present; all-state and runtime-switch verification required" : "No mapped WinForms theme hook evidenced",
                relatedDocs.Select(doc => doc.Path).ToArray(), missingSections,
                demoFiles.Where(file => controlNames.Length > 0 && controlNames.Any(name => file.Text.Contains(name, StringComparison.Ordinal))).Select(file => file.Path).ToArray());
        }).ToArray();
    }

    public static void Generate(string repositoryRoot, string outputDirectory, IReadOnlyList<ControlEvidence> types, object themeSnapshot, object gridFeatures)
    {
        var coverage = Reconcile(types, repositoryRoot);
        Directory.CreateDirectory(outputDirectory);
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(Path.Combine(outputDirectory, "Evidence.json"), JsonSerializer.Serialize(new
        {
            AssessmentDate = VendorInventory.AssessmentDate,
            Completeness = "Landing-page controls/families classified. Vendor package variants, palettes and exact behavior inventories remain incomplete.",
            Themes = VendorInventory.Themes, Controls = coverage, PublicTypes = types, ThemeSnapshot = themeSnapshot, GridFeatures = gridFeatures
        }, jsonOptions));

        string Intro(string title) => $"# {title}\n\nAssessment: {VendorInventory.AssessmentDate}. Evidence-based baseline, not a parity certification.\n\n";
        string Cell(string? value) => (value ?? "Not evidenced").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        void Write(string name, string title, string body) => File.WriteAllText(Path.Combine(outputDirectory, name + ".md"), Intro(title) + body);
        string ThemeTable(string vendor)
        {
            var text = new StringBuilder("Names are documented or explicitly marked as candidates. This is not a certified complete shipping-theme inventory. Exact visual measurements require permitted runtime inspection; no vendor assets are copied.\n\n");
            text.AppendLine("| Theme | Family / variant | Evidence | EnterpriseUI support | Source |");
            text.AppendLine("|---|---|---|---|---|");
            foreach (var theme in VendorInventory.Themes.Where(theme => theme.Vendor == vendor))
                text.AppendLine($"| {Cell(theme.Name)} | {Cell(theme.Family + " / " + theme.Variant)} | {Cell(theme.Evidence)} | Exact equivalent not verified | {theme.Source} |");
            text.AppendLine("\nFor every row: visual characteristics, palette, typography, density, spacing, chrome, borders, selection, hover, focus and accessibility measurements are **unverified** unless supported by a linked observation. Vendor pages do not publish a complete token specification. Palette-dependent variants must be inventoried individually.\n");
            return text.ToString();
        }
        Write("01-TelerikThemeInventory", "Telerik Theme Inventory", ThemeTable("Telerik"));
        Write("02-DevExpressThemeInventory", "DevExpress Theme Inventory", ThemeTable("DevExpress") + "\nThe official SkinManager.Skins API enumerates installed skins. A licensed runtime inventory must include bonus skin registration and every vector palette. Supplied API links for SkinStyle and SkinSvgPalette resolve to a namespace page and are not accepted as a complete enumeration.\n");
        Write("03-ThemeGapAnalysis", "EnterpriseUI Theme Gap Analysis", "## Implemented foundation\n\nImmutable token snapshots, custom-base inheritance, schema/version metadata, variables, validated JSON import/export and explicit failure-safe file reload have focused tests. Studio, ModernOffice and CompactBusiness each provide original light/dark profiles. HighContrast-Black and HighContrast-White are separate standalone themes. The Demo Center has a live token/font/density/spacing playground with versioned document export and complete C# token emission. This establishes lifecycle and visual foundations, not vendor parity.\n\n## Verification commands\n\nThemingTests and ParityAuditTests validate lifecycle and evidence contracts. Demo CLI --verify-themes compiles every registered profile, compares all generated tokens, and exercises preview updates and invalid-edit rollback. Results are produced by the command, not inferred from report generation.\n\n## Remaining gaps\n\nPer-control typed font/density adapters; automatic file watching and UI-thread dispatch; complete vendor theme/palette observations; certified equivalent visual profiles; high-contrast state validation; brand packaging; marketplace trust/signing; control-wide switch/disposal tests.\n\n## Current themes\n\n```json\n" + JsonSerializer.Serialize(themeSnapshot, jsonOptions) + "\n```\n");
        Write("04-MissingThemes", "Missing Themes Report", "No exact Telerik/DevExpress visual equivalent is certified by this audit. Existing Fluent/Office/Material names are not proof of matching vendor chrome or behavior. All documented/candidate themes above remain mapping or visual-verification work.\n\nOriginal Studio, ModernOffice and CompactBusiness light/dark profiles are implemented as foundations. HighContrast-Black and HighContrast-White remain standalone themes. Remaining work includes verified vendor-profile mappings, touch-density styling and normal/hover/pressed/focused/disabled/selected/error state captures for every concrete control.\n");
        var controlTable = new StringBuilder("All entries extracted from the referenced landing-page catalogs have a classification. DevExpress entries are families, not an exhaustive individual-editor inventory. Match candidates are heuristic and must be reviewed; a text reference is not verified demo/documentation coverage.\n\n| Vendor | Category | Control/family | Status | Candidates | Theme | Docs references | Demo references |\n|---|---|---|---|---|---|---|---|\n");
        foreach (var row in coverage) controlTable.AppendLine($"| {row.Vendor.Vendor} | {Cell(row.Vendor.Category)} | {Cell(row.Vendor.Name)} | {Cell(row.Status)} | {Cell(string.Join(", ", row.Candidates))} | {Cell(row.ThemeSupport)} | {row.Documentation.Count} | {row.DemoEvidence.Count} |");
        Write("05-ControlCoverageMatrix", "Control Coverage Matrix", controlTable.ToString());
        var missing = coverage.Where(row => !row.Status.StartsWith("WinForms implementation", StringComparison.Ordinal)).ToArray();
        Write("06-MissingControls", "Missing Controls Report", string.Join("\n", missing.Select(row => $"- **{row.Vendor.Vendor} / {row.Vendor.Category} / {row.Vendor.Name}**: {row.Status}.")) + "\n\n## Specification gate for each row\n\nSpecify rendering/layout ownership, public API and events, keyboard/mouse/touch state machine, focus/selection/editing, accessibility providers, theme tokens, localization/RTL, design-time serialization, demos, documentation and benchmark workloads. Implement against that spec; do not publish a placeholder adapter as a complete control.\n");
        Write("07-MissingFeatures", "Missing Features Report", "## Grid catalog baseline\n\n```json\n" + JsonSerializer.Serialize(gridFeatures, jsonOptions) + "\n```\n\nCatalog status is an implementation claim, not certified vendor parity. Exact APIs/events and behavioral comparison remain required. Known grid partial/planned areas include visual composite/range filtering, icon sets, independent editable child grids, localization, RTL, merged cells, auto-height, frozen rows, specialized editors, clipboard paste and selected-row printing.\n\n## Cross-suite gaps\n\nFull control-level API/event inventories and touch, focus, accessibility, animation, runtime appearance, enterprise workflow comparisons are outstanding. Missing evidence is not treated as success.\n");
        Write("08-ThemeBuilderArchitecture", "Theme Builder Architecture", "## Data flow\n\nThemeDocument -> schema/token validation -> named-base DAG resolution -> variable resolution -> immutable Theme -> preview -> complete source/JSON export.\n\n## Implemented\n\nCustom-base snapshot inheritance, variables, cycle rejection, versioned JSON documents and strict import checks.\n\n## Required extensions\n\nTyped color/font/spacing/density tokens; component token contracts; undoable editing; brand manifests; white-label identity metadata; schema migrations; compatible profile variants; generated-source compilation and pixel comparison.\n\n## Marketplace architecture\n\nDistribute data-only brand/theme packs, never executable plugin code. Manifest: ID, numeric/schema version, license, author, base dependencies, target compatibility, content hashes and provenance. Validate limits, integrity, dependency cycles and trust before installation. Marketplace signing, retrieval and migration are planned, not implemented here.\n");
        Write("09-RuntimeThemeEngineArchitecture", "Runtime Theme Engine Architecture", "## Current flow\n\nValidated document -> resolve snapshot -> register/apply -> ThemeChanged -> existing control hooks. Reload parses fully before replacing the active theme; invalid reload retains last-known-good appearance.\n\n## Ownership\n\nApplications must call apply/reload on their UI context. Controls must unsubscribe on disposal and resolve appearance through platform-specific typed adapters. The current manager is not a concurrent multi-UI-thread service.\n\n## Remaining runtime work\n\nDebounced file watching; ordered UI-thread notifications; OS accent/light/dark/high-contrast precedence; per-window and per-control scopes; safe font/GDI resource ownership; layout invalidation on typography/density changes; latency and leak tests.\n");
        Write("10-DemoCenterModernization", "Demo Center Modernization Plan", "## Existing owning application\n\nExtend EnterpriseUI.WinFormsDemo, not the separate TestDataGrid sample. Keep dense work-focused navigation and inspectable controls.\n\n## Explorer views\n\nControl, Feature, Theme, API, Property, Event, Accessibility, Performance, Documentation, Architecture, Source and Learning explorers must share catalog IDs and evidence references.\n\n## Live configuration\n\nOne typed runtime configuration drives the demo, C# source, Designer source, fluent API, JSON and theme configuration. Changes must update all views; compile and instantiate generated output in an isolated validation harness. Theme playground must edit tokens/colors/fonts/spacing/density and support import/export and source output.\n\n## Layout gates\n\n1920x1080 at 100%, 125%, 150%, 200% DPI; additional 1366x768 and 2560x1440 cases. Avoid unnecessary page scrolling; use overflow for toolbars and deliberate scrolling only for inherently scrollable data. Check text bounds, focus order and images.\n");
        var documentation = new StringBuilder("This is a discovery report, not certification: headings/text mentions do not prove accurate examples, complete APIs or accessible behavior.\n\n| Vendor control/family | Missing required sections | Referencing documents |\n|---|---|---|\n");
        foreach (var row in coverage) documentation.AppendLine($"| {Cell(row.Vendor.Vendor + " / " + row.Vendor.Name)} | {Cell(string.Join(", ", row.MissingDocumentationSections))} | {Cell(string.Join(", ", row.Documentation))} |");
        Write("11-DocumentationCoverage", "Documentation Coverage Report", documentation.ToString());
        Write("12-FeatureParityRoadmap", "EnterpriseUI Feature Parity Roadmap", "## Release gates\n\n1. Freeze versioned vendor inventories; classify all concrete controls, models, tools, variants and palettes. Obtain permitted runtime observations for unpublished metrics.\n2. Complete typed appearance contracts and original theme profiles; validate every visual state and DPI variant.\n3. Close and test grid feature gaps, prioritizing data integrity, editing, filtering, RTL, accessibility and export.\n4. Implement missing controls category by category with rendering, API/events, input, accessibility, localization, designers, demos and docs.\n5. Complete explorer/playground and shared generated-configuration pipeline.\n6. Publish reproducible QA reports and release only features that meet all gates.\n\n## QA dimensions\n\nTheme, control, feature, documentation, demo, accessibility and performance coverage each need a denominator, version, evidence path, execution result and environment. No global parity percentage is assigned.\n\n## Exit condition\n\nComparable quality must be demonstrated through workflow, visual, accessibility, designer, generated-code and performance results. The complete mission remains open: this release supplies audit infrastructure and theme lifecycle improvements, not all missing control implementations or certified theme equivalents.\n");
        File.WriteAllText(Path.Combine(outputDirectory, "Coverage.json"), JsonSerializer.Serialize(new
        {
            VendorEntries = coverage.Count, ConcreteWinFormsTypes = types.Count(type => type.Kind == "Concrete WinForms control"),
            DemoOnlyWinFormsTypes = types.Count(type => type.Kind == "Demo-only WinForms control"),
            EntriesWithoutWinFormsEvidence = missing.Length, MissingDocumentationRows = coverage.Count(row => row.MissingDocumentationSections.Count > 0),
            ThemeCoverage = "Visual parity not verified", FeatureCoverage = "Grid catalog recorded; competitive tests pending",
            DemoCoverage = "Source references only; workflow verification pending", AccessibilityCoverage = "Theme hooks/provider overrides inspected; conformance not certified",
            PerformanceCoverage = "No competitive benchmark result accepted", InventoryComplete = false, ParityCertified = false
        }, jsonOptions));
        var reportLinks = Directory.GetFiles(outputDirectory, "*.md").Select(Path.GetFileName)
            .Where(name => name != null && name.Length > 2 && char.IsDigit(name[0]) && char.IsDigit(name[1]))
            .Order(StringComparer.Ordinal).Select(name => $"- [{name}]({name})");
        File.WriteAllText(Path.Combine(outputDirectory, "README.md"), Intro("EnterpriseUI Parity Program") + "This directory contains the twelve requested reports plus machine-readable Evidence.json and Coverage.json. Reports are generated from current source metadata and dated vendor references. Inventories explicitly retain incompleteness and unverified findings.\n\n" + string.Join("\n", reportLinks) + "\n\nRegenerate from the repository root:\n\n```powershell\ndotnet run --configuration Release --project samples/EnterpriseUI.WinFormsDemo -- --generate-parity-audit . docs/Architecture/Parity\n```\n\nVerify generated themes and native preview behavior:\n\n```powershell\ndotnet run --configuration Release --project samples/EnterpriseUI.WinFormsDemo -- --verify-themes artifacts/parity-verification\n```\n");
    }
}