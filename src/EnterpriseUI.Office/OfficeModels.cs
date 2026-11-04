using EnterpriseUI.Abstractions.Accessibility;
using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Core.Mvvm;

namespace EnterpriseUI.Office;

public class SpreadsheetCell
{
    public string CellReference { get; set; } = "A1";
    public object? Value { get; set; }
    public string? Formula { get; set; }
    public string? FormatString { get; set; }
}

public class SpreadsheetWorksheet
{
    public string Name { get; set; } = "Sheet1";
    public Dictionary<string, SpreadsheetCell> Cells { get; } = new(StringComparer.OrdinalIgnoreCase);

    public SpreadsheetCell GetOrCreateCell(string cellRef)
    {
        if (!Cells.TryGetValue(cellRef, out var cell))
        {
            cell = new SpreadsheetCell { CellReference = cellRef };
            Cells[cellRef] = cell;
        }
        return cell;
    }
}

public class SpreadsheetWorkbook
{
    public List<SpreadsheetWorksheet> Worksheets { get; } = new();
}

/// <summary>
/// Superset Spreadsheet control combining DevExpress SpreadsheetControl and Telerik RadSpreadsheet.
/// </summary>
public class SpreadsheetControl : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    private SpreadsheetWorkbook _workbook = new();

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public SpreadsheetWorkbook Workbook { get => _workbook; set => SetProperty(ref _workbook, value); }
    public string ActiveWorksheetName { get; set; } = "Sheet1";

    public AriaRole Role { get; set; } = AriaRole.Grid;
    public string? AriaLabel { get; set; } = "Office Spreadsheet";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

