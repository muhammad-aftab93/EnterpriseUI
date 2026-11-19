using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace EnterpriseUI.Pdf;

/// <summary>
/// Headless PDF processing: writes, reads, merges and splits text documents. Reading supports classic cross-reference files
/// with plain or Flate-compressed content streams; object streams and encrypted files are not supported.
/// </summary>
public static class EnterprisePdfEngine
{
    private const double Margin = 54;
    private const double FontSize = 11;
    private const double Leading = 14;

    /// <summary>Lays out lines onto pages, wrapping long lines and starting a new page when one is full.</summary>
    public static PdfDocument CreateFromLines(string title, IEnumerable<string> lines, int maxCharsPerLine = 90, double pageWidth = 612, double pageHeight = 792)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (maxCharsPerLine < 10) throw new ArgumentOutOfRangeException(nameof(maxCharsPerLine));
        var document = new PdfDocument { Title = title };
        var linesPerPage = Math.Max(1, (int)((pageHeight - 2 * Margin) / Leading));
        PdfPage? page = null;

        void Add(string line)
        {
            if (page is null || page.TextElements.Count >= linesPerPage)
            {
                page = new PdfPage { PageNumber = document.Pages.Count + 1, Width = pageWidth, Height = pageHeight };
                document.Pages.Add(page);
            }
            page.TextElements.Add(line);
        }

        foreach (var raw in lines)
        {
            var line = raw ?? string.Empty;
            if (line.Length == 0) { Add(string.Empty); continue; }
            while (line.Length > maxCharsPerLine)
            {
                var cut = line.LastIndexOf(' ', maxCharsPerLine);
                if (cut <= 0) cut = maxCharsPerLine;
                Add(line[..cut].TrimEnd());
                line = line[cut..].TrimStart();
            }
            Add(line);
        }

        if (document.Pages.Count == 0) document.Pages.Add(new PdfPage { Width = pageWidth, Height = pageHeight });
        return document;
    }

    public static byte[] Write(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var pages = document.Pages.Count == 0 ? new List<PdfPage> { new() } : document.Pages.ToList();
        var offsets = new List<long>();
        using var stream = new MemoryStream();

        void Raw(string text) { var bytes = Encoding.Latin1.GetBytes(text); stream.Write(bytes, 0, bytes.Length); }
        void BeginObject(int number) { offsets.Add(stream.Position); Raw($"{number} 0 obj\n"); }

        Raw("%PDF-1.4\n%\u00e2\u00e3\u00cf\u00d3\n");

        // Object layout: 1 catalog, 2 pages, 3 font, 4 info, then a page object and a content object per page.
        BeginObject(1); Raw("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        var kids = string.Join(" ", pages.Select((_, i) => $"{5 + i * 2} 0 R"));
        BeginObject(2); Raw($"<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>\nendobj\n");
        BeginObject(3); Raw("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");
        BeginObject(4); Raw($"<< /Title ({Escape(document.Title)}) /Author ({Escape(document.Author)}) /Producer (EnterpriseUI) >>\nendobj\n");

        for (var i = 0; i < pages.Count; i++)
        {
            var page = pages[i];
            var width = page.Width.ToString("0.##", CultureInfo.InvariantCulture);
            var height = page.Height.ToString("0.##", CultureInfo.InvariantCulture);
            BeginObject(5 + i * 2);
            Raw($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] /Resources << /Font << /F1 3 0 R >> >> /Contents {6 + i * 2} 0 R >>\nendobj\n");

            var content = new StringBuilder();
            content.Append("BT\n/F1 ").Append(FontSize.ToString(CultureInfo.InvariantCulture)).Append(" Tf\n").Append(Leading.ToString(CultureInfo.InvariantCulture)).Append(" TL\n");
            content.Append(Margin.ToString(CultureInfo.InvariantCulture)).Append(' ').Append((page.Height - Margin).ToString("0.##", CultureInfo.InvariantCulture)).Append(" Td\n");
            foreach (var line in page.TextElements) content.Append('(').Append(Escape(line)).Append(") '\n");
            content.Append("ET\n");
            var body = Encoding.Latin1.GetBytes(content.ToString());
            BeginObject(6 + i * 2);
            Raw($"<< /Length {body.Length} >>\nstream\n");
            stream.Write(body, 0, body.Length);
            Raw("\nendstream\nendobj\n");
        }

        var xref = stream.Position;
        Raw($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) Raw($"{offset:D10} 00000 n \n");
        Raw($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R /Info 4 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return stream.ToArray();
    }

    public static PdfDocument Read(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var text = Encoding.Latin1.GetString(data);
        if (!text.StartsWith("%PDF-", StringComparison.Ordinal)) throw new InvalidDataException("The data is not a PDF file.");

        var objects = new Dictionary<int, PdfObject>();
        foreach (Match match in Regex.Matches(text, @"(?<![0-9])(\d+)\s+(\d+)\s+obj\b"))
        {
            var start = match.Index + match.Length;
            var end = text.IndexOf("endobj", start, StringComparison.Ordinal);
            if (end < 0) continue;
            var body = text[start..end];
            byte[]? streamBytes = null;
            var streamMarker = Regex.Match(body, @"stream\r?\n");
            var streamEnd = streamMarker.Success ? body.LastIndexOf("endstream", StringComparison.Ordinal) : -1;
            if (streamMarker.Success && streamEnd > streamMarker.Index)
            {
                var from = streamMarker.Index + streamMarker.Length;
                var to = streamEnd;
                if (to > from && body[to - 1] == '\n') to--;
                if (to > from && body[to - 1] == '\r') to--;
                streamBytes = Encoding.Latin1.GetBytes(body[from..Math.Max(from, to)]);
                body = body[..streamMarker.Index];
            }
            objects[int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)] = new PdfObject(body, streamBytes);
        }

        var document = new PdfDocument { Title = string.Empty, Author = string.Empty };
        var info = objects.Values.FirstOrDefault(o => Regex.IsMatch(o.Dictionary, @"/(Title|Author)\s*\("));
        if (info is not null)
        {
            document.Title = ReadString(info.Dictionary, "Title");
            document.Author = ReadString(info.Dictionary, "Author");
        }

        foreach (var pageObject in OrderedPages(objects))
        {
            var page = new PdfPage { PageNumber = document.Pages.Count + 1 };
            var box = Regex.Match(pageObject.Dictionary, @"/MediaBox\s*\[\s*([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s*\]");
            if (box.Success)
            {
                page.Width = double.Parse(box.Groups[3].Value, CultureInfo.InvariantCulture) - double.Parse(box.Groups[1].Value, CultureInfo.InvariantCulture);
                page.Height = double.Parse(box.Groups[4].Value, CultureInfo.InvariantCulture) - double.Parse(box.Groups[2].Value, CultureInfo.InvariantCulture);
            }

            foreach (var reference in Regex.Matches(Regex.Match(pageObject.Dictionary, @"/Contents\s*(\[[^\]]*\]|\d+\s+\d+\s+R)").Value, @"(\d+)\s+\d+\s+R").Cast<Match>())
            {
                if (!objects.TryGetValue(int.Parse(reference.Groups[1].Value, CultureInfo.InvariantCulture), out var content) || content.Stream is null) continue;
                var bytes = content.Dictionary.Contains("/FlateDecode", StringComparison.Ordinal) ? Inflate(content.Stream) : content.Stream;
                page.TextElements.AddRange(ExtractLines(Encoding.Latin1.GetString(bytes)));
            }

            document.Pages.Add(page);
        }

        if (document.Pages.Count == 0) throw new InvalidDataException("The PDF contains no readable pages.");
        return document;
    }

    public static PdfDocument Merge(IEnumerable<PdfDocument> documents, string title = "Merged.pdf")
    {
        ArgumentNullException.ThrowIfNull(documents);
        var merged = new PdfDocument { Title = title };
        foreach (var source in documents)
        {
            if (merged.Pages.Count == 0) merged.Author = source.Author;
            foreach (var page in source.Pages)
            {
                var copy = new PdfPage { PageNumber = merged.Pages.Count + 1, Width = page.Width, Height = page.Height };
                copy.TextElements.AddRange(page.TextElements);
                merged.Pages.Add(copy);
            }
        }
        return merged;
    }

    /// <summary>Copies the given 1-based pages, in the requested order, into a new document.</summary>
    public static PdfDocument ExtractPages(PdfDocument document, IEnumerable<int> pageNumbers)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(pageNumbers);
        var result = new PdfDocument { Title = document.Title, Author = document.Author };
        foreach (var number in pageNumbers)
        {
            if (number < 1 || number > document.Pages.Count) throw new ArgumentOutOfRangeException(nameof(pageNumbers), $"Page {number} does not exist.");
            var source = document.Pages[number - 1];
            var copy = new PdfPage { PageNumber = result.Pages.Count + 1, Width = source.Width, Height = source.Height };
            copy.TextElements.AddRange(source.TextElements);
            result.Pages.Add(copy);
        }
        return result;
    }

    public static IReadOnlyList<PdfDocument> Split(PdfDocument document, int pagesPerPart)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (pagesPerPart < 1) throw new ArgumentOutOfRangeException(nameof(pagesPerPart));
        var parts = new List<PdfDocument>();
        for (var first = 1; first <= document.Pages.Count; first += pagesPerPart)
            parts.Add(ExtractPages(document, Enumerable.Range(first, Math.Min(pagesPerPart, document.Pages.Count - first + 1))));
        return parts;
    }

    public static string ExtractText(PdfDocument document) =>
        string.Join("\n", document.Pages.SelectMany(p => p.TextElements));

    private sealed record PdfObject(string Dictionary, byte[]? Stream);

    private static IEnumerable<PdfObject> OrderedPages(Dictionary<int, PdfObject> objects)
    {
        var tree = objects.Values.FirstOrDefault(o => Regex.IsMatch(o.Dictionary, @"/Type\s*/Pages\b") && o.Dictionary.Contains("/Kids", StringComparison.Ordinal));
        if (tree is not null)
        {
            var kids = Regex.Match(tree.Dictionary, @"/Kids\s*\[([^\]]*)\]");
            foreach (Match reference in Regex.Matches(kids.Groups[1].Value, @"(\d+)\s+\d+\s+R"))
                if (objects.TryGetValue(int.Parse(reference.Groups[1].Value, CultureInfo.InvariantCulture), out var kid) && Regex.IsMatch(kid.Dictionary, @"/Type\s*/Page\b"))
                    yield return kid;
            yield break;
        }

        foreach (var pair in objects.OrderBy(p => p.Key))
            if (Regex.IsMatch(pair.Value.Dictionary, @"/Type\s*/Page\b")) yield return pair.Value;
    }

    private static byte[] Inflate(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
#if NET6_0_OR_GREATER
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
#else
        if (compressed.Length >= 2) input.Position = 2;
        using var zlib = new DeflateStream(input, CompressionMode.Decompress);
#endif
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    private static IEnumerable<string> ExtractLines(string content)
    {
        var lines = new List<string>();
        var pending = new StringBuilder();
        var hasPending = false;
        var i = 0;
        while (i < content.Length)
        {
            var c = content[i];
            if (c == '(')
            {
                pending.Append(ReadLiteral(content, ref i));
                hasPending = true;
            }
            else if (c == '[' || c == ']' || char.IsWhiteSpace(c) || c == '-' || c == '.' || char.IsDigit(c))
            {
                i++;
            }
            else
            {
                var start = i;
                while (i < content.Length && !char.IsWhiteSpace(content[i]) && content[i] != '(' && content[i] != '[' && content[i] != ']') i++;
                var op = content[start..i];
                if (op is "Tj" or "TJ" or "'" or "\"")
                {
                    if (hasPending) lines.Add(pending.ToString());
                    pending.Clear();
                    hasPending = false;
                }
                else if (op is "ET" or "BT" or "Td" or "TD" or "T*" or "Tm")
                {
                    if (hasPending) lines.Add(pending.ToString());
                    pending.Clear();
                    hasPending = false;
                }
            }
        }
        return lines;
    }

    private static string ReadLiteral(string s, ref int i)
    {
        var sb = new StringBuilder();
        var depth = 0;
        for (; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '\\' && i + 1 < s.Length)
            {
                var n = s[++i];
                switch (n)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case '\r': if (i + 1 < s.Length && s[i + 1] == '\n') i++; break;
                    case '\n': break;
                    case >= '0' and <= '7':
                        var value = n - '0';
                        for (var digits = 1; digits < 3 && i + 1 < s.Length && s[i + 1] is >= '0' and <= '7'; digits++) value = value * 8 + (s[++i] - '0');
                        sb.Append((char)(value & 0xFF));
                        break;
                    default: sb.Append(n); break;
                }
                continue;
            }
            if (c == '(') { if (depth++ > 0) sb.Append(c); continue; }
            if (c == ')') { if (--depth == 0) { i++; return sb.ToString(); } sb.Append(c); continue; }
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string ReadString(string dictionary, string key)
    {
        var match = Regex.Match(dictionary, @"/" + key + @"\s*\(");
        if (!match.Success) return string.Empty;
        var index = match.Index + match.Length - 1;
        return ReadLiteral(dictionary, ref index);
    }

    private static string Escape(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '(': sb.Append("\\("); break;
                case ')': sb.Append("\\)"); break;
                case '\r': case '\n': sb.Append(' '); break;
                default: sb.Append(c <= 0xFF && (c >= 0x20) ? c : '?'); break;
            }
        }
        return sb.ToString();
    }
}
