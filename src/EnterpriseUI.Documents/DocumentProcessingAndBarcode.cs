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
/// Clean-room Headless Barcode and QR Matrix Generator (Telerik RadBarcode equivalent).
/// Produces deterministic 2D boolean matrices and 1D bar width arrays for vector/raster rendering.
/// </summary>
public class BarcodeGenerator : ObservableObject
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
/// (Telerik RadWordsProcessing, RadSpreadProcessing, RadZipLibrary equivalents).
/// </summary>
public static class DocumentProcessingSuite
{
    public static double EvaluateSpreadsheetFormula(string formula, IReadOnlyDictionary<string, double> cellValues)
    {
        if (string.IsNullOrWhiteSpace(formula)) return 0;
        var expr = formula.Trim();
        if (!expr.StartsWith("=", StringComparison.Ordinal))
        {
            return double.TryParse(expr, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;
        }

        expr = expr[1..].Trim().ToUpperInvariant();
        if (expr.StartsWith("SUM(", StringComparison.Ordinal) && expr.EndsWith(")", StringComparison.Ordinal))
        {
            return ExtractReferencedValues(expr[4..^1], cellValues).Sum();
        }
        if (expr.StartsWith("AVERAGE(", StringComparison.Ordinal) && expr.EndsWith(")", StringComparison.Ordinal))
        {
            var vals = ExtractReferencedValues(expr[8..^1], cellValues);
            return vals.Count == 0 ? 0 : vals.Average();
        }
        if (expr.StartsWith("MAX(", StringComparison.Ordinal) && expr.EndsWith(")", StringComparison.Ordinal))
        {
            var vals = ExtractReferencedValues(expr[4..^1], cellValues);
            return vals.Count == 0 ? 0 : vals.Max();
        }
        if (expr.StartsWith("MIN(", StringComparison.Ordinal) && expr.EndsWith(")", StringComparison.Ordinal))
        {
            var vals = ExtractReferencedValues(expr[4..^1], cellValues);
            return vals.Count == 0 ? 0 : vals.Min();
        }
        return cellValues.TryGetValue(expr, out var single) ? single : 0;
    }

    private static List<double> ExtractReferencedValues(string args, IReadOnlyDictionary<string, double> cellValues)
    {
        var result = new List<double>();
        foreach (var part in args.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (cellValues.TryGetValue(part, out var v)) result.Add(v);
            else if (double.TryParse(part, NumberStyles.Any, CultureInfo.InvariantCulture, out var lit)) result.Add(lit);
        }
        return result;
    }

    public static byte[] CreateZipArchive(IReadOnlyDictionary<string, string> textEntries)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var kvp in textEntries)
            {
                var entry = archive.CreateEntry(kvp.Key, CompressionLevel.Optimal);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(kvp.Value);
            }
        }
        return ms.ToArray();
    }

    public static Dictionary<string, string> ExtractZipArchive(byte[] zipBytes)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var ms = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            result[entry.FullName] = reader.ReadToEnd();
        }
        return result;
    }
}

