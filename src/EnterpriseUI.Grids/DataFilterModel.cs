using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EnterpriseUI.Data;

namespace EnterpriseUI.Grids;

public abstract class FilterNode
{
    public FilterGroupNode? Parent { get; internal set; }

    /// <summary>Inverts the node (NOT).</summary>
    public bool Negate { get; set; }
}

public sealed class FilterGroupNode : FilterNode
{
    public FilterLogicalOperator Operator { get; set; } = FilterLogicalOperator.And;
    public List<FilterNode> Children { get; } = new();

    public FilterConditionNode AddCondition(string field, FilterOperator op, string? value = null, string? value2 = null)
    {
        var node = new FilterConditionNode { Field = field, Operator = op, Value = value, Value2 = value2, Parent = this };
        Children.Add(node);
        return node;
    }

    public FilterGroupNode AddGroup(FilterLogicalOperator op = FilterLogicalOperator.Or)
    {
        var node = new FilterGroupNode { Operator = op, Parent = this };
        Children.Add(node);
        return node;
    }

    public void Remove(FilterNode node)
    {
        if (Children.Remove(node)) node.Parent = null;
    }
}

public sealed class FilterConditionNode : FilterNode
{
    public string Field { get; set; } = string.Empty;
    public FilterOperator Operator { get; set; } = FilterOperator.IsEqualTo;

    /// <summary>Operand as typed by the user; converted to the field type when the filter is built.</summary>
    public string? Value { get; set; }

    public string? Value2 { get; set; }
}

/// <summary>
/// Editable AND/OR/NOT expression tree over the fields of <typeparamref name="TItem"/>, convertible to filter descriptors, text and a predicate.
/// </summary>
public sealed class DataFilterModel<TItem>
{
    private static readonly FilterOperator[] TextOperators =
    {
        FilterOperator.Contains, FilterOperator.DoesNotContain, FilterOperator.StartsWith, FilterOperator.EndsWith, FilterOperator.IsEqualTo,
        FilterOperator.IsNotEqualTo, FilterOperator.IsEmpty, FilterOperator.IsNotEmpty, FilterOperator.IsNull, FilterOperator.IsNotNull
    };

    private static readonly FilterOperator[] ComparableOperators =
    {
        FilterOperator.IsEqualTo, FilterOperator.IsNotEqualTo, FilterOperator.IsGreaterThan, FilterOperator.IsGreaterThanOrEqualTo,
        FilterOperator.IsLessThan, FilterOperator.IsLessThanOrEqualTo, FilterOperator.IsBetween, FilterOperator.IsNotBetween, FilterOperator.IsNull, FilterOperator.IsNotNull
    };

    private static readonly FilterOperator[] EqualityOperators = { FilterOperator.IsEqualTo, FilterOperator.IsNotEqualTo, FilterOperator.IsNull, FilterOperator.IsNotNull };

    private readonly Type _itemType;

    public DataFilterModel() : this(typeof(TItem))
    {
    }

    /// <summary>Creates a model over the properties of a runtime type, for untyped hosts such as a control.</summary>
    public DataFilterModel(Type itemType)
    {
        _itemType = itemType ?? throw new ArgumentNullException(nameof(itemType));
        Fields = _itemType.GetProperties()
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && IsSimple(p.PropertyType))
            .Select(p => p.Name).ToList();
    }

    public FilterGroupNode Root { get; } = new();

    public event EventHandler? Changed;

    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>Public readable properties with a simple type, the fields a condition can use.</summary>
    public IReadOnlyList<string> Fields { get; }

    public Type FieldTypeOf(string field) => PropertyPath.GetPathType(_itemType, field) ?? typeof(string);

    public static Type FieldType(string field) => PropertyPath.GetPathType(typeof(TItem), field) ?? typeof(string);

    public static IReadOnlyList<FilterOperator> OperatorsFor(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(string)) return TextOperators;
        if (type == typeof(bool) || type.IsEnum || type == typeof(Guid)) return EqualityOperators;
        return ComparableOperators;
    }

    public static bool NeedsValue(FilterOperator op) => op is not (FilterOperator.IsNull or FilterOperator.IsNotNull or FilterOperator.IsEmpty or FilterOperator.IsNotEmpty);

    public static bool NeedsSecondValue(FilterOperator op) => op is FilterOperator.IsBetween or FilterOperator.IsNotBetween;

    public static string Describe(FilterOperator op) => op switch
    {
        FilterOperator.IsEqualTo => "is equal to",
        FilterOperator.IsNotEqualTo => "is not equal to",
        FilterOperator.IsLessThan => "is less than",
        FilterOperator.IsLessThanOrEqualTo => "is less than or equal to",
        FilterOperator.IsGreaterThan => "is greater than",
        FilterOperator.IsGreaterThanOrEqualTo => "is greater than or equal to",
        FilterOperator.StartsWith => "starts with",
        FilterOperator.EndsWith => "ends with",
        FilterOperator.Contains => "contains",
        FilterOperator.DoesNotContain => "does not contain",
        FilterOperator.IsNull => "is null",
        FilterOperator.IsNotNull => "is not null",
        FilterOperator.IsEmpty => "is empty",
        FilterOperator.IsNotEmpty => "is not empty",
        FilterOperator.IsBetween => "is between",
        FilterOperator.IsNotBetween => "is not between",
        FilterOperator.IsContainedIn => "is in",
        FilterOperator.IsNotContainedIn => "is not in",
        _ => op.ToString()
    };

    private static bool IsSimple(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid) || type == typeof(TimeSpan);
    }

    public static bool TryConvert(Type type, string? text, out object? value)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        value = null;
        if (text == null) return true;
        if (type == typeof(string)) { value = text; return true; }
        if (string.IsNullOrWhiteSpace(text)) return false;
        try
        {
            if (type.IsEnum) { value = Enum.Parse(type, text, true); return true; }
            if (type == typeof(bool)) { value = bool.Parse(text); return true; }
            if (type == typeof(Guid)) { value = Guid.Parse(text); return true; }
            if (type == typeof(DateTime)) { value = DateTime.Parse(text, CultureInfo.CurrentCulture); return true; }
            if (type == typeof(DateTimeOffset)) { value = DateTimeOffset.Parse(text, CultureInfo.CurrentCulture); return true; }
            if (type == typeof(TimeSpan)) { value = TimeSpan.Parse(text, CultureInfo.CurrentCulture); return true; }
            value = Convert.ChangeType(text, type, CultureInfo.CurrentCulture);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException or InvalidCastException)
        {
            return false;
        }
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        void Visit(FilterNode node)
        {
            if (node is FilterGroupNode group)
            {
                foreach (var child in group.Children) Visit(child);
                return;
            }

            var condition = (FilterConditionNode)node;
            var type = FieldTypeOf(condition.Field);
            if (NeedsValue(condition.Operator) && !TryConvert(type, condition.Value, out _)) errors.Add($"{condition.Field}: '{condition.Value}' is not a valid {type.Name}.");
            if (NeedsSecondValue(condition.Operator) && !TryConvert(type, condition.Value2, out _)) errors.Add($"{condition.Field}: '{condition.Value2}' is not a valid {type.Name}.");
        }
        Visit(Root);
        return errors;
    }

    /// <summary>Builds the descriptor tree; null when the model has no conditions. NOT is pushed down with De Morgan's laws.</summary>
    public IFilterDescriptor? ToDescriptor() => Build(Root, false);

    private IFilterDescriptor? Build(FilterNode node, bool inverted)
    {
        var negate = inverted ^ node.Negate;
        if (node is FilterGroupNode group)
        {
            var children = group.Children.Select(c => Build(c, negate)).Where(c => c != null).Cast<IFilterDescriptor>().ToList();
            if (children.Count == 0) return null;
            if (children.Count == 1) return children[0];
            var op = negate ? (group.Operator == FilterLogicalOperator.And ? FilterLogicalOperator.Or : FilterLogicalOperator.And) : group.Operator;
            var composite = new CompositeFilterDescriptor { LogicalOperator = op };
            composite.FilterDescriptors.AddRange(children);
            return composite;
        }

        var condition = (FilterConditionNode)node;
        var type = FieldTypeOf(condition.Field);
        TryConvert(type, condition.Value, out var value);
        TryConvert(type, condition.Value2, out var value2);
        var effective = condition.Operator;
        if (!negate) return new FilterDescriptor(condition.Field, effective, value, value2);

        var inverse = Inverse(effective);
        if (inverse != null) return new FilterDescriptor(condition.Field, inverse.Value, value, value2);

        var field = condition.Field;
        return new CustomFilterDescriptor(field, "not " + Describe(effective), v => !FilterEvaluator.Matches(v, effective, value, value2));
    }

    private static FilterOperator? Inverse(FilterOperator op) => op switch
    {
        FilterOperator.IsEqualTo => FilterOperator.IsNotEqualTo,
        FilterOperator.IsNotEqualTo => FilterOperator.IsEqualTo,
        FilterOperator.IsLessThan => FilterOperator.IsGreaterThanOrEqualTo,
        FilterOperator.IsLessThanOrEqualTo => FilterOperator.IsGreaterThan,
        FilterOperator.IsGreaterThan => FilterOperator.IsLessThanOrEqualTo,
        FilterOperator.IsGreaterThanOrEqualTo => FilterOperator.IsLessThan,
        FilterOperator.Contains => FilterOperator.DoesNotContain,
        FilterOperator.DoesNotContain => FilterOperator.Contains,
        FilterOperator.IsNull => FilterOperator.IsNotNull,
        FilterOperator.IsNotNull => FilterOperator.IsNull,
        FilterOperator.IsEmpty => FilterOperator.IsNotEmpty,
        FilterOperator.IsNotEmpty => FilterOperator.IsEmpty,
        FilterOperator.IsBetween => FilterOperator.IsNotBetween,
        FilterOperator.IsNotBetween => FilterOperator.IsBetween,
        FilterOperator.IsContainedIn => FilterOperator.IsNotContainedIn,
        FilterOperator.IsNotContainedIn => FilterOperator.IsContainedIn,
        _ => null
    };

    public bool Matches(TItem item) => Evaluate(Root, item) ?? true;

    private bool? Evaluate(FilterNode node, TItem item)
    {
        bool? result;
        if (node is FilterGroupNode group)
        {
            var parts = group.Children.Select(c => Evaluate(c, item)).Where(r => r != null).Select(r => r!.Value).ToList();
            if (parts.Count == 0) return null;
            result = group.Operator == FilterLogicalOperator.And ? parts.All(p => p) : parts.Any(p => p);
        }
        else
        {
            var condition = (FilterConditionNode)node;
            var type = FieldTypeOf(condition.Field);
            TryConvert(type, condition.Value, out var value);
            TryConvert(type, condition.Value2, out var value2);
            result = FilterEvaluator.Matches(PropertyPath.GetValue(item, condition.Field), condition.Operator, value, value2);
        }
        return node.Negate ? !result : result;
    }

    public IEnumerable<TItem> Apply(IEnumerable<TItem> items) => items.Where(Matches);

    /// <summary>Readable text such as <c>(Region is equal to EMEA AND NOT (Total is less than 100))</c>.</summary>
    public string ToText() => Text(Root);

    private static string Text(FilterNode node)
    {
        string body;
        if (node is FilterGroupNode group)
        {
            var parts = group.Children.Select(Text).Where(t => t.Length > 0).ToList();
            if (parts.Count == 0) return string.Empty;
            body = parts.Count == 1 ? parts[0] : "(" + string.Join(group.Operator == FilterLogicalOperator.And ? " AND " : " OR ", parts) + ")";
        }
        else
        {
            var c = (FilterConditionNode)node;
            body = $"{c.Field} {Describe(c.Operator)}" + (NeedsValue(c.Operator) ? $" {c.Value}" : string.Empty) + (NeedsSecondValue(c.Operator) ? $" and {c.Value2}" : string.Empty);
        }
        return node.Negate ? "NOT " + (body.StartsWith('(') ? body : "(" + body + ")") : body;
    }

    /// <summary>Replaces the tree with the given descriptor; custom predicates cannot be represented and are skipped.</summary>
    public void Load(IFilterDescriptor? descriptor)
    {
        Root.Children.Clear();
        Root.Negate = false;
        if (descriptor == null) { RaiseChanged(); return; }

        void Add(FilterGroupNode parent, IFilterDescriptor d)
        {
            switch (d)
            {
                case CompositeFilterDescriptor composite:
                    var group = parent.AddGroup(composite.LogicalOperator);
                    foreach (var child in composite.FilterDescriptors) Add(group, child);
                    break;
                case FilterDescriptor filter:
                    parent.AddCondition(filter.Member, filter.Operator, filter.Value == null ? null : Convert.ToString(filter.Value, CultureInfo.CurrentCulture), filter.Value2 == null ? null : Convert.ToString(filter.Value2, CultureInfo.CurrentCulture));
                    break;
            }
        }

        if (descriptor is CompositeFilterDescriptor root)
        {
            Root.Operator = root.LogicalOperator;
            foreach (var child in root.FilterDescriptors) Add(Root, child);
        }
        else
        {
            Add(Root, descriptor);
        }
        RaiseChanged();
    }
}
