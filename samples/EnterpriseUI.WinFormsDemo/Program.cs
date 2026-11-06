using System;
using System.Windows.Forms;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Length == 2 && args[0] == "--verify-demo")
            return DemoVerification.Run(Path.GetFullPath(args[1]));

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
