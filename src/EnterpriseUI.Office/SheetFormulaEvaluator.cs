using System.Globalization;
using System.Text;

namespace EnterpriseUI.Office;

/// <summary>Evaluates worksheet formulas: arithmetic, comparison, text join, cell and range references, and common functions.</summary>
internal sealed class SheetFormulaEvaluator
{
    private readonly SpreadsheetWorksheet _sheet;
    private readonly Dictionary<string, object?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _active = new(StringComparer.OrdinalIgnoreCase);
    private string _text = string.Empty;
    private int _pos;

    public SheetFormulaEvaluator(SpreadsheetWorksheet sheet) => _sheet = sheet;

    public object? EvaluateCell(string reference)
    {
        if (!SheetReference.TryParse(reference, out var row, out var col)) return SheetError.Reference;
        var key = SheetReference.Format(row, col);
        if (_cache.TryGetValue(key, out var cached)) return cached;
        if (!_sheet.Cells.TryGetValue(key, out var cell)) return null;
        if (string.IsNullOrEmpty(cell.Formula)) return Normalize(cell.Value);
        if (!_active.Add(key)) return SheetError.Reference;
        try
        {
            var result = EvaluateFormula(cell.Formula);
            _cache[key] = result;
            return result;
        }
        finally { _active.Remove(key); }
    }

    public object? EvaluateFormula(string formula)
    {
        var saveText = _text;
        var savePos = _pos;
        try
        {
            _text = formula.TrimStart().TrimStart('=');
            _pos = 0;
            var value = ParseComparison();
            SkipSpaces();
            return _pos < _text.Length ? SheetError.Name : value;
        }
        catch (FormatException) { return SheetError.Name; }
        finally { _text = saveText; _pos = savePos; }
    }

    private static object? Normalize(object? value) => value switch
    {
        null => null,
        string or bool or SheetError or double => value,
        DateTime d => d.ToOADate(),
        sbyte or byte or short or ushort or int or uint or long or ulong or float or decimal => Convert.ToDouble(value, CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)
    };

    private void SkipSpaces() { while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos])) _pos++; }

    private bool Match(string token)
    {
        SkipSpaces();
        if (string.CompareOrdinal(_text, _pos, token, 0, token.Length) != 0) return false;
        _pos += token.Length;
        return true;
    }

    private object? ParseComparison()
    {
        var left = ParseConcat();
        while (true)
        {
            string? op = null;
            foreach (var candidate in new[] { "<>", "<=", ">=", "=", "<", ">" })
            {
                SkipSpaces();
                if (string.CompareOrdinal(_text, _pos, candidate, 0, candidate.Length) == 0) { op = candidate; break; }
            }
            if (op is null) return left;
            _pos += op.Length;
            var right = ParseConcat();
            if (left is SheetError le) return le;
            if (right is SheetError re) return re;
            var cmp = Compare(left, right);
            left = op switch { "=" => cmp == 0, "<>" => cmp != 0, "<" => cmp < 0, ">" => cmp > 0, "<=" => cmp <= 0, _ => cmp >= 0 };
        }
    }

    private static int Compare(object? a, object? b)
    {
        if (a is null) a = b is string ? string.Empty : b is bool ? false : 0d;
        if (b is null) b = a is string ? string.Empty : a is bool ? false : 0d;
        if (a is double x && b is double y) return x.CompareTo(y);
        if (a is bool p && b is bool q) return p.CompareTo(q);
        if (a is string s && b is string t) return string.Compare(s, t, StringComparison.OrdinalIgnoreCase);
        return a is string ? 1 : b is string ? -1 : a is bool ? 1 : -1;
    }

    private object? ParseConcat()
    {
        var left = ParseAdditive();
        while (Match("&"))
        {
            var right = ParseAdditive();
            if (left is SheetError le) return le;
            if (right is SheetError re) return re;
            left = AsText(left) + AsText(right);
        }
        return left;
    }

    private object? ParseAdditive()
    {
        var left = ParseTerm();
        while (true)
        {
            SkipSpaces();
            if (_pos >= _text.Length || _text[_pos] is not ('+' or '-')) return left;
            var op = _text[_pos++];
            var right = ParseTerm();
            left = Arithmetic(left, right, (a, b) => op == '+' ? a + b : a - b);
        }
    }

    private object? ParseTerm()
    {
        var left = ParsePower();
        while (true)
        {
            SkipSpaces();
            if (_pos >= _text.Length || _text[_pos] is not ('*' or '/')) return left;
            var op = _text[_pos++];
            var right = ParsePower();
            if (op == '/' && right is not SheetError && AsNumber(right) is 0d) return SheetError.DivideByZero;
            left = Arithmetic(left, right, (a, b) => op == '*' ? a * b : a / b);
        }
    }

    private object? ParsePower()
    {
        var left = ParseUnary();
        while (Match("^"))
        {
            var right = ParseUnary();
            left = Arithmetic(left, right, Math.Pow);
        }
        return left;
    }

    private object? ParseUnary()
    {
        SkipSpaces();
        if (_pos < _text.Length && _text[_pos] == '-') { _pos++; var v = ParseUnary(); return v is SheetError ? v : AsNumber(v) is { } n ? -n : SheetError.Value; }
        if (_pos < _text.Length && _text[_pos] == '+') { _pos++; return ParseUnary(); }
        var value = ParsePrimary();
        while (Match("%")) value = value is SheetError ? value : AsNumber(value) is { } p ? p / 100 : SheetError.Value;
        return value;
    }

    private object? ParsePrimary()
    {
        SkipSpaces();
        if (_pos >= _text.Length) throw new FormatException("Unexpected end of formula.");
        var c = _text[_pos];

        if (c == '(')
        {
            _pos++;
            var inner = ParseComparison();
            if (!Match(")")) throw new FormatException("Missing ')'.");
            return inner;
        }

        if (c == '"')
        {
            var sb = new StringBuilder();
            _pos++;
            while (true)
            {
                if (_pos >= _text.Length) throw new FormatException("Unterminated string.");
                if (_text[_pos] == '"')
                {
                    if (_pos + 1 < _text.Length && _text[_pos + 1] == '"') { sb.Append('"'); _pos += 2; continue; }
                    _pos++;
                    return sb.ToString();
                }
                sb.Append(_text[_pos++]);
            }
        }

        if (char.IsDigit(c) || c == '.')
        {
            var start = _pos;
            while (_pos < _text.Length && (char.IsDigit(_text[_pos]) || _text[_pos] == '.')) _pos++;
            if (_pos < _text.Length && _text[_pos] is 'e' or 'E' && _pos + 1 < _text.Length && (char.IsDigit(_text[_pos + 1]) || _text[_pos + 1] is '+' or '-'))
            {
                _pos += 2;
                while (_pos < _text.Length && char.IsDigit(_text[_pos])) _pos++;
            }
            return double.TryParse(_text.AsSpan(start, _pos - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : throw new FormatException("Bad number.");
        }

        if (char.IsLetter(c) || c == '$' || c == '_')
        {
            var start = _pos;
            while (_pos < _text.Length && (char.IsLetterOrDigit(_text[_pos]) || _text[_pos] is '$' or '_' or '.')) _pos++;
            var word = _text[start.._pos];
            SkipSpaces();

            if (_pos < _text.Length && _text[_pos] == '(') return ParseFunction(word);
            if (word.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) return true;
            if (word.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) return false;

            if (SheetReference.TryParse(word, out _, out _))
            {
                if (_pos < _text.Length && _text[_pos] == ':')
                {
                    _pos++;
                    SkipSpaces();
                    var endStart = _pos;
                    while (_pos < _text.Length && (char.IsLetterOrDigit(_text[_pos]) || _text[_pos] == '$')) _pos++;
                    return new RangeValue(RangeCells(word, _text[endStart.._pos]));
                }
                return EvaluateCell(word);
            }
            return SheetError.Name;
        }

        throw new FormatException($"Unexpected '{c}'.");
    }

    private sealed record RangeValue(List<object?> Values);

    private List<object?> RangeCells(string from, string to)
    {
        if (!SheetReference.TryParse(from, out var r1, out var c1) || !SheetReference.TryParse(to, out var r2, out var c2)) throw new FormatException("Bad range.");
        var values = new List<object?>();
        for (var r = Math.Min(r1, r2); r <= Math.Max(r1, r2); r++)
            for (var c = Math.Min(c1, c2); c <= Math.Max(c1, c2); c++)
                values.Add(EvaluateCell(SheetReference.Format(r, c)));
        return values;
    }

    private object? ParseFunction(string name)
    {
        _pos++;
        var args = new List<object?>();
        SkipSpaces();
        if (_pos < _text.Length && _text[_pos] == ')') _pos++;
        else
        {
            while (true)
            {
                args.Add(ParseComparison());
                if (Match(",") || Match(";")) continue;
                if (Match(")")) break;
                throw new FormatException("Expected ',' or ')'.");
            }
        }
        return Call(name.ToUpperInvariant(), args);
    }

    private static IEnumerable<object?> Flatten(IEnumerable<object?> args) =>
        args.SelectMany(a => a is RangeValue r ? (IEnumerable<object?>)r.Values : new[] { a });

    private static object? Call(string name, List<object?> args)
    {
        var firstError = Flatten(args).OfType<SheetError>().FirstOrDefault();
        if (firstError is not null && name is not ("IF" or "AND" or "OR" or "NOT")) return firstError;

        // Numbers from ranges ignore text and blanks; direct arguments must be numeric.
        IEnumerable<double> Numbers() => args.SelectMany(a => a is RangeValue r ? r.Values.OfType<double>() : AsNumber(a) is { } n ? (IEnumerable<double>)new[] { n } : Array.Empty<double>());
        double? Arg(int i) => i < args.Count ? AsNumber(args[i] is RangeValue rv ? rv.Values.FirstOrDefault() : args[i]) : null;
        string ArgText(int i) => i < args.Count ? AsText(args[i] is RangeValue rv ? rv.Values.FirstOrDefault() : args[i]) : string.Empty;

        switch (name)
        {
            case "SUM": return Numbers().Sum();
            case "AVERAGE": { var n = Numbers().ToList(); return n.Count == 0 ? SheetError.DivideByZero : n.Average(); }
            case "MIN": { var n = Numbers().ToList(); return n.Count == 0 ? 0d : n.Min(); }
            case "MAX": { var n = Numbers().ToList(); return n.Count == 0 ? 0d : n.Max(); }
            case "COUNT": return (double)Flatten(args).Count(v => v is double);
            case "COUNTA": return (double)Flatten(args).Count(v => v is not null && v is not string { Length: 0 });
            case "ABS": return Arg(0) is { } a ? Math.Abs(a) : SheetError.Value;
            case "SQRT": return Arg(0) is { } s ? (s < 0 ? SheetError.Number : Math.Sqrt(s)) : SheetError.Value;
            case "ROUND": return Arg(0) is { } v && (Arg(1) ?? 0) is var digits ? Math.Round(v, (int)Math.Clamp(digits, 0, 15), MidpointRounding.AwayFromZero) : SheetError.Value;
            case "MOD": return Arg(0) is { } x && Arg(1) is { } y ? (y == 0 ? SheetError.DivideByZero : x - y * Math.Floor(x / y)) : SheetError.Value;
            case "POWER": return Arg(0) is { } b && Arg(1) is { } e ? Math.Pow(b, e) : SheetError.Value;
            case "LEN": return (double)ArgText(0).Length;
            case "UPPER": return ArgText(0).ToUpperInvariant();
            case "LOWER": return ArgText(0).ToLowerInvariant();
            case "CONCAT" or "CONCATENATE": return string.Concat(Flatten(args).Select(AsText));
            case "NOT": return args.Count == 1 && AsBool(args[0]) is { } nb ? !nb : SheetError.Value;
            case "AND": { var all = Flatten(args).Where(v => v is not null).Select(AsBool).ToList(); return all.Count == 0 || all.Any(v => v is null) ? SheetError.Value : all.All(v => v == true); }
            case "OR": { var all = Flatten(args).Where(v => v is not null).Select(AsBool).ToList(); return all.Count == 0 || all.Any(v => v is null) ? SheetError.Value : all.Any(v => v == true); }
            case "IF":
                if (args.Count < 2) return SheetError.Value;
                if (args[0] is SheetError cond) return cond;
                var test = AsBool(args[0]);
                if (test is null) return SheetError.Value;
                return test.Value ? ScalarOf(args[1]) : args.Count > 2 ? ScalarOf(args[2]) : false;
            default: return SheetError.Name;
        }
    }

    private static object? ScalarOf(object? value) => value is RangeValue r ? r.Values.FirstOrDefault() : value;

    private static object? Arithmetic(object? left, object? right, Func<double, double, double> op)
    {
        if (left is SheetError le) return le;
        if (right is SheetError re) return re;
        if (AsNumber(left) is not { } a || AsNumber(right) is not { } b) return SheetError.Value;
        var result = op(a, b);
        return double.IsNaN(result) || double.IsInfinity(result) ? SheetError.Number : result;
    }

    internal static double? AsNumber(object? value) => value switch
    {
        null => 0d,
        double d => d,
        bool b => b ? 1d : 0d,
        string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) => n,
        string { Length: 0 } => 0d,
        _ => null
    };

    private static bool? AsBool(object? value) => value switch
    {
        bool b => b,
        double d => d != 0,
        string s when s.Equals("TRUE", StringComparison.OrdinalIgnoreCase) => true,
        string s when s.Equals("FALSE", StringComparison.OrdinalIgnoreCase) => false,
        null => false,
        _ => null
    };

    private static string AsText(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        bool b => b ? "TRUE" : "FALSE",
        double d => d.ToString("G15", CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };
}
