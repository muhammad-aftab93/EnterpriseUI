using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Xml;

namespace EnterpriseUI.Exporting;

public enum ExportFormat
{
    Csv,
    Json,
    Html,
    ExcelXml,
    Xml,
    Pdf,
    Xlsx
}

/// <summary>
/// One-call export of plain object lists. Grids use <see cref="ExportService"/> with a document built from their state
/// (columns, groups, hierarchies, summaries, formatting) instead.
/// </summary>
public static class ExportEngine
{
    public static string FormatId(ExportFormat format) => format switch
    {
        ExportFormat.Csv => "csv",
        ExportFormat.Json => "json",
        ExportFormat.Html => "html",
        ExportFormat.ExcelXml => "excelxml",
        ExportFormat.Xml => "xml",
        ExportFormat.Pdf => "pdf",
        ExportFormat.Xlsx => "xlsx",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    public static string FileExtension(ExportFormat format) => format switch
    {
        ExportFormat.Csv => ".csv",
        ExportFormat.Json => ".json",
        ExportFormat.Html => ".html",
        ExportFormat.ExcelXml or ExportFormat.Xml => ".xml",
        ExportFormat.Pdf => ".pdf",
        ExportFormat.Xlsx => ".xlsx",
        _ => string.Empty
    };

    /// <summary>Builds a one-sheet document over the public readable properties of <typeparamref name="T"/>; rows are read lazily.</summary>
    public static ExportDocument CreateDocument<T>(IEnumerable<T> data, string? title = null, string sheetName = "Data")
    {
        ArgumentNullException.ThrowIfNull(data);
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray();
        var columns = properties.Select(p =>
        {
            var type = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            var numeric = type.IsPrimitive && type != typeof(bool) && type != typeof(char) || type == typeof(decimal);
            return new ExportColumn(p.Name) { DataType = p.PropertyType, Alignment = numeric ? ExportAlignment.Right : ExportAlignment.Left };
        }).ToList();

        var sheet = new ExportSheet(sheetName, columns, Rows(data, properties))
        {
            Title = title,
            RowCountHint = data is ICollection<T> collection ? collection.Count : null
        };
        var document = new ExportDocument { Title = title ?? typeof(T).Name };
        document.Sheets.Add(sheet);
        return document;
    }

    private static IEnumerable<ExportRow> Rows<T>(IEnumerable<T> data, PropertyInfo[] properties)
    {
        foreach (var item in data)
        {
            var cells = new object?[properties.Length];
            for (var i = 0; i < properties.Length; i++)
            {
                cells[i] = item == null ? null : properties[i].GetValue(item);
            }
            yield return new ExportRow(ExportRowKind.Data, 0, cells);
        }
    }

    public static async Task ExportAsync<T>(IEnumerable<T> data, ExportFormat format, Stream outputStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(outputStream);

        switch (format)
        {
            case ExportFormat.Json:
                await JsonSerializer.SerializeAsync(outputStream, data, cancellationToken: cancellationToken).ConfigureAwait(false);
                return;
            case ExportFormat.Xml:
                await Task.Run(() => WriteLegacyXml(data, outputStream, cancellationToken), cancellationToken).ConfigureAwait(false);
                return;
            default:
                await ExportService.Default.ExportAsync(CreateDocument(data), FormatId(format), outputStream, cancellationToken: cancellationToken).ConfigureAwait(false);
                return;
        }
    }

    private static void WriteLegacyXml<T>(IEnumerable<T> data, Stream stream, CancellationToken cancellationToken)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0).ToArray();
        var typeName = XmlConvert.EncodeLocalName(typeof(T).Name.Split('`')[0]);
        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false), CloseOutput = false };
        using var xml = XmlWriter.Create(stream, settings);
        xml.WriteStartDocument();
        xml.WriteStartElement($"ArrayOf{typeName}");
        foreach (var item in data)
        {
            cancellationToken.ThrowIfCancellationRequested();
            xml.WriteStartElement(typeName);
            foreach (var property in properties)
            {
                var value = item == null ? null : property.GetValue(item);
                xml.WriteElementString(XmlConvert.EncodeLocalName(property.Name), XmlExportProvider.Clean(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)));
            }
            xml.WriteEndElement();
        }
        xml.WriteEndElement();
        xml.WriteEndDocument();
    }
}
