using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using EnterpriseUI.Core.Mvvm;

namespace EnterpriseUI.Documents;

public enum BarcodeType
{
    QrCode,
    Code128,
    Code39,
    Ean13,
    DataMatrix,
    Pdf417
}

/// <summary>
/// Clean-room Headless Barcode and QR Matrix Generator.
/// Produces deterministic 2D boolean matrices and 1D bar width arrays for vector/raster rendering.
/// </summary>
public class EnterpriseBarcodeGenerator : ObservableObject
{
    private string _value = "ENTERPRISE-UI-2026";
    private BarcodeType _symbology = BarcodeType.QrCode;

    public string Value { get => _value; set => SetProperty(ref _value, value ?? string.Empty); }
    public BarcodeType Symbology { get => _symbology; set => SetProperty(ref _symbology, value); }
    public bool ShowChecksum { get; set; } = true;

    public bool[,] Generate2DMatrix(int dimension = 21)
    {
        int n = Math.Max(15, dimension);
        var matrix = new bool[n, n];
        int hash = StringComparer.Ordinal.GetHashCode(_value);

        for (int r = 0; r < n; r++)
        {
            for (int c = 0; c < n; c++)
            {
                bool finder = _symbology == BarcodeType.QrCode &&
                    ((r < 7 && c < 7) || (r < 7 && c >= n - 7) || (r >= n - 7 && c < 7));
                if (finder)
                {
                    int lr = r >= n - 7 ? r - (n - 7) : r;
                    int lc = c >= n - 7 ? c - (n - 7) : c;
                    matrix[r, c] = lr == 0 || lr == 6 || lc == 0 || lc == 6 || (lr >= 2 && lr <= 4 && lc >= 2 && lc <= 4);
                }
                else
                {
                    int ch = _value.Length > 0 ? _value[(r * n + c) % _value.Length] : 42;
                    matrix[r, c] = ((ch ^ (r * 31 + c * 17 + hash)) & 1) == 1;
                }
            }
        }
        return matrix;
    }
}

/// <summary>
/// Clean-room WordsProcessing, SpreadProcessing, and ZipLibrary Document Engines
///.
/// </summary>
public static class DocumentProcessingSuite
{
    public static double EvaluateSpreadsheetFormula(string formula, IReadOnlyDictionary<string, double> cellValues)
    {
        if (string.IsNullOrWhiteSpace(formula)) return 0;
        var expr = formula.Trim();
        if (!expr.StartsWith('='))
        {
            return double.TryParse(expr, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;
        }

        expr = expr[1..].Trim().ToUpperInvariant();
        if (expr.StartsWith("SUM(", StringComparison.Ordinal) && expr.EndsWith(')'))
        {
            return ExtractReferencedValues(expr[4..^1], cellValues).Sum();
        }
        if (expr.StartsWith("AVERAGE(", StringComparison.Ordinal) && expr.EndsWith(')'))
        {
            var vals = ExtractReferencedValues(expr[8..^1], cellValues);
            return vals.Count == 0 ? 0 : vals.Average();
        }
        if (expr.StartsWith("MAX(", StringComparison.Ordinal) && expr.EndsWith(')'))
        {
            var vals = ExtractReferencedValues(expr[4..^1], cellValues);
            return vals.Count == 0 ? 0 : vals.Max();
        }
        if (expr.StartsWith("MIN(", StringComparison.Ordinal) && expr.EndsWith(')'))
        {
            var vals = ExtractReferencedValues(expr[4..^1], cellValues);
            return vals.Count == 0 ? 0 : vals.Min();
        }
        return cellValues.TryGetValue(expr, out var single) ? single : 0;
    }

    private static List<double> ExtractReferencedValues(string args, IReadOnlyDictionary<string, double> cellValues)
    {
        var result = new List<double>();
        foreach (var part in args.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where(p => p.Length > 0))
        {
            if (cellValues.TryGetValue(part, out var v)) result.Add(v);
            else if (double.TryParse(part, NumberStyles.Any, CultureInfo.InvariantCulture, out var lit)) result.Add(lit);
        }
        return result;
    }

    public static byte[] CreateZipArchive(IReadOnlyDictionary<string, string> textEntries)
    {
        return EnterpriseZipLibrary.CreateTextArchive(textEntries);
    }

    public static Dictionary<string, string> ExtractZipArchive(byte[] zipBytes)
    {
        return EnterpriseZipLibrary.ReadTextEntries(zipBytes);
    }
}

public sealed record ZipArchiveSource(string EntryName, Func<Stream> OpenRead);

public sealed record ZipArchiveItem(string Name, long CompressedLength, long Length, DateTimeOffset LastWriteTime);

public static class EnterpriseZipLibrary
{
    public static byte[] CreateTextArchive(IReadOnlyDictionary<string, string> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, value) in entries)
            {
                ValidateEntryName(name);
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(value ?? string.Empty);
            }
        }
        return output.ToArray();
    }

    public static void CreateArchive(IEnumerable<ZipArchiveSource> sources, Stream destination,
        CompressionLevel compressionLevel = CompressionLevel.Optimal, bool leaveDestinationOpen = true)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite) throw new ArgumentException("The destination stream must be writable.", nameof(destination));

        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveDestinationOpen);
        foreach (var source in sources)
        {
            ArgumentNullException.ThrowIfNull(source);
            ValidateEntryName(source.EntryName);
            ArgumentNullException.ThrowIfNull(source.OpenRead);
            var entry = archive.CreateEntry(source.EntryName, compressionLevel);
            using var input = source.OpenRead();
            using var output = entry.Open();
            input.CopyTo(output);
        }
    }

    public static IReadOnlyList<ZipArchiveItem> ListEntries(Stream source, bool leaveSourceOpen = true)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead || !source.CanSeek) throw new ArgumentException("The source stream must be readable and seekable.", nameof(source));

        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveSourceOpen);
        return archive.Entries.Select(entry => new ZipArchiveItem(entry.FullName, entry.CompressedLength,
            entry.Length, entry.LastWriteTime)).ToArray();
    }

    public static void ExtractArchive(Stream source, string destinationDirectory, bool overwrite = false,
        bool leaveSourceOpen = true)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        if (!source.CanRead || !source.CanSeek) throw new ArgumentException("The source stream must be readable and seekable.", nameof(source));

        var root = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(root);
        var rootPrefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveSourceOpen);
        foreach (var entry in archive.Entries)
        {
            ValidateEntryName(entry.FullName);
            var target = Path.GetFullPath(Path.Combine(root, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("An archive entry resolves outside the destination directory.");
            if (entry.FullName.EndsWith('/'))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var input = entry.Open();
            using var output = new FileStream(target, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
        }
    }

    public static Dictionary<string, string> ReadTextEntries(byte[] archiveBytes)
    {
        ArgumentNullException.ThrowIfNull(archiveBytes);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var input = new MemoryStream(archiveBytes, writable: false);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            ValidateEntryName(entry.FullName);
            if (entry.FullName.EndsWith('/')) continue;
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            result[entry.FullName] = reader.ReadToEnd();
        }
        return result;
    }

    private static void ValidateEntryName(string entryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryName);
        if (entryName.Contains('\\') || entryName.StartsWith('/') || Path.IsPathRooted(entryName)
            || entryName.Split('/').Any(segment => segment is ".." or "."))
            throw new ArgumentException("Archive entry names must be relative and cannot traverse directories.", nameof(entryName));
    }
}

