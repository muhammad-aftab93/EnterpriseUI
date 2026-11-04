# EnterpriseUI Testing & Quality Assurance Guide

Comprehensive testing is foundational to EnterpriseUI. The test hierarchy guarantees reliability across UI layout, query calculation, virtualization math, theme tokens, and accessibility.

```
tests/
├── UnitTests                    # xUnit tests for logic, query compilation, virtualization math
├── IntegrationTests             # Component lifecycle and inter-service messaging tests
├── UIAutomationTests            # Automated driver tests simulating user clicks and keystrokes
├── PerformanceTests             # Throughput, GC allocations, and latency benchmarks
└── AccessibilityTests           # WCAG 2.2 AA ARIA verification and screen reader announcements
```

---

## 1. Unit Testing Components with xUnit

EnterpriseUI components do not depend on the OS message pump or browser DOM to execute their core logic, making them **100% unit-testable in headless CI environments**.

### Example: Testing DataGrid Filtering & Sorting
```csharp
using EnterpriseUI.Data;
using EnterpriseUI.Grids;
using Xunit;

public class DataGridLogicTests
{
    [Fact]
    public async Task DataGrid_ApplyFilter_FiltersItemsAccurately()
    {
        var grid = new DataGrid<TestEntity>();
        grid.Columns.Add(new GridColumn { Field = "Category" });
        grid.DataSource = new List<TestEntity>
        {
            new() { Id = 1, Category = "Hardware" },
            new() { Id = 2, Category = "Software" },
            new() { Id = 3, Category = "Hardware" }
        };

        grid.FilterBy("Category", FilterOperator.IsEqualTo, "Hardware");
        await grid.RefreshDataAsync();

        Assert.Equal(2, grid.RenderedItems.Count);
        Assert.All(grid.RenderedItems, x => Assert.Equal("Hardware", x.Category));
    }
}
```

---

## 2. Automated Accessibility Testing (WCAG 2.2 AA)

Components are verified against WCAG guidelines:

```csharp
using EnterpriseUI.Accessibility;
using EnterpriseUI.Grids;
using Xunit;

public class AccessibilityVerificationTests
{
    [Fact]
    public void DataGrid_HasCorrectAriaGridRole()
    {
        var grid = new DataGrid<TestEntity>();
        var ariaMarkup = AccessibilityManager.GenerateAriaAttributes(grid);

        Assert.Contains("role=\"grid\"", ariaMarkup);
        Assert.Contains("aria-label=\"Data Grid\"", ariaMarkup);
    }
}
```

---

## 3. High-Throughput Performance Testing

Performance tests run continuously in CI using high-resolution timers to assert that hot-path execution does not degrade:

```csharp
[Fact]
public void Virtualization_CalculatesOneMillionRowsUnderThreshold()
{
    var virtualizer = new VirtualizationManager();
    var sw = Stopwatch.StartNew();

    for (int i = 0; i < 100_000; i++)
    {
        _ = virtualizer.CalculateVisibleRows(i % 5000, 800, 1_000_000);
    }
    sw.Stop();

    // Assert that 100,000 updates complete in less than 50 ms
    Assert.True(sw.ElapsedMilliseconds < 50, $"Elapsed was {sw.ElapsedMilliseconds} ms");
}
```
