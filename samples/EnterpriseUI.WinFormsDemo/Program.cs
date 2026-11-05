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
