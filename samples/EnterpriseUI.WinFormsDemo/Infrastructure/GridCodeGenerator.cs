using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EnterpriseUI.Data;
using EnterpriseUI.Grids;
using EnterpriseUI.WinForms;

namespace EnterpriseUI.WinFormsDemo.Infrastructure;

/// <summary>
/// Generates compilable source from the live state of an <see cref="EnterpriseDataGrid{TItem}"/>: imperative C#, WinForms Designer code,
/// the fluent <see cref="GridBuilder{TItem}"/> API, a JSON configuration and a complete exportable example. Only values that differ
/// from a fresh grid are emitted, so the output reflects what was changed in the demo.
/// </summary>
public static class GridCodeGenerator
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static readonly string[] FlagNames =
    {
        "SelectionMode", "EditMode", "PagingMode", "PageSize", "EnableVirtualization", "EnableGrouping", "EnableFiltering", "EnableSorting",
        "EnableMultiColumnSorting", "EnableAlternateRows", "EnableMasterDetail", "ShowGroupPanel", "EnableSearchPanel", "EnableQuickFilterRow",
        "GroupsExpandedByDefault", "ShowGroupFooters", "RejectInvalidEdits", "ClipboardCopyMode", "ItemHeight", "ShowFilterRow", "ShowSummaryFooter",
        "ShowBandedHeaders", "ShowConditionalDataBars", "FillColumnsToWidth", "AutoSizeMode", "ShowColumnSummaries", "ShowPager", "ShowSearchPanel",
        "AllowRowDrag", "AllowRowDrop", "ObserveItemChanges", "AllowRowDelete", "AriaLabel"
    };

    private static readonly string[] DedicatedFlags = { "SelectionMode", "EditMode", "PagingMode", "PageSize", "AriaLabel" };

    private static readonly string[] ColumnPropertyNames =
    {
        "Visible", "Sortable", "Filterable", "Groupable", "Resizable", "Reorderable", "ReadOnly", "PinPosition", "Alignment", "ColumnType",
        "FormatString", "Tooltip", "MinWidth", "MaxWidth", "FilterType", "AllowFilterSearch", "AllowColumnChooser", "CanHide", "Searchable",
        "Expression", "RunningTotalOf"
    };

    private static readonly string[] RulePropertyNames =
    {
        "Name", "ColumnField", "RuleType", "Operator", "Value1", "Value2", "BackgroundColorHex", "ForegroundColorHex", "ApplyToRow", "Enabled"
    };

    public static bool TryGenerate(object? target, DemoItem? demo, CodeOutputFormat format, out string code)
    {
        code = string.Empty;
        if (format is CodeOutputFormat.ThemeConfig or CodeOutputFormat.DependencyInjection) return false;

        var type = target?.GetType();
        if (type == null || !type.IsGenericType || type.GetGenericTypeDefinition() != typeof(EnterpriseDataGrid<>)) return false;

        var method = typeof(GridCodeGenerator).GetMethod(nameof(GenerateTyped), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(type.GetGenericArguments()[0]);
        try
        {
            code = (string)method.Invoke(null, new[] { target, demo, format })!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            code = $"// Code generation failed: {ex.InnerException.Message}";
        }
        return true;
    }

    /// <summary>Generates the requested format for any grid; used by tests and the code export tooling.</summary>
    public static string Generate<TItem>(EnterpriseDataGrid<TItem> grid, DemoItem? demo, CodeOutputFormat format) => GenerateTyped(grid, demo, format);

    private static string GenerateTyped<TItem>(EnterpriseDataGrid<TItem> grid, DemoItem? demo, CodeOutputFormat format)
    {
        var snapshot = Capture(grid, demo);
        return format switch
        {
            CodeOutputFormat.CSharpImperative => GenerateCSharp(snapshot),
            CodeOutputFormat.WinFormsDesigner => GenerateDesigner(snapshot),
            CodeOutputFormat.FluentBuilder => GenerateFluent(snapshot),
            CodeOutputFormat.JsonConfig => GenerateJson(snapshot),
            CodeOutputFormat.XmlConfig => JsonToXml(GenerateJson(snapshot)),
            CodeOutputFormat.ControlStateConfig => GridLayoutSerializer.ToJson(snapshot.Profile),
            CodeOutputFormat.ExportableExample => GenerateExportable(snapshot),
            _ => GenerateCSharp(snapshot)
        };
    }

    // ------------------------------------------------------------------
    // Snapshot
    // ------------------------------------------------------------------

    private sealed class Snapshot
    {
        public string Namespace = "Generated";
        public string Title = "Grid";
        public Type ItemType = typeof(object);
        public string ItemName => ItemType.Name;
        public List<GridColumn> Columns = new();
        public List<GridColumnBand> Bands = new();
        public DataSourceRequest Request = new();
        public Dictionary<string, string> QuickFilters = new();
        public List<GroupSummaryDescriptor> GroupSummaries = new();
        public List<GridFormatRule> Rules = new();
        public List<GridHierarchyRelation> Relations = new();
        public List<(string Name, object? Value)> Flags = new();
        public string? Search;
        public List<object> SampleRows = new();
        public ColumnLayoutProfile Profile = new();
        public GridColumn DefaultColumn = new();
    }

    private static Snapshot Capture<TItem>(EnterpriseDataGrid<TItem> grid, DemoItem? demo)
    {
        using var fresh = new EnterpriseDataGrid<TItem>();
        var snapshot = new Snapshot
        {
            Namespace = "Generated." + ToPascal(demo?.Id ?? "Grid"),
            Title = demo?.Title ?? "EnterpriseUI DataGrid",
            ItemType = typeof(TItem),
            Columns = grid.Columns.ToList(),
            Bands = grid.Bands.ToList(),
            QuickFilters = new Dictionary<string, string>(grid.QuickFilters),
            GroupSummaries = grid.GroupSummaries.ToList(),
            Rules = grid.FormatRules.ToList(),
            Relations = grid.Relations.ToList(),
            Search = grid.SearchPanelText,
            SampleRows = grid.RenderedItems.Where(i => i != null).Take(5).Cast<object>().ToList(),
            Profile = grid.CreateLayoutProfile(demo?.Id ?? "Default")
        };
        snapshot.Request.Filters.AddRange(grid.CurrentRequest.Filters);
        snapshot.Request.Sorts.AddRange(grid.CurrentRequest.Sorts);
        snapshot.Request.Groups.AddRange(grid.CurrentRequest.Groups);

        foreach (var name in FlagNames)
        {
            var property = typeof(EnterpriseDataGrid<TItem>).GetProperty(name);
            if (property == null || !property.CanWrite) continue;
            var value = property.GetValue(grid);
            if (!Equals(value, property.GetValue(fresh))) snapshot.Flags.Add((name, value));
        }

        return snapshot;
    }

    // ------------------------------------------------------------------
    // Literals and names
    // ------------------------------------------------------------------

    private static string Quote(string value)
    {
        var sb = new StringBuilder("\"");
        foreach (var ch in value)
        {
            sb.Append(ch switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => ch.ToString()
            });
        }
        return sb.Append('"').ToString();
    }

    private static string Literal(object? value) => value switch
    {
        null => "null",
        string s => Quote(s),
        bool b => b ? "true" : "false",
        char c => $"'{c}'",
        int or long or short or byte => Convert.ToString(value, Invariant)!,
        decimal d => d.ToString(Invariant) + "m",
        double d when double.IsFinite(d) => d.ToString("R", Invariant) + "d",
        float f when float.IsFinite(f) => f.ToString("R", Invariant) + "f",
        DateTime dt => $"new DateTime({dt.Year}, {dt.Month}, {dt.Day}, {dt.Hour}, {dt.Minute}, {dt.Second}, DateTimeKind.{dt.Kind})",
        Guid g => $"Guid.Parse(\"{g}\")",
        Enum e => $"{e.GetType().Name}.{e}",
        _ => Quote(Convert.ToString(value, Invariant) ?? string.Empty)
    };

    private static string ToPascal(string id)
    {
        var parts = id.Split(new[] { '-', '_', ' ', '.' }, StringSplitOptions.RemoveEmptyEntries);
        var name = string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1)));
        if (name.Length == 0 || char.IsDigit(name[0])) name = "G" + name;
        return name;
    }

    private static string TypeName(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null) return TypeName(underlying) + "?";
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(short)) return "short";
        if (type == typeof(byte)) return "byte";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(double)) return "double";
        if (type == typeof(float)) return "float";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(string)) return "string";
        if (type == typeof(DateTime)) return "DateTime";
        if (type == typeof(Guid)) return "Guid";
        return "object";
    }

    private static string DefaultInitializer(Type type)
    {
        if (type == typeof(string)) return " = string.Empty;";
        if (type == typeof(object)) return " = string.Empty;";
        return string.Empty;
    }

    // ------------------------------------------------------------------
    // Shared pieces
    // ------------------------------------------------------------------

    private static IEnumerable<(string Name, string Value)> ColumnProperties(GridColumn column, Snapshot s)
    {
        foreach (var name in ColumnPropertyNames)
        {
            var property = typeof(GridColumn).GetProperty(name);
            if (property == null) continue;
            var value = property.GetValue(column);
            if (!Equals(value, property.GetValue(s.DefaultColumn))) yield return (name, Literal(value));
        }

        if (column.SortDirection != null) { /* sort state is emitted through SortBy */ }
    }

    private static string SummaryExpression(GridColumnSummary summary)
    {
        var text = $"new GridColumnSummary(AggregateFunction.{summary.Function}{(summary.Format != null ? ", " + Quote(summary.Format) : string.Empty)})";
        return summary.Label != null ? text + $" {{ Label = {Quote(summary.Label)} }}" : text;
    }

    private static string? RuleExpression(GridValidationRule rule)
    {
        var tail = string.Empty;
        if (rule.Message != null) tail = $" {{ Message = {Quote(rule.Message)} }}";
        return rule switch
        {
            RequiredRule => "new RequiredRule" + (tail.Length == 0 ? "()" : tail),
            RangeRule r => $"new RangeRule({Literal(r.Minimum)}, {Literal(r.Maximum)})" + tail,
            LengthRule l => $"new LengthRule({l.Minimum}, {l.Maximum})" + tail,
            _ => null
        };
    }

    private static string RuleInitializer(GridFormatRule rule)
    {
        var defaults = new GridFormatRule();
        var parts = new List<string>();
        foreach (var name in RulePropertyNames)
        {
            var property = typeof(GridFormatRule).GetProperty(name)!;
            var value = property.GetValue(rule);
            if (name == "Name" || name == "ColumnField" || !Equals(value, property.GetValue(defaults))) parts.Add($"{name} = {Literal(value)}");
        }
        return "new GridFormatRule { " + string.Join(", ", parts) + " }";
    }

    private static string ColumnExpression(GridColumn column, Snapshot s, Dictionary<string, string> bandVars, string indent)
    {
        var parts = new List<string> { $"Field = {Quote(column.Field)}" };
        if (!string.IsNullOrEmpty(column.Title)) parts.Add($"Title = {Quote(column.Title)}");
        if (column.Width != null) parts.Add($"Width = {Literal(column.Width)}");
        foreach (var (name, value) in ColumnProperties(column, s)) parts.Add($"{name} = {value}");
        if (column.BandId != null && bandVars.TryGetValue(column.BandId, out var bandVar)) parts.Add($"BandId = {bandVar}.Id");
        if (column.ComboItems is { Count: > 0 }) parts.Add($"ComboItems = new object[] {{ {string.Join(", ", column.ComboItems.Select(Literal))} }}");

        var extras = new List<string>();
        if (column.FooterSummaries.Count > 0) extras.Add($"FooterSummaries = {{ {string.Join(", ", column.FooterSummaries.Select(SummaryExpression))} }}");
        var rules = column.ValidationRules.Select(RuleExpression).Where(r => r != null).ToList();
        if (rules.Count > 0) extras.Add($"ValidationRules = {{ {string.Join(", ", rules)} }}");
        parts.AddRange(extras);

        return "new GridColumn { " + string.Join(", ", parts) + " }";
    }

    private static Dictionary<string, string> BandVariables(Snapshot s)
    {
        var result = new Dictionary<string, string>();
        var counter = 0;
        void Visit(IEnumerable<GridColumnBand> bands)
        {
            foreach (var band in bands)
            {
                result[band.Id] = $"band{++counter}";
                Visit(band.ChildBands);
            }
        }
        Visit(s.Bands);
        return result;
    }

    private static string ColumnList(string relationVar, GridHierarchyRelation relation)
    {
        _ = relationVar;
        return relation.ChildColumns.Count == 0 ? string.Empty : $"// {relation.ChildColumns.Count} child column(s) were customized";
    }

    private static IEnumerable<string> RelationStatements(Snapshot s, string gridVar, Func<GridHierarchyRelation, string> childList)
    {
        var index = 0;
        foreach (var relation in s.Relations)
        {
            index++;
            if (relation.Mode == HierarchyMode.SelfReferencing)
            {
                yield return $"{gridVar}.Engine.AddSelfReference({Quote(relation.KeyField)}, {Quote(relation.ForeignField)});";
                continue;
            }

            var childType = relation.ChildItemType?.Name ?? "object";
            var variable = $"relation{index}";
            yield return $"var {variable}Rows = new List<{childType}>(); // supply the child rows";
            yield return $"var {variable} = {gridVar}.Engine.AddRelation({Quote(relation.RelationName)}, {Quote(relation.KeyField)}, {Quote(relation.ForeignField)}, {variable}Rows, typeof({childType}));";
            if (relation.Mode != HierarchyMode.MasterDetail) yield return $"{variable}.Mode = HierarchyMode.{relation.Mode};";
            if (relation.LoadChildrenAsync != null) yield return $"// {variable} loads its children lazily through LoadChildrenAsync (delegate not serializable)";
            foreach (var line in NestedRelationStatements(relation, variable, 1)) yield return line;
            _ = childList;
        }
    }

    private static IEnumerable<string> NestedRelationStatements(GridHierarchyRelation parent, string parentVar, int depth)
    {
        var index = 0;
        foreach (var child in parent.ChildRelations)
        {
            index++;
            var variable = $"{parentVar}Child{index}";
            var childType = child.ChildItemType?.Name ?? "object";
            yield return $"var {variable} = new GridHierarchyRelation {{ RelationName = {Quote(child.RelationName)}, KeyField = {Quote(child.KeyField)}, ForeignField = {Quote(child.ForeignField)}, ChildItemType = typeof({childType}), ChildSource = new List<{childType}>() }};";
            yield return $"{parentVar}.ChildRelations.Add({variable});";
            foreach (var line in NestedRelationStatements(child, variable, depth + 1)) yield return line;
        }
    }

    private static IEnumerable<string> QueryStatements(Snapshot s, string gridVar)
    {
        foreach (var sort in s.Request.Sorts)
        {
            yield return $"{gridVar}.SortBy({Quote(sort.Member)}, ListSortDirection.{sort.SortDirection});";
        }

        foreach (var group in s.Request.Groups) yield return $"{gridVar}.GroupBy({Quote(group.Member)});";

        foreach (var (field, text) in s.QuickFilters) yield return $"{gridVar}.SetQuickFilter({Quote(field)}, {Quote(text)});";

        foreach (var column in s.Columns.Where(c => c.ExcelFilterValues.Count > 0))
        {
            yield return $"{gridVar}.ApplyExcelFilter({Quote(column.Field)}, new object[] {{ {string.Join(", ", column.ExcelFilterValues.Select(Literal))} }});";
        }

        foreach (var filter in FreeFilters(s))
        {
            yield return filter.Value2 != null
                ? $"{gridVar}.Engine.FilterBy({Quote(filter.Member)}, FilterOperator.{filter.Operator}, {Literal(filter.Value)}, {Literal(filter.Value2)});"
                : $"{gridVar}.FilterBy({Quote(filter.Member)}, FilterOperator.{filter.Operator}, {Literal(filter.Value)});";
        }

        if (!string.IsNullOrWhiteSpace(s.Search)) yield return $"{gridVar}.SearchPanelText = {Quote(s.Search)};";
    }

    private static IEnumerable<FilterDescriptor> FreeFilters(Snapshot s) =>
        s.Request.Filters.OfType<FilterDescriptor>().Where(f =>
            !s.QuickFilters.ContainsKey(f.Member) && s.Columns.All(c => !string.Equals(c.Field, f.Member, StringComparison.OrdinalIgnoreCase) || c.ExcelFilterValues.Count == 0));

    // ------------------------------------------------------------------
    // C# imperative
    // ------------------------------------------------------------------

    private static List<string> ImperativeStatements(Snapshot s, string gridVar)
    {
        var lines = new List<string>();
        var bandVars = BandVariables(s);

        void EmitBands(IEnumerable<GridColumnBand> bands, string? parentVar)
        {
            foreach (var band in bands)
            {
                var variable = bandVars[band.Id];
                lines.Add($"var {variable} = new GridColumnBand {{ Title = {Quote(band.Title)} }};");
                lines.Add(parentVar == null ? $"{gridVar}.Bands.Add({variable});" : $"{parentVar}.ChildBands.Add({variable});");
                EmitBands(band.ChildBands, variable);
            }
        }
        EmitBands(s.Bands, null);

        foreach (var column in s.Columns) lines.Add($"{gridVar}.Columns.Add({ColumnExpression(column, s, bandVars, string.Empty)});");

        foreach (var (name, value) in s.Flags) lines.Add($"{gridVar}.{name} = {Literal(value)};");
        foreach (var rule in s.Rules) lines.Add($"{gridVar}.FormatRules.Add({RuleInitializer(rule)});");
        foreach (var summary in s.GroupSummaries) lines.Add($"{gridVar}.GroupSummaries.Add(new GroupSummaryDescriptor({Quote(summary.Member)}, AggregateFunction.{summary.Function}, {Quote(summary.FormatString)}));");
        lines.AddRange(RelationStatements(s, gridVar, ColumnListSelector));
        lines.AddRange(QueryStatements(s, gridVar));
        return lines;
    }

    private static string ColumnListSelector(GridHierarchyRelation relation) => ColumnList(string.Empty, relation);

    private static string Header(Snapshot s, string kind) =>
        $"// {kind} generated from the live grid state ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) of \"{s.Title}\".\n" +
        "// Only settings that differ from a new EnterpriseDataGrid are listed.\n" +
        $"// The item type {s.ItemName} must have the properties named in the columns (see the exportable example for a model class).\n";

    private const string Usings =
        "using System;\nusing System.Collections.Generic;\nusing System.ComponentModel;\nusing System.Windows.Forms;\nusing EnterpriseUI.Data;\nusing EnterpriseUI.Grids;\nusing EnterpriseUI.WinForms;\n";

    private static string GenerateCSharp(Snapshot s)
    {
        var sb = new StringBuilder();
        sb.Append(Header(s, "Imperative C#")).Append(Usings).AppendLine().AppendLine($"namespace {s.Namespace};").AppendLine();
        sb.AppendLine("public static class GeneratedGrid");
        sb.AppendLine("{");
        sb.AppendLine($"    public static EnterpriseDataGrid<{s.ItemName}> Create(IEnumerable<{s.ItemName}> data)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var grid = new EnterpriseDataGrid<{s.ItemName}> {{ Dock = DockStyle.Fill }};");
        foreach (var line in ImperativeStatements(s, "grid")) sb.Append("        ").AppendLine(line);
        sb.AppendLine("        grid.DataSource = data;");
        sb.AppendLine("        return grid;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Designer
    // ------------------------------------------------------------------

    private static string GenerateDesigner(Snapshot s)
    {
        var sb = new StringBuilder();
        sb.Append(Header(s, "WinForms Designer")).Append(Usings).AppendLine().AppendLine($"namespace {s.Namespace};").AppendLine();
        sb.AppendLine("public partial class GeneratedGridForm : Form");
        sb.AppendLine("{");
        sb.AppendLine("    public GeneratedGridForm()");
        sb.AppendLine("    {");
        sb.AppendLine("        InitializeComponent();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public IEnumerable<{s.ItemName}>? Data");
        sb.AppendLine("    {");
        sb.AppendLine("        get => grid.DataSource;");
        sb.AppendLine("        set => grid.DataSource = value;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("partial class GeneratedGridForm");
        sb.AppendLine("{");
        sb.AppendLine("    private IContainer? components = null;");
        sb.AppendLine($"    private EnterpriseDataGrid<{s.ItemName}> grid = null!;");
        sb.AppendLine();
        sb.AppendLine("    protected override void Dispose(bool disposing)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (disposing && components != null)");
        sb.AppendLine("        {");
        sb.AppendLine("            components.Dispose();");
        sb.AppendLine("        }");
        sb.AppendLine("        base.Dispose(disposing);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private void InitializeComponent()");
        sb.AppendLine("    {");
        sb.AppendLine($"        this.grid = new EnterpriseDataGrid<{s.ItemName}>();");
        sb.AppendLine("        this.SuspendLayout();");
        sb.AppendLine("        this.grid.Name = \"grid\";");
        sb.AppendLine("        this.grid.Dock = DockStyle.Fill;");
        sb.AppendLine("        this.grid.TabIndex = 0;");
        foreach (var line in ImperativeStatements(s, "this.grid")) sb.Append("        ").AppendLine(line);
        sb.AppendLine("        this.AutoScaleMode = AutoScaleMode.Dpi;");
        sb.AppendLine("        this.ClientSize = new System.Drawing.Size(1100, 640);");
        sb.AppendLine("        this.Controls.Add(this.grid);");
        sb.AppendLine("        this.Name = \"GeneratedGridForm\";");
        sb.AppendLine($"        this.Text = {Quote(s.Title)};");
        sb.AppendLine("        this.ResumeLayout(false);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Fluent
    // ------------------------------------------------------------------

    private static List<string> FluentCalls(Snapshot s)
    {
        var calls = new List<string>();
        var bandVars = new Dictionary<string, string>();
        var ariaLabel = s.Flags.FirstOrDefault(f => f.Name == "AriaLabel");
        if (ariaLabel.Name != null) calls.Add($".Label({Literal(ariaLabel.Value)})");

        foreach (var column in s.Columns)
        {
            var extras = ColumnProperties(column, s).Select(p => $"c.{p.Name} = {p.Value};").ToList();
            if (column.ComboItems is { Count: > 0 }) extras.Add($"c.ComboItems = new object[] {{ {string.Join(", ", column.ComboItems.Select(Literal))} }};");
            foreach (var summary in column.FooterSummaries) extras.Add($"c.FooterSummaries.Add({SummaryExpression(summary)});");
            foreach (var rule in column.ValidationRules.Select(RuleExpression).Where(r => r != null)) extras.Add($"c.ValidationRules.Add({rule});");

            var args = $"{Quote(column.Field)}, {(string.IsNullOrEmpty(column.Title) ? "null" : Quote(column.Title))}, {(column.Width != null ? Literal(column.Width) : "null")}";
            calls.Add(extras.Count == 0 ? $".Column({args})" : $".Column({args}, c => {{ {string.Join(" ", extras)} }})");
        }

        void EmitBands(IEnumerable<GridColumnBand> bands, string? parentTitle)
        {
            foreach (var band in bands)
            {
                var fields = s.Columns.Where(c => c.BandId == band.Id).Select(c => Quote(c.Field)).ToList();
                var list = fields.Count == 0 ? string.Empty : ", " + string.Join(", ", fields);
                calls.Add(parentTitle == null ? $".Band({Quote(band.Title)}{list})" : $".NestedBand({Quote(parentTitle)}, {Quote(band.Title)}{list})");
                EmitBands(band.ChildBands, band.Title);
            }
        }
        EmitBands(s.Bands, null);

        var flags = s.Flags.ToDictionary(f => f.Name, f => f.Value);
        if (flags.TryGetValue("PagingMode", out var paging))
        {
            calls.Add(flags.TryGetValue("PageSize", out var size) ? $".Paging({Literal(paging)}, {Literal(size)})" : $".Paging({Literal(paging)})");
        }
        else if (flags.TryGetValue("PageSize", out var onlySize))
        {
            calls.Add($".Configure(g => g.PageSize = {Literal(onlySize)})");
        }

        if (flags.TryGetValue("SelectionMode", out var selection)) calls.Add($".Selection({Literal(selection)})");
        if (flags.TryGetValue("EditMode", out var edit)) calls.Add($".Editing({Literal(edit)})");

        foreach (var sort in s.Request.Sorts) calls.Add($".SortBy({Quote(sort.Member)}, ListSortDirection.{sort.SortDirection})");
        foreach (var group in s.Request.Groups) calls.Add($".GroupBy({Quote(group.Member)})");
        foreach (var (field, text) in s.QuickFilters) calls.Add($".QuickFilter({Quote(field)}, {Quote(text)})");
        foreach (var filter in FreeFilters(s).Where(f => f.Value2 == null)) calls.Add($".Filter({Quote(filter.Member)}, FilterOperator.{filter.Operator}, {Literal(filter.Value)})");
        if (!string.IsNullOrWhiteSpace(s.Search)) calls.Add($".Search({Quote(s.Search)})");
        foreach (var rule in s.Rules) calls.Add($".FormatRule({RuleInitializer(rule)})");
        foreach (var summary in s.GroupSummaries) calls.Add($".GroupSummary({Quote(summary.Member)}, AggregateFunction.{summary.Function}, {Quote(summary.FormatString)})");

        var remaining = s.Flags.Where(f => !DedicatedFlags.Contains(f.Name)).ToList();
        var configure = new List<string>();
        configure.AddRange(remaining.Select(f => $"g.{f.Name} = {Literal(f.Value)};"));
        configure.AddRange(s.Request.Filters.OfType<FilterDescriptor>().Where(f => f.Value2 != null).Select(f => $"g.Engine.FilterBy({Quote(f.Member)}, FilterOperator.{f.Operator}, {Literal(f.Value)}, {Literal(f.Value2)});"));
        configure.AddRange(s.Columns.Where(c => c.ExcelFilterValues.Count > 0).Select(c => $"g.ApplyExcelFilter({Quote(c.Field)}, new object[] {{ {string.Join(", ", c.ExcelFilterValues.Select(Literal))} }});"));
        configure.AddRange(RelationStatements(s, "g", ColumnListSelector));
        if (configure.Count > 0) calls.Add($".Configure(g =>\n    {{\n{string.Join("\n", configure.Select(l => "        " + l))}\n    }})");

        return calls;
    }

    private static string GenerateFluent(Snapshot s)
    {
        var sb = new StringBuilder();
        sb.Append(Header(s, "Fluent API")).Append(Usings).AppendLine().AppendLine($"namespace {s.Namespace};").AppendLine();
        sb.AppendLine("public static class GeneratedGridFluent");
        sb.AppendLine("{");
        sb.AppendLine($"    public static EnterpriseDataGrid<{s.ItemName}> Create(IEnumerable<{s.ItemName}> data) =>");
        sb.AppendLine($"        GridBuilder<{s.ItemName}>.Create()");
        foreach (var call in FluentCalls(s)) sb.Append("            ").AppendLine(call.Replace("\n", "\n            "));
        sb.AppendLine("            .DataSource(data)");
        sb.AppendLine("            .Build();");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // JSON
    // ------------------------------------------------------------------

    private static JsonNode? JsonValue(object? value) => value switch
    {
        null => null,
        Enum e => JsonValue(e.ToString()),
        string or bool or int or long or short or byte or decimal or double or float => JsonSerializer.SerializeToNode(value),
        DateTime dt => JsonValue(dt.ToString("O", Invariant)),
        _ => JsonValue(Convert.ToString(value, Invariant))
    };

    private static string GenerateJson(Snapshot s)
    {
        var root = new JsonObject
        {
            ["schema"] = "enterpriseui.grid/1",
            ["title"] = s.Title,
            ["itemType"] = s.ItemType.Name
        };

        var options = new JsonObject();
        foreach (var (name, value) in s.Flags) options[name] = JsonValue(value);
        root["options"] = options;

        var columns = new JsonArray();
        foreach (var column in s.Columns)
        {
            var node = new JsonObject { ["field"] = column.Field, ["title"] = column.Title, ["width"] = JsonValue(column.Width) };
            foreach (var name in ColumnPropertyNames)
            {
                var property = typeof(GridColumn).GetProperty(name)!;
                var value = property.GetValue(column);
                if (!Equals(value, property.GetValue(s.DefaultColumn))) node[ToCamel(name)] = JsonValue(value);
            }
            if (column.BandId != null) node["band"] = FindBandTitle(s.Bands, column.BandId);
            if (column.FooterSummaries.Count > 0)
            {
                node["summaries"] = new JsonArray(column.FooterSummaries.Select(f => (JsonNode?)new JsonObject { ["function"] = f.Function.ToString(), ["format"] = f.Format }).ToArray());
            }
            if (column.ValidationRules.Count > 0)
            {
                node["validation"] = new JsonArray(column.ValidationRules.Select(r => (JsonNode?)new JsonObject { ["rule"] = r.GetType().Name, ["message"] = r.Message }).ToArray());
            }
            columns.Add(node);
        }
        root["columns"] = columns;

        root["bands"] = new JsonArray(s.Bands.Select(BandNode).ToArray());
        root["sorts"] = new JsonArray(s.Request.Sorts.Select(x => (JsonNode?)new JsonObject { ["field"] = x.Member, ["direction"] = x.SortDirection.ToString() }).ToArray());
        root["groups"] = new JsonArray(s.Request.Groups.Select(x => (JsonNode?)new JsonObject { ["field"] = x.Member, ["direction"] = x.SortDirection.ToString() }).ToArray());
        root["filters"] = new JsonArray(s.Request.Filters.OfType<FilterDescriptor>().Select(f => (JsonNode?)new JsonObject
        {
            ["field"] = f.Member, ["operator"] = f.Operator.ToString(), ["value"] = JsonValue(f.Value), ["value2"] = JsonValue(f.Value2)
        }).ToArray());
        root["quickFilters"] = JsonSerializer.SerializeToNode(s.QuickFilters);
        root["search"] = s.Search;
        root["groupSummaries"] = new JsonArray(s.GroupSummaries.Select(g => (JsonNode?)new JsonObject { ["field"] = g.Member, ["function"] = g.Function.ToString(), ["format"] = g.FormatString }).ToArray());
        root["formatRules"] = new JsonArray(s.Rules.Select(r =>
        {
            var node = new JsonObject();
            foreach (var name in RulePropertyNames) node[ToCamel(name)] = JsonValue(typeof(GridFormatRule).GetProperty(name)!.GetValue(r));
            return (JsonNode?)node;
        }).ToArray());
        root["relations"] = new JsonArray(s.Relations.Select(RelationNode).ToArray());
        root["layoutProfile"] = JsonNode.Parse(GridLayoutSerializer.ToJson(s.Profile));

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ToCamel(string name) => char.ToLowerInvariant(name[0]) + name.Substring(1);

    private static string JsonToXml(string json)
    {
        static System.Xml.Linq.XElement Convert(string name, JsonNode? node)
        {
            var element = new System.Xml.Linq.XElement(System.Xml.XmlConvert.EncodeLocalName(name));
            switch (node)
            {
                case JsonObject obj:
                    foreach (var (key, child) in obj) element.Add(Convert(key, child));
                    break;
                case JsonArray array:
                    foreach (var child in array) element.Add(Convert("item", child));
                    break;
                case null:
                    break;
                default:
                    element.Value = node.ToString();
                    break;
            }
            return element;
        }

        return new System.Xml.Linq.XDocument(new System.Xml.Linq.XDeclaration("1.0", "utf-8", null), Convert("grid", JsonNode.Parse(json))).ToString();
    }

    private static string? FindBandTitle(IEnumerable<GridColumnBand> bands, string id)
    {
        foreach (var band in bands)
        {
            if (band.Id == id) return band.Title;
            var nested = FindBandTitle(band.ChildBands, id);
            if (nested != null) return nested;
        }
        return null;
    }

    private static JsonNode? BandNode(GridColumnBand band) => new JsonObject
    {
        ["title"] = band.Title,
        ["children"] = new JsonArray(band.ChildBands.Select(BandNode).ToArray())
    };

    private static JsonNode? RelationNode(GridHierarchyRelation relation) => new JsonObject
    {
        ["name"] = relation.RelationName,
        ["mode"] = relation.Mode.ToString(),
        ["keyField"] = relation.KeyField,
        ["foreignField"] = relation.ForeignField,
        ["childType"] = relation.ChildItemType?.Name,
        ["children"] = new JsonArray(relation.ChildRelations.Select(RelationNode).ToArray())
    };

    // ------------------------------------------------------------------
    // Exportable example
    // ------------------------------------------------------------------

    private static IEnumerable<Type> ModelTypes(Snapshot s)
    {
        var seen = new HashSet<Type> { s.ItemType };
        yield return s.ItemType;

        IEnumerable<Type> Visit(IEnumerable<GridHierarchyRelation> relations)
        {
            foreach (var relation in relations)
            {
                if (relation.ChildItemType != null && seen.Add(relation.ChildItemType)) yield return relation.ChildItemType;
                foreach (var nested in Visit(relation.ChildRelations)) yield return nested;
            }
        }

        foreach (var type in Visit(s.Relations)) yield return type;
    }

    private static void AppendModel(StringBuilder sb, Type type)
    {
        sb.AppendLine($"public sealed class {type.Name}");
        sb.AppendLine("{");
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0))
        {
            var typeName = TypeName(property.PropertyType);
            sb.AppendLine($"    public {typeName} {property.Name} {{ get; set; }}{DefaultInitializer(property.PropertyType)}");
        }
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static string SampleRow(object row)
    {
        var parts = row.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && TypeName(p.PropertyType) != "object")
            .Select(p => (Property: p, Value: p.GetValue(row)))
            .Where(x => x.Value != null)
            .Select(x => $"{x.Property.Name} = {Literal(x.Value)}");
        return $"new {row.GetType().Name} {{ {string.Join(", ", parts)} }}";
    }

    private static string GenerateExportable(Snapshot s)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// ======================================================================");
        sb.AppendLine($"// Complete example generated from the live grid state of \"{s.Title}\".");
        sb.AppendLine("// Add this file to a Windows Forms project that references EnterpriseUI.WinForms:");
        sb.AppendLine("//   <Project Sdk=\"Microsoft.NET.Sdk\">");
        sb.AppendLine("//     <PropertyGroup><OutputType>WinExe</OutputType><TargetFramework>net8.0-windows</TargetFramework><UseWindowsForms>true</UseWindowsForms></PropertyGroup>");
        sb.AppendLine("//     <ItemGroup><ProjectReference Include=\"EnterpriseUI.WinForms.csproj\" /></ItemGroup>");
        sb.AppendLine("//   </Project>");
        sb.AppendLine("// ======================================================================");
        sb.Append(Usings).AppendLine().AppendLine($"namespace {s.Namespace};").AppendLine();

        foreach (var type in ModelTypes(s)) AppendModel(sb, type);

        sb.AppendLine("public sealed class ExampleForm : Form");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly EnterpriseDataGrid<{s.ItemName}> _grid;");
        sb.AppendLine();
        sb.AppendLine("    public ExampleForm()");
        sb.AppendLine("    {");
        sb.AppendLine($"        Text = {Quote(s.Title)};");
        sb.AppendLine("        ClientSize = new System.Drawing.Size(1100, 640);");
        sb.AppendLine($"        _grid = GridBuilder<{s.ItemName}>.Create()");
        foreach (var call in FluentCalls(s)) sb.Append("            ").AppendLine(call.Replace("\n", "\n            "));
        sb.AppendLine("            .DataSource(SampleData())");
        sb.AppendLine("            .Build();");
        sb.AppendLine("        Controls.Add(_grid);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Replace with your own data; the first rows below were taken from the live grid.</summary>");
        sb.AppendLine($"    private static List<{s.ItemName}> SampleData() => new()");
        sb.AppendLine("    {");
        foreach (var row in s.SampleRows) sb.AppendLine($"        {SampleRow(row)},");
        sb.AppendLine("    };");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("public static class Program");
        sb.AppendLine("{");
        sb.AppendLine("    [STAThread]");
        sb.AppendLine("    public static void Main()");
        sb.AppendLine("    {");
        sb.AppendLine("        Application.EnableVisualStyles();");
        sb.AppendLine("        Application.SetCompatibleTextRenderingDefault(false);");
        sb.AppendLine("        Application.Run(new ExampleForm());");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("// To restore the full layout (column order, widths, pins, sorts, groups, filters) saved with the JSON configuration:");
        sb.AppendLine("//   grid.ApplyLayoutProfile(GridLayoutSerializer.FromJson(json[\"layoutProfile\"].ToJsonString()));");
        return sb.ToString();
    }
}
