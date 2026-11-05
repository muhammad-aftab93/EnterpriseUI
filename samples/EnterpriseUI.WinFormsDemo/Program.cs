using System;
using System.Windows.Forms;
using EnterpriseUI.WinFormsDemo.Infrastructure;
using EnterpriseUI.CodeGenerators.Parity;
using EnterpriseUI.Theming;
using EnterpriseUI.Grids;
using System.Reflection;

namespace EnterpriseUI.WinFormsDemo;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Length == 2 && args[0] == "--verify-themes")
        {
            try
            {
                ThemeVerification.RunAsync(Path.GetFullPath(args[1])).GetAwaiter().GetResult();
                return 0;
            }
            catch (Exception exception)
            {
                Directory.CreateDirectory(Path.GetFullPath(args[1]));
                File.WriteAllText(Path.Combine(Path.GetFullPath(args[1]), "failure.txt"), exception.ToString());
                return 1;
            }
        }

        if (args.Length == 3 && args[0] == "--generate-parity-audit")
        {
            var types = Directory.GetFiles(AppContext.BaseDirectory, "EnterpriseUI.*.dll")
                .Select(path => Assembly.LoadFrom(path)).SelectMany(assembly => assembly.GetExportedTypes());
            ParityAudit.Generate(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), ParityAudit.InspectTypes(types),
                ThemeManager.Instance.AvailableThemes.Select(theme => new { theme.Name, theme.Type, theme.Scheme, Tokens = theme.DesignTokens }).ToArray(),
                GridFeatureCatalog.All);
            return 0;
        }

        // Tooling mode: regenerate docs/Grid from the workshop pages and the feature catalog.
        if (args.Length == 2 && args[0] == "--generate-grid-docs")
        {
            var count = GridDocGenerator.GenerateAll(args[1]);
            Console.WriteLine($"Generated {count} DataGrid documentation pages in {args[1]}");
            return 0;
        }

        Application.Run(new MainForm());
        return 0;
    }
}
