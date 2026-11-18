using System;
using System.Collections.Generic;
using System.Linq;
using EnterpriseUI.Core.Mvvm;
using EnterpriseUI.Navigation;
using EnterpriseUI.WinForms;

namespace EnterpriseUI.WinFormsDemo.Controls;

/// <summary>The Demo Center ribbon: one tab per area of the suite with shortcuts to its demos, plus a File button for the system backstage; built on the library ribbon.</summary>
public class EnterpriseRibbonControl : EnterpriseRibbonBarControl
{
    private static readonly Dictionary<string, string> TabGlyphs = new()
    {
        ["Home"] = "\u2302", ["Data & Grids"] = "\u25A6", ["Visualizations"] = "\u25D4", ["Navigation & Layout"] = "\u2630", ["Editors & Docs"] = "\u270E",
        ["Component Catalog"] = "\u25A4", ["Intelligence"] = "\u2726", ["Theming"] = "\u25D0", ["QA & Lab"] = "\u2714", ["Developer Tools"] = "\u2699"
    };

    public event Action? BackstageClicked;
    public event Action<string>? TabChanged;

    public EnterpriseRibbonControl()
    {
        foreach (var title in TabGlyphs.Keys.Where(title => title != "Developer Tools")) AddTab(title);
        QuickAccessItems.Add(new RibbonItem { Text = "File", Icon = "\u2630", ToolTip = "System backstage and diagnostics", Command = new RelayCommand(() => BackstageClicked?.Invoke()) });
        TabSelected += (_, e) => TabChanged?.Invoke(e.Tab.Title);
        RefreshRibbon();
    }

    private RibbonTab AddTab(string title)
    {
        var tab = new RibbonTab { Title = title };
        tab.Groups.Add(new RibbonGroup { Title = title });
        Tabs.Add(tab);
        return tab;
    }

    /// <summary>Adds (or rebinds) a ribbon button on a tab; unknown tabs are created.</summary>
    public void RegisterAction(string tab, string text, Action onClick)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tab);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(onClick);
        var target = Tabs.FirstOrDefault(candidate => candidate.Title == tab) ?? AddTab(tab);
        var group = target.Groups[0];
        var command = new RelayCommand(onClick);
        var existing = group.Items.FirstOrDefault(item => item.Text == text);
        if (existing != null) existing.Command = command;
        else group.Items.Add(new RibbonItem { Text = text, Icon = TabGlyphs.GetValueOrDefault(tab, "\u25B8"), Command = command, ToolTip = text.Replace('\n', ' ') });
        RefreshRibbon();
    }
}
