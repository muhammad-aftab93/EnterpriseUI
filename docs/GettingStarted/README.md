# Getting Started with EnterpriseUI

This guide walks you through bootstrapping your first application with EnterpriseUI on **.NET 8+** in under 5 minutes.

---

## 1. Prerequisites

- **.NET SDK**: 8.0.100 or later (fully compatible with .NET 9 and .NET 10 preview)
- **IDE**: Visual Studio 2022 (version 17.8+), Visual Studio Code with C# Dev Kit, or JetBrains Rider 2024.1+
- **OS**: Windows 10/11 (for WinForms/WPF), Windows/macOS/Linux (for Blazor, ASP.NET Core, MAUI)

---

## 2. Licensing Setup

EnterpriseUI is a commercial-grade product. Initialize your enterprise license in your application's entrypoint before rendering any components:

```csharp
using EnterpriseUI.Core.Licensing;

// In Program.cs or Application Startup
EnterpriseLicenseProvider.RegisterLicense("YOUR-COMMERCIAL-LICENSE-KEY-HERE");
```

> [!NOTE]
> During evaluation and local development, an automatic trial mode is provided out of the box with zero runtime blocking.

---

## 3. Platform Quick Starts

### Option A: Blazor (WebAssembly or Server)

#### Step 1: Install NuGet Packages
```bash
dotnet add package EnterpriseUI.Core
dotnet add package EnterpriseUI.Abstractions
dotnet add package EnterpriseUI.Theming
dotnet add package EnterpriseUI.Grids
dotnet add package EnterpriseUI.Blazor
```

#### Step 2: Register Services in `Program.cs`
```csharp
using EnterpriseUI.Theming;
using EnterpriseUI.Theming.Themes;

var builder = WebApplication.CreateBuilder(args);

// Register EnterpriseUI services
builder.Services.AddSingleton(ThemeManager.Instance);

var app = builder.Build();

// Set default theme (Fluent Light, Material, Bootstrap, Office, Modern)
ThemeManager.Instance.ApplyTheme(BuiltInThemes.CreateFluent(ColorScheme.Light));

app.Run();
```

#### Step 3: Add CSS Variables to `App.razor` or `index.html`
```html
<link rel="stylesheet" href="_content/EnterpriseUI.Blazor/css/enterpriseui.min.css" />
```

#### Step 4: Use DataGrid in a Razor Component (`Orders.razor`)
```razor
@page "/orders"
@using EnterpriseUI.Grids
@using EnterpriseUI.Blazor

<h3>Customer Orders</h3>

<div class="eui-grid-wrapper" data-theme="fluent-light">
    <!-- Blazor DataGrid implementation -->
    <p>Active Theme: @ThemeManager.Instance.CurrentTheme.Name</p>
</div>

@code {
    public class Order
    {
        public int Id { get; set; }
        public string Customer { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
```

---

### Option B: Windows Forms (.NET 8-windows)

#### Step 1: Reference Windows Forms Platform Project
```xml
<ItemGroup>
  <ProjectReference Include="..\EnterpriseUI.WinForms\EnterpriseUI.WinForms.csproj" />
</ItemGroup>
```

#### Step 2: Initialize Theme & DataGrid in `Form1.cs`
```csharp
using System.Drawing;
using System.Windows.Forms;
using EnterpriseUI.Grids;
using EnterpriseUI.Theming;
using EnterpriseUI.Theming.Themes;
using EnterpriseUI.WinForms;

namespace MyWinFormsApp;

public partial class MainForm : Form
{
    private readonly WinFormsDataGrid<CustomerOrder> _grid;

    public MainForm()
    {
        InitializeComponent();
        Text = "EnterpriseUI WinForms Showcase";
        Size = new Size(1000, 650);

        // 1. Configure Theme
        ThemeManager.Instance.ApplyTheme(BuiltInThemes.CreateFluent(ColorScheme.Light));

        // 2. Initialize DataGrid Control
        _grid = new WinFormsDataGrid<CustomerOrder>
        {
            Dock = DockStyle.Fill
        };

        // 3. Define Columns
        _grid.Engine.Columns.Add(new GridColumn { Field = "OrderId", Title = "Order #", Width = 90 });
        _grid.Engine.Columns.Add(new GridColumn { Field = "CustomerName", Title = "Customer", Width = 220 });
        _grid.Engine.Columns.Add(new GridColumn { Field = "Product", Title = "Product Line", Width = 200 });
        _grid.Engine.Columns.Add(new GridColumn { Field = "TotalAmount", Title = "Total ($)", Width = 140, Alignment = ColumnTextAlignment.Right });

        // 4. Bind Data
        _grid.Engine.DataSource = GetOrders();

        Controls.Add(_grid);
    }

    private static List<CustomerOrder> GetOrders() => new()
    {
        new CustomerOrder { OrderId = 1001, CustomerName = "Global Logistics", Product = "Fleet Telematics", TotalAmount = 14250.00m },
        new CustomerOrder { OrderId = 1002, CustomerName = "Apollo Health", Product = "EMR Integration", TotalAmount = 28900.00m },
        new CustomerOrder { OrderId = 1003, CustomerName = "Starlight Media", Product = "CDN Enterprise", TotalAmount = 8400.50m }
    };
}

public class CustomerOrder
{
    public int OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
}
```

---

### Option C: WPF (.NET 8-windows)

#### Step 1: Reference WPF Platform Project
```xml
<ItemGroup>
  <ProjectReference Include="..\EnterpriseUI.Wpf\EnterpriseUI.Wpf.csproj" />
</ItemGroup>
```

#### Step 2: Apply Theme Tokens in `App.xaml.cs`
```csharp
using System.Windows;
using EnterpriseUI.Theming;
using EnterpriseUI.Theming.Themes;
using EnterpriseUI.Wpf;

namespace MyWpfApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeManager.Instance.ApplyTheme(BuiltInThemes.CreateModern(ColorScheme.Dark));
    }
}
```

---

## 4. Next Steps

- Explore the **[Architecture Guide](Architecture/SystemArchitecture.md)** to understand the decoupled pipeline.
- Read the **[DataGrid Architecture & Performance Guide](Grid/ArchitectureGuide.md)**.
- Review **[Theming Engine & Design Tokens](Themes/ThemingEngineAndTokens.md)** to brand your application.
- Learn about the **[AI Assistant & Natural Language Query](AI/AIAssistantAndCopilot.md)** engine.
