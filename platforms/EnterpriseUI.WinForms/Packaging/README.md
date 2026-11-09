# EnterpriseUI WinForms Controls

[Source code, documentation, and examples](https://github.com/muhammad-aftab93/EnterpriseUI)

EnterpriseUI.WinForms is the Windows Forms adapter package for the EnterpriseUI component platform. It contains controls across data presentation, editing, application navigation, layout, visualization, scheduling, documents, notifications, and media.

## Component Areas

- **Data:** typed and virtualized grids, hierarchical and pivot views, card and list views, filtering, paging, grouping, and selection.
- **Editors and forms:** text and numeric input, pickers, masked and autocomplete editors, buttons, switches, dialogs, and validation.
- **Navigation and layout:** ribbon and navigation controls, menus, page views, docking and layout containers, breadcrumbs, and wizards.
- **Visualization:** charts, gauges, maps, diagrams, barcodes, and related data views.
- **Scheduling and workflow:** calendars, schedulers, task boards, and Gantt views.
- **Documents and media:** PDF and rich-text views, spreadsheet and document workflows, image editing, webcam, and virtual keyboard controls.
- **Application services:** notifications, printing and preview, multi-format export, accessibility integration, and runtime theming.

The exact controls exposed in Visual Studio's Toolbox are the concrete, nongeneric Windows Forms controls in the package. Generic controls, models, and headless services are intended to be used from code or through their platform-specific adapters.

## Requirements and Installation

Requires Windows and a Windows Forms application targeting `net8.0-windows` or a compatible later Windows target, with `UseWindowsForms` enabled.

Install from a NuGet source containing the EnterpriseUI packages:

```powershell
dotnet add package EnterpriseUI.WinForms --version 1.1.0
```

The package references companion EnterpriseUI libraries; NuGet restores those dependencies from the configured package sources.

## Quick Start: EnterpriseDataGrid

The data grid is one of the controls in this package. This example creates a strongly typed grid in code:

```csharp
using System.Windows.Forms;
using EnterpriseUI.Grids;
using EnterpriseUI.WinForms;

public sealed record SalesRow(int Id, string Customer, decimal Total);

public sealed class SalesForm : Form
{
    public SalesForm()
    {
        var grid = GridBuilder<SalesRow>.Create()
            .Label("Sales records")
            .Column(nameof(SalesRow.Id), "Order", 90d)
            .Column(nameof(SalesRow.Customer), "Customer", 240d)
            .Column(nameof(SalesRow.Total), "Total", 140d,
                column => column.FormatString = "C2")
            .DataSource(new[]
            {
                new SalesRow(1001, "Contoso", 1250m),
                new SalesRow(1002, "Fabrikam", 2840m)
            })
            .Build();

        Text = "Sales";
        Width = 1000;
        Height = 600;
        grid.Dock = DockStyle.Fill;
        Controls.Add(grid);
    }
}
```

## Examples and Documentation

The repository includes setup guides, component documentation, architecture notes, and an interactive WinForms Demo Center. Run the demonstration app from a source checkout:

```powershell
dotnet run --project samples/EnterpriseUI.WinFormsDemo/EnterpriseUI.WinFormsDemo.csproj
```

Browse the [repository](https://github.com/muhammad-aftab93/EnterpriseUI) for component guides and examples. Individual components may have platform, feature, or API limitations; consult their documentation and demonstrations for supported behavior.

## License

Licensed under Apache-2.0. No activation keys are required.
