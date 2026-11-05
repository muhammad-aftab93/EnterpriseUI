using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace EnterpriseUI.WinFormsDemo.Modules;

/// <summary>A stock quote that raises PropertyChanged so the grid repaints when the price ticks.</summary>
public sealed class LiveQuote : INotifyPropertyChanged
{
    private decimal _price;
    private decimal _changePercent;
    private long _volume;
    private DateTime _updated = DateTime.Now;

    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public decimal Price { get => _price; set => Set(ref _price, value); }
    public decimal ChangePercent { get => _changePercent; set => Set(ref _changePercent, value); }
    public long Volume { get => _volume; set => Set(ref _volume, value); }
    public DateTime Updated { get => _updated; set => Set(ref _updated, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>Organization chart node linked to its parent through ParentId (self-referencing tree).</summary>
public sealed class OrgNode
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public int Headcount { get; set; }
}

public sealed class CustomerRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Segment { get; set; } = string.Empty;
}

public sealed class OrderRow
{
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class OrderLineRow
{
    public int LineId { get; set; }
    public int OrderId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

/// <summary>Employee record with validated, typed columns for the editing demos.</summary>
public sealed class EmployeeRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = "Engineering";
    public decimal Salary { get; set; }
    public bool Active { get; set; } = true;
    public DateTime Hired { get; set; } = DateTime.Today;
    public int Rating { get; set; }
}

public static class GridScenarioData
{
    private static readonly string[] Customers = { "Acme Corporation", "Contoso Global", "Fabrikam Aerospace", "Northwind Logistics", "Tailspin Technologies", "AdventureWorks Financial" };
    private static readonly string[] Products = { "Cloud Infrastructure Suite", "Real-Time Telemetry Node", "Enterprise Security Agent", "Workflow Execution Core", "High-Throughput Analytics" };
    private static readonly string[] Regions = { "North America", "EMEA", "APAC", "LATAM" };
    private static readonly string[] Priorities = { "Normal", "High", "Critical", "Low" };
    private static readonly string[] Statuses = { "Completed", "Processing", "Pending", "Verified" };

    public static List<GridDemoRecord> Records(int count, int seed = 1337)
    {
        var list = new List<GridDemoRecord>(count);
        var rng = new Random(seed);
        for (var i = 1; i <= count; i++)
        {
            list.Add(new GridDemoRecord
            {
                Id = 10000 + i,
                Customer = Customers[rng.Next(Customers.Length)],
                Product = Products[rng.Next(Products.Length)],
                Region = Regions[rng.Next(Regions.Length)],
                UnitPrice = 450m + rng.Next(1, 100) * 25m,
                Quantity = rng.Next(1, 20),
                Priority = Priorities[rng.Next(Priorities.Length)],
                Status = Statuses[rng.Next(Statuses.Length)],
                Timestamp = new DateTime(2024, 1, 1).AddDays(rng.Next(0, 365))
            });
        }
        return list;
    }

    public static List<EmployeeRow> Employees(int count)
    {
        var departments = new[] { "Engineering", "Sales", "Finance", "Operations", "Support" };
        var first = new[] { "Ava", "Liam", "Noah", "Mia", "Zoe", "Eli", "Ivy", "Max", "Uma", "Leo" };
        var last = new[] { "Stone", "Rivera", "Nguyen", "Kaur", "Meyer", "Okafor", "Silva", "Tanaka" };
        var rng = new Random(7);
        return Enumerable.Range(1, count).Select(i =>
        {
            var name = $"{first[rng.Next(first.Length)]} {last[rng.Next(last.Length)]}";
            return new EmployeeRow
            {
                Id = i,
                Name = name,
                Email = $"{name.ToLowerInvariant().Replace(' ', '.')}@contoso.example",
                Department = departments[rng.Next(departments.Length)],
                Salary = 42000 + rng.Next(0, 90) * 1000,
                Active = rng.Next(10) > 1,
                Hired = DateTime.Today.AddDays(-rng.Next(30, 3000)),
                Rating = rng.Next(1, 6)
            };
        }).ToList();
    }

    public static List<LiveQuote> Quotes(int count)
    {
        var rng = new Random(11);
        return Enumerable.Range(1, count).Select(i =>
        {
            var symbol = $"{(char)('A' + i % 26)}{(char)('A' + i / 26 % 26)}{(char)('A' + i / 676 % 26)}{i % 10}";
            return new LiveQuote
            {
                Id = i,
                Symbol = symbol,
                Name = $"Enterprise Holding {i}",
                Price = Math.Round(20m + (decimal)rng.NextDouble() * 480m, 2),
                Volume = rng.Next(10_000, 5_000_000)
            };
        }).ToList();
    }

    public static List<OrgNode> Organization()
    {
        var nodes = new List<OrgNode>();
        var id = 1;
        OrgNode Add(int? parent, string name, string role, decimal budget, int headcount)
        {
            var node = new OrgNode { Id = id++, ParentId = parent, Name = name, Role = role, Budget = budget, Headcount = headcount };
            nodes.Add(node);
            return node;
        }

        var ceo = Add(null, "Corporate HQ", "Chief Executive", 4_500_000m, 6);
        foreach (var division in new[] { "Engineering", "Sales", "Operations", "Finance" })
        {
            var d = Add(ceo.Id, division, "Division Head", 1_200_000m, 12);
            for (var team = 1; team <= 3; team++)
            {
                var t = Add(d.Id, $"{division} Team {team}", "Team Lead", 240_000m + team * 20_000m, 5 + team);
                for (var member = 1; member <= 3; member++)
                {
                    Add(t.Id, $"{division[0]}{team}-Member {member}", "Specialist", 85_000m + member * 4_000m, 1);
                }
            }
        }
        return nodes;
    }

    public static (List<CustomerRow> Customers, List<OrderRow> Orders, List<OrderLineRow> Lines) Sales(int customers = 40)
    {
        var rng = new Random(21);
        var countries = new[] { "USA", "Germany", "Japan", "Brazil", "UK" };
        var segments = new[] { "Enterprise", "SMB", "Public" };
        var statuses = new[] { "Open", "Shipped", "Invoiced", "Paid" };
        var customerRows = new List<CustomerRow>();
        var orders = new List<OrderRow>();
        var lines = new List<OrderLineRow>();
        var orderId = 1000;
        var lineId = 1;
        for (var c = 1; c <= customers; c++)
        {
            customerRows.Add(new CustomerRow { Id = c, Name = $"{Customers[c % Customers.Length]} #{c}", Country = countries[rng.Next(countries.Length)], Segment = segments[rng.Next(segments.Length)] });
            for (var o = 0; o < rng.Next(1, 5); o++)
            {
                var order = new OrderRow { OrderId = ++orderId, CustomerId = c, OrderDate = DateTime.Today.AddDays(-rng.Next(1, 400)), Status = statuses[rng.Next(statuses.Length)] };
                decimal total = 0;
                for (var l = 0; l < rng.Next(1, 5); l++)
                {
                    var price = 20m + rng.Next(1, 80);
                    var qty = rng.Next(1, 10);
                    total += price * qty;
                    lines.Add(new OrderLineRow { LineId = lineId++, OrderId = order.OrderId, Sku = $"SKU-{rng.Next(100, 999)}", Quantity = qty, Price = price });
                }
                order.Amount = total;
                orders.Add(order);
            }
        }
        return (customerRows, orders, lines);
    }
}
