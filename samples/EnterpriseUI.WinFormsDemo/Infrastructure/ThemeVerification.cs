using System.Diagnostics;
using System.Reflection;
using System.Text;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.ThemeDesigner;
using EnterpriseUI.Theming;
using EnterpriseUI.WinFormsDemo.Modules;

namespace EnterpriseUI.WinFormsDemo.Infrastructure;

internal static class ThemeVerification
{
    internal static async Task RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var log = new StringBuilder();
        var themes = new ThemeManager().AvailableThemes.ToArray();
        var designer = new ThemeDesignerEngine();
        var generated = new StringBuilder("using EnterpriseUI.Theming;\nusing EnterpriseUI.Abstractions.Theming;\n");
        for (var index = 0; index < themes.Length; index++)
        {
            var theme = themes[index];
            designer.LoadTheme(ThemeBuilder.Create(theme.Name).BasedOn(theme).WithToken("brand-test", "#123456").Build());
            var export = designer.Export();
            var restored = ThemeDocument.FromJson(export.DocumentJson).Build();
            foreach (var token in designer.ActiveTheme.DesignTokens)
                if (restored.GetToken(token.Key) != token.Value) throw new InvalidOperationException($"JSON token mismatch: {theme.Name}/{token.Key}");
            generated.AppendLine($"public static class ThemeFactory{index} {{ public static Theme Create() {{ {export.CSharpBuilderCode} return myTheme; }} }}");
            log.AppendLine($"PASS JSON: {theme.Name}");
        }
        var compilation = Path.Combine(directory, "generated-source");
        Directory.CreateDirectory(compilation);
        var project = new System.Xml.Linq.XDocument(new System.Xml.Linq.XElement("Project", new System.Xml.Linq.XAttribute("Sdk", "Microsoft.NET.Sdk"),
            new System.Xml.Linq.XElement("PropertyGroup", new System.Xml.Linq.XElement("TargetFramework", "net8.0")),
            new System.Xml.Linq.XElement("ItemGroup",
                new System.Xml.Linq.XElement("Reference", new System.Xml.Linq.XAttribute("Include", "EnterpriseUI.Theming"), new System.Xml.Linq.XElement("HintPath", typeof(Theme).Assembly.Location)),
                new System.Xml.Linq.XElement("Reference", new System.Xml.Linq.XAttribute("Include", "EnterpriseUI.Abstractions"), new System.Xml.Linq.XElement("HintPath", typeof(ITheme).Assembly.Location)))));
        var projectPath = Path.Combine(compilation, "GeneratedThemes.csproj");
        project.Save(projectPath);
        await File.WriteAllTextAsync(Path.Combine(compilation, "GeneratedThemes.cs"), generated.ToString());
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("build"); start.ArgumentList.Add(projectPath); start.ArgumentList.Add("--nologo"); start.ArgumentList.Add("--verbosity"); start.ArgumentList.Add("quiet");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not launch generated-source compiler.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await stdout + await stderr;
        if (process.ExitCode != 0) throw new InvalidOperationException(output);
        var assembly = Assembly.LoadFrom(Path.Combine(compilation, "bin", "Debug", "net8.0", "GeneratedThemes.dll"));
        for (var index = 0; index < themes.Length; index++)
        {
            var restored = (Theme)assembly.GetType($"ThemeFactory{index}")!.GetMethod("Create")!.Invoke(null, null)!;
            foreach (var token in themes[index].DesignTokens)
                if (restored.GetToken(token.Key) != token.Value) throw new InvalidOperationException($"Generated token mismatch: {themes[index].Name}/{token.Key}");
            if (restored.GetToken("brand-test") != "#123456") throw new InvalidOperationException("Generated source omitted custom tokens.");
            log.AppendLine($"PASS C#: {themes[index].Name}");
        }
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var form = new Form { ClientSize = new Size(1500, 850), StartPosition = FormStartPosition.CenterScreen };
            using var playground = new ThemePlaygroundView(new DemoItem()) { Dock = DockStyle.Fill };
            form.Controls.Add(playground);
            form.Shown += async (_, _) =>
            {
                try { await playground.VerifyAsync(directory); completion.SetResult(); }
                catch (Exception exception) { completion.SetException(exception); }
                finally { form.Close(); }
            };
            Application.Run(form);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task;
        log.AppendLine("PASS playground: live tokens, density, invalid-edit rollback and screenshots.");
        await File.WriteAllTextAsync(Path.Combine(directory, "verification.txt"), log.ToString());
    }
}