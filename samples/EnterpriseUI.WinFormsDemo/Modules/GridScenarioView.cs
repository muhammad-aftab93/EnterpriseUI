using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EnterpriseUI.WinForms;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

/// <summary>Description and builder of one DataGrid workshop page.</summary>
public sealed class GridScenario
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string[] Features { get; init; }
    public required string Documentation { get; init; }
    public required Func<ScenarioHost, Control> Build { get; init; }
    public string Architecture { get; init; } = string.Empty;
    public string Performance { get; init; } = string.Empty;
    public string Accessibility { get; init; } = string.Empty;
    public string Troubleshooting { get; init; } = string.Empty;
    public string Api { get; init; } = string.Empty;
    public string BestPractices { get; init; } = string.Empty;
    public string Related { get; init; } = string.Empty;
    public string Usage { get; init; } = string.Empty;
}

/// <summary>Lets a scenario add toolbar controls, publish metrics and register the grid that feeds the generated code.</summary>
public sealed class ScenarioHost
{
    private readonly GridScenarioView _view;

    internal ScenarioHost(GridScenarioView view) => _view = view;

    public void Button(string text, Action action)
    {
        var button = NewButton(text);
        button.Click += (_, _) => Run(action);
        _view.Toolbar.Controls.Add(button);
    }

    public void AsyncButton(string text, Func<Task> action)
    {
        var button = NewButton(text);
        button.Click += async (_, _) =>
        {
            button.Enabled = false;
            try
            {
                await action();
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.IO.IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                Metric($"{text}: {ex.Message}");
            }
            finally
            {
                button.Enabled = true;
            }
        };
        _view.Toolbar.Controls.Add(button);
    }

    /// <summary>A two-state button; the check mark in front of the text shows the current state.</summary>
    public Button Toggle(string text, bool initial, Action<bool> changed)
    {
        var state = initial;
        var button = NewButton(Prefix(text, state));
        button.AccessibleName = text;
        button.Click += (_, _) =>
        {
            state = !state;
            button.Text = Prefix(text, state);
            Run(() => changed(state));
        };
        _view.Toolbar.Controls.Add(button);
        return button;
    }

    private static string Prefix(string text, bool on) => (on ? "\u2713 " : "\u2610 ") + text;

    public ComboBox Combo(string label, IEnumerable<string> items, string selected, Action<string> changed)
    {
        _view.Toolbar.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(8, 6, 2, 0) });
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140, Margin = new Padding(2), AccessibleName = label.TrimEnd(':') };
        combo.Items.AddRange(items.Cast<object>().ToArray());
        combo.SelectedItem = selected;
        combo.SelectedIndexChanged += (_, _) => Run(() => changed(combo.SelectedItem?.ToString() ?? selected));
        _view.Toolbar.Controls.Add(combo);
        return combo;
    }

    public void Separator() => _view.Toolbar.Controls.Add(new Label { Text = "|", AutoSize = true, Padding = new Padding(4, 6, 4, 0), Enabled = false });

    public void Metric(string text)
    {
        if (_view.IsDisposed) return;
        if (_view.InvokeRequired)
        {
            try { _view.BeginInvoke(new Action(() => Metric(text))); } catch (InvalidOperationException) { }
            return;
        }
        _view.MetricLabel.Text = text;
    }

    public void Log(string name, string details) => _view.Base.LogEvent(name, details);

    /// <summary>Registers the grid whose live state feeds the generated code and keeps that code current.</summary>
    public EnterpriseDataGrid<TItem> Attach<TItem>(EnterpriseDataGrid<TItem> grid)
    {
        _view.Base.CodeTarget = grid;
        grid.QueryChanged += (_, _) => _view.ScheduleCodeRefresh();
        grid.PageChanged += (_, _) => _view.ScheduleCodeRefresh();
        grid.DataWindowChanged += (_, _) => _view.ScheduleCodeRefresh();
        grid.Engine.PropertyChanged += (_, _) => _view.ScheduleCodeRefresh();
        return grid;
    }

    public void RefreshCode() => _view.ScheduleCodeRefresh();

    private static Button NewButton(string text) => new() { Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat, Padding = new Padding(4, 1, 4, 1), Margin = new Padding(2) };

    private void Run(Action action)
    {
        try
        {
            action();
            _view.ScheduleCodeRefresh();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NotSupportedException or System.IO.IOException)
        {
            Metric(ex.Message);
        }
    }
}

public sealed class GridScenarioView : UserControl
{
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 350 };

    public GridScenarioView(DemoItem demo, GridScenario scenario)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        Base = new BaseDemoView { Dock = DockStyle.Fill };
        Base.BindDemoMetadata(demo);
        Controls.Add(Base);

        Toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new Padding(4) };
        MetricLabel = new Label { Dock = DockStyle.Bottom, Height = 24, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Padding = new Padding(6, 0, 0, 0), AccessibleName = "Scenario status" };

        var host = new ScenarioHost(this);
        var root = scenario.Build(host);
        root.Dock = DockStyle.Fill;
        Base.DemoContainer.Controls.Add(root);
        Base.DemoContainer.Controls.Add(Toolbar);
        Base.DemoContainer.Controls.Add(MetricLabel);

        _refreshTimer.Tick += (_, _) =>
        {
            _refreshTimer.Stop();
            Base.RefreshLiveCode();
        };
        Base.RefreshLiveCode();
    }

    public BaseDemoView Base { get; }
    public FlowLayoutPanel Toolbar { get; }
    public Label MetricLabel { get; }

    public void ScheduleCodeRefresh()
    {
        if (IsDisposed) return;
        _refreshTimer.Stop();
        _refreshTimer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _refreshTimer.Dispose();
        base.Dispose(disposing);
    }
}
