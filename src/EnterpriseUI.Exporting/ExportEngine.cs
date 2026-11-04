using System.Reflection;
using System.Text;
using System.Text.Json;

namespace EnterpriseUI.Exporting;

public enum ExportFormat
{
    Csv,
    Json,
    Html,
    ExcelXml,
    Xml,
    Pdf
}

/// <summary>
/// High-throughput streaming data exporter for tabular and hierarchical datasets.
/// Supports CSV, JSON, HTML, Excel SpreadsheetML, XML, and PDF document streams.
/// </summary>
public static class ExportEngine
{
    public static async Task ExportAsync<T>(IEnumerable<T> data, ExportFormat format, Stream outputStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(outputStream);

        switch (format)
        {
            case ExportFormat.Csv:
                await ExportCsvAsync(data, outputStream, cancellationToken);
                break;
            case ExportFormat.Json:
                await JsonSerializer.SerializeAsync(outputStream, data, cancellationToken: cancellationToken);
                break;
            case ExportFormat.Html:
                await ExportHtmlAsync(data, outputStream, cancellationToken);
                break;
            case ExportFormat.ExcelXml:
                await ExportExcelXmlAsync(data, outputStream, cancellationToken);
                break;
            case ExportFormat.Xml:
                await ExportXmlAsync(data, outputStream, cancellationToken);
                break;
            case ExportFormat.Pdf:
                await ExportPdfAsync(data, outputStream, cancellationToken);
                break;
        }
    }

    private static async Task ExportCsvAsync<T>(IEnumerable<T> data, Stream stream, CancellationToken ct)
    {
        using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Header
        await writer.WriteLineAsync(string.Join(",", properties.Select(p => $"\"{p.Name}\""))).WaitAsync(ct);

        // Rows
        foreach (var item in data)
        {
            var values = properties.Select(p =>
            {
                var val = p.GetValue(item)?.ToString()?.Replace("\"", "\"\"") ?? "";
                return $"\"{val}\"";
            });
            await writer.WriteLineAsync(string.Join(",", values)).WaitAsync(ct);
        }
    }

    private static async Task ExportHtmlAsync<T>(IEnumerable<T> data, Stream stream, CancellationToken ct)
    {
        using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        await writer.WriteLineAsync("<table border=\"1\" cellpadding=\"4\" cellspacing=\"0\"><thead><tr>").WaitAsync(ct);
        foreach (var p in properties)
        {
            await writer.WriteLineAsync($"<th>{p.Name}</th>").WaitAsync(ct);
        }
        await writer.WriteLineAsync("</tr></thead><tbody>").WaitAsync(ct);

        foreach (var item in data)
        {
            await writer.WriteLineAsync("<tr>").WaitAsync(ct);
            foreach (var p in properties)
            {
                var val = p.GetValue(item)?.ToString() ?? "";
                await writer.WriteLineAsync($"<td>{System.Net.WebUtility.HtmlEncode(val)}</td>").WaitAsync(ct);
            }
            await writer.WriteLineAsync("</tr>").WaitAsync(ct);
        }

        await writer.WriteLineAsync("</tbody></table>").WaitAsync(ct);
    }

    private static async Task ExportExcelXmlAsync<T>(IEnumerable<T> data, Stream stream, CancellationToken ct)
    {
        using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        await writer.WriteLineAsync("<?xml version=\"1.0\"?>\n<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">\n<Worksheet ss:Name=\"Sheet1\">\n<Table>").WaitAsync(ct);

        // Header
        await writer.WriteLineAsync("<Row>").WaitAsync(ct);
        foreach (var p in properties)
        {
            await writer.WriteLineAsync($"<Cell><Data ss:Type=\"String\">{p.Name}</Data></Cell>").WaitAsync(ct);
        }
        await writer.WriteLineAsync("</Row>").WaitAsync(ct);

        // Rows
        foreach (var item in data)
        {
            await writer.WriteLineAsync("<Row>").WaitAsync(ct);
            foreach (var p in properties)
            {
                var val = p.GetValue(item)?.ToString() ?? "";
                await writer.WriteLineAsync($"<Cell><Data ss:Type=\"String\">{System.Security.SecurityElement.Escape(val)}</Data></Cell>").WaitAsync(ct);
            }
            await writer.WriteLineAsync("</Row>").WaitAsync(ct);
        }

        await writer.WriteLineAsync("</Table>\n</Worksheet>\n</Workbook>").WaitAsync(ct);
    }

    private static async Task ExportXmlAsync<T>(IEnumerable<T> data, Stream stream, CancellationToken ct)
    {
        using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var typeName = typeof(T).Name;

        await writer.WriteLineAsync($"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<ArrayOf{typeName}>").WaitAsync(ct);
        foreach (var item in data)
        {
            await writer.WriteLineAsync($"  <{typeName}>").WaitAsync(ct);
            foreach (var p in properties)
            {
                var val = p.GetValue(item)?.ToString() ?? "";
                await writer.WriteLineAsync($"    <{p.Name}>{System.Security.SecurityElement.Escape(val)}</{p.Name}>").WaitAsync(ct);
            }
            await writer.WriteLineAsync($"  </{typeName}>").WaitAsync(ct);
        }
        await writer.WriteLineAsync($"</ArrayOf{typeName}>").WaitAsync(ct);
    }

    private static async Task ExportPdfAsync<T>(IEnumerable<T> data, Stream stream, CancellationToken ct)
    {
        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var contentSb = new StringBuilder();
        contentSb.Append("BT /F1 10 Tf 36 756 Td (EnterpriseUI DataGrid PDF Export) Tj 0 -18 Td ");
        var headerLine = string.Join(" | ", properties.Select(p => p.Name));
        contentSb.Append($"({EscapePdfString(headerLine)}) Tj 0 -14 Td ");

        int count = 0;
        foreach (var item in data)
        {
            if (count++ >= 48) break; // Fit first page in standard stream
            var rowText = string.Join(" | ", properties.Select(p => p.GetValue(item)?.ToString() ?? ""));
            contentSb.Append($"({EscapePdfString(rowText)}) Tj 0 -12 Td ");
        }
        contentSb.Append("ET");
        var streamData = contentSb.ToString();

        var pdf = new StringBuilder();
        pdf.Append("%PDF-1.4\n");
        pdf.Append("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n");
        pdf.Append("2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n");
        pdf.Append("3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >> endobj\n");
        pdf.Append("4 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj\n");
        pdf.Append($"5 0 obj << /Length {streamData.Length} >> stream\n{streamData}\nendstream endobj\n");
        pdf.Append("xref\n0 6\n0000000000 65535 f \ntrailer << /Size 6 /Root 1 0 R >>\n%%EOF\n");

        await writer.WriteAsync(pdf.ToString()).WaitAsync(ct);
    }

    private static string EscapePdfString(string input)
    {
        return input.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }
}
