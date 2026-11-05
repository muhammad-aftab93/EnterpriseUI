using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace EnterpriseUI.Data;

public sealed class ExpressionException : Exception
{
    public ExpressionException(string message, int position = -1) : base(position >= 0 ? $"{message} (at position {position})" : message)
    {
        Position = position;
    }

    public int Position { get; }
}

/// <summary>A parsed calculated-field expression. Evaluation is side-effect free: only field reads and a fixed function set, never reflection invocation or code execution.</summary>
public sealed class CompiledExpression
{
    internal CompiledExpression(string text, ExpressionNode root, IReadOnlyCollection<string> fields)
    {
        Text = text;
        Root = root;
        Fields = fields;
    }

    public string Text { get; }
    internal ExpressionNode Root { get; }

    /// <summary>Field names the expression reads, e.g. for dependency tracking of calculated columns.</summary>
    public IReadOnlyCollection<string> Fields { get; }

    public object? Evaluate(Func<string, object?> resolveField) => Root.Evaluate(resolveField);

    public object? Evaluate(object? item) => Root.Evaluate(name => PropertyPath.GetValue(item, name));
}

/// <summary>
/// Compiles and evaluates spreadsheet-style expressions such as <c>[UnitPrice] * [Quantity] * (1 - Discount)</c>.
/// Supports + - * / %, comparisons, and/or/not, and the functions IIF, ISNULL, COALESCE, ABS, ROUND, FLOOR, CEILING, SQRT, POW,
/// MIN, MAX, LEN, UPPER, LOWER, TRIM, SUBSTRING (1-based), CONCAT, CONTAINS, STARTSWITH, ENDSWITH, YEAR, MONTH, DAY, TOSTRING.
/// Null operands propagate to a null result; division by zero yields null.
/// </summary>
public static class ExpressionEngine
{
    private const int MaxLength = 4096;
    private static readonly ConcurrentDictionary<string, CompiledExpression> Cache = new(StringComparer.Ordinal);

    public static CompiledExpression Compile(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        if (Cache.TryGetValue(expression, out var cached))
        {
            return cached;
        }

        var compiled = Parse(expression);
        if (Cache.Count > 2048)
        {
            Cache.Clear();
        }

        Cache[expression] = compiled;
        return compiled;
    }

    public static bool TryCompile(string expression, out CompiledExpression? compiled, out string? error)
    {
        try
        {
            compiled = Compile(expression);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is ExpressionException or ArgumentException)
        {
            compiled = null;
            error = ex.Message;
            return false;
        }
    }

    public static object? Evaluate(string expression, object? item) => Compile(expression).Evaluate(item);

    internal static CompiledExpression Parse(string expression)
    {
        if (expression.Length > MaxLength)
        {
            throw new ExpressionException($"Expression is longer than {MaxLength} characters.");
        }

        var parser = new ExpressionParser(Tokenize(expression));
        var root = parser.ParseAll();
        return new CompiledExpression(expression, root, parser.Fields);
    }

    // ------------------------------------------------------------------
    // Tokenizer
    // ------------------------------------------------------------------

    internal enum TokenKind { Number, String, Date, Identifier, Field, Operator, LeftParen, RightParen, Comma, End }

    internal readonly record struct Token(TokenKind Kind, string Text, int Position);

    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            var start = i;
            if (char.IsDigit(c) || (c == '.' && i + 1 < text.Length && char.IsDigit(text[i + 1])))
            {
                while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.')) i++;
                if (i < text.Length && (text[i] is 'e' or 'E') && i + 1 < text.Length && (char.IsDigit(text[i + 1]) || text[i + 1] is '+' or '-'))
                {
                    i += 2;
                    while (i < text.Length && char.IsDigit(text[i])) i++;
                }
                tokens.Add(new Token(TokenKind.Number, text[start..i], start));
            }
            else if (c is '\'' or '"')
            {
                var sb = new StringBuilder();
                i++;
                var closed = false;
                while (i < text.Length)
                {
                    if (text[i] == c)
                    {
                        if (i + 1 < text.Length && text[i + 1] == c) { sb.Append(c); i += 2; continue; }
                        closed = true;
                        i++;
                        break;
                    }
                    sb.Append(text[i++]);
                }
                if (!closed) throw new ExpressionException("Unterminated string literal.", start);
                tokens.Add(new Token(TokenKind.String, sb.ToString(), start));
            }
            else if (c == '[')
            {
                var end = text.IndexOf(']', i + 1);
                if (end < 0) throw new ExpressionException("Unterminated field name.", start);
                tokens.Add(new Token(TokenKind.Field, text[(i + 1)..end].Trim(), start));
                i = end + 1;
            }
            else if (c == '#')
            {
                var end = text.IndexOf('#', i + 1);
                if (end < 0) throw new ExpressionException("Unterminated date literal.", start);
                tokens.Add(new Token(TokenKind.Date, text[(i + 1)..end], start));
                i = end + 1;
            }
            else if (char.IsLetter(c) || c == '_')
            {
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] is '_' or '.')) i++;
                tokens.Add(new Token(TokenKind.Identifier, text[start..i], start));
            }
            else if (c == '(') { tokens.Add(new Token(TokenKind.LeftParen, "(", i++)); }
            else if (c == ')') { tokens.Add(new Token(TokenKind.RightParen, ")", i++)); }
            else if (c == ',') { tokens.Add(new Token(TokenKind.Comma, ",", i++)); }
            else
            {
                var two = i + 1 < text.Length ? text.Substring(i, 2) : string.Empty;
                if (two is "==" or "!=" or "<>" or "<=" or ">=" or "&&" or "||")
                {
                    tokens.Add(new Token(TokenKind.Operator, two, i));
                    i += 2;
                }
                else if (c is '+' or '-' or '*' or '/' or '%' or '=' or '<' or '>' or '!')
                {
                    tokens.Add(new Token(TokenKind.Operator, c.ToString(), i++));
                }
                else
                {
                    throw new ExpressionException($"Unexpected character '{c}'.", i);
                }
            }
        }

        tokens.Add(new Token(TokenKind.End, string.Empty, text.Length));
        return tokens;
    }

    // ------------------------------------------------------------------
    // Parser
    // ------------------------------------------------------------------

    private sealed class ExpressionParser
    {
        private const int MaxDepth = 100;
        private readonly List<Token> _tokens;
        private readonly HashSet<string> _fields = new(StringComparer.OrdinalIgnoreCase);
        private int _index;
        private int _depth;

        public ExpressionParser(List<Token> tokens) => _tokens = tokens;

        public IReadOnlyCollection<string> Fields => _fields;
        private Token Current => _tokens[_index];

        public ExpressionNode ParseAll()
        {
            var node = ParseOr();
            if (Current.Kind != TokenKind.End)
            {
                throw new ExpressionException($"Unexpected '{Current.Text}'.", Current.Position);
            }
            return node;
        }

        private bool IsWord(string word) => Current.Kind == TokenKind.Identifier && string.Equals(Current.Text, word, StringComparison.OrdinalIgnoreCase);
        private bool IsOp(params string[] ops) => Current.Kind == TokenKind.Operator && ops.Contains(Current.Text);

        private ExpressionNode ParseOr()
        {
            var left = ParseAnd();
            while (IsWord("or") || IsOp("||"))
            {
                _index++;
                left = new BinaryNode("or", left, ParseAnd());
            }
            return left;
        }

        private ExpressionNode ParseAnd()
        {
            var left = ParseNot();
            while (IsWord("and") || IsOp("&&"))
            {
                _index++;
                left = new BinaryNode("and", left, ParseNot());
            }
            return left;
        }

        private ExpressionNode ParseNot()
        {
            if (IsWord("not") || IsOp("!"))
            {
                _index++;
                return new UnaryNode("not", Guarded(ParseNot));
            }
            return ParseComparison();
        }

        private ExpressionNode ParseComparison()
        {
            var left = ParseAdditive();
            while (IsOp("=", "==", "!=", "<>", "<", ">", "<=", ">="))
            {
                var op = Current.Text switch { "==" => "=", "<>" => "!=", var other => other };
                _index++;
                left = new BinaryNode(op, left, ParseAdditive());
            }
            return left;
        }

        private ExpressionNode ParseAdditive()
        {
            var left = ParseMultiplicative();
            while (IsOp("+", "-"))
            {
                var op = Current.Text;
                _index++;
                left = new BinaryNode(op, left, ParseMultiplicative());
            }
            return left;
        }

        private ExpressionNode ParseMultiplicative()
        {
            var left = ParseUnary();
            while (IsOp("*", "/", "%"))
            {
                var op = Current.Text;
                _index++;
                left = new BinaryNode(op, left, ParseUnary());
            }
            return left;
        }

        private ExpressionNode ParseUnary()
        {
            if (IsOp("-", "+"))
            {
                var op = Current.Text;
                _index++;
                return new UnaryNode(op, Guarded(ParseUnary));
            }
            return ParsePrimary();
        }

        private ExpressionNode Guarded(Func<ExpressionNode> parse)
        {
            if (++_depth > MaxDepth) throw new ExpressionException("Expression is nested too deeply.", Current.Position);
            try { return parse(); }
            finally { _depth--; }
        }

        private ExpressionNode ParsePrimary()
        {
            var token = Current;
            switch (token.Kind)
            {
                case TokenKind.Number:
                    _index++;
                    return decimal.TryParse(token.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                        ? new LiteralNode(number)
                        : double.TryParse(token.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var big)
                            ? new LiteralNode(big)
                            : throw new ExpressionException($"Invalid number '{token.Text}'.", token.Position);

                case TokenKind.String:
                    _index++;
                    return new LiteralNode(token.Text);

                case TokenKind.Date:
                    _index++;
                    return DateTime.TryParse(token.Text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                        ? new LiteralNode(date)
                        : throw new ExpressionException($"Invalid date '{token.Text}'.", token.Position);

                case TokenKind.Field:
                    _index++;
                    _fields.Add(token.Text);
                    return new FieldNode(token.Text);

                case TokenKind.LeftParen:
                {
                    _index++;
                    var inner = Guarded(ParseOr);
                    Expect(TokenKind.RightParen, ")");
                    return inner;
                }

                case TokenKind.Identifier:
                    _index++;
                    if (Current.Kind == TokenKind.LeftParen)
                    {
                        return ParseCall(token);
                    }

                    if (string.Equals(token.Text, "true", StringComparison.OrdinalIgnoreCase)) return new LiteralNode(true);
                    if (string.Equals(token.Text, "false", StringComparison.OrdinalIgnoreCase)) return new LiteralNode(false);
                    if (string.Equals(token.Text, "null", StringComparison.OrdinalIgnoreCase)) return new LiteralNode(null);
                    _fields.Add(token.Text);
                    return new FieldNode(token.Text);

                default:
                    throw new ExpressionException(token.Kind == TokenKind.End ? "Unexpected end of expression." : $"Unexpected '{token.Text}'.", token.Position);
            }
        }

        private ExpressionNode ParseCall(Token name)
        {
            Expect(TokenKind.LeftParen, "(");
            var args = new List<ExpressionNode>();
            if (Current.Kind != TokenKind.RightParen)
            {
                do
                {
                    args.Add(Guarded(ParseOr));
                }
                while (TryConsume(TokenKind.Comma));
            }
            Expect(TokenKind.RightParen, ")");

            var function = name.Text.ToUpperInvariant();
            if (!FunctionNode.IsKnown(function))
            {
                throw new ExpressionException($"Unknown function '{name.Text}'.", name.Position);
            }
            return new FunctionNode(function, args);
        }

        private bool TryConsume(TokenKind kind)
        {
            if (Current.Kind != kind) return false;
            _index++;
            return true;
        }

        private void Expect(TokenKind kind, string text)
        {
            if (!TryConsume(kind)) throw new ExpressionException($"Expected '{text}'.", Current.Position);
        }
    }
}

internal abstract class ExpressionNode
{
    public abstract object? Evaluate(Func<string, object?> resolve);

    public static bool Truthy(object? value) => value switch
    {
        bool b => b,
        null => false,
        string s => s.Length > 0,
        _ => NumericValue.TryGet(value, out var n) && n != 0
    };

    public static bool Equal(object? left, object? right)
    {
        if (left == null || right == null) return left == null && right == null;
        if (left is string ls && right is string rs) return string.Equals(ls, rs, StringComparison.OrdinalIgnoreCase);
        return ValueComparer.Instance.Compare(left, right) == 0;
    }
}

internal sealed class LiteralNode : ExpressionNode
{
    private readonly object? _value;
    public LiteralNode(object? value) => _value = value;
    public object? Value => _value;
    public override object? Evaluate(Func<string, object?> resolve) => _value;
}

internal sealed class FieldNode : ExpressionNode
{
    public FieldNode(string name) => Name = name;
    public string Name { get; }
    public override object? Evaluate(Func<string, object?> resolve) => resolve(Name);
}

internal sealed class UnaryNode : ExpressionNode
{
    public UnaryNode(string op, ExpressionNode operand)
    {
        Operator = op;
        Operand = operand;
    }

    public string Operator { get; }
    public ExpressionNode Operand { get; }

    public override object? Evaluate(Func<string, object?> resolve)
    {
        var value = Operand.Evaluate(resolve);
        if (Operator == "not") return !Truthy(value);
        if (value == null) return null;
        if (!NumericValue.TryGet(value, out var number)) return null;
        return Operator == "-" ? -number : number;
    }
}

internal sealed class BinaryNode : ExpressionNode
{
    public BinaryNode(string op, ExpressionNode left, ExpressionNode right)
    {
        Operator = op;
        Left = left;
        Right = right;
    }

    public string Operator { get; }
    public ExpressionNode Left { get; }
    public ExpressionNode Right { get; }

    public override object? Evaluate(Func<string, object?> resolve)
    {
        if (Operator == "and") return Truthy(Left.Evaluate(resolve)) && Truthy(Right.Evaluate(resolve));
        if (Operator == "or") return Truthy(Left.Evaluate(resolve)) || Truthy(Right.Evaluate(resolve));

        var left = Left.Evaluate(resolve);
        var right = Right.Evaluate(resolve);

        switch (Operator)
        {
            case "=": return Equal(left, right);
            case "!=": return !Equal(left, right);
            case "<": return left != null && right != null && ValueComparer.Instance.Compare(left, right) < 0;
            case ">": return left != null && right != null && ValueComparer.Instance.Compare(left, right) > 0;
            case "<=": return left != null && right != null && ValueComparer.Instance.Compare(left, right) <= 0;
            case ">=": return left != null && right != null && ValueComparer.Instance.Compare(left, right) >= 0;
        }

        if (left == null || right == null) return null;
        return Arithmetic(left, right);
    }

    private object? Arithmetic(object left, object right)
    {
        if (left is DateTime date)
        {
            if (right is TimeSpan span) return Operator == "+" ? date + span : Operator == "-" ? date - span : null;
            if (right is DateTime other && Operator == "-") return date - other;
        }

        if (NumericValue.TryGet(left, out var a) && NumericValue.TryGet(right, out var b))
        {
            try
            {
                return Operator switch
                {
                    "+" => a + b,
                    "-" => a - b,
                    "*" => a * b,
                    "/" => b == 0 ? null : a / b,
                    "%" => b == 0 ? null : a % b,
                    _ => null
                };
            }
            catch (OverflowException)
            {
                return null;
            }
        }

        if (Operator == "+")
        {
            return Convert.ToString(left, CultureInfo.CurrentCulture) + Convert.ToString(right, CultureInfo.CurrentCulture);
        }

        return null;
    }
}

internal sealed class FunctionNode : ExpressionNode
{
    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        "IIF", "IF", "ISNULL", "COALESCE", "ABS", "ROUND", "FLOOR", "CEILING", "CEIL", "SQRT", "POW", "MIN", "MAX",
        "LEN", "UPPER", "LOWER", "TRIM", "SUBSTRING", "CONCAT", "CONTAINS", "STARTSWITH", "ENDSWITH", "YEAR", "MONTH", "DAY", "TOSTRING",
        "IN", "BETWEEN", "ISEMPTY"
    };

    private readonly string _name;
    private readonly List<ExpressionNode> _args;

    public FunctionNode(string name, List<ExpressionNode> args)
    {
        _name = name;
        _args = args;
    }

    public string Name => _name;
    public IReadOnlyList<ExpressionNode> Arguments => _args;

    public static bool IsKnown(string name) => Known.Contains(name);

    public override object? Evaluate(Func<string, object?> resolve)
    {
        if (_name is "IIF" or "IF")
        {
            Require(3);
            return Truthy(_args[0].Evaluate(resolve)) ? _args[1].Evaluate(resolve) : _args[2].Evaluate(resolve);
        }

        if (_name == "COALESCE")
        {
            foreach (var arg in _args)
            {
                var value = arg.Evaluate(resolve);
                if (value != null) return value;
            }
            return null;
        }

        var values = _args.Select(a => a.Evaluate(resolve)).ToList();

        switch (_name)
        {
            case "ISNULL":
                Require(2);
                return values[0] ?? values[1];
            case "ISEMPTY":
                Require(1);
                return values[0] == null || (values[0] is string s && s.Length == 0);
            case "ABS": return Unary(values, Math.Abs);
            case "FLOOR": return Unary(values, Math.Floor);
            case "CEILING":
            case "CEIL": return Unary(values, Math.Ceiling);
            case "SQRT": return Unary(values, n => n < 0 ? null : (object)(decimal)Math.Sqrt((double)n));
            case "ROUND":
            {
                if (values.Count is < 1 or > 2) throw new ExpressionException("ROUND takes 1 or 2 arguments.");
                if (!NumericValue.TryGet(values[0], out var number)) return null;
                var digits = values.Count == 2 && NumericValue.TryGet(values[1], out var d) ? (int)Math.Clamp(d, 0, 28) : 0;
                return Math.Round(number, digits, MidpointRounding.AwayFromZero);
            }
            case "POW":
            {
                Require(2);
                if (!NumericValue.TryGet(values[0], out var b) || !NumericValue.TryGet(values[1], out var e)) return null;
                var result = Math.Pow((double)b, (double)e);
                return double.IsFinite(result) && Math.Abs(result) < 7.9e28 ? (decimal)result : null;
            }
            case "MIN":
            case "MAX":
            {
                var present = values.Where(v => v != null).ToList();
                if (present.Count == 0) return null;
                return _name == "MIN" ? present.MinBy(v => v, ValueComparer.Instance) : present.MaxBy(v => v, ValueComparer.Instance);
            }
            case "LEN": return Text(values, 1, t => t[0].Length);
            case "UPPER": return Text(values, 1, t => t[0].ToUpperInvariant());
            case "LOWER": return Text(values, 1, t => t[0].ToLowerInvariant());
            case "TRIM": return Text(values, 1, t => t[0].Trim());
            case "TOSTRING": return values.Count == 1 ? values[0] == null ? null : Convert.ToString(values[0], CultureInfo.CurrentCulture) : throw new ExpressionException("TOSTRING takes 1 argument.");
            case "CONCAT": return string.Concat(values.Select(v => Convert.ToString(v, CultureInfo.CurrentCulture)));
            case "CONTAINS": return Text(values, 2, t => t[0].Contains(t[1], StringComparison.OrdinalIgnoreCase));
            case "STARTSWITH": return Text(values, 2, t => t[0].StartsWith(t[1], StringComparison.OrdinalIgnoreCase));
            case "ENDSWITH": return Text(values, 2, t => t[0].EndsWith(t[1], StringComparison.OrdinalIgnoreCase));
            case "SUBSTRING":
            {
                if (values.Count is < 2 or > 3) throw new ExpressionException("SUBSTRING takes 2 or 3 arguments.");
                if (values[0] == null || !NumericValue.TryGet(values[1], out var start)) return null;
                var text = Convert.ToString(values[0], CultureInfo.CurrentCulture) ?? string.Empty;
                var from = Math.Clamp((int)start - 1, 0, text.Length);
                var length = values.Count == 3 && NumericValue.TryGet(values[2], out var len) ? Math.Clamp((int)len, 0, text.Length - from) : text.Length - from;
                return text.Substring(from, length);
            }
            case "YEAR": return DatePart(values, d => d.Year);
            case "MONTH": return DatePart(values, d => d.Month);
            case "DAY": return DatePart(values, d => d.Day);
            case "IN":
            {
                if (values.Count < 2) throw new ExpressionException("IN takes a value and at least one candidate.");
                return values.Skip(1).Any(v => Equal(values[0], v));
            }
            case "BETWEEN":
            {
                Require(3);
                if (values.Any(v => v == null)) return false;
                var comparer = ValueComparer.Instance;
                return comparer.Compare(values[0], values[1]) >= 0 && comparer.Compare(values[0], values[2]) <= 0;
            }
            default:
                return null;
        }
    }

    private void Require(int count)
    {
        if (_args.Count != count) throw new ExpressionException($"{_name} takes {count} argument(s).");
    }

    private object? Unary(List<object?> values, Func<decimal, decimal> function)
    {
        Require(1);
        return NumericValue.TryGet(values[0], out var n) ? function(n) : null;
    }

    private object? Unary(List<object?> values, Func<decimal, object?> function)
    {
        Require(1);
        return NumericValue.TryGet(values[0], out var n) ? function(n) : null;
    }

    private object? Text(List<object?> values, int count, Func<string[], object?> function)
    {
        Require(count);
        if (values.Any(v => v == null)) return null;
        return function(values.Select(v => Convert.ToString(v, CultureInfo.CurrentCulture) ?? string.Empty).ToArray());
    }

    private object? DatePart(List<object?> values, Func<DateTime, int> part)
    {
        Require(1);
        return values[0] switch
        {
            DateTime d => part(d),
            DateTimeOffset o => part(o.DateTime),
            DateOnly o => part(o.ToDateTime(TimeOnly.MinValue)),
            _ => null
        };
    }
}
