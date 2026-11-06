# EnterpriseUI WinForms

[Source Code and Documentation](https://github.com/muhammad-aftab93/EnterpriseUI)

## EnterpriseDataGrid for .NET 8

Build data-intensive Windows desktop applications with `EnterpriseDataGrid<TItem>`, a typed Windows Forms grid backed by a separate data engine and DPI-aware GDI+ rendering.

Query, edit, explore, export, and print your data in one control. Windowed rendering draws rows near the viewport rather than creating a UI control for every record.

## Grid Capabilities

- **Data and queries:** typed columns, data binding, paging, multi-column sorting, filtering, grouping, and search highlighting.
- **Editing:** in-cell and popup editors, validation, and undo/redo.
- **Structured data:** master-detail expansion, tree hierarchy, column bands, and summaries.
- **Everyday workflows:** row, cell, and column selection; column resizing, reordering, pinning, and a column chooser.
- **Output:** CSV, JSON, XML, HTML, XLSX, and PDF export, plus printing and print preview.
- **Presentation:** theme-derived colors, conditional formatting, and layout persistence.
- **Accessibility:** keyboard navigation, focus indicators, Windows high-contrast integration, and UI Automation support.

## Install

Requires a Windows Forms application targeting `net8.0-windows` or a compatible newer Windows target, with `UseWindowsForms` enabled.

For the local package feed:

```powershell
dotnet add YourApp.csproj package EnterpriseUI.WinForms --version 1.0.0 --source C:\LocalNugetPackages
```

NuGet restores the required EnterpriseUI component packages alongside the WinForms package.

## Visual Studio Toolbox

The non-generic `EnterpriseDataGrid` can be added from the Windows Forms Toolbox. It supports runtime data binding through `IEnumerable<object>` and explicitly configured columns. Use `EnterpriseDataGrid<TItem>` or `GridBuilder<TItem>` in code when you need strongly typed grid APIs.

Toolbox entries use the `Enterprise` prefix, such as `EnterpriseButtonControl` and `EnterpriseTextBoxControl`. Only concrete Windows Forms controls appear in the Toolbox; component-engine models and open generic types do not.

When replacing a local package without changing version `1.0.0`, NuGet may continue using an older cached DLL. If you see names starting with `WinForms`:

1. Close Visual Studio to release the designer's assemblies.
2. Remove only the cached WinForms package (not the local feed):

    ```powershell
    Remove-Item "$env:USERPROFILE\.nuget\packages\enterpriseui.winforms\1.0.0" -Recurse -Force
    ```

3. Restore and rebuild the consuming application from the local feed, then reopen its designer.
4. If the Toolbox still shows old entries, reset the Toolbox or remove and re-add this library's entries through **Choose Items**.

Existing forms using the old `WinForms...Control` type names must be updated to their `Enterprise...Control` equivalents.

## Quick Start

Add this control to a Windows Forms form:

```csharp
using System.Windows.Forms;
using EnterpriseUI.WinForms;

public sealed record SalesRow(int Id, string Customer, decimal Total);

public sealed class MainForm : Form
{
    public MainForm()
    {
        Text = "Sales";
        Width = 1000;
        Height = 600;

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

        grid.Dock = DockStyle.Fill;
        Controls.Add(grid);
    }
}
```

## Package Scope

`EnterpriseDataGrid` is included in the shared `EnterpriseUI.WinForms` assembly, not a standalone grid-only package. The package also contains the WinForms editors, navigation, layout, visualization, and other controls; its companion packages provide the underlying component engines.

## Documentation and Samples

The source distribution contains grid guides in `docs/Grid` and an interactive Windows Forms demonstration application in `samples/EnterpriseUI.WinFormsDemo`.

From a source checkout, launch the demo:

```powershell
dotnet run --project samples/EnterpriseUI.WinFormsDemo/EnterpriseUI.WinFormsDemo.csproj
```

## License

Licensed under **Apache-2.0**, for personal and commercial use. No license keys or activation are required.