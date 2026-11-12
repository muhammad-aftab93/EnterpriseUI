using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EnterpriseUI.Editors.Interactivity;

public enum SmartPasteFieldKind { Text, Name, Email, Phone, Url, Date, Number, Currency, Street, City, Region, PostalCode, Country, Code, Notes }

/// <summary>A form field that clipboard text can be mapped to.</summary>
public sealed class SmartPasteField
{
    public SmartPasteField(string name, SmartPasteFieldKind kind = SmartPasteFieldKind.Text, params string[] aliases)
    {
        Name = name;
        Kind = kind;
        Aliases.AddRange(aliases);
    }

    public string Name { get; }
    public string? Label { get; set; }
    public SmartPasteFieldKind Kind { get; set; }
    public List<string> Aliases { get; } = new();
}

public enum SmartPasteContentKind { Empty, Json, KeyValue, Delimited, Address, Code, FreeText }

public sealed record SmartPasteAssignment(string FieldName, string Value, double Confidence, string Source);

public sealed class SmartPasteResult
{
    public SmartPasteContentKind ContentKind { get; init; }
    public List<SmartPasteAssignment> Assignments { get; } = new();
    public List<string> UnmatchedFields { get; } = new();
    public string? Value(string field) => Assignments.Find(a => string.Equals(a.FieldName, field, StringComparison.OrdinalIgnoreCase))?.Value;
}

/// <summary>Optional model-backed mapper used by the smart paste button for text the built-in recognizers cannot place.</summary>
public interface ISmartPasteProvider
{
    Task<IReadOnlyDictionary<string, string>> MapAsync(string text, IReadOnlyList<SmartPasteField> fields, CancellationToken cancellationToken);
}

public static class SmartPasteMapper
{
    private static readonly Regex Email = new(@"[\w.+-]+@[\w-]+(?:\.[\w-]+)+", RegexOptions.Compiled);
    private static readonly Regex Phone = new(@"(?<![\w.])(?:\+?\d{1,3}[\s.-]?)?(?:\(\d{2,4}\)|\d{2,4})[\s.-]?\d{3,4}[\s.-]?\d{3,4}(?![\w])", RegexOptions.Compiled);
    private static readonly Regex Url = new(@"\b(?:https?://|www\.)[^\s<>""]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Currency = new(@"[$€£¥]\s?\d[\d,]*(?:\.\d+)?", RegexOptions.Compiled);
    private static readonly Regex DateText = new(@"\b(?:\d{4}-\d{2}-\d{2}|\d{1,2}/\d{1,2}/\d{2,4}|(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\.? \d{1,2},? \d{4})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private const string PostalPattern = @"\d{5}(?:-\d{4})?|[A-Z]{1,2}\d[A-Z\d]? ?\d[A-Z]{2}|[A-Z]\d[A-Z] ?\d[A-Z]\d";
    private static readonly Regex CityRegionPostal = new($@"^(?<city>[\p{{L}} .'-]+?),?\s+(?<region>[A-Za-z]{{2,}}\.?)\s+(?<zip>{PostalPattern})$", RegexOptions.Compiled);
    private static readonly Regex StreetLine = new(@"^\d+[\w\s.#,-]*\b(?:St|Street|Ave|Avenue|Rd|Road|Blvd|Boulevard|Dr|Drive|Ln|Lane|Way|Ct|Court|Pl|Place|Suite|Ste|Hwy|Highway)\b.*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex KeyValueLine = new(@"^\s*(?<key>[^:=\t\r\n]{1,40}?)\s*[:=]\s*(?<value>\S.*)$", RegexOptions.Compiled);
    private static readonly string[] Countries = { "USA", "US", "United States", "United States of America", "UK", "United Kingdom", "Canada", "Germany", "France", "Spain", "Italy", "Australia", "India", "Japan", "Netherlands" };
    private static readonly Regex CodeMarker = new(@"(;\s*$|\{\s*$|=>|\bfunction\b|\bdef\s+\w+\(|\bclass\s+\w+|\bpublic\s+\w+|\bimport\s+[\w.]+|#include|\bSELECT\b.*\bFROM\b|</?\w+>)", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly Dictionary<SmartPasteFieldKind, string[]> Synonyms = new()
    {
        [SmartPasteFieldKind.Name] = new[] { "name", "fullname", "customer", "customername", "contact", "contactname" },
        [SmartPasteFieldKind.Email] = new[] { "email", "emailaddress", "mail" },
        [SmartPasteFieldKind.Phone] = new[] { "phone", "phonenumber", "tel", "telephone", "mobile", "cell" },
        [SmartPasteFieldKind.Url] = new[] { "url", "website", "web", "link", "homepage" },
        [SmartPasteFieldKind.Date] = new[] { "date", "dob", "birthday", "duedate", "orderdate" },
        [SmartPasteFieldKind.Currency] = new[] { "amount", "total", "price", "cost", "balance" },
        [SmartPasteFieldKind.Street] = new[] { "street", "address", "address1", "addressline1", "streetaddress", "street1" },
        [SmartPasteFieldKind.City] = new[] { "city", "town" },
        [SmartPasteFieldKind.Region] = new[] { "state", "province", "region", "county" },
        [SmartPasteFieldKind.PostalCode] = new[] { "zip", "zipcode", "postal", "postalcode", "postcode" },
        [SmartPasteFieldKind.Country] = new[] { "country", "nation" }
    };

    public static string Normalize(string? value) => string.Concat((value ?? string.Empty).Where(char.IsLetterOrDigit)).ToLowerInvariant();

    public static SmartPasteContentKind Classify(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return SmartPasteContentKind.Empty;
        var trimmed = text.Trim();
        if ((trimmed.StartsWith('{') || trimmed.StartsWith('[')) && TryParseJson(trimmed, out _)) return SmartPasteContentKind.Json;
        var lines = NonEmptyLines(trimmed);
        if (CodeMarker.Matches(trimmed).Count >= 2 || (lines.Length > 1 && CodeMarker.IsMatch(trimmed) && lines.Any(l => l.StartsWith("    ", StringComparison.Ordinal) || l.StartsWith('\t')))) return SmartPasteContentKind.Code;
        if (lines.Length > 0 && lines.Count(l => KeyValueLine.IsMatch(l)) * 2 >= lines.Length && !LooksLikeUrlOnly(lines)) return SmartPasteContentKind.KeyValue;
        if (IsDelimited(lines, out _)) return SmartPasteContentKind.Delimited;
        if (lines.Any(l => StreetLine.IsMatch(l)) || lines.Any(l => CityRegionPostal.IsMatch(l))) return SmartPasteContentKind.Address;
        return SmartPasteContentKind.FreeText;
    }

    private static bool LooksLikeUrlOnly(string[] lines) => lines.Length == 1 && Url.IsMatch(lines[0]) && lines[0].Trim().IndexOf(':') == lines[0].Trim().IndexOf("://", StringComparison.Ordinal);

    private static string[] NonEmptyLines(string text) => text.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd()).Where(l => l.Trim().Length > 0).ToArray();

    private static bool TryParseJson(string text, out JsonElement element)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            element = doc.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            element = default;
            return false;
        }
    }

    private static char? Delimiter(string[] lines)
    {
        foreach (var d in new[] { '\t', ';', ',', '|' })
        {
            var counts = lines.Select(l => l.Count(c => c == d)).ToArray();
            if (counts.Length > 0 && counts.Min() >= 1 && counts.Distinct().Count() == 1) return d;
        }

        return null;
    }

    private static bool IsDelimited(string[] lines, out char delimiter)
    {
        var d = Delimiter(lines);
        delimiter = d ?? ',';
        return d != null && (d != ',' || lines.Length > 1 || lines[0].Split(',').All(p => p.Trim().Length < 40));
    }

    public static SmartPasteResult Map(string? text, IReadOnlyList<SmartPasteField> fields)
    {
        var kind = Classify(text);
        var result = new SmartPasteResult { ContentKind = kind };
        if (kind == SmartPasteContentKind.Empty)
        {
            result.UnmatchedFields.AddRange(fields.Select(f => f.Name));
            return result;
        }

        var trimmed = text!.Trim();
        switch (kind)
        {
            case SmartPasteContentKind.Json:
                TryParseJson(trimmed, out var json);
                MapPairs(result, fields, Flatten(json), "json");
                break;
            case SmartPasteContentKind.KeyValue:
                MapPairs(result, fields, NonEmptyLines(trimmed).Select(l => KeyValueLine.Match(l)).Where(m => m.Success).Select(m => (m.Groups["key"].Value.Trim(), m.Groups["value"].Value.Trim())), "label");
                break;
            case SmartPasteContentKind.Delimited:
                MapDelimited(result, fields, NonEmptyLines(trimmed));
                break;
            case SmartPasteContentKind.Code:
                foreach (var f in fields.Where(f => f.Kind is SmartPasteFieldKind.Code or SmartPasteFieldKind.Notes))
                {
                    result.Assignments.Add(new SmartPasteAssignment(f.Name, trimmed, 0.9, "code"));
                    break;
                }

                break;
        }

        if (kind is SmartPasteContentKind.FreeText or SmartPasteContentKind.Address or SmartPasteContentKind.KeyValue or SmartPasteContentKind.Json or SmartPasteContentKind.Delimited) Recognize(result, fields, trimmed);
        result.UnmatchedFields.AddRange(fields.Where(f => result.Value(f.Name) == null).Select(f => f.Name));
        return result;
    }

    private static IEnumerable<(string Key, string Value)> Flatten(JsonElement element, string prefix = "")
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in element.EnumerateObject())
                    foreach (var pair in Flatten(p.Value, prefix.Length == 0 ? p.Name : prefix + "." + p.Name)) yield return pair;
                break;
            case JsonValueKind.Array:
                var first = element.EnumerateArray().FirstOrDefault();
                if (first.ValueKind != JsonValueKind.Undefined)
                    foreach (var pair in Flatten(first, prefix)) yield return pair;
                break;
            case JsonValueKind.Null: break;
            default: yield return (prefix, element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : element.ToString()); break;
        }
    }

    private static IEnumerable<string> KeysOf(SmartPasteField field)
    {
        yield return Normalize(field.Name);
        if (field.Label != null) yield return Normalize(field.Label);
        foreach (var a in field.Aliases) yield return Normalize(a);
        if (Synonyms.TryGetValue(field.Kind, out var synonyms))
            foreach (var s in synonyms) yield return s;
    }

    private static (SmartPasteField? Field, double Confidence) Match(string label, IReadOnlyList<SmartPasteField> fields)
    {
        var path = Normalize(label);
        var leaf = Normalize(label.Contains('.') ? label[(label.LastIndexOf('.') + 1)..] : label);
        foreach (var f in fields)
        {
            var keys = KeysOf(f).Where(k => k.Length > 0).ToArray();
            if (keys.Contains(path) || keys.Contains(leaf)) return (f, 1.0);
        }

        foreach (var f in fields)
        {
            if (KeysOf(f).Any(k => k.Length >= 3 && leaf.Length >= 3 && (leaf.Contains(k, StringComparison.Ordinal) || k.Contains(leaf, StringComparison.Ordinal)))) return (f, 0.7);
        }

        return (null, 0);
    }

    private static void MapPairs(SmartPasteResult result, IReadOnlyList<SmartPasteField> fields, IEnumerable<(string Key, string Value)> pairs, string source)
    {
        foreach (var (key, value) in pairs)
        {
            var (field, confidence) = Match(key, fields);
            if (field == null || value.Length == 0 || result.Value(field.Name) != null) continue;
            result.Assignments.Add(new SmartPasteAssignment(field.Name, value, confidence, source));
        }
    }

    private static void MapDelimited(SmartPasteResult result, IReadOnlyList<SmartPasteField> fields, string[] lines)
    {
        var d = Delimiter(lines) ?? ',';
        var rows = lines.Select(l => l.Split(d).Select(c => c.Trim().Trim('"')).ToArray()).ToArray();
        if (rows.Length >= 2 && rows[0].Count(h => Match(h, fields).Field != null) >= Math.Min(2, rows[0].Length))
        {
            MapPairs(result, fields, rows[0].Select((h, i) => (h, i < rows[1].Length ? rows[1][i] : string.Empty)), "header");
            return;
        }

        var positional = fields.Where(f => f.Kind is not (SmartPasteFieldKind.Notes or SmartPasteFieldKind.Code)).ToArray();
        for (var i = 0; i < Math.Min(positional.Length, rows[0].Length); i++)
            if (rows[0][i].Length > 0) result.Assignments.Add(new SmartPasteAssignment(positional[i].Name, rows[0][i], 0.5, "position"));
    }

    private static SmartPasteFieldKind EffectiveKind(SmartPasteField field)
    {
        if (field.Kind != SmartPasteFieldKind.Text) return field.Kind;
        var keys = KeysOf(field).ToHashSet();
        foreach (var (kind, synonyms) in Synonyms)
            if (synonyms.Any(keys.Contains)) return kind;
        return SmartPasteFieldKind.Text;
    }

    private static void Recognize(SmartPasteResult result, IReadOnlyList<SmartPasteField> fields, string text)
    {
        var lines = NonEmptyLines(text).Select(l => l.Trim()).ToArray();
        var found = new Dictionary<SmartPasteFieldKind, string>();

        void Put(SmartPasteFieldKind kind, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !found.ContainsKey(kind)) found[kind] = value.Trim();
        }

        Put(SmartPasteFieldKind.Email, Email.Match(text) is { Success: true } e ? e.Value : null);
        Put(SmartPasteFieldKind.Url, Url.Match(text) is { Success: true } u ? u.Value.TrimEnd('.', ',', ')') : null);
        var noEmail = Url.Replace(Email.Replace(text, " "), " ");
        Put(SmartPasteFieldKind.Phone, Phone.Matches(noEmail).Cast<Match>().Select(m => m.Value.Trim()).FirstOrDefault(v => v.Count(char.IsDigit) >= 7 && !DateText.IsMatch(v)));
        Put(SmartPasteFieldKind.Currency, Currency.Match(text) is { Success: true } c ? c.Value : null);
        Put(SmartPasteFieldKind.Date, DateText.Match(text) is { Success: true } d ? d.Value : null);

        foreach (var line in lines)
        {
            if (CityRegionPostal.Match(line) is { Success: true } m)
            {
                Put(SmartPasteFieldKind.City, m.Groups["city"].Value);
                Put(SmartPasteFieldKind.Region, m.Groups["region"].Value.TrimEnd('.'));
                Put(SmartPasteFieldKind.PostalCode, m.Groups["zip"].Value);
            }
            else if (StreetLine.IsMatch(line)) Put(SmartPasteFieldKind.Street, line.TrimEnd(','));
            else if (Countries.Contains(line, StringComparer.OrdinalIgnoreCase)) Put(SmartPasteFieldKind.Country, line);
        }

        var first = lines.FirstOrDefault();
        if (first != null && !first.Any(char.IsDigit) && !first.Contains('@') && !first.Contains(':') && first.Split(' ', StringSplitOptions.RemoveEmptyEntries) is { Length: >= 2 and <= 4 } words
            && words.All(w => char.IsUpper(w[0]))) Put(SmartPasteFieldKind.Name, first);

        foreach (var field in fields)
        {
            if (result.Value(field.Name) != null) continue;
            var kind = EffectiveKind(field);
            if (kind == SmartPasteFieldKind.Number && found.TryGetValue(SmartPasteFieldKind.Currency, out var money))
            {
                result.Assignments.Add(new SmartPasteAssignment(field.Name, ParseNumber(money), 0.6, "pattern"));
                continue;
            }

            if (found.TryGetValue(kind, out var value))
            {
                if (kind == SmartPasteFieldKind.Currency && field.Kind == SmartPasteFieldKind.Currency) value = ParseNumber(value);
                result.Assignments.Add(new SmartPasteAssignment(field.Name, value, field.Kind == SmartPasteFieldKind.Text ? 0.65 : 0.85, "pattern"));
            }
        }
    }

    private static string ParseNumber(string text)
    {
        var digits = new string(text.Where(ch => char.IsDigit(ch) || ch == '.').ToArray());
        return decimal.TryParse(digits, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number.ToString(CultureInfo.InvariantCulture) : text;
    }
}
