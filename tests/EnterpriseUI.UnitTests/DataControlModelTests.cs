using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using EnterpriseUI.Data;
using EnterpriseUI.Grids;
using Xunit;

namespace EnterpriseUI.Tests.Unit;

public class TreeMapLayoutTests
{
    [Fact]
    public void RectanglesTileTheAreaInProportionToTheirValues()
    {
        var items = new[] { new TreeMapItem("a", 6), new TreeMapItem("b", 6), new TreeMapItem("c", 4), new TreeMapItem("d", 3), new TreeMapItem("e", 2), new TreeMapItem("f", 2), new TreeMapItem("g", 1) };

        var rects = TreeMapLayout.Compute(items, 0, 0, 600, 400);

        Assert.Equal(7, rects.Count);
        Assert.Equal(600 * 400, rects.Sum(r => r.Width * r.Height), 3);
        foreach (var r in rects)
        {
            Assert.Equal(r.Item.TotalValue / 24d * 240000, r.Width * r.Height, 3);
            Assert.InRange(r.X, -0.001, 600.001);
            Assert.True(r.X + r.Width <= 600.001 && r.Y + r.Height <= 400.001);
        }
        Assert.All(rects, r => Assert.True(Math.Max(r.Width / r.Height, r.Height / r.Width) < 6, "squarified rectangles stay close to square"));
    }

    [Fact]
    public void RectanglesNeverOverlap()
    {
        var rng = new Random(3);
        var items = Enumerable.Range(0, 40).Select(i => new TreeMapItem("n" + i, rng.Next(1, 100))).ToList();

        var rects = TreeMapLayout.Compute(items, 10, 20, 500, 300);

        for (var i = 0; i < rects.Count; i++)
        {
            for (var j = i + 1; j < rects.Count; j++)
            {
                var a = rects[i];
                var b = rects[j];
                var overlapW = Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X);
                var overlapH = Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y);
                Assert.False(overlapW > 0.001 && overlapH > 0.001, $"{a.Item.Name} overlaps {b.Item.Name}");
            }
        }
    }

    [Fact]
    public void ParentsReserveAHeaderAndContainTheirChildren()
    {
        var root = new TreeMapItem("root");
        var group = root.Add(new TreeMapItem("group"));
        group.Add("x", 5);
        group.Add("y", 3);
        root.Add("z", 2);

        var rects = TreeMapLayout.Compute(root.Children, 0, 0, 400, 300, maxDepth: 2, headerHeight: 20, padding: 2);

        var parent = rects.Single(r => r.Item.Name == "group");
        Assert.True(parent.IsHeader);
        var x = rects.Single(r => r.Item.Name == "x");
        Assert.Equal(1, x.Depth);
        Assert.True(x.Y >= parent.Y + 20 + 2 - 0.001);
        Assert.True(x.X >= parent.X + 2 - 0.001 && x.X + x.Width <= parent.X + parent.Width - 2 + 0.001);
        Assert.True(x.Y + x.Height <= parent.Y + parent.Height - 2 + 0.001);
        Assert.Equal(0, root.Children.IndexOf(group));
        Assert.Equal("root / group / x", x.Item.Path);
    }

    [Fact]
    public void EmptyZeroAndNegativeItemsAreSkipped()
    {
        Assert.Empty(TreeMapLayout.Compute(Array.Empty<TreeMapItem>(), 0, 0, 100, 100));
        Assert.Empty(TreeMapLayout.Compute(new[] { new TreeMapItem("z", 0), new TreeMapItem("n", -5) }, 0, 0, 100, 100));
        Assert.Empty(TreeMapLayout.Compute(new[] { new TreeMapItem("a", 5) }, 0, 0, 0, 100));
    }
}

public class PivotEngineTests
{
    private sealed record Sale(string Region, string Product, string Channel, decimal Amount, int Qty);

    private static readonly Sale[] Sales =
    {
        new("EMEA", "Bike", "Web", 100, 1), new("EMEA", "Bike", "Shop", 200, 2), new("EMEA", "Helmet", "Web", 30, 3),
        new("APAC", "Bike", "Web", 400, 4), new("APAC", "Helmet", "Shop", 50, 5), new("APAC", "Helmet", "Web", 70, 6)
    };

    private static PivotEngine<Sale> Engine(Action<PivotEngine<Sale>> configure)
    {
        var engine = new PivotEngine<Sale>();
        configure(engine);
        return engine;
    }

    [Fact]
    public void CrossTabAggregatesRowAndColumnFieldsWithGrandTotals()
    {
        var result = Engine(e =>
        {
            e.RowFields.Add("Region");
            e.ColumnFields.Add("Product");
            e.Values.Add(new PivotValueField("Amount"));
        }).Compute(Sales);

        Assert.Equal(new[] { "Bike", "Helmet", "Grand total" }, result.HeaderRows[0].Select(h => h.Text));
        var apac = result.Rows[0];
        var emea = result.Rows[1];
        Assert.Equal("APAC", apac.Labels[0]);
        Assert.Equal(new object?[] { 400m, 120m, 520m }, apac.Values);
        Assert.Equal(new object?[] { 300m, 30m, 330m }, emea.Values);
        var grand = result.Rows[^1];
        Assert.Equal(PivotRowKind.GrandTotal, grand.Kind);
        Assert.Equal(new object?[] { 700m, 150m, 850m }, grand.Values);
    }

    [Fact]
    public void TwoRowLevelsProduceHierarchicalLabelsAndSubtotals()
    {
        var result = Engine(e =>
        {
            e.RowFields.AddRange(new[] { "Region", "Product" });
            e.Values.Add(new PivotValueField("Qty"));
        }).Compute(Sales);

        var kinds = result.Rows.Select(r => r.Kind).ToList();
        Assert.Equal(new[] { PivotRowKind.Detail, PivotRowKind.Detail, PivotRowKind.Subtotal, PivotRowKind.Detail, PivotRowKind.Detail, PivotRowKind.Subtotal, PivotRowKind.GrandTotal }, kinds);
        Assert.Equal("APAC", result.Rows[0].Labels[0]);
        Assert.Equal(string.Empty, result.Rows[1].Labels[0]);
        Assert.Equal("Total APAC", result.Rows[2].Labels[0]);
        Assert.Equal(15m, result.Rows[2].Values[0]);
        Assert.Equal(21m, result.Rows[^1].Values[0]);
    }

    [Fact]
    public void CollapsingAGroupShowsOnlyItsSubtotalAndSubtotalsCanBeHidden()
    {
        var engine = Engine(e =>
        {
            e.RowFields.AddRange(new[] { "Region", "Product" });
            e.Values.Add(new PivotValueField("Qty"));
        });
        var rows = engine.Compute(Sales).Rows;
        var groupKey = rows[0].GroupKeys[0]!;

        engine.CollapsedGroups.Add(groupKey);
        var collapsed = engine.Compute(Sales).Rows;

        Assert.Equal(PivotRowKind.Subtotal, collapsed[0].Kind);
        Assert.True(collapsed[0].IsCollapsed);
        Assert.Equal("APAC", collapsed[0].Labels[0]);
        Assert.Equal(15m, collapsed[0].Values[0]);
        Assert.Equal(rows.Count - 2, collapsed.Count);

        engine.CollapsedGroups.Clear();
        engine.ShowSubtotals = false;
        Assert.DoesNotContain(engine.Compute(Sales).Rows, r => r.Kind == PivotRowKind.Subtotal);
    }

    [Fact]
    public void SeveralValueFieldsAndColumnLevelsBuildSpanningHeaders()
    {
        var result = Engine(e =>
        {
            e.RowFields.Add("Region");
            e.ColumnFields.AddRange(new[] { "Product", "Channel" });
            e.Values.Add(new PivotValueField("Amount", AggregateFunction.Sum, "Amount"));
            e.Values.Add(new PivotValueField("Qty", AggregateFunction.Count, "Orders"));
        }).Compute(Sales);

        Assert.Equal(3, result.HeaderRows.Count);
        var product = result.HeaderRows[0];
        Assert.Equal("Bike", product[0].Text);
        Assert.Equal(4, product[0].Span);
        Assert.Equal(0, product[0].Start);
        Assert.Equal("Helmet", product[1].Text);
        Assert.Equal(4, product[1].Span);
        Assert.Equal(8, product[2].Start);
        Assert.Equal(result.ColumnCount, result.Rows[0].Values.Length);
        Assert.Equal(new[] { "Amount", "Orders" }, result.LeafHeaders.Take(2).Select(h => h.Text));
    }

    [Fact]
    public void FunctionsAndEmptyDataAreHandled()
    {
        var avg = Engine(e => { e.Values.Add(new PivotValueField("Amount", AggregateFunction.Average)); e.Values.Add(new PivotValueField("Amount", AggregateFunction.Max, "Max")); e.Values.Add(new PivotValueField("Region", AggregateFunction.CountDistinct, "Regions")); }).Compute(Sales);
        Assert.Single(avg.Rows);
        Assert.Equal(850m / 6, (decimal)avg.Rows[0].Values[0]!, 4);
        Assert.Equal(400m, avg.Rows[0].Values[1]);
        Assert.Equal(2, avg.Rows[0].Values[2]);

        var empty = Engine(e => { e.RowFields.Add("Region"); e.ColumnFields.Add("Product"); e.Values.Add(new PivotValueField("Amount")); }).Compute(Array.Empty<Sale>());
        Assert.Single(empty.Rows);
        Assert.Equal(PivotRowKind.GrandTotal, empty.Rows[0].Kind);
    }

    [Fact]
    public void ValueFormatsApplyToDisplayText()
    {
        var result = Engine(e => { e.RowFields.Add("Region"); e.Values.Add(new PivotValueField("Amount", format: "N0")); }).Compute(Sales);
        Assert.Equal(1000.ToString("N0"), result.FormatValue(0, 1000m));
        Assert.Equal(string.Empty, result.FormatValue(0, null));
    }
}

public class FacetModelTests
{
    private sealed record Product(string Category, string Brand, decimal Price);

    private static readonly Product[] Products =
    {
        new("Bikes", "Acme", 100), new("Bikes", "Zed", 300), new("Helmets", "Acme", 40), new("Helmets", "Zed", 60), new("Helmets", "Acme", 80)
    };

    private static FacetModel<Product> Model()
    {
        var model = new FacetModel<Product>();
        model.AddList("Category");
        model.AddList("Brand");
        model.AddRange("Price");
        model.Load(Products);
        return model;
    }

    [Fact]
    public void LoadComputesValuesWithCountsAndRangeBounds()
    {
        var model = Model();

        Assert.Equal(new[] { ("Bikes", 2), ("Helmets", 3) }, model.Fields[0].Values.Select(v => (v.Text, v.Count)));
        Assert.Equal(40m, model.Fields[2].Minimum);
        Assert.Equal(300m, model.Fields[2].Maximum);
    }

    [Fact]
    public void SelectionsAreOrWithinAFacetAndAndAcrossFacets()
    {
        var model = Model();
        model.Toggle("Category", "Helmets", true);
        Assert.Equal(3, model.Apply(Products).Count());

        model.Toggle("Brand", "Acme", true);
        Assert.Equal(2, model.Apply(Products).Count());

        model.Toggle("Brand", "Zed", true);
        Assert.Equal(3, model.Apply(Products).Count());

        model.SetRange("Price", 50m, null);
        Assert.Equal(2, model.Apply(Products).Count());
        model.ClearAll();
        Assert.False(model.HasSelection);
        Assert.Equal(5, model.Apply(Products).Count());
    }

    [Fact]
    public void DescriptorsGiveTheSameResultAsThePredicate()
    {
        var model = Model();
        model.Toggle("Category", "Helmets", true);
        model.Toggle("Brand", "Acme", true);
        model.Toggle("Brand", "Zed", true);
        model.SetRange("Price", 50m, 100m);

        var request = new DataSourceRequest { PageSize = 100 };
        request.Filters.AddRange(model.ToDescriptors());
        var viaEngine = QueryEngine.Execute(Products.AsQueryable(), request).Data!.ToList();

        Assert.Equal(model.Apply(Products).ToList(), viaEngine);
        Assert.Equal(2, viaEngine.Count);
    }

    [Fact]
    public void CountsReflectTheOtherSelectedFacets()
    {
        var model = Model();
        model.Toggle("Category", "Helmets", true);

        Assert.Equal(2, model.CountWith(model.Fields[1], "Acme"));
        Assert.Equal(1, model.CountWith(model.Fields[1], "Zed"));
    }
}

public class DataFilterModelTests
{
    private sealed record Row(string Region, decimal Total, DateTime Date, bool Active, string? Note);

    private static readonly Row[] Rows =
    {
        new("EMEA", 50, new DateTime(2024, 1, 5), true, null), new("EMEA", 500, new DateTime(2024, 3, 5), false, "x"),
        new("APAC", 150, new DateTime(2024, 2, 5), true, "y"), new("AMER", 900, new DateTime(2024, 6, 5), true, ""), new("APAC", 20, new DateTime(2023, 12, 5), false, null)
    };

    private static DataFilterModel<Row> Complex()
    {
        var model = new DataFilterModel<Row>();
        var or = model.Root.AddGroup(FilterLogicalOperator.Or);
        or.AddCondition("Region", FilterOperator.IsEqualTo, "EMEA");
        or.AddCondition("Total", FilterOperator.IsGreaterThan, "800");
        var not = model.Root.AddGroup(FilterLogicalOperator.And);
        not.Negate = true;
        not.AddCondition("Active", FilterOperator.IsEqualTo, "false");
        not.AddCondition("Total", FilterOperator.IsLessThan, "100");
        return model;
    }

    [Fact]
    public void DescriptorTreeEvaluatesLikeThePredicateIncludingNot()
    {
        var model = Complex();

        var request = new DataSourceRequest { PageSize = 100 };
        request.Filters.Add(model.ToDescriptor()!);
        var viaEngine = QueryEngine.Execute(Rows.AsQueryable(), request).Data!.ToList();

        Assert.Equal(model.Apply(Rows).ToList(), viaEngine);
        Assert.NotEmpty(viaEngine);
        Assert.True(viaEngine.Count < Rows.Length);
    }

    [Theory]
    [InlineData(FilterOperator.StartsWith, "E")]
    [InlineData(FilterOperator.Contains, "ME")]
    [InlineData(FilterOperator.IsEqualTo, "EMEA")]
    public void NegatedConditionsProduceTheComplementEvenWithoutAnInverseOperator(FilterOperator op, string value)
    {
        var model = new DataFilterModel<Row>();
        var condition = model.Root.AddCondition("Region", op, value);
        var positive = model.Apply(Rows).Count();
        condition.Negate = true;

        var request = new DataSourceRequest { PageSize = 100 };
        request.Filters.Add(model.ToDescriptor()!);
        var negated = QueryEngine.Execute(Rows.AsQueryable(), request).Data!.Count();

        Assert.Equal(Rows.Length - positive, negated);
        Assert.Equal(Rows.Length - positive, model.Apply(Rows).Count());
    }

    [Fact]
    public void TextDescribesTheExpression()
    {
        Assert.Equal("((Region is equal to EMEA OR Total is greater than 800) AND NOT (Active is equal to false AND Total is less than 100))", Complex().ToText());
        Assert.Equal(string.Empty, new DataFilterModel<Row>().ToText());
        Assert.Null(new DataFilterModel<Row>().ToDescriptor());
    }

    [Fact]
    public void ValidationReportsValuesThatDoNotFitTheFieldType()
    {
        var model = new DataFilterModel<Row>();
        model.Root.AddCondition("Total", FilterOperator.IsGreaterThan, "abc");
        model.Root.AddCondition("Date", FilterOperator.IsBetween, "2024-01-01", "nope");
        model.Root.AddCondition("Note", FilterOperator.IsNull);

        var errors = model.Validate();

        Assert.Equal(2, errors.Count);
        Assert.Contains("Total", errors[0]);
    }

    [Fact]
    public void OperatorsDependOnTheFieldType()
    {
        Assert.Contains(FilterOperator.Contains, DataFilterModel<Row>.OperatorsFor(typeof(string)));
        Assert.DoesNotContain(FilterOperator.Contains, DataFilterModel<Row>.OperatorsFor(typeof(decimal)));
        Assert.Contains(FilterOperator.IsBetween, DataFilterModel<Row>.OperatorsFor(typeof(DateTime)));
        Assert.Equal(4, DataFilterModel<Row>.OperatorsFor(typeof(bool)).Count);
        Assert.False(DataFilterModel<Row>.NeedsValue(FilterOperator.IsNull));
        Assert.True(DataFilterModel<Row>.NeedsSecondValue(FilterOperator.IsBetween));
        Assert.Equal(new[] { "Region", "Total", "Date", "Active", "Note" }, new DataFilterModel<Row>().Fields);
    }

    [Fact]
    public void LoadRebuildsTheTreeFromADescriptor()
    {
        var model = new DataFilterModel<Row>();
        model.Load(new CompositeFilterDescriptor
        {
            LogicalOperator = FilterLogicalOperator.Or,
            FilterDescriptors = { new FilterDescriptor("Region", FilterOperator.IsEqualTo, "APAC"), new FilterDescriptor("Total", FilterOperator.IsGreaterThan, 800m) }
        });

        Assert.Equal(FilterLogicalOperator.Or, model.Root.Operator);
        Assert.Equal(2, model.Root.Children.Count);
        Assert.Equal(3, model.Apply(Rows).Count());
        Assert.Equal("(Region is equal to APAC OR Total is greater than 800)", model.ToText());
    }
}

public class PropertyGridModelTests
{
    public enum Mode { Fast, Slow }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class Address
    {
        public string City { get; set; } = "Oslo";
        public int Zip { get; set; } = 150;
    }

    public sealed class Settings
    {
        [Category("General"), Description("The display name.")] public string Title { get; set; } = "Demo";
        [Category("General")] public int Count { get; set; } = 3;
        [Category("Behavior")] public bool Enabled { get; set; } = true;
        [Category("Behavior")] public Mode Speed { get; set; } = Mode.Fast;
        [Category("Appearance")] public System.Drawing.Color Accent { get; set; } = System.Drawing.Color.Red;
        [Category("Appearance")] public DateTime Created { get; set; } = new DateTime(2024, 1, 2);
        [Category("Data")] public Address Where { get; set; } = new();
        [Category("Data"), ReadOnly(true)] public string Id { get; set; } = "abc";
        [Browsable(false)] public string Hidden { get; set; } = "x";
        public double? Limit { get; set; }
    }

    public sealed class ThemeChoiceConverter : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context) => new(new[] { "Light", "Dark" });
    }

    public sealed class Themed
    {
        [TypeConverter(typeof(ThemeChoiceConverter))] public string Theme { get; set; } = "Light";
    }

    [Fact]
    public void StringPropertiesWithStandardValuesBecomeChoices()
    {
        var entry = PropertyGridModel.Entries(new Themed()).Single();

        Assert.Equal(PropertyEditorKind.Choice, entry.Kind);
        Assert.Equal(new[] { "Light", "Dark" }, entry.Choices);
        Assert.True(entry.TrySetText("Dark", out _));
        Assert.Equal("Dark", entry.Value);
    }

    [Fact]
    public void PropertiesAreGroupedByCategoryAndHiddenOnesAreSkipped()
    {
        var categories = PropertyGridModel.Build(new Settings());

        Assert.Equal(new[] { "Appearance", "Behavior", "Data", "General", "Misc" }, categories.Select(c => c.Name));
        Assert.DoesNotContain(categories.SelectMany(c => c.Entries), e => e.Name == "Hidden");
        Assert.Equal("The display name.", categories.SelectMany(c => c.Entries).Single(e => e.Name == "Title").Description);
    }

    [Fact]
    public void EditorKindsFollowThePropertyType()
    {
        var kinds = PropertyGridModel.Entries(new Settings()).ToDictionary(e => e.Name, e => e.Kind);

        Assert.Equal(PropertyEditorKind.Text, kinds["Title"]);
        Assert.Equal(PropertyEditorKind.Number, kinds["Count"]);
        Assert.Equal(PropertyEditorKind.Boolean, kinds["Enabled"]);
        Assert.Equal(PropertyEditorKind.Enum, kinds["Speed"]);
        Assert.Equal(PropertyEditorKind.Color, kinds["Accent"]);
        Assert.Equal(PropertyEditorKind.DateTime, kinds["Created"]);
        Assert.Equal(PropertyEditorKind.Nested, kinds["Where"]);
        Assert.Equal(PropertyEditorKind.Number, kinds["Limit"]);
    }

    [Fact]
    public void ValuesAreConvertedFromTextAndInvalidInputIsRejectedWithAMessage()
    {
        var settings = new Settings();
        var entries = PropertyGridModel.Entries(settings).ToDictionary(e => e.Name);

        Assert.True(entries["Count"].TrySetText("42", out _));
        Assert.Equal(42, settings.Count);
        Assert.False(entries["Count"].TrySetText("many", out var error));
        Assert.False(string.IsNullOrEmpty(error));
        Assert.Equal(42, settings.Count);

        Assert.True(entries["Speed"].TrySetText("Slow", out _));
        Assert.Equal(Mode.Slow, settings.Speed);
        Assert.Equal(new[] { "Fast", "Slow" }, entries["Speed"].Choices);

        Assert.True(entries["Enabled"].TrySetText("False", out _));
        Assert.False(settings.Enabled);
        Assert.True(entries["Limit"].TrySetText(2.5.ToString(System.Globalization.CultureInfo.CurrentCulture), out _));
        Assert.Equal(2.5, settings.Limit);
        Assert.True(entries["Limit"].TrySetText("", out _));
        Assert.Null(settings.Limit);

        Assert.False(entries["Id"].TrySetText("zzz", out error));
        Assert.Equal("abc", settings.Id);
        Assert.True(entries["Id"].IsReadOnly);
    }

    [Fact]
    public void ColorsRoundTripThroughTheirTextForm()
    {
        var settings = new Settings();
        var accent = PropertyGridModel.Entries(settings).Single(e => e.Name == "Accent");

        Assert.Equal("Red", accent.DisplayText);
        Assert.True(accent.TrySetText("Blue", out _));
        Assert.Equal(System.Drawing.Color.Blue, settings.Accent);
    }

    [Fact]
    public void NestedObjectsExposeTheirOwnProperties()
    {
        var settings = new Settings();
        var where = PropertyGridModel.Entries(settings).Single(e => e.Name == "Where");

        var children = where.Children;

        Assert.Equal(new[] { "City", "Zip" }, children.Select(c => c.Name));
        Assert.All(children, c => Assert.Equal(1, c.Depth));
        Assert.True(children[1].TrySetText("999", out _));
        Assert.Equal(999, settings.Where.Zip);
    }

    [Fact]
    public void FilteringSearchesNamesDescriptionsAndValuesAndFlatModeSortsAlphabetically()
    {
        var settings = new Settings();

        Assert.Equal(new[] { "Title" }, PropertyGridModel.Build(settings, true, "display name").SelectMany(c => c.Entries).Select(e => e.Name));
        Assert.Equal(new[] { "Speed" }, PropertyGridModel.Build(settings, true, "fast").SelectMany(c => c.Entries).Select(e => e.Name));

        var flat = PropertyGridModel.Build(settings, categorized: false);
        Assert.Single(flat);
        Assert.Equal(flat[0].Entries.OrderBy(e => e.DisplayName, StringComparer.CurrentCultureIgnoreCase).Select(e => e.Name), flat[0].Entries.Select(e => e.Name));
        Assert.DoesNotContain(PropertyGridModel.Build(settings, true, null, includeReadOnly: false).SelectMany(c => c.Entries), e => e.Name == "Id");
        Assert.Empty(PropertyGridModel.Build(null));
    }
}

public class EntityFormModelTests
{
    public enum Level { Junior, Senior }

    public sealed class Person
    {
        [Required, StringLength(10, MinimumLength = 2)] public string Name { get; set; } = "Ann";
        [EmailAddress] public string? Email { get; set; }
        [Range(18, 70)] public int Age { get; set; } = 30;
        public Level Level { get; set; }
        public bool Active { get; set; } = true;
        [DataType(DataType.Date)] public DateTime Hired { get; set; } = new DateTime(2020, 1, 1);
        [Display(Name = "Monthly pay", Order = 1)] public decimal Salary { get; set; } = 1000;
        [DataType(DataType.Password)] public string? Secret { get; set; }
        public int ReadOnlyId => 7;
        [Browsable(false)] public string Internal { get; set; } = "";
    }

    [Fact]
    public void FieldsComeFromPropertiesWithEditorKindsAndLabels()
    {
        var model = new EntityFormModel(new Person());

        Assert.Equal("Monthly pay", model.Fields[0].Label);
        Assert.DoesNotContain(model.Fields, f => f.Name == "Internal");
        Assert.Equal(EntityEditorKind.Number, model.Field("Age")!.Kind);
        Assert.Equal(EntityEditorKind.Boolean, model.Field("Active")!.Kind);
        Assert.Equal(EntityEditorKind.Choice, model.Field("Level")!.Kind);
        Assert.Equal(EntityEditorKind.Date, model.Field("Hired")!.Kind);
        Assert.Equal(EntityEditorKind.Email, model.Field("Email")!.Kind);
        Assert.Equal(EntityEditorKind.Password, model.Field("Secret")!.Kind);
        Assert.True(model.Field("ReadOnlyId")!.IsReadOnly);
        Assert.True(model.Field("Name")!.IsRequired);
        Assert.Equal(10, model.Field("Name")!.MaxLength);
    }

    [Fact]
    public void EditsStayPendingUntilAppliedAndInvalidValuesBlockApply()
    {
        var person = new Person();
        var model = new EntityFormModel(person);

        Assert.True(model.SetText("Name", "Bob", out _));
        Assert.Equal("Ann", person.Name);
        Assert.True(model.HasChanges);

        Assert.False(model.SetText("Age", "12", out var error));
        Assert.Contains("18", error);
        Assert.False(model.Apply());
        Assert.Equal("Ann", person.Name);

        Assert.True(model.SetText("Age", "44", out _));
        Assert.True(model.Apply());
        Assert.Equal("Bob", person.Name);
        Assert.Equal(44, person.Age);
        Assert.False(model.HasChanges);
    }

    [Fact]
    public void ConversionErrorsExplainWhatIsExpected()
    {
        var model = new EntityFormModel(new Person());

        Assert.False(model.SetText("Age", "abc", out var number));
        Assert.Equal("Age must be a number.", number);
        Assert.False(model.SetText("Hired", "yesterday-ish", out var date));
        Assert.Contains("date", date);
        Assert.False(model.SetText("Level", "Boss", out var level));
        Assert.Contains("Junior", level);
        Assert.False(model.SetText("Email", "nope", out var email));
        Assert.False(string.IsNullOrEmpty(email));
        Assert.False(model.SetText("Name", "", out var required));
        Assert.Contains("required", required);
        Assert.Equal(5, model.Errors.Count);
    }

    [Fact]
    public void ResetDiscardsPendingValuesAndErrors()
    {
        var model = new EntityFormModel(new Person());
        model.SetText("Age", "1", out _);

        model.Reset();

        Assert.False(model.HasChanges);
        Assert.Empty(model.Errors);
        Assert.Equal("30", model.CurrentText("Age"));
    }

    [Fact]
    public void EnumsBooleansAndDatesConvertFromText()
    {
        var person = new Person();
        var model = new EntityFormModel(person);

        Assert.True(model.SetText("Level", "senior", out _));
        Assert.True(model.SetText("Active", "False", out _));
        Assert.True(model.SetText("Hired", new DateTime(2021, 5, 6).ToString("d"), out _));
        Assert.True(model.SetText("Salary", 1234.5m.ToString(System.Globalization.CultureInfo.CurrentCulture), out _));
        Assert.True(model.Apply());

        Assert.Equal(Level.Senior, person.Level);
        Assert.False(person.Active);
        Assert.Equal(new DateTime(2021, 5, 6), person.Hired);
        Assert.Equal(1234.5m, person.Salary);
        Assert.Throws<ArgumentException>(() => model.SetText("Missing", "x", out _));
    }
}
