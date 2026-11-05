using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace EnterpriseUI.Data;

/// <summary>Named predicates that custom filter operators resolve to when saved filters are loaded.</summary>
public static class FilterOperatorRegistry
{
    private static readonly Dictionary<string, Func<object?, bool>> Operators = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();

    public static void Register(string name, Func<object?, bool> predicate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(predicate);
        lock (Gate) Operators[name] = predicate;
    }

    public static bool TryGet(string name, out Func<object?, bool>? predicate)
    {
        lock (Gate) return Operators.TryGetValue(name, out predicate);
    }

    public static IReadOnlyList<string> Names
    {
        get { lock (Gate) return Operators.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList(); }
    }
}

public sealed record FilterLoadResult(IReadOnlyList<IFilterDescriptor> Filters, IReadOnlyList<string> Warnings);

/// <summary>Lossless JSON persistence of filter trees for saved filters and layouts. Custom filters are stored by operator name only.</summary>
public static class FilterSerializer
{
    public static string Serialize(IEnumerable<IFilterDescriptor> filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", 1);
            writer.WriteStartArray("filters");
            foreach (var filter in filters) WriteFilter(writer, filter);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static FilterLoadResult Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var warnings = new List<string>();
        var filters = new List<IFilterDescriptor>();
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("filters", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in array.EnumerateArray())
            {
                var filter = ReadFilter(element, warnings);
                if (filter != null) filters.Add(filter);
            }
        }
        return new FilterLoadResult(filters, warnings);
    }

    private static void WriteFilter(Utf8JsonWriter writer, IFilterDescriptor filter)
    {
        writer.WriteStartObject();
        switch (filter)
        {
            case FilterDescriptor fd:
                writer.WriteString("type", "filter");
                writer.WriteString("member", fd.Member);
                writer.WriteString("operator", fd.Operator.ToString());
                writer.WriteBoolean("ignoreCase", fd.IgnoreCase);
                WriteValue(writer, "value", fd.Value);
                WriteValue(writer, "value2", fd.Value2);
                break;
            case CustomFilterDescriptor custom:
                writer.WriteString("type", "custom");
                writer.WriteString("member", custom.Member);
                writer.WriteString("operatorName", custom.OperatorName);
                break;
            case CompositeFilterDescriptor composite:
                writer.WriteString("type", "composite");
                writer.WriteString("logicalOperator", composite.LogicalOperator.ToString());
                writer.WriteStartArray("filters");
                foreach (var child in composite.FilterDescriptors) WriteFilter(writer, child);
                writer.WriteEndArray();
                break;
        }
        writer.WriteEndObject();
    }

    private static void WriteValue(Utf8JsonWriter writer, string name, object? value)
    {
        if (value == null) return;
        writer.WriteStartObject(name);
        if (value is IEnumerable items and not string)
        {
            writer.WriteString("t", "Array");
            writer.WriteStartArray("items");
            foreach (var item in items)
            {
                writer.WriteStartObject();
                writer.WriteString("t", TypeName(item));
                writer.WriteString("v", Text(item));
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        else
        {
            writer.WriteString("t", TypeName(value));
            writer.WriteString("v", Text(value));
        }
        writer.WriteEndObject();
    }

    private static string TypeName(object? value) => value switch
    {
        null => "Null",
        Enum => "String",
        _ => value.GetType().Name
    };

    private static string Text(object? value) => value switch
    {
        null => string.Empty,
        DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static IFilterDescriptor? ReadFilter(JsonElement element, List<string> warnings)
    {
        var type = element.TryGetProperty("type", out var t) ? t.GetString() : null;
        switch (type)
        {
            case "filter":
            {
                var member = element.GetProperty("member").GetString() ?? string.Empty;
                if (!Enum.TryParse<FilterOperator>(element.GetProperty("operator").GetString(), out var op))
                {
                    warnings.Add($"Unknown operator for '{member}' was skipped.");
                    return null;
                }

                return new FilterDescriptor(member, op, ReadValue(element, "value"))
                {
                    Value2 = ReadValue(element, "value2"),
                    IgnoreCase = !element.TryGetProperty("ignoreCase", out var ic) || ic.GetBoolean()
                };
            }

            case "custom":
            {
                var name = element.GetProperty("operatorName").GetString() ?? string.Empty;
                if (!FilterOperatorRegistry.TryGet(name, out var predicate) || predicate == null)
                {
                    warnings.Add($"Custom filter operator '{name}' is not registered and was skipped.");
                    return null;
                }
                return new CustomFilterDescriptor(element.GetProperty("member").GetString() ?? string.Empty, name, predicate);
            }

            case "composite":
            {
                var composite = new CompositeFilterDescriptor
                {
                    LogicalOperator = Enum.TryParse<FilterLogicalOperator>(element.GetProperty("logicalOperator").GetString(), out var lo) ? lo : FilterLogicalOperator.And
                };
                foreach (var child in element.GetProperty("filters").EnumerateArray())
                {
                    var filter = ReadFilter(child, warnings);
                    if (filter != null) composite.FilterDescriptors.Add(filter);
                }
                return composite;
            }

            default:
                warnings.Add($"Unknown filter type '{type}' was skipped.");
                return null;
        }
    }

    private static object? ReadValue(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var element)) return null;
        var type = element.GetProperty("t").GetString();
        if (type == "Array")
        {
            return element.GetProperty("items").EnumerateArray().Select(i => Convert(i.GetProperty("t").GetString(), i.GetProperty("v").GetString() ?? string.Empty)).ToList();
        }
        return Convert(type, element.GetProperty("v").GetString() ?? string.Empty);
    }

    private static object? Convert(string? type, string text)
    {
        var invariant = CultureInfo.InvariantCulture;
        return type switch
        {
            "Null" => null,
            "Boolean" => bool.Parse(text),
            "Byte" => byte.Parse(text, invariant),
            "Int16" => short.Parse(text, invariant),
            "Int32" => int.Parse(text, invariant),
            "Int64" => long.Parse(text, invariant),
            "Single" => float.Parse(text, invariant),
            "Double" => double.Parse(text, invariant),
            "Decimal" => decimal.Parse(text, invariant),
            "DateTime" => DateTime.Parse(text, invariant, DateTimeStyles.RoundtripKind),
            "DateTimeOffset" => DateTimeOffset.Parse(text, invariant),
            "DateOnly" => DateOnly.Parse(text, invariant),
            "TimeOnly" => TimeOnly.Parse(text, invariant),
            "TimeSpan" => TimeSpan.Parse(text, invariant),
            "Guid" => Guid.Parse(text),
            "Char" => text.Length > 0 ? text[0] : ' ',
            _ => text
        };
    }
}

/// <summary>Fluent construction of filter trees, the programmatic side of a filter builder dialog.</summary>
public sealed class FilterBuilder
{
    private readonly CompositeFilterDescriptor _root;

    private FilterBuilder(FilterLogicalOperator logicalOperator) => _root = new CompositeFilterDescriptor { LogicalOperator = logicalOperator };

    /// <summary>All added conditions must match.</summary>
    public static FilterBuilder All() => new(FilterLogicalOperator.And);

    /// <summary>Any added condition may match.</summary>
    public static FilterBuilder Any() => new(FilterLogicalOperator.Or);

    public FilterBuilder Where(string member, FilterOperator op, object? value = null, object? value2 = null)
    {
        _root.FilterDescriptors.Add(new FilterDescriptor(member, op, value) { Value2 = value2 });
        return this;
    }

    public FilterBuilder Is(string member, object? value) => Where(member, FilterOperator.IsEqualTo, value);
    public FilterBuilder Contains(string member, string text) => Where(member, FilterOperator.Contains, text);
    public FilterBuilder Between(string member, object? lower, object? upper) => Where(member, FilterOperator.IsBetween, lower, upper);
    public FilterBuilder In(string member, IEnumerable values) => Where(member, FilterOperator.IsContainedIn, values);

    public FilterBuilder Custom(string member, string operatorName, Func<object?, bool> predicate)
    {
        _root.FilterDescriptors.Add(new CustomFilterDescriptor(member, operatorName, predicate));
        return this;
    }

    public FilterBuilder Group(FilterLogicalOperator logicalOperator, Action<FilterBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var child = new FilterBuilder(logicalOperator);
        configure(child);
        _root.FilterDescriptors.Add(child._root);
        return this;
    }

    public CompositeFilterDescriptor Build() => _root;

    public override string ToString() => FilterText.Format(_root);
}

/// <summary>Round-trips filters through readable text such as <c>[Region] = 'EMEA' AND ([Total] &gt; 1000 OR CONTAINS([Status], 'x'))</c>.</summary>
public static class FilterText
{
    public static string Format(IFilterDescriptor filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return filter switch
        {
            CompositeFilterDescriptor composite => FormatComposite(composite),
            FilterDescriptor fd => FormatFilter(fd),
            CustomFilterDescriptor custom => $"CUSTOM({Field(custom.Member)}, {Literal(custom.OperatorName)})",
            _ => string.Empty
        };
    }

    public static string Format(IEnumerable<IFilterDescriptor> filters) =>
        string.Join(" AND ", filters.Select(Format).Where(t => t.Length > 0));

    /// <summary>Parses filter text; throws <see cref="ExpressionException"/> for syntax errors or constructs that cannot be expressed as filters.</summary>
    public static IFilterDescriptor Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return Convert(ExpressionEngine.Parse(text).Root);
    }

    public static bool TryParse(string text, out IFilterDescriptor? filter, out string? error)
    {
        try
        {
            filter = Parse(text);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is ExpressionException or ArgumentException)
        {
            filter = null;
            error = ex.Message;
            return false;
        }
    }

    private static string FormatComposite(CompositeFilterDescriptor composite)
    {
        var parts = composite.FilterDescriptors.Select(Format).Where(p => p.Length > 0).ToList();
        if (parts.Count == 0) return string.Empty;
        if (parts.Count == 1) return parts[0];
        return "(" + string.Join(composite.LogicalOperator == FilterLogicalOperator.And ? " AND " : " OR ", parts) + ")";
    }

    private static string FormatFilter(FilterDescriptor fd)
    {
        var field = Field(fd.Member);
        return fd.Operator switch
        {
            FilterOperator.IsEqualTo => $"{field} = {Literal(fd.Value)}",
            FilterOperator.IsNotEqualTo => $"{field} != {Literal(fd.Value)}",
            FilterOperator.IsLessThan => $"{field} < {Literal(fd.Value)}",
            FilterOperator.IsLessThanOrEqualTo => $"{field} <= {Literal(fd.Value)}",
            FilterOperator.IsGreaterThan => $"{field} > {Literal(fd.Value)}",
            FilterOperator.IsGreaterThanOrEqualTo => $"{field} >= {Literal(fd.Value)}",
            FilterOperator.Contains => $"CONTAINS({field}, {Literal(fd.Value)})",
            FilterOperator.DoesNotContain => $"NOT CONTAINS({field}, {Literal(fd.Value)})",
            FilterOperator.StartsWith => $"STARTSWITH({field}, {Literal(fd.Value)})",
            FilterOperator.EndsWith => $"ENDSWITH({field}, {Literal(fd.Value)})",
            FilterOperator.IsNull => $"{field} = null",
            FilterOperator.IsNotNull => $"{field} != null",
            FilterOperator.IsEmpty => $"ISEMPTY({field})",
            FilterOperator.IsNotEmpty => $"NOT ISEMPTY({field})",
            FilterOperator.IsContainedIn => $"IN({field}{ListLiterals(fd.Value)})",
            FilterOperator.IsNotContainedIn => $"NOT IN({field}{ListLiterals(fd.Value)})",
            FilterOperator.IsBetween => $"BETWEEN({field}, {Literal(fd.Value)}, {Literal(fd.Value2)})",
            FilterOperator.IsNotBetween => $"NOT BETWEEN({field}, {Literal(fd.Value)}, {Literal(fd.Value2)})",
            _ => string.Empty
        };
    }

    private static string ListLiterals(object? value) =>
        value is IEnumerable items and not string ? string.Concat(items.Cast<object?>().Select(i => ", " + Literal(i))) : ", " + Literal(value);

    private static string Field(string member) => "[" + member + "]";

    public static string Literal(object? value) => value switch
    {
        null => "null",
        string s => "'" + s.Replace("'", "''") + "'",
        bool b => b ? "true" : "false",
        DateTime dt => "#" + dt.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + "#",
        DateTimeOffset dto => "#" + dto.DateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + "#",
        IFormattable f when value.GetType().IsPrimitive || value is decimal => f.ToString(null, CultureInfo.InvariantCulture),
        _ => "'" + (Convert(value) ?? string.Empty).Replace("'", "''") + "'"
    };

    private static string? Convert(object value) => System.Convert.ToString(value, CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------
    // Parsing: expression AST -> filter tree
    // ------------------------------------------------------------------

    private static IFilterDescriptor Convert(ExpressionNode node)
    {
        switch (node)
        {
            case BinaryNode { Operator: "and" or "or" } logical:
            {
                var op = logical.Operator == "and" ? FilterLogicalOperator.And : FilterLogicalOperator.Or;
                var composite = new CompositeFilterDescriptor { LogicalOperator = op };
                AddOperand(composite, logical.Left, op);
                AddOperand(composite, logical.Right, op);
                return composite;
            }

            case BinaryNode comparison:
                return Comparison(comparison);

            case UnaryNode { Operator: "not" } not:
                return Negate(Convert(not.Operand));

            case FunctionNode function:
                return Function(function);

            case FieldNode field:
                return new FilterDescriptor(field.Name, FilterOperator.IsEqualTo, true);

            default:
                throw new ExpressionException("The expression cannot be used as a filter.");
        }
    }

    private static void AddOperand(CompositeFilterDescriptor target, ExpressionNode node, FilterLogicalOperator op)
    {
        var child = Convert(node);
        if (child is CompositeFilterDescriptor { } nested && nested.LogicalOperator == op && node is BinaryNode)
        {
            target.FilterDescriptors.AddRange(nested.FilterDescriptors);
        }
        else
        {
            target.FilterDescriptors.Add(child);
        }
    }

    private static FilterDescriptor Comparison(BinaryNode node)
    {
        FieldNode? field = node.Left as FieldNode;
        LiteralNode? literal = node.Right as LiteralNode;
        var flipped = false;
        if (field == null || literal == null)
        {
            field = node.Right as FieldNode;
            literal = node.Left as LiteralNode;
            flipped = true;
        }

        if (field == null || literal == null)
        {
            throw new ExpressionException("Filters compare one field with one literal value.");
        }

        var op = node.Operator switch
        {
            "=" => FilterOperator.IsEqualTo,
            "!=" => FilterOperator.IsNotEqualTo,
            "<" => flipped ? FilterOperator.IsGreaterThan : FilterOperator.IsLessThan,
            "<=" => flipped ? FilterOperator.IsGreaterThanOrEqualTo : FilterOperator.IsLessThanOrEqualTo,
            ">" => flipped ? FilterOperator.IsLessThan : FilterOperator.IsGreaterThan,
            ">=" => flipped ? FilterOperator.IsLessThanOrEqualTo : FilterOperator.IsGreaterThanOrEqualTo,
            _ => throw new ExpressionException($"Operator '{node.Operator}' cannot be used in a filter.")
        };

        if (literal.Value == null)
        {
            return op switch
            {
                FilterOperator.IsEqualTo => new FilterDescriptor(field.Name, FilterOperator.IsNull, null),
                FilterOperator.IsNotEqualTo => new FilterDescriptor(field.Name, FilterOperator.IsNotNull, null),
                _ => throw new ExpressionException("Only = and != can compare with null.")
            };
        }

        return new FilterDescriptor(field.Name, op, literal.Value);
    }

    private static FilterDescriptor Function(FunctionNode function)
    {
        var args = function.Arguments;
        var field = args.Count > 0 && args[0] is FieldNode f ? f.Name : throw new ExpressionException($"{function.Name} expects a field as its first argument.");
        object? Value(int index) => index < args.Count && args[index] is LiteralNode l ? l.Value : throw new ExpressionException($"{function.Name} expects literal arguments.");

        switch (function.Name)
        {
            case "CONTAINS": return new FilterDescriptor(field, FilterOperator.Contains, Value(1));
            case "STARTSWITH": return new FilterDescriptor(field, FilterOperator.StartsWith, Value(1));
            case "ENDSWITH": return new FilterDescriptor(field, FilterOperator.EndsWith, Value(1));
            case "ISEMPTY": return new FilterDescriptor(field, FilterOperator.IsEmpty, null);
            case "ISNULL" when args.Count == 1: return new FilterDescriptor(field, FilterOperator.IsNull, null);
            case "BETWEEN": return new FilterDescriptor(field, FilterOperator.IsBetween, Value(1)) { Value2 = Value(2) };
            case "IN":
                return new FilterDescriptor(field, FilterOperator.IsContainedIn, Enumerable.Range(1, args.Count - 1).Select(Value).ToList());
            default:
                throw new ExpressionException($"Function {function.Name} cannot be used in a filter.");
        }
    }

    private static IFilterDescriptor Negate(IFilterDescriptor filter)
    {
        switch (filter)
        {
            case CompositeFilterDescriptor composite:
            {
                var negated = new CompositeFilterDescriptor
                {
                    LogicalOperator = composite.LogicalOperator == FilterLogicalOperator.And ? FilterLogicalOperator.Or : FilterLogicalOperator.And
                };
                negated.FilterDescriptors.AddRange(composite.FilterDescriptors.Select(Negate));
                return negated;
            }

            case FilterDescriptor fd:
            {
                var inverse = fd.Operator switch
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
                    FilterOperator.IsContainedIn => FilterOperator.IsNotContainedIn,
                    FilterOperator.IsNotContainedIn => FilterOperator.IsContainedIn,
                    FilterOperator.IsBetween => FilterOperator.IsNotBetween,
                    FilterOperator.IsNotBetween => FilterOperator.IsBetween,
                    _ => throw new ExpressionException($"NOT cannot be applied to {fd.Operator}.")
                };
                return new FilterDescriptor(fd.Member, inverse, fd.Value) { Value2 = fd.Value2, IgnoreCase = fd.IgnoreCase };
            }

            default:
                throw new ExpressionException("NOT cannot be applied to a custom filter.");
        }
    }
}
