using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;

namespace EnterpriseUI.Grids;

/// <summary>
/// Resolved grid colors as #RRGGBB, derived from the active theme's design tokens. Every grid visual, including the new
/// selection, search highlight, error and focus indicators, takes its color from here, so all themes (Fluent, Office,
/// Material, dark, high contrast) are supported without feature-specific colors.
/// </summary>
public sealed record GridPalette(
    string Surface,
    string Alt,
    string Header,
    string Border,
    string Text,
    string Muted,
    string Primary,
    string PrimaryText,
    string Danger,
    string Warning,
    string Success,
    string SelectionBack,
    string SelectionText,
    string HoverBack,
    string FocusRing,
    string HighlightBack,
    string HighlightText,
    string ErrorBack,
    bool HighContrast)
{
    public static GridPalette FromTheme(ITheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        var highContrast = theme.Scheme == ColorScheme.HighContrast || theme.Type == ThemeType.HighContrast;

        var surface = Resolve(theme, DesignTokens.Surface, "#FFFFFF", null);
        var text = EnsureContrast(Resolve(theme, DesignTokens.TextColor, "#000000", surface), surface, 7);
        var alt = Resolve(theme, DesignTokens.SurfaceAlt, Blend(surface, text, 0.04), surface);
        var primary = Resolve(theme, DesignTokens.Primary, "#0078D4", surface);
        var primaryText = Resolve(theme, DesignTokens.PrimaryText, BestTextOn(primary), primary);
        var danger = EnsureContrast(EnsureContrast(Resolve(theme, DesignTokens.Danger, "#D13438", surface), surface, 3.0), alt, 3.0);
        var warning = Resolve(theme, DesignTokens.Warning, "#FFB900", surface);
        var success = Resolve(theme, DesignTokens.Success, "#107C10", surface);

        var border = highContrast ? text : Resolve(theme, DesignTokens.BorderColor, Blend(surface, text, 0.18), surface);
        var muted = EnsureContrast(Resolve(theme, DesignTokens.TextMuted, Blend(surface, text, 0.6), surface), alt, 4.5);
        muted = EnsureContrast(muted, surface, 4.5);

        var selectionBack = highContrast ? primary : Blend(surface, primary, 0.22);
        var selectionText = highContrast ? EnsureContrast(primaryText, primary, 4.5) : EnsureContrast(text, selectionBack, 4.5);
        var hover = highContrast ? alt : Blend(surface, primary, 0.09);
        var highlightBack = highContrast ? warning : Blend(surface, warning, 0.55);
        var errorBack = highContrast ? surface : Blend(surface, danger, 0.14);

        return new GridPalette(
            surface, alt, Resolve(theme, DesignTokens.SurfaceAlt, alt, surface), border, text, muted, primary, primaryText, danger, warning, success,
            selectionBack, selectionText, hover,
            highContrast ? text : primary,
            highlightBack, BestTextOn(highlightBack), errorBack, highContrast);
    }

    // ------------------------------------------------------------------
    // Color math
    // ------------------------------------------------------------------

    /// <summary>Reads a token as #RRGGBB: accepts #rgb, #rrggbb, #rrggbbaa, rgb() and rgba() (composited over <paramref name="background"/>).</summary>
    public static string Resolve(ITheme theme, string token, string fallback, string? background)
    {
        var value = theme.GetToken(token);
        return string.IsNullOrWhiteSpace(value) || !TryParse(value, background ?? "#FFFFFF", out var hex) ? fallback : hex;
    }

    public static bool TryParse(string value, string background, out string hex)
    {
        hex = string.Empty;
        value = value.Trim();

        if (value.StartsWith('#'))
        {
            var digits = value[1..];
            if (digits.Length == 3) digits = string.Concat(digits.Select(c => $"{c}{c}"));
            if (digits.Length is not (6 or 8) || !digits.All(Uri.IsHexDigit)) return false;

            var r = Convert.ToInt32(digits[..2], 16);
            var g = Convert.ToInt32(digits[2..4], 16);
            var b = Convert.ToInt32(digits[4..6], 16);
            var alpha = digits.Length == 8 ? Convert.ToInt32(digits[6..8], 16) / 255.0 : 1;
            hex = alpha >= 1 ? Format(r, g, b) : Composite(r, g, b, alpha, background);
            return true;
        }

        var open = value.IndexOf('(');
        var close = value.LastIndexOf(')');
        if (open > 0 && close > open && value[..open].Trim().ToLowerInvariant() is "rgb" or "rgba")
        {
            var parts = value[(open + 1)..close].Split(new[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length is 3 or 4 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var r) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var g) &&
                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
            {
                var alpha = 1.0;
                if (parts.Length == 4 && !double.TryParse(parts[3].TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out alpha)) return false;
                if (parts.Length == 4 && parts[3].EndsWith('%')) alpha /= 100;
                hex = Composite((int)Math.Round(r), (int)Math.Round(g), (int)Math.Round(b), Math.Clamp(alpha, 0, 1), background);
                return true;
            }
        }

        return false;
    }

    private static string Composite(int r, int g, int b, double alpha, string background)
    {
        TryRgb(background, out var br, out var bg, out var bb);
        return Format(
            (int)Math.Round(r * alpha + br * (1 - alpha)),
            (int)Math.Round(g * alpha + bg * (1 - alpha)),
            (int)Math.Round(b * alpha + bb * (1 - alpha)));
    }

    public static string Blend(string from, string to, double amount)
    {
        TryRgb(from, out var r1, out var g1, out var b1);
        TryRgb(to, out var r2, out var g2, out var b2);
        amount = Math.Clamp(amount, 0, 1);
        return Format((int)Math.Round(r1 + (r2 - r1) * amount), (int)Math.Round(g1 + (g2 - g1) * amount), (int)Math.Round(b1 + (b2 - b1) * amount));
    }

    public static bool TryRgb(string hex, out int r, out int g, out int b)
    {
        r = g = b = 0;
        if (string.IsNullOrEmpty(hex)) return false;
        var digits = hex.TrimStart('#');
        if (digits.Length < 6 || !digits.Take(6).All(Uri.IsHexDigit)) return false;
        r = Convert.ToInt32(digits[..2], 16);
        g = Convert.ToInt32(digits[2..4], 16);
        b = Convert.ToInt32(digits[4..6], 16);
        return true;
    }

    private static string Format(int r, int g, int b) => $"#{Math.Clamp(r, 0, 255):X2}{Math.Clamp(g, 0, 255):X2}{Math.Clamp(b, 0, 255):X2}";

    /// <summary>WCAG 2.x relative luminance.</summary>
    public static double Luminance(string hex)
    {
        TryRgb(hex, out var r, out var g, out var b);
        static double Channel(int value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);
    }

    /// <summary>WCAG contrast ratio between two colors, from 1 to 21.</summary>
    public static double ContrastRatio(string foreground, string background)
    {
        var a = Luminance(foreground);
        var b = Luminance(background);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    public static string BestTextOn(string background) =>
        ContrastRatio("#000000", background) >= ContrastRatio("#FFFFFF", background) ? "#000000" : "#FFFFFF";

    /// <summary>Keeps <paramref name="foreground"/> when it reaches the ratio on <paramref name="background"/>; otherwise moves it toward black or white until it does.</summary>
    public static string EnsureContrast(string foreground, string background, double minimumRatio)
    {
        if (ContrastRatio(foreground, background) >= minimumRatio) return foreground;

        var target = BestTextOn(background);
        for (var step = 1; step <= 20; step++)
        {
            var candidate = Blend(foreground, target, step / 20.0);
            if (ContrastRatio(candidate, background) >= minimumRatio) return candidate;
        }
        return target;
    }
}

public sealed record ContrastCheck(string Name, string Foreground, string Background, double Ratio, double Required)
{
    public bool Passes => Ratio + 0.005 >= Required;
}

/// <summary>Describes grid state and cells for screen readers, lists the keyboard model, and checks theme contrast against WCAG 2.2.</summary>
public static class GridAccessibility
{
    /// <summary>WCAG 2.2 success criteria 1.4.3 (text 4.5:1) and 1.4.11 (UI components and focus indicators 3:1).</summary>
    public static IReadOnlyList<ContrastCheck> Audit(GridPalette p)
    {
        ContrastCheck Check(string name, string fg, string bg, double required) => new(name, fg, bg, GridPalette.ContrastRatio(fg, bg), required);

        return new[]
        {
            Check("Cell text on surface", p.Text, p.Surface, 4.5),
            Check("Cell text on alternate row", p.Text, p.Alt, 4.5),
            Check("Header text on header", p.Text, p.Header, 4.5),
            Check("Muted text on surface", p.Muted, p.Surface, 4.5),
            Check("Muted text on header", p.Muted, p.Header, 4.5),
            Check("Selected row text", p.SelectionText, p.SelectionBack, 4.5),
            Check("Hovered row text", p.Text, p.HoverBack, 4.5),
            Check("Search highlight text", p.HighlightText, p.HighlightBack, 4.5),
            Check("Error text on error cell", p.Text, p.ErrorBack, 4.5),
            Check("Focus indicator on surface", p.FocusRing, p.Surface, 3.0),
            Check("Error indicator on surface", p.Danger, p.Surface, 3.0),
            Check("Sort and filter glyph on header", p.Primary, p.Header, 3.0)
        };
    }

    public static IReadOnlyList<ContrastCheck> Audit(ITheme theme) => Audit(GridPalette.FromTheme(theme));

    public sealed record KeyBinding(string Keys, string Action);

    /// <summary>The grid's keyboard model, for help overlays and documentation.</summary>
    public static IReadOnlyList<KeyBinding> KeyboardMap { get; } = new[]
    {
        new KeyBinding("Up / Down", "Move focus one row"),
        new KeyBinding("Left / Right", "Move focus one column"),
        new KeyBinding("Page Up / Page Down", "Move focus one page"),
        new KeyBinding("Home / End", "First / last column of the row"),
        new KeyBinding("Ctrl+Home / Ctrl+End", "First / last cell of the grid"),
        new KeyBinding("Shift+Arrows", "Extend the selection"),
        new KeyBinding("Ctrl+A", "Select all"),
        new KeyBinding("Space", "Toggle the checkbox / row selection"),
        new KeyBinding("Enter / F2", "Edit the focused cell"),
        new KeyBinding("Escape", "Cancel editing or close a popup"),
        new KeyBinding("Alt+Down", "Expand a group, tree node or detail row"),
        new KeyBinding("Alt+Up", "Collapse a group, tree node or detail row"),
        new KeyBinding("Ctrl+Z / Ctrl+Y", "Undo / redo an edit"),
        new KeyBinding("Ctrl+C", "Copy the selection"),
        new KeyBinding("Ctrl+F", "Focus the search box"),
        new KeyBinding("F3 / Shift+F3", "Next / previous search match"),
        new KeyBinding("Ctrl+Click header", "Add the column to a multi-column sort"),
        new KeyBinding("Type letters", "Jump to the next row starting with the typed text"),
        new KeyBinding("Shift+F10 / Menu", "Open the context menu"),
        new KeyBinding("Ctrl+PageUp / Ctrl+PageDown", "Previous / next page when paging")
    };

    public static string DescribeGrid<TItem>(DataGrid<TItem> grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var parts = new List<string>
        {
            $"{grid.AriaLabel ?? "Data grid"}, {grid.TotalRecordCount:N0} row{(grid.TotalRecordCount == 1 ? string.Empty : "s")}, {GridColumnLayout.OrderVisible(grid.Columns).Count} columns"
        };

        var sorts = grid.CurrentRequest.Sorts.Select(s => $"{ColumnTitle(grid, s.Member)} {(s.SortDirection == ListSortDirection.Ascending ? "ascending" : "descending")}").ToList();
        if (sorts.Count > 0) parts.Add("sorted by " + string.Join(", ", sorts));
        if (grid.CurrentRequest.Groups.Count > 0) parts.Add("grouped by " + string.Join(", ", grid.CurrentRequest.Groups.Select(g => ColumnTitle(grid, g.Member))));
        if (grid.CurrentRequest.Filters.Count > 0) parts.Add($"{grid.CurrentRequest.Filters.Count} filter{(grid.CurrentRequest.Filters.Count == 1 ? string.Empty : "s")} applied");
        if (grid.IsPaged) parts.Add($"page {grid.PageIndex + 1} of {grid.PageCount}");
        if (grid.HasErrors) parts.Add($"{grid.ValidationSummary.Count} validation error{(grid.ValidationSummary.Count == 1 ? string.Empty : "s")}");
        if (grid.IsLoading) parts.Add("loading");
        return string.Join(", ", parts);
    }

    public static string DescribeColumnHeader<TItem>(DataGrid<TItem> grid, GridColumn column)
    {
        var parts = new List<string> { string.IsNullOrEmpty(column.Title) ? column.Field : column.Title, "column header" };
        if (column.SortDirection != null) parts.Add(column.SortDirection == ListSortDirection.Ascending ? "sorted ascending" : "sorted descending");
        if (grid.HasFilter(column.Field)) parts.Add("filtered");
        if (column.PinPosition != ColumnPinPosition.None) parts.Add($"pinned {column.PinPosition.ToString().ToLowerInvariant()}");
        if (column.Sortable && grid.EnableSorting) parts.Add("press Enter to sort");
        return string.Join(", ", parts);
    }

    public static string DescribeRow<TItem>(DataGrid<TItem> grid, GridRow<TItem> row)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(row);
        var total = grid.TotalItemsCount;
        switch (row.Kind)
        {
            case GridRowKind.GroupHeader:
                return $"{grid.GetGroupHeaderText(row)}, group level {row.Level + 1}, {(row.IsExpanded ? "expanded" : "collapsed")}";
            case GridRowKind.GroupFooter:
                return "Group totals";
            case GridRowKind.DetailHeader:
                return $"{row.Relation?.Title ?? "Details"}, child table, level {row.Level}";
            case GridRowKind.DetailData:
                return $"{row.Relation?.Title ?? "Details"} row, level {row.Level}";
            case GridRowKind.DetailSummary:
                return "Row details";
            default:
                var text = $"Row {row.Index + 1} of {total:N0}";
                if (row.IsExpandable) text += row.IsExpanded ? ", expanded" : ", collapsed";
                if (row.Level > 0) text += $", level {row.Level + 1}";
                if (grid.Selection.IsRowSelected(row.Index)) text += ", selected";
                return text;
        }
    }

    public static string DescribeCell<TItem>(DataGrid<TItem> grid, GridRow<TItem> row, GridColumn column, int columnIndex, int columnCount)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(column);

        var value = grid.FormatCellValue(column, grid.GetRowCellValue(row, column));
        var title = string.IsNullOrEmpty(column.Title) ? column.Field : column.Title;
        var text = $"{title}, {(value.Length == 0 ? "blank" : value)}, row {row.Index + 1}, column {columnIndex + 1} of {columnCount}";
        if (grid.Selection.IsCellSelected(row.Index, columnIndex)) text += ", selected";
        if (!column.IsEditable || grid.EditMode == GridEditMode.None) text += ", read only";
        if (row.Item != null)
        {
            var errors = grid.GetCellErrors(row.Item, column.Field);
            if (errors.Count > 0) text += $", {(errors[0].Severity == GridValidationSeverity.Error ? "error" : "warning")}: {errors[0].Message}";
            if (grid.IsCellDirty(row.Item, column.Field)) text += ", modified";
        }
        return text;
    }

    private static string ColumnTitle<TItem>(DataGrid<TItem> grid, string field)
    {
        var column = grid.FindColumn(field);
        return column == null || string.IsNullOrEmpty(column.Title) ? field : column.Title;
    }
}
