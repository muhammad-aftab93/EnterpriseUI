using System.Diagnostics;
using System.Globalization;

namespace EnterpriseUI.Exporting;

public interface IExportProvider
{
    /// <summary>Stable identifier such as "xlsx"; used to look the provider up.</summary>
    string Id { get; }
    string DisplayName { get; }
    string FileExtension { get; }
    string ContentType { get; }

    /// <summary>Writes the document. Rows are enumerated once, in order; the output stream is not closed.</summary>
    Task WriteAsync(ExportDocument document, Stream output, ExportContext context);
}

public sealed class ExportContext
{
    public ExportContext(ExportOptions options, CancellationToken cancellationToken)
    {
        Options = options;
        CancellationToken = cancellationToken;
    }

    public ExportOptions Options { get; }
    public CancellationToken CancellationToken { get; }
}

/// <summary>
/// Streams an <see cref="ExportDocument"/> through a registered <see cref="IExportProvider"/> with progress, cancellation and row/cell events.
/// Custom formats are added with <see cref="Register"/>.
/// </summary>
public sealed class ExportService
{
    private readonly Dictionary<string, IExportProvider> _providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public static ExportService Default { get; } = new();

    public ExportService()
    {
        Register(new CsvExportProvider());
        Register(new JsonExportProvider());
        Register(new XmlExportProvider());
        Register(new HtmlExportProvider());
        Register(new XlsxExportProvider());
        Register(new SpreadsheetMlExportProvider());
        Register(new PdfExportProvider());
    }

    /// <summary>Raised for every row before it is written; set <see cref="ExportRowEventArgs.Skip"/> to omit it.</summary>
    public event EventHandler<ExportRowEventArgs>? RowExporting;

    /// <summary>Raised for every cell before it is written; change <see cref="ExportCellEventArgs.Value"/> or <see cref="ExportCellEventArgs.Style"/>.</summary>
    public event EventHandler<ExportCellEventArgs>? CellExporting;

    public event EventHandler<string>? Started;
    public event EventHandler<ExportResult>? Completed;

    public void Register(IExportProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (_gate) _providers[provider.Id] = provider;
    }

    public IReadOnlyList<IExportProvider> Providers
    {
        get { lock (_gate) return _providers.Values.OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase).ToList(); }
    }

    public IExportProvider? Find(string formatId)
    {
        lock (_gate) return _providers.TryGetValue(formatId, out var provider) ? provider : null;
    }

    public async Task<ExportResult> ExportAsync(
        ExportDocument document,
        string formatId,
        Stream output,
        ExportOptions? options = null,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(output);
        var provider = Find(formatId) ?? throw new ArgumentException($"No export provider is registered for '{formatId}'.", nameof(formatId));
        options ??= new ExportOptions();

        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();
        Started?.Invoke(this, provider.Id);

        var state = new PumpState();
        var instrumented = new ExportDocument { Title = document.Title, Author = document.Author, CreatedUtc = document.CreatedUtc };
        foreach (var sheet in document.Sheets)
        {
            instrumented.Sheets.Add(Instrument(sheet, options, progress, state, cancellationToken));
        }

        await using var counting = new CountingStream(output);
        await provider.WriteAsync(instrumented, counting, new ExportContext(options, cancellationToken)).ConfigureAwait(false);
        await counting.FlushAsync(cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();
        progress?.Report(new ExportProgress(state.Rows, state.Rows, string.Empty));
        var result = new ExportResult(provider.Id, state.Rows, instrumented.Sheets.Count, counting.BytesWritten, stopwatch.Elapsed);
        Completed?.Invoke(this, result);
        return result;
    }

    /// <summary>Exports to a file. The file is written under a temporary name and moved into place on success, so a cancelled or failed export never leaves a partial file.</summary>
    public async Task<ExportResult> ExportToFileAsync(
        ExportDocument document,
        string formatId,
        string path,
        ExportOptions? options = null,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var temp = path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        try
        {
            ExportResult result;
            await using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                result = await ExportAsync(document, formatId, file, options, progress, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temp, path, overwrite: true);
            return result;
        }
        catch
        {
            try { File.Delete(temp); } catch (IOException) { }
            throw;
        }
    }

    private sealed class PumpState
    {
        public long Rows;
    }

    private ExportSheet Instrument(ExportSheet source, ExportOptions options, IProgress<ExportProgress>? progress, PumpState state, CancellationToken ct)
    {
        var target = new ExportSheet(source.Name, source.Columns, Pump(source, options, progress, state, ct))
        {
            Title = source.Title,
            Footer = options.IncludeSummaries ? source.Footer : Array.Empty<ExportRow>(),
            RowCountHint = source.RowCountHint,
            FreezeHeader = source.FreezeHeader,
            AutoFilter = source.AutoFilter,
            IsStructured = source.IsStructured && (options.IncludeGroups || options.IncludeDetails)
        };
        foreach (var format in source.ConditionalFormats) target.ConditionalFormats.Add(format);
        return target;
    }

    private IEnumerable<ExportRow> Pump(ExportSheet sheet, ExportOptions options, IProgress<ExportProgress>? progress, PumpState state, CancellationToken ct)
    {
        var rowHandler = RowExporting;
        var cellHandler = CellExporting;
        var interval = Math.Max(1, options.ProgressInterval);

        foreach (var row in sheet.Rows)
        {
            ct.ThrowIfCancellationRequested();

            if (!options.IncludeGroups && row.Kind is ExportRowKind.GroupHeader or ExportRowKind.GroupFooter) continue;
            if (!options.IncludeDetails && row.Kind is ExportRowKind.DetailHeader or ExportRowKind.DetailData) continue;
            if (!options.IncludeGroups && row.Kind == ExportRowKind.Data) row.Level = 0;

            if (rowHandler != null)
            {
                var rowArgs = new ExportRowEventArgs(sheet, row);
                rowHandler(this, rowArgs);
                if (rowArgs.Skip) continue;
            }

            if (cellHandler != null && row.Cells.Length > 0)
            {
                for (var i = 0; i < row.Cells.Length; i++)
                {
                    var column = row.Kind is ExportRowKind.Data or ExportRowKind.Summary or ExportRowKind.GroupFooter && i < sheet.Columns.Count ? sheet.Columns[i] : null;
                    var style = row.CellStyles != null && i < row.CellStyles.Length ? row.CellStyles[i] : null;
                    var args = new ExportCellEventArgs(sheet, row, column, i, row.Cells[i]) { Style = style };
                    cellHandler(this, args);
                    row.Cells[i] = args.Value;
                    if (!ReferenceEquals(args.Style, style))
                    {
                        row.CellStyles ??= new ExportCellStyle?[row.Cells.Length];
                        row.CellStyles[i] = args.Style;
                    }
                }
            }

            state.Rows++;
            if (progress != null && state.Rows % interval == 0)
            {
                progress.Report(new ExportProgress(state.Rows, sheet.RowCountHint, sheet.Name));
            }

            yield return row;
        }
    }
}

internal sealed class CountingStream : Stream
{
    private readonly Stream _inner;

    public CountingStream(Stream inner) => _inner = inner;

    public long BytesWritten { get; private set; }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => BytesWritten; set => throw new NotSupportedException(); }

    public override void Flush() => _inner.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        _inner.Write(buffer, offset, count);
        BytesWritten += count;
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _inner.Write(buffer);
        BytesWritten += buffer.Length;
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        BytesWritten += buffer.Length;
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    protected override void Dispose(bool disposing)
    {
        // The wrapped stream belongs to the caller.
        base.Dispose(disposing);
    }
}

/// <summary>Text, color and number-format helpers shared by the providers.</summary>
public static class ExportFormatting
{
    public static string FormatCell(object? value, ExportColumn? column, CultureInfo culture)
    {
        switch (value)
        {
            case null:
                return string.Empty;
            case string text:
                return text;
            case DateTime dateTime when string.IsNullOrEmpty(column?.Format):
                return dateTime.TimeOfDay == TimeSpan.Zero ? dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            case DateTimeOffset offset when string.IsNullOrEmpty(column?.Format):
                return offset.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
            case DateOnly dateOnly when string.IsNullOrEmpty(column?.Format):
                return dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            case IFormattable formattable:
                if (!string.IsNullOrEmpty(column?.Format))
                {
                    try
                    {
                        return formattable.ToString(column!.Format, culture);
                    }
                    catch (FormatException)
                    {
                        // Fall through to the default representation when the format does not suit the value.
                    }
                }
                return formattable.ToString(null, culture);
            case bool flag:
                return flag.ToString(culture);
            default:
                return Convert.ToString(value, culture) ?? string.Empty;
        }
    }

    public static bool IsNumeric(object? value) => value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;

    public static bool IsDate(object? value) => value is DateTime or DateTimeOffset or DateOnly;

    /// <summary>Parses #RRGGBB (or RRGGBB); returns false for anything else.</summary>
    public static bool TryParseColor(string? hex, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        var span = hex.AsSpan().Trim();
        if (span.Length > 0 && span[0] == '#') span = span[1..];
        return span.Length == 6 &&
               byte.TryParse(span[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r) &&
               byte.TryParse(span[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g) &&
               byte.TryParse(span[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b);
    }

    /// <summary>Maps a .NET numeric or date format to an Excel number format code; null keeps the General format.</summary>
    public static string? ToExcelNumberFormat(string? format, Type? type, CultureInfo culture)
    {
        var underlying = type == null ? null : Nullable.GetUnderlyingType(type) ?? type;
        var isDate = underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset) || underlying == typeof(DateOnly);
        if (string.IsNullOrEmpty(format))
        {
            return isDate ? "yyyy-mm-dd" : null;
        }

        var kind = char.ToUpperInvariant(format[0]);
        var digits = format.Length > 1 && int.TryParse(format.AsSpan(1), out var n) ? n : (int?)null;
        string Zeros(int count, string defaultValue = "") => count > 0 ? "." + new string('0', count) : defaultValue;

        if (format.Length <= 3 && char.IsLetter(format[0]) && (digits != null || format.Length == 1))
        {
            if (isDate)
            {
                return format switch
                {
                    "d" => "yyyy-mm-dd",
                    "D" => "dddd, mmmm d, yyyy",
                    "g" => "yyyy-mm-dd hh:mm",
                    "G" => "yyyy-mm-dd hh:mm:ss",
                    "t" => "hh:mm",
                    "T" => "hh:mm:ss",
                    "s" => "yyyy-mm-ddThh:mm:ss",
                    "u" => "yyyy-mm-dd hh:mm:ss",
                    "M" or "m" => "mmmm d",
                    "Y" or "y" => "mmmm yyyy",
                    _ => "yyyy-mm-dd"
                };
            }

            switch (kind)
            {
                case 'N': return "#,##0" + Zeros(digits ?? 2);
                case 'F': return "0" + Zeros(digits ?? 2);
                case 'D': return digits is > 0 ? new string('0', digits.Value) : "0";
                case 'P': return "0" + Zeros(digits ?? 2) + "%";
                case 'C':
                {
                    var symbol = culture.NumberFormat.CurrencySymbol.Replace("\"", string.Empty);
                    return $"\"{symbol}\"#,##0{Zeros(digits ?? culture.NumberFormat.CurrencyDecimalDigits)}";
                }
                case 'E': return "0" + Zeros(digits ?? 6) + "E+00";
                case 'G': return null;
            }
        }

        // Custom format: date patterns keep their letters (Excel uses lower-case months/minutes in context), numeric ones pass through.
        return isDate ? format.Replace("MM", "mm").Replace("M", "m").Replace("tt", "AM/PM") : format;
    }
}
