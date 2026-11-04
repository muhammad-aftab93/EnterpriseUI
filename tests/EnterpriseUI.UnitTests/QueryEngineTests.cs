using System.ComponentModel;
using EnterpriseUI.Data;
using Xunit;

namespace EnterpriseUI.Tests.Unit;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
}

public class QueryEngineTests
{
    private readonly List<Product> _products = new()
    {
        new Product { Id = 1, Name = "Chai Tea", Price = 18.0m, Category = "Beverages" },
        new Product { Id = 2, Name = "Chang Beer", Price = 19.0m, Category = "Beverages" },
        new Product { Id = 3, Name = "Aniseed Syrup", Price = 10.0m, Category = "Condiments" },
        new Product { Id = 4, Name = "Chef Anton Cajun Seasoning", Price = 22.0m, Category = "Condiments" },
        new Product { Id = 5, Name = "Chef Anton Gumbo Mix", Price = 21.35m, Category = "Condiments" },
        new Product { Id = 6, Name = "Grandma Boysenberry Spread", Price = 25.0m, Category = "Condiments" },
        new Product { Id = 7, Name = "Uncle Bob Organic Dried Pears", Price = 30.0m, Category = "Produce" },
        new Product { Id = 8, Name = "Northwoods Cranberry Sauce", Price = 40.0m, Category = "Condiments" },
        new Product { Id = 9, Name = "Mishi Kobe Niku", Price = 97.0m, Category = "Meat" },
        new Product { Id = 10, Name = "Ikura", Price = 31.0m, Category = "Seafood" }
    };

    [Fact]
    public void Execute_FilterByContains_ReturnsCorrectMatches()
    {
        var request = new DataSourceRequest
        {
            Page = 1,
            PageSize = 20
        };
        request.Filters.Add(new FilterDescriptor("Name", FilterOperator.Contains, "Chef"));

        var result = QueryEngine.Execute(_products.AsQueryable(), request);

        Assert.Equal(2, result.Total);
        Assert.All(result.Data!, p => Assert.Contains("Chef", p.Name));
    }

    [Fact]
    public void Execute_SortDescending_OrdersCorrectly()
    {
        var request = new DataSourceRequest
        {
            Page = 1,
            PageSize = 5
        };
        request.Sorts.Add(new SortDescriptor("Price", ListSortDirection.Descending));

        var result = QueryEngine.Execute(_products.AsQueryable(), request);

        var list = result.Data!.ToList();
        Assert.Equal(5, list.Count);
        Assert.Equal(97.0m, list[0].Price);
        Assert.Equal(40.0m, list[1].Price);
    }

    [Fact]
    public void Execute_Paging_SlicesCorrectWindow()
    {
        var request = new DataSourceRequest
        {
            Page = 2,
            PageSize = 3
        };
        request.Sorts.Add(new SortDescriptor("Id", ListSortDirection.Ascending));

        var result = QueryEngine.Execute(_products.AsQueryable(), request);

        Assert.Equal(10, result.Total);
        var list = result.Data!.ToList();
        Assert.Equal(3, list.Count);
        Assert.Equal(4, list[0].Id);
        Assert.Equal(5, list[1].Id);
        Assert.Equal(6, list[2].Id);
    }
}

