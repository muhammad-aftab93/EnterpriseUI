using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace EnterpriseUI.Documents;

public enum WordParagraphStyle { Normal, Heading1, Heading2, Heading3, ListBullet }

public sealed class WordRun
{
    public WordRun() { }
    public WordRun(string text, bool bold = false, bool italic = false, bool underline = false)
    {
        Text = text;
        Bold = bold;
        Italic = italic;
        Underline = underline;
    }

    public string Text { get; set; } = string.Empty;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
}

public abstract class WordBlock { }

public sealed class WordParagraph : WordBlock
{
    public WordParagraphStyle Style { get; set; }
    public List<WordRun> Runs { get; } = new();
    public string Text => string.Concat(Runs.Select(r => r.Text));
}

public sealed class WordTable : WordBlock
{
    public bool HasHeaderRow { get; set; } = true;
    public List<List<string>> Rows { get; } = new();
}

public sealed class WordDocument
{
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = "EnterpriseUI";
    public List<WordBlock> Blocks { get; } = new();

    public WordParagraph AddParagraph(string text, WordParagraphStyle style = WordParagraphStyle.Normal, bool bold = false)
    {
        var paragraph = new WordParagraph { Style = style };
        paragraph.Runs.Add(new WordRun(text, bold));
        Blocks.Add(paragraph);
        return paragraph;
    }

    public WordTable AddTable(IEnumerable<IEnumerable<string>> rows, bool hasHeaderRow = true)
    {
        var table = new WordTable { HasHeaderRow = hasHeaderRow };
        foreach (var row in rows) table.Rows.Add(row.ToList());
        Blocks.Add(table);
        return table;
    }
}

/// <summary>Headless WordsProcessing: writes and reads .docx packages (paragraphs, headings, bullets, formatted runs and tables).</summary>
public static class EnterpriseWordEngine
{
    private const long MaxEntryBytes = 64L * 1024 * 1024;
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace Ct = "http://schemas.openxmlformats.org/package/2006/content-types";
    private static readonly XNamespace Cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";

    public static byte[] Write(WordDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml", new XElement(Ct + "Types",
                new XElement(Ct + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(Ct + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                Override("/word/document.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"),
                Override("/word/styles.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"),
                Override("/docProps/core.xml", "application/vnd.openxmlformats-package.core-properties+xml")));

            Add(zip, "_rels/.rels", new XElement(Rel + "Relationships",
                Relationship("rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "word/document.xml"),
                Relationship("rId2", "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties", "docProps/core.xml")));

            Add(zip, "word/_rels/document.xml.rels", new XElement(Rel + "Relationships",
                Relationship("rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml")));

            Add(zip, "docProps/core.xml", new XElement(Cp + "coreProperties",
                new XElement(Dc + "title", document.Title),
                new XElement(Dc + "creator", document.Author)));

            Add(zip, "word/styles.xml", BuildStyles());

            var body = new XElement(W + "body");
            foreach (var block in document.Blocks)
            {
                if (block is WordParagraph paragraph) body.Add(BuildParagraph(paragraph));
                else if (block is WordTable table) body.Add(BuildTable(table));
            }
            Add(zip, "word/document.xml", new XElement(W + "document", new XAttribute(XNamespace.Xmlns + "w", W.NamespaceName), body));
        }
        return output.ToArray();
    }

    public static WordDocument Read(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var input = new MemoryStream(data, writable: false);
        using var zip = new ZipArchive(input, ZipArchiveMode.Read);
        var main = Load(zip, "word/document.xml") ?? throw new InvalidDataException("The package has no word/document.xml part.");
        var result = new WordDocument { Title = string.Empty, Author = string.Empty };

        var core = Load(zip, "docProps/core.xml");
        if (core?.Root is not null)
        {
            result.Title = core.Root.Element(Dc + "title")?.Value ?? string.Empty;
            result.Author = core.Root.Element(Dc + "creator")?.Value ?? string.Empty;
        }

        var body = main.Root?.Element(W + "body") ?? throw new InvalidDataException("The document has no body.");
        foreach (var element in body.Elements())
        {
            if (element.Name == W + "p") result.Blocks.Add(ReadParagraph(element));
            else if (element.Name == W + "tbl") result.Blocks.Add(ReadTable(element));
        }
        return result;
    }

    public static string ExtractText(WordDocument document)
    {
        var sb = new StringBuilder();
        foreach (var block in document.Blocks)
        {
            if (block is WordParagraph p) sb.AppendLine(p.Text);
            else if (block is WordTable t) foreach (var row in t.Rows) sb.AppendLine(string.Join("\t", row));
        }
        return sb.ToString();
    }

    private static XElement Override(string part, string type) =>
        new(Ct + "Override", new XAttribute("PartName", part), new XAttribute("ContentType", type));

    private static XElement Relationship(string id, string type, string target) =>
        new(Rel + "Relationship", new XAttribute("Id", id), new XAttribute("Type", type), new XAttribute("Target", target));

    private static XElement BuildStyles()
    {
        XElement Style(string id, string name, bool bold, int halfPoints) =>
            new(W + "style", new XAttribute(W + "type", "paragraph"), new XAttribute(W + "styleId", id),
                new XElement(W + "name", new XAttribute(W + "val", name)),
                new XElement(W + "rPr", bold ? new XElement(W + "b") : null, new XElement(W + "sz", new XAttribute(W + "val", halfPoints))));

        return new XElement(W + "styles", new XAttribute(XNamespace.Xmlns + "w", W.NamespaceName),
            Style("Normal", "Normal", false, 22),
            Style("Heading1", "heading 1", true, 36),
            Style("Heading2", "heading 2", true, 30),
            Style("Heading3", "heading 3", true, 26),
            Style("ListBullet", "List Bullet", false, 22));
    }

    private static string StyleId(WordParagraphStyle style) => style == WordParagraphStyle.Normal ? "Normal" : style.ToString();

    private static XElement BuildParagraph(WordParagraph paragraph)
    {
        var element = new XElement(W + "p");
        if (paragraph.Style != WordParagraphStyle.Normal)
            element.Add(new XElement(W + "pPr", new XElement(W + "pStyle", new XAttribute(W + "val", StyleId(paragraph.Style)))));

        foreach (var run in paragraph.Runs)
        {
            var properties = new XElement(W + "rPr");
            if (run.Bold) properties.Add(new XElement(W + "b"));
            if (run.Italic) properties.Add(new XElement(W + "i"));
            if (run.Underline) properties.Add(new XElement(W + "u", new XAttribute(W + "val", "single")));
            var runElement = new XElement(W + "r");
            if (properties.HasElements) runElement.Add(properties);
            var parts = SanitizeXml(run.Text).Split('\n');
            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0) runElement.Add(new XElement(W + "br"));
                runElement.Add(new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), parts[i]));
            }
            element.Add(runElement);
        }
        return element;
    }

    private static XElement BuildTable(WordTable table)
    {
        var borders = new XElement(W + "tblBorders", new[] { "top", "left", "bottom", "right", "insideH", "insideV" }
            .Select(side => new XElement(W + side, new XAttribute(W + "val", "single"), new XAttribute(W + "sz", 4), new XAttribute(W + "color", "808080"))));
        var element = new XElement(W + "tbl", new XElement(W + "tblPr", borders));
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var row = new XElement(W + "tr");
            foreach (var cell in table.Rows[r])
            {
                var paragraph = new WordParagraph();
                paragraph.Runs.Add(new WordRun(cell, bold: table.HasHeaderRow && r == 0));
                row.Add(new XElement(W + "tc", BuildParagraph(paragraph)));
            }
            element.Add(row);
        }
        return element;
    }

    private static WordParagraph ReadParagraph(XElement element)
    {
        var paragraph = new WordParagraph();
        var style = element.Element(W + "pPr")?.Element(W + "pStyle")?.Attribute(W + "val")?.Value;
        if (style is not null && Enum.TryParse<WordParagraphStyle>(style, true, out var parsed)) paragraph.Style = parsed;

        foreach (var run in element.Descendants(W + "r"))
        {
            var properties = run.Element(W + "rPr");
            var text = new StringBuilder();
            foreach (var child in run.Elements())
            {
                if (child.Name == W + "t") text.Append(child.Value);
                else if (child.Name == W + "tab") text.Append('\t');
                else if (child.Name == W + "br") text.Append('\n');
            }
            paragraph.Runs.Add(new WordRun(text.ToString(), HasFlag(properties, "b"), HasFlag(properties, "i"), properties?.Element(W + "u") is { } u && u.Attribute(W + "val")?.Value != "none"));
        }
        return paragraph;
    }

    private static bool HasFlag(XElement? properties, string name)
    {
        var flag = properties?.Element(W + name);
        if (flag is null) return false;
        var value = flag.Attribute(W + "val")?.Value;
        return value is null || (value != "0" && !value.Equals("false", StringComparison.OrdinalIgnoreCase));
    }

    private static WordTable ReadTable(XElement element)
    {
        var table = new WordTable { HasHeaderRow = false };
        foreach (var row in element.Elements(W + "tr"))
            table.Rows.Add(row.Elements(W + "tc").Select(cell => string.Join("\n", cell.Elements(W + "p").Select(p => string.Concat(p.Descendants(W + "t").Select(t => t.Value))))).ToList());

        var first = element.Elements(W + "tr").FirstOrDefault()?.Descendants(W + "r").ToList();
        table.HasHeaderRow = first is { Count: > 0 } && first.All(r => HasFlag(r.Element(W + "rPr"), "b"));
        return table;
    }

    private static string SanitizeXml(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) { sb.Append(c).Append(text[++i]); }
            else if (c == '\t' || c == '\n' || (c >= 0x20 && !char.IsSurrogate(c) && c != 0xFFFE && c != 0xFFFF)) sb.Append(c);
        }
        return sb.ToString();
    }

    private static void Add(ZipArchive zip, string path, XElement root)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root).Save(stream);
    }

    private static XDocument? Load(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path);
        if (entry is null) return null;
        if (entry.Length > MaxEntryBytes) throw new InvalidDataException($"The part '{path}' is too large.");
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return XDocument.Load(reader);
    }
}
