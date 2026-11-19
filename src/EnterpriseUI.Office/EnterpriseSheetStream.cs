using System.Globalization;
using System.IO.Compression;
using System.Xml;

namespace EnterpriseUI.Office;

/// <summary>
/// Headless SpreadStreamProcessing writer: appends rows to .xlsx worksheets in forward-only fashion so memory use does not grow with row count.
/// Values are written as inline strings, numbers, booleans, dates or <see cref="SheetFormula"/>.
/// </summary>
public sealed class EnterpriseSheetStreamWriter : IDisposable
{
    private readonly ZipArchive _zip;
    private readonly List<string> _sheetNames = new();
    private Stream? _entryStream;
    private XmlWriter? _writer;
    private int _row;
    private bool _completed;

    public EnterpriseSheetStreamWriter(Stream output, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(output);
        _zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen);
    }

    /// <summary>Rows written to the current sheet, including skipped rows.</summary>
    public int RowCount => _row;

    public int SheetCount => _sheetNames.Count;

    public void BeginSheet(string name)
    {
        ThrowIfCompleted();
        EndSheet();
        var safeName = XlsxPackage.SafeSheetName(name, _sheetNames);
        _sheetNames.Add(safeName);
        var entry = _zip.CreateEntry($"xl/worksheets/sheet{_sheetNames.Count}.xml", CompressionLevel.Fastest);
        _entryStream = entry.Open();
        _writer = XlsxPackage.CreateWriter(_entryStream);
        _writer.WriteStartDocument();
        _writer.WriteStartElement("worksheet", XlsxPackage.MainNs);
        _writer.WriteStartElement("sheetData");
        _row = 0;
    }

    public void SkipRows(int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        EnsureSheet();
        if ((long)_row + count > SheetReference.MaxRows) throw new InvalidOperationException("The worksheet row limit was exceeded.");
        _row += count;
    }

    public void WriteRow(IEnumerable<object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        EnsureSheet();
        if (_row >= SheetReference.MaxRows) throw new InvalidOperationException("The worksheet row limit was exceeded.");
        _row++;
        var w = _writer!;
        w.WriteStartElement("row");
        w.WriteAttributeString("r", _row.ToString(CultureInfo.InvariantCulture));
        var column = 0;
        foreach (var value in values)
        {
            column++;
            if (column > SheetReference.MaxColumns) throw new InvalidOperationException("The worksheet column limit was exceeded.");
            XlsxPackage.WriteCell(w, SheetReference.Format(_row, column), value);
        }
        w.WriteEndElement();
    }

    public void WriteRow(params object?[] values) => WriteRow((IEnumerable<object?>)values);

    /// <summary>Finishes the last sheet and writes the workbook parts; called by <see cref="Dispose"/> when needed.</summary>
    public void Complete()
    {
        if (_completed) return;
        EndSheet();
        if (_sheetNames.Count == 0) BeginSheet("Sheet1");
        EndSheet();
        XlsxPackage.WriteWorkbookParts(_zip, _sheetNames);
        _completed = true;
    }

    public void Dispose()
    {
        if (!_completed)
        {
            try { Complete(); }
            catch (InvalidOperationException) { }
        }
        _zip.Dispose();
    }

    private void EndSheet()
    {
        if (_writer is null) return;
        _writer.WriteEndElement();
        _writer.WriteEndElement();
        _writer.WriteEndDocument();
        _writer.Dispose();
        _entryStream?.Dispose();
        _writer = null;
        _entryStream = null;
    }

    private void EnsureSheet()
    {
        ThrowIfCompleted();
        if (_writer is null) throw new InvalidOperationException("Call BeginSheet before writing rows.");
    }

    private void ThrowIfCompleted()
    {
        if (_completed) throw new InvalidOperationException("The workbook has already been completed.");
    }
}

/// <summary>Headless SpreadStreamProcessing reader: enumerates worksheet rows lazily from a seekable .xlsx stream.</summary>
public sealed class EnterpriseSheetStreamReader : IDisposable
{
    private readonly XlsxReaderPackage _package;

    public EnterpriseSheetStreamReader(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.CanSeek) throw new ArgumentException("The stream must be seekable.", nameof(input));
        _package = new XlsxReaderPackage(input);
    }

    public IReadOnlyList<string> SheetNames => _package.SheetNames;

    /// <summary>Yields rows in order; missing columns are null and rows without cells are skipped.</summary>
    public IEnumerable<SheetStreamRow> ReadRows(string? sheetName = null)
    {
        var values = new List<object?>();
        var currentRow = 0;
        foreach (var cell in _package.ReadCells(sheetName))
        {
            if (cell.Row != currentRow)
            {
                if (currentRow != 0) yield return new SheetStreamRow(currentRow, values.ToArray());
                values.Clear();
                currentRow = cell.Row;
            }
            while (values.Count < cell.Column) values.Add(null);
            values[cell.Column - 1] = cell.Value;
        }
        if (currentRow != 0) yield return new SheetStreamRow(currentRow, values.ToArray());
    }

    public void Dispose() => _package.Dispose();
}

public sealed record SheetStreamRow(int RowNumber, IReadOnlyList<object?> Values);
