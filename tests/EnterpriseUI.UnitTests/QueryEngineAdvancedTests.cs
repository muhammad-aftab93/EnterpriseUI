using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using EnterpriseUI.Data;
using Xunit;

namespace EnterpriseUI.Tests.Unit;

public class QueryEngineAdvancedTests
{
    public enum Level { Low, High }

    public sealed class Order
    {
        public int Id { get; set; }
        public string Customer { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string Region { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public int? Quantity { get; set; }
        public DateTime Date { get; set; }
        public Level Priority { get; set; }
        public Guid Key { get; set; }
        public Address? Address { get; set; }
    }

    public sealed class Address
    {
        public string City { get; set; } = string.Empty;
        public int Zip { get; set; }
    }

    private static List<Order> Orders() => new()
    {
        new() { Id = 1, Customer = "Acme", Region = "EMEA", Total = 100m, Quantity = 1, Date = new DateTime(2024, 1, 5), Priority = Level.High, Note = "rush", Address = new Address { City = "Berlin", Zip = 10115 } },
        new() { Id = 2, Customer = "acme", Region = "APAC", Total = 250.5m, Quantity = null, Date = new DateTime(2024, 2, 5), Priority = Level.Low, Note = "", Address = new Address { City = "Tokyo", Zip = 1000 } },
        new() { Id = 3, Customer = "Contoso", Region = "EMEA", Total = 75m, Quantity = 3, Date = new DateTime(2023, 12, 1), Priority = Level.Low },
        new() { Id = 4, Customer = "Fabrikam", Region = "EMEA", Total = 400m, Quantity = 10, Date = new DateTime(2024, 3, 9), Priority = Level.High, Address = new Address { City = "Berlin", Zip = 12000 } }
    };

    private static List<int> Ids(DataSourceRequest request) =>
        QueryEngine.Execute(Orders().AsQueryable(), request).Data!.Select(o => o.Id).ToList();

    private static DataSourceRequest Where(params IFilterDescriptor[] filters)
    {
        var request = new DataSourceRequest { Take = 0 };
        request.Filters.AddRange(filters);
        return request;
    }

    [Fact]
    public void StringFiltersAreCaseInsensitiveByDefaultAndCanBeCaseSensitive()
    {
        Assert.Equal(new[] { 1, 2 }, Ids(Where(new FilterDescriptor("Customer", FilterOperator.IsEqualTo, "ACME"))));
        Assert.Equal(new[] { 1 }, Ids(Where(new FilterDescriptor("Customer", FilterOperator.IsEqualTo, "Acme") { IgnoreCase = false })));
        Assert.Equal(new[] { 3 }, Ids(Where(new FilterDescriptor("Customer", FilterOperator.Contains, "TOSO"))));
        Assert.Equal(new[] { 4 }, Ids(Where(new FilterDescriptor("Customer", FilterOperator.StartsWith, "fab"))));
        Assert.Equal(new[] { 4 }, Ids(Where(new FilterDescriptor("Customer", FilterOperator.EndsWith, "KAM"))));
    }

    [Fact]
    public void DoesNotContainAndEmptyOperatorsWork()
    {
        Assert.Equal(new[] { 3, 4 }, Ids(Where(new FilterDescriptor("Customer", FilterOperator.DoesNotContain, "acme"))));
        Assert.Equal(new[] { 2, 3, 4 }, Ids(Where(new FilterDescriptor("Note", FilterOperator.IsEmpty, null))));
        Assert.Equal(new[] { 1 }, Ids(Where(new FilterDescriptor("Note", FilterOperator.IsNotEmpty, null))));
        Assert.Equal(new[] { 3, 4 }, Ids(Where(new FilterDescriptor("Note", FilterOperator.IsNull, null))));
    }

    [Fact]
    public void ContainsOnNonStringMembersSearchesTheirText()
    {
        Assert.Equal(new[] { 2 }, Ids(Where(new FilterDescriptor("Total", FilterOperator.Contains, "250"))));
        Assert.Equal(new[] { 1, 4 }, Ids(Where(new FilterDescriptor("Priority", FilterOperator.Contains, "hig"))));
    }

    [Fact]
    public void UnconvertibleValuesMatchNothingInsteadOfThrowing()
    {
        Assert.Empty(Ids(Where(new FilterDescriptor("Total", FilterOperator.IsEqualTo, "not a number"))));
        Assert.Equal(new[] { 1, 2, 3, 4 }, Ids(Where(new FilterDescriptor("Total", FilterOperator.IsNotEqualTo, "not a number"))));
        Assert.Empty(Ids(Where(new FilterDescriptor("Date", FilterOperator.IsGreaterThan, "garbage"))));
        Assert.Empty(Ids(Where(new FilterDescriptor("Priority", FilterOperator.IsEqualTo, "Nonexistent"))));
    }

    [Fact]
    public void SearchAcrossMixedColumnsDoesNotThrowForNumericColumns()
    {
        var search = new CompositeFilterDescriptor { LogicalOperator = FilterLogicalOperator.Or };
        foreach (var member in new[] { "Id", "Customer", "Total", "Quantity", "Date", "Priority", "Key" })
        {
            search.FilterDescriptors.Add(new FilterDescriptor(member, FilterOperator.Contains, "fab"));
        }

        Assert.Equal(new[] { 4 }, Ids(Where(search)));
    }

    [Fact]
    public void NumericDateEnumAndNullableComparisonsConvertValues()
    {
        Assert.Equal(new[] { 2, 4 }, Ids(Where(new FilterDescriptor("Total", FilterOperator.IsGreaterThan, "200"))));
        Assert.Equal(new[] { 3, 4 }, Ids(Where(new FilterDescriptor("Quantity", FilterOperator.IsGreaterThanOrEqualTo, 3))));
        Assert.Equal(new[] { 1, 2, 4 }, Ids(Where(new FilterDescriptor("Date", FilterOperator.IsGreaterThan, "2024-01-01"))));
        Assert.Equal(new[] { 1, 4 }, Ids(Where(new FilterDescriptor("Priority", FilterOperator.IsEqualTo, "high"))));
        Assert.Equal(new[] { 2 }, Ids(Where(new FilterDescriptor("Quantity", FilterOperator.IsNull, null))));
    }

    [Fact]
    public void StringRelationalOperatorsUseOrdinalComparison()
    {
        Assert.Equal(new[] { 3, 4 }, Ids(Where(new FilterDescriptor("Customer", FilterOperator.IsGreaterThan, "B"))));
    }

    [Fact]
    public void InAndBetweenOperatorsSupportCollectionsAndRanges()
    {
        Assert.Equal(new[] { 1, 3 }, Ids(Where(new FilterDescriptor("Id", FilterOperator.IsContainedIn, new[] { 1, 3, 99 }))));
        Assert.Equal(new[] { 2, 4 }, Ids(Where(new FilterDescriptor("Id", FilterOperator.IsNotContainedIn, new[] { 1, 3 }))));
        Assert.Equal(new[] { 1, 3 }, Ids(Where(new FilterDescriptor("Total", FilterOperator.IsBetween, 50, 100))));
        Assert.Equal(new[] { 2, 4 }, Ids(Where(new FilterDescriptor("Total", FilterOperator.IsNotBetween, 50, 100))));
        Assert.Equal(new[] { 1, 2, 3 }, Ids(Where(new FilterDescriptor("Customer", FilterOperator.IsContainedIn, new[] { "acme", "CONTOSO" }) { IgnoreCase = true })));
        Assert.Equal(new[] { 2, 3 }, Ids(Where(new FilterDescriptor("Customer", FilterOperator.IsContainedIn, new[] { "acme", "Contoso" }) { IgnoreCase = false })));
    }

    [Fact]
    public void NestedPropertyPathsAreNullSafe()
    {
        Assert.Equal(new[] { 1, 4 }, Ids(Where(new FilterDescriptor("Address.City", FilterOperator.IsEqualTo, "berlin"))));
        Assert.Equal(new[] { 4 }, Ids(Where(new FilterDescriptor("Address.Zip", FilterOperator.IsGreaterThan, 11000))));
        Assert.Equal(new[] { 3 }, Ids(Where(new FilterDescriptor("Address.City", FilterOperator.IsNull, null))));
    }

    [Fact]
    public void CompositeAndCustomFiltersCombine()
    {
        var composite = new CompositeFilterDescriptor { LogicalOperator = FilterLogicalOperator.Or };
        composite.FilterDescriptors.Add(new FilterDescriptor("Region", FilterOperator.IsEqualTo, "APAC"));
        composite.FilterDescriptors.Add(new CustomFilterDescriptor("Total", "Over 300", v => v is decimal d && d > 300m));

        Assert.Equal(new[] { 2, 4 }, Ids(Where(composite)));
    }

    [Fact]
    public void UnknownMembersAreIgnored()
    {
        Assert.Equal(new[] { 1, 2, 3, 4 }, Ids(Where(new FilterDescriptor("NoSuchField", FilterOperator.IsEqualTo, 1))));
    }

    [Fact]
    public void SortingSupportsMultiKeyCustomComparerAndNestedPaths()
    {
        var request = new DataSourceRequest { Take = 0 };
        request.Sorts.Add(new SortDescriptor("Region"));
        request.Sorts.Add(new SortDescriptor("Total", ListSortDirection.Descending));
        Assert.Equal(new[] { 2, 4, 1, 3 }, Ids(request));

        var custom = new DataSourceRequest { Take = 0 };
        custom.Sorts.Add(new SortDescriptor("Customer") { Comparer = Comparer<object?>.Create((a, b) => string.Compare(((string)a!).ToLowerInvariant(), ((string)b!).ToLowerInvariant(), StringComparison.Ordinal)) });
        custom.Sorts.Add(new SortDescriptor("Id", ListSortDirection.Descending));
        Assert.Equal(new[] { 2, 1, 3, 4 }, Ids(custom));

        var nested = new DataSourceRequest { Take = 0 };
        nested.Sorts.Add(new SortDescriptor("Address.City"));
        Assert.Equal(3, Ids(nested)[0]);
    }

    [Fact]
    public void SortingAnAlreadyOrderedQueryableDoesNotThrow()
    {
        var ordered = Orders().AsQueryable().OrderBy(o => o.Id).Where(o => o.Id > 1);
        var request = new DataSourceRequest { Take = 0 };
        request.Sorts.Add(new SortDescriptor("Total"));

        Assert.Equal(new[] { 3, 2, 4 }, QueryEngine.Execute(ordered, request).Data!.Select(o => o.Id));
    }

    [Fact]
    public void AggregatesComputeAllFunctions()
    {
        var request = new DataSourceRequest { Take = 0 };
        foreach (var function in new[] { AggregateFunction.Sum, AggregateFunction.Min, AggregateFunction.Max, AggregateFunction.Average, AggregateFunction.Count, AggregateFunction.CountDistinct, AggregateFunction.Median })
        {
            request.Aggregates.Add(new AggregateDescriptor { Member = "Total", Function = function });
        }

        var result = QueryEngine.Execute(Orders().AsQueryable(), request);

        Assert.Equal(825.5m, result.FindAggregate("Sum(Total)")!.Value);
        Assert.Equal(75m, result.FindAggregate("Min(Total)")!.Value);
        Assert.Equal(400m, result.FindAggregate("Max(Total)")!.Value);
        Assert.Equal(825.5m / 4, result.FindAggregate("Average(Total)")!.Value);
        Assert.Equal(4, result.FindAggregate("Count(Total)")!.Value);
        Assert.Equal(4, result.FindAggregate("CountDistinct(Total)")!.Value);
        Assert.Equal((100m + 250.5m) / 2, result.FindAggregate("Median(Total)")!.Value);
    }

    [Fact]
    public void IntegerAndNullableAggregatesSkipNulls()
    {
        var request = new DataSourceRequest { Take = 0 };
        request.Aggregates.Add(new AggregateDescriptor { Member = "Quantity", Function = AggregateFunction.Sum });
        request.Aggregates.Add(new AggregateDescriptor { Member = "Quantity", Function = AggregateFunction.Average });
        request.Aggregates.Add(new AggregateDescriptor { Member = "Total", Alias = "Doubled", Custom = values => values.Sum(v => (decimal)v! * 2) });

        var result = QueryEngine.Execute(Orders().AsQueryable(), request);

        Assert.Equal(14L, result.FindAggregate("Sum(Quantity)")!.Value);
        Assert.Equal(14.0 / 3, (double)result.FindAggregate("Average(Quantity)")!.Value!, 6);
        Assert.Equal(1651m, result.FindAggregate("Doubled")!.Value);
    }

    [Fact]
    public void GroupingBuildsNestedTreeWithAggregatesAndSortedKeys()
    {
        var request = new DataSourceRequest { Take = 0 };
        var region = new GroupDescriptor { Member = "Region", SortDirection = ListSortDirection.Descending };
        region.Aggregates.Add(new AggregateDescriptor { Member = "Total", Function = AggregateFunction.Sum });
        request.Groups.Add(region);
        request.Groups.Add(new GroupDescriptor { Member = "Priority" });

        var result = QueryEngine.Execute(Orders().AsQueryable(), request);
        var groups = result.GroupedData!.ToList();

        Assert.Equal(new object[] { "EMEA", "APAC" }, groups.Select(g => g.Key!));
        Assert.Equal(3, groups[0].ItemCount);
        Assert.Equal(575m, groups[0].FindAggregate("Sum(Total)")!.Value);
        Assert.Equal(new object[] { Level.Low, Level.High }, groups[0].Subgroups!.Select(g => g.Key!));
        Assert.Equal(new[] { 3 }, groups[0].Subgroups![0].Items!.Select(o => o.Id));
        Assert.Equal(4, result.Total);
    }

    [Fact]
    public void GroupKeySelectorBucketsValues()
    {
        var request = new DataSourceRequest { Take = 0 };
        request.Groups.Add(new GroupDescriptor { Member = "Date", KeySelector = v => ((DateTime)v!).Year });

        var groups = QueryEngine.Execute(Orders().AsQueryable(), request).GroupedData!.ToList();

        Assert.Equal(new object[] { 2023, 2024 }, groups.Select(g => g.Key!));
        Assert.Equal(3, groups[1].ItemCount);
    }

    [Fact]
    public void GroupedRequestsStillPageTheFlatData()
    {
        var request = new DataSourceRequest { Skip = 1, Take = 2 };
        request.Groups.Add(new GroupDescriptor { Member = "Region" });

        var result = QueryEngine.Execute(Orders().AsQueryable(), request);

        Assert.Equal(2, result.Data!.Count());
        Assert.Equal(4, result.Total);
    }

    [Fact]
    public void ExpressionEngineEvaluatesCalculatedFields()
    {
        var order = Orders()[0];
        Assert.Equal(200m, ExpressionEngine.Evaluate("[Total] * 2", order));
        Assert.Equal("ACME-EMEA", ExpressionEngine.Evaluate("UPPER(Customer) + '-' + Region", order));
        Assert.Equal("big", ExpressionEngine.Evaluate("IIF(Total >= 100 and Quantity = 1, 'big', 'small')", order));
        Assert.Equal(3m, ExpressionEngine.Evaluate("ROUND(Total / 30, 0)", order));
        Assert.Null(ExpressionEngine.Evaluate("Total / 0", order));
        Assert.Null(ExpressionEngine.Evaluate("Total * Quantity", Orders()[1]));
        Assert.Equal(0m, ExpressionEngine.Evaluate("ISNULL(Quantity, 0) * Total", Orders()[1]));
        Assert.Equal(2024, ExpressionEngine.Evaluate("YEAR(Date)", order));
        Assert.Equal("Berlin", ExpressionEngine.Evaluate("Address.City", order));
    }

    [Fact]
    public void ExpressionEngineReportsErrorsAndFieldDependencies()
    {
        Assert.False(ExpressionEngine.TryCompile("1 +", out _, out var error));
        Assert.NotNull(error);
        Assert.False(ExpressionEngine.TryCompile("NOPE(1)", out _, out _));
        Assert.False(ExpressionEngine.TryCompile("'open", out _, out _));
        Assert.False(ExpressionEngine.TryCompile(new string('(', 500) + "1" + new string(')', 500), out _, out _));

        var compiled = ExpressionEngine.Compile("[Unit Price] * Quantity + ABS(Discount)");
        Assert.Equal(new[] { "Discount", "Quantity", "Unit Price" }, compiled.Fields.OrderBy(f => f));
    }

    [Fact]
    public void FilterTextRoundTripsThroughParserAndFormatter()
    {
        var text = "[Region] = 'EMEA' AND ([Total] > 1000 OR CONTAINS([Customer], 'o''brien')) AND NOT ISEMPTY([Note])";
        var filter = FilterText.Parse(text);

        var composite = Assert.IsType<CompositeFilterDescriptor>(filter);
        Assert.Equal(3, composite.FilterDescriptors.Count);

        var reparsed = FilterText.Parse(FilterText.Format(filter));
        Assert.Equal(FilterText.Format(filter), FilterText.Format(reparsed));

        var request = Where(FilterText.Parse("Region = 'EMEA' and Total >= 100"));
        Assert.Equal(new[] { 1, 4 }, Ids(request));
        Assert.Equal(new[] { 3, 4 }, Ids(Where(FilterText.Parse("NOT (Customer = 'Acme')"))));
        Assert.False(FilterText.TryParse("Total + 1", out _, out _));
    }

    [Fact]
    public void FilterBuilderComposesGroups()
    {
        var filter = FilterBuilder.All()
            .Is("Region", "EMEA")
            .Group(FilterLogicalOperator.Or, g => g.Where("Total", FilterOperator.IsGreaterThan, 300m).Contains("Customer", "conto"))
            .Build();

        Assert.Equal(new[] { 3, 4 }, Ids(Where(filter)));
        Assert.Contains("OR", FilterText.Format(filter));
    }

    [Fact]
    public void FilterSerializerRoundTripsTypedValuesAndCustomOperators()
    {
        FilterOperatorRegistry.Register("Big spender", v => v is decimal d && d > 300m);
        var guid = Guid.NewGuid();
        var date = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);
        var composite = new CompositeFilterDescriptor { LogicalOperator = FilterLogicalOperator.Or };
        composite.FilterDescriptors.Add(new FilterDescriptor("Total", FilterOperator.IsBetween, 1.5m, 9m));
        composite.FilterDescriptors.Add(new FilterDescriptor("Key", FilterOperator.IsEqualTo, guid));
        composite.FilterDescriptors.Add(new CustomFilterDescriptor("Total", "Big spender", _ => false));
        var filters = new IFilterDescriptor[] { composite, new FilterDescriptor("Date", FilterOperator.IsLessThan, date) { IgnoreCase = false }, new FilterDescriptor("Id", FilterOperator.IsContainedIn, new[] { 1, 2 }) };

        var json = FilterSerializer.Serialize(filters);
        var loaded = FilterSerializer.Deserialize(json);

        Assert.Empty(loaded.Warnings);
        var restored = Assert.IsType<CompositeFilterDescriptor>(loaded.Filters[0]);
        var between = Assert.IsType<FilterDescriptor>(restored.FilterDescriptors[0]);
        Assert.Equal(1.5m, between.Value);
        Assert.Equal(9m, between.Value2);
        Assert.Equal(guid, ((FilterDescriptor)restored.FilterDescriptors[1]).Value);
        Assert.True(((CustomFilterDescriptor)restored.FilterDescriptors[2]).Predicate(400m));
        var dateFilter = Assert.IsType<FilterDescriptor>(loaded.Filters[1]);
        Assert.Equal(date, dateFilter.Value);
        Assert.False(dateFilter.IgnoreCase);
        Assert.Equal(new List<object?> { 1, 2 }, Assert.IsType<List<object?>>(((FilterDescriptor)loaded.Filters[2]).Value));
    }

    [Fact]
    public void FilterSerializerWarnsAboutUnregisteredCustomOperators()
    {
        var json = FilterSerializer.Serialize(new IFilterDescriptor[] { new CustomFilterDescriptor("Total", "Never registered operator", _ => true) });
        var loaded = FilterSerializer.Deserialize(json);

        Assert.Empty(loaded.Filters);
        Assert.Single(loaded.Warnings);
    }

    [Fact]
    public void ODataBuilderEscapesLiteralsAndOmitsUnpagedTake()
    {
        var request = new DataSourceRequest { Take = 0 };
        request.Filters.Add(new FilterDescriptor("Customer", FilterOperator.IsEqualTo, "O'Brien"));
        request.Filters.Add(new FilterDescriptor("Total", FilterOperator.IsGreaterThan, 10.5m));
        request.Sorts.Add(new SortDescriptor("Address.City", ListSortDirection.Descending));

        var query = RemoteDataSource<Order>.ToODataParameters(request);

        Assert.DoesNotContain("$top", query);
        Assert.Contains("$orderby=Address%2FCity%20desc", query);
        Assert.Contains(Uri.EscapeDataString("tolower(Customer) eq 'o''brien'"), query);
        Assert.Contains(Uri.EscapeDataString("Total gt 10.5"), query);
        Assert.Contains("$count=true", query);

        var paged = new DataSourceRequest { Skip = 40, Take = 20 };
        var pagedQuery = RemoteDataSource<Order>.ToODataParameters(paged);
        Assert.Contains("$top=20", pagedQuery);
        Assert.Contains("$skip=40", pagedQuery);
    }

    [Fact]
    public async Task LargeDataSourcesCacheSortedResultsUntilInvalidated()
    {
        var items = Enumerable.Range(0, 30_000).Select(i => new Order { Id = i, Total = (i * 7919) % 30_000 }).ToList();
        var source = new DataSource<Order> (items) { OffloadThreshold = int.MaxValue };
        var request = new DataSourceRequest { Skip = 0, Take = 3 };
        request.Sorts.Add(new SortDescriptor("Total"));

        var first = await source.ReadAsync(request);
        Assert.Equal(0, first.Data!.First().Id);

        var runnerUp = items.First(o => o.Total == 1m);
        runnerUp.Total = -5m;
        var cached = await source.ReadAsync(request);
        Assert.Equal(0, cached.Data!.First().Id);

        source.Invalidate();
        var fresh = await source.ReadAsync(request);
        Assert.Equal(runnerUp.Id, fresh.Data!.First().Id);

        request.Skip = 100;
        var window = await source.ReadAsync(request);
        Assert.Equal(3, window.Data!.Count());
        Assert.Equal(30_000, window.Total);
    }

    [Fact]
    public async Task OffloadedReadsHonorCancellation()
    {
        var items = Enumerable.Range(0, 60_000).Select(i => new Order { Id = i }).ToList();
        var source = new DataSource<Order>(items);
        using var cts = new System.Threading.CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.ReadAsync(new DataSourceRequest(), cts.Token));
    }
}
