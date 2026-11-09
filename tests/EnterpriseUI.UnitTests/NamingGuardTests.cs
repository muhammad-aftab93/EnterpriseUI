using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace EnterpriseUI.Tests.Unit;

/// <summary>Keeps third-party product names and product-style prefixes out of the code, the samples and the documentation.</summary>
public class NamingGuardTests
{
    private static readonly string[] Extensions = { ".cs", ".md", ".json", ".csproj", ".props", ".slnx", ".txt", ".xml", ".yml", ".yaml", ".resx" };
    private static readonly string[] SkippedFolders = { "bin", "obj", ".git", "artifacts", "node_modules", ".vs" };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EnterpriseUI.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("EnterpriseUI.slnx was not found above the test directory.");
    }

    private static IEnumerable<string> SourceFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Where(f => !f.Substring(root.Length).Split(Path.DirectorySeparatorChar).Any(part => SkippedFolders.Contains(part, StringComparer.OrdinalIgnoreCase)))
            .Where(f => !f.EndsWith("NamingGuardTests.cs", StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void NoThirdPartyVendorNameAppearsInTheRepository()
    {
        var root = FindRepositoryRoot();
        var vendor = new Regex("tele" + "rik", RegexOptions.IgnoreCase);
        var hits = SourceFiles(root).Where(f => vendor.IsMatch(File.ReadAllText(f))).Select(f => f.Substring(root.Length + 1)).ToList();
        Assert.True(hits.Count == 0, "Vendor name found in: " + string.Join(", ", hits));
    }

    [Fact]
    public void NoProductStylePrefixIsUsedInNames()
    {
        var root = FindRepositoryRoot();
        var prefix = new Regex(@"\bRa" + @"d[A-Z]");
        var hits = SourceFiles(root)
            .SelectMany(f => File.ReadLines(f).Select((line, index) => (File: f, Line: index + 1, Text: line)))
            .Where(x => prefix.IsMatch(x.Text))
            .Select(x => $"{x.File.Substring(root.Length + 1)}:{x.Line}")
            .Take(10)
            .ToList();
        Assert.True(hits.Count == 0, "Product-style prefix found at: " + string.Join(", ", hits));
    }

    [Fact]
    public void NoVendorProjectOrPackageIsReferenced()
    {
        var root = FindRepositoryRoot();
        var vendor = new Regex("tele" + "rik", RegexOptions.IgnoreCase);
        var projectFiles = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".props", StringComparison.OrdinalIgnoreCase) || f.EndsWith("packages.lock.json", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Substring(root.Length).Split(Path.DirectorySeparatorChar).Any(part => SkippedFolders.Contains(part, StringComparer.OrdinalIgnoreCase)));
        Assert.DoesNotContain(projectFiles, f => vendor.IsMatch(File.ReadAllText(f)));
    }
}
