using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;
using EnterpriseUI.WinForms;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Controls;

/// <summary>
/// Live Control Playground, Property Grid, Runtime Designer, Multi-Format Code Generator,
/// Source Code Explainer, and Event Monitor.
/// </summary>
public class WinFormsPropertyInspectorControl : UserControl, IThemeableControl
{
    private readonly PropertyGrid _propertyGrid;
    private readonly ListBox _eventLog;
    private readonly TabControl _tabControl;
    private readonly ComboBox _cmbCodeFormat;
    private readonly RichTextBox _generatedCodeBox;
    private readonly RichTextBox _explainerBox;
    private readonly ComboBox _cmbDock;
    private readonly CheckBox _chkRtl;
    private readonly CheckBox _chkEnabled;
    private readonly NumericUpDown _numPadding;
    private readonly ComboBox _cmbPlaygroundTheme;
    private readonly ComboBox _cmbDataSourcePreset;
    private readonly CheckBox _chkAllowDrop;
    private readonly CheckBox _chkCausesValidation;
    private readonly RichTextBox _stateViewerBox;
    private readonly List<PropertyMutationRecord> _mutations = new();

    private object? _inspectedTarget;
    private DemoItem? _activeDemo;
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private bool _syncingPlayground;

    public string? ThemeOverride { get; set; }
    public event Action<PropertyMutationRecord>? PropertyMutated;
    public event Action? ResetRequested;

    public IReadOnlyList<PropertyMutationRecord> Mutations => _mutations;

    public WinFormsPropertyInspectorControl()
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(6, 4)
        };

        // 1. Live Property Grid Tab
        var tabProps = new TabPage("Property Grid");
        _propertyGrid = new PropertyGrid
        {
            Dock = DockStyle.Fill,
            HelpVisible = true,
            ToolbarVisible = true,
            Font = new Font("Segoe UI", 9f)
        };
        _propertyGrid.PropertyValueChanged += OnPropertyGridValueChanged;
        tabProps.Controls.Add(_propertyGrid);

        // 2. Runtime Designer & Behavior Playground Tab
        var tabPlayground = new TabPage("Playground");
        var playgroundPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), AutoScroll = true };

        var lblHeader = new Label
        {
            Text = "Live Runtime Designer & Layout Playground",
            Location = new Point(12, 10),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };

        var lblDock = new Label { Text = "Dock Mode:", Location = new Point(12, 42), AutoSize = true };
        _cmbDock = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(130, 38),
            Width = 260,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        foreach (var d in Enum.GetValues<DockStyle>()) _cmbDock.Items.Add(d);
        _cmbDock.SelectedItem = DockStyle.Fill;
        _cmbDock.SelectedIndexChanged += (_, _) =>
        {
            if (_syncingPlayground || _inspectedTarget is not Control ctrl || _cmbDock.SelectedItem is not DockStyle ds) return;
            var oldVal = ctrl.Dock.ToString();
            ctrl.Dock = ds;
            RecordMutation(ctrl.GetType().Name, "Dock", oldVal, ds.ToString());
        };

        var lblPad = new Label { Text = "Padding (px):", Location = new Point(12, 76), AutoSize = true };
        _numPadding = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 64,
            Value = 10,
            Location = new Point(130, 74),
            Width = 260,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _numPadding.ValueChanged += (_, _) =>
        {
            if (_syncingPlayground || _inspectedTarget is not Control ctrl) return;
            var oldVal = ctrl.Padding.ToString();
            var p = (int)_numPadding.Value;
            ctrl.Padding = new Padding(p);
            ctrl.PerformLayout();
            ctrl.Invalidate(true);
            RecordMutation(ctrl.GetType().Name, "Padding", oldVal, ctrl.Padding.ToString());
        };

        _chkRtl = new CheckBox
        {
            Text = "Enable Right-To-Left (RTL) Mirroring",
            Location = new Point(12, 112),
            AutoSize = true
        };
        _chkRtl.CheckedChanged += (_, _) =>
        {
            if (_syncingPlayground || _inspectedTarget is not Control ctrl) return;
            var oldVal = ctrl.RightToLeft.ToString();
            ctrl.RightToLeft = _chkRtl.Checked ? RightToLeft.Yes : RightToLeft.No;
            ctrl.Invalidate(true);
            RecordMutation(ctrl.GetType().Name, "RightToLeft", oldVal, ctrl.RightToLeft.ToString());
        };

        _chkEnabled = new CheckBox
        {
            Text = "Control Enabled (Interactive State)",
            Location = new Point(12, 142),
            Checked = true,
            AutoSize = true
        };
        _chkEnabled.CheckedChanged += (_, _) =>
        {
            if (_syncingPlayground || _inspectedTarget is not Control ctrl) return;
            var oldVal = ctrl.Enabled.ToString();
            ctrl.Enabled = _chkEnabled.Checked;
            RecordMutation(ctrl.GetType().Name, "Enabled", oldVal, ctrl.Enabled.ToString());
        };

        var btnResetPlayground = new Button
        {
            Text = "↺ Reset Control & Clear Deltas",
            Location = new Point(12, 174),
            Size = new Size(378, 30),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            FlatStyle = FlatStyle.Flat
        };
        btnResetPlayground.Click += (_, _) =>
        {
            _mutations.Clear();
            ResetRequested?.Invoke();
            RefreshGeneratedCode();
            LogEvent("Playground state reset to baseline.");
        };

        var lblThemeSel = new Label { Text = "Theme Selector:", Location = new Point(12, 218), AutoSize = true, Font = new Font("Segoe UI", 8.75f, FontStyle.Bold) };
        _cmbPlaygroundTheme = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(130, 214),
            Width = 260,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        foreach (var t in ThemeManager.Instance.AvailableThemes) _cmbPlaygroundTheme.Items.Add(t.Name);
        if (_cmbPlaygroundTheme.Items.Count > 0) _cmbPlaygroundTheme.SelectedIndex = 0;
        _cmbPlaygroundTheme.SelectedIndexChanged += (_, _) =>
        {
            if (_syncingPlayground || _cmbPlaygroundTheme.SelectedItem is not string tName) return;
            ThemeManager.Instance.ApplyTheme(tName);
        };

        var lblDataSource = new Label { Text = "Data Source:", Location = new Point(12, 250), AutoSize = true, Font = new Font("Segoe UI", 8.75f, FontStyle.Bold) };
        _cmbDataSourcePreset = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(130, 246),
            Width = 260,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _cmbDataSourcePreset.Items.AddRange(new object[]
        {
            "Default Dataset",
            "High-Volume (100K+)",
            "Filtered Audit Subset",
            "Regional EMEA/APAC"
        });
        _cmbDataSourcePreset.SelectedIndex = 0;
        _cmbDataSourcePreset.SelectedIndexChanged += (_, _) =>
        {
            if (_syncingPlayground) return;
            RecordMutation(_inspectedTarget?.GetType().Name ?? "Control", "DataSourcePreset", "Default Dataset", _cmbDataSourcePreset.SelectedItem?.ToString() ?? "Custom");
        };

        var lblBehaviors = new Label
        {
            Text = "Behavior Configuration Panel",
            Location = new Point(12, 282),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        _chkAllowDrop = new CheckBox { Text = "Allow Drag & Drop Operations", Location = new Point(12, 304), AutoSize = true };
        _chkAllowDrop.CheckedChanged += (_, _) =>
        {
            if (_syncingPlayground || _inspectedTarget is not Control ctrl) return;
            var oldVal = ctrl.AllowDrop.ToString();
            ctrl.AllowDrop = _chkAllowDrop.Checked;
            RecordMutation(ctrl.GetType().Name, "AllowDrop", oldVal, ctrl.AllowDrop.ToString());
        };

        _chkCausesValidation = new CheckBox { Text = "Causes Input Validation", Location = new Point(12, 328), Checked = true, AutoSize = true };
        _chkCausesValidation.CheckedChanged += (_, _) =>
        {
            if (_syncingPlayground || _inspectedTarget is not Control ctrl) return;
            var oldVal = ctrl.CausesValidation.ToString();
            ctrl.CausesValidation = _chkCausesValidation.Checked;
            RecordMutation(ctrl.GetType().Name, "CausesValidation", oldVal, ctrl.CausesValidation.ToString());
        };

        var lblStateViewer = new Label
        {
            Text = "Live Control State Viewer",
            Location = new Point(12, 356),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        _stateViewerBox = new RichTextBox
        {
            Location = new Point(12, 378),
            Size = new Size(378, 150),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            ReadOnly = true,
            Font = new Font("Consolas", 8.25f),
            BackColor = Color.FromArgb(248, 250, 252)
        };

        playgroundPanel.Controls.AddRange(new Control[]
        {
            lblHeader, lblDock, _cmbDock, lblPad, _numPadding, _chkRtl, _chkEnabled, btnResetPlayground,
            lblThemeSel, _cmbPlaygroundTheme, lblDataSource, _cmbDataSourcePreset,
            lblBehaviors, _chkAllowDrop, _chkCausesValidation, lblStateViewer, _stateViewerBox
        });
        tabPlayground.Controls.Add(playgroundPanel);

        // 3. Live Generated Code Tab
        var tabCode = new TabPage("Generated Code");
        var codeToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 64,
            Padding = new Padding(4),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        _cmbCodeFormat = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 175
        };
        _cmbCodeFormat.Items.AddRange(new object[]
        {
            "C# Imperative Code",
            "WinForms Designer Code",
            "Fluent Builder API",
            "JSON Configuration",
            "XML Configuration",
            "Theme Configuration",
            "Control State Snapshot",
            "DI Service Registration"
        });
        _cmbCodeFormat.SelectedIndex = 0;
        _cmbCodeFormat.SelectedIndexChanged += (_, _) => RefreshGeneratedCode();

        var btnCopyCode = new Button { Text = "Copy", Size = new Size(58, 26), FlatStyle = FlatStyle.Flat };
        btnCopyCode.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(_generatedCodeBox.Text))
            {
                Clipboard.SetText(_generatedCodeBox.Text);
                LogEvent("Copied generated code to clipboard.");
            }
        };

        var btnExportCode = new Button { Text = "Export", Size = new Size(62, 26), FlatStyle = FlatStyle.Flat };
        btnExportCode.Click += (_, _) => ExportCurrentCode();

        var btnSaveSample = new Button { Text = "Save Sample", Size = new Size(92, 26), FlatStyle = FlatStyle.Flat };
        btnSaveSample.Click += (_, _) => SaveCustomSample();

        var btnResetCode = new Button { Text = "Reset", Size = new Size(58, 26), FlatStyle = FlatStyle.Flat };
        btnResetCode.Click += (_, _) =>
        {
            _mutations.Clear();
            RefreshGeneratedCode();
        };

        codeToolbar.Controls.AddRange(new Control[] { _cmbCodeFormat, btnCopyCode, btnExportCode, btnSaveSample, btnResetCode });

        _generatedCodeBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Font = new Font("Consolas", 8.75f),
            BackColor = Color.FromArgb(250, 250, 252),
            BorderStyle = BorderStyle.None,
            WordWrap = false
        };
        tabCode.Controls.Add(_generatedCodeBox);
        tabCode.Controls.Add(codeToolbar);

        // 4. Code Explainer Tab
        var tabExplain = new TabPage("Code Explainer");
        _explainerBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Font = new Font("Segoe UI", 9f),
            BackColor = Color.FromArgb(250, 250, 252),
            BorderStyle = BorderStyle.None
        };
        tabExplain.Controls.Add(_explainerBox);

        // 5. Event Monitor Tab
        var tabEvents = new TabPage("Events");
        _eventLog = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 8.5f),
            IntegralHeight = false
        };
        var clearBtn = new Button
        {
            Text = "Clear Events",
            Dock = DockStyle.Bottom,
            Height = 28,
            FlatStyle = FlatStyle.Flat
        };
        clearBtn.Click += (_, _) => _eventLog.Items.Clear();

        tabEvents.Controls.Add(_eventLog);
        tabEvents.Controls.Add(clearBtn);

        _tabControl.TabPages.AddRange(new[] { tabProps, tabPlayground, tabCode, tabExplain, tabEvents });
        Controls.Add(_tabControl);

        ThemeManager.Instance.ThemeChanged += (_, newTheme) =>
        {
            OnThemeChanged(newTheme);
            RecordMutation("ThemeManager", "CurrentTheme", _theme.Name, newTheme.Name);
        };
    }

    private void OnPropertyGridValueChanged(object? s, PropertyValueChangedEventArgs e)
    {
        if (e.ChangedItem == null) return;
        var propName = e.ChangedItem.Label;
        var oldVal = e.OldValue?.ToString() ?? "null";
        var newVal = e.ChangedItem.Value?.ToString() ?? "null";
        var targetName = _inspectedTarget?.GetType().Name ?? "Control";

        if (_inspectedTarget is Control ctrl)
        {
            ctrl.Invalidate(true);
        }

        RecordMutation(targetName, propName, oldVal, newVal);
    }

    public void RecordMutation(string targetTypeName, string propertyName, string oldValue, string newValue)
    {
        var rec = new PropertyMutationRecord(DateTime.Now, targetTypeName, propertyName, oldValue, newValue);
        _mutations.Insert(0, rec);
        if (_mutations.Count > 50) _mutations.RemoveAt(50);

        LogEvent($"{targetTypeName}.{propertyName}: {oldValue} -> {newValue}");
        RefreshGeneratedCode();
        PropertyMutated?.Invoke(rec);
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        _theme = newTheme;
        WinFormsThemeEngine.ApplyToControl(this, newTheme);
    }

    public void InspectObject(object? target, DemoItem? demo = null)
    {
        _inspectedTarget = target;
        if (demo != null) _activeDemo = demo;
        _mutations.Clear();

        _propertyGrid.SelectedObject = target;

        if (target is Control ctrl)
        {
            _syncingPlayground = true;
            _cmbDock.SelectedItem = ctrl.Dock;
            _numPadding.Value = Math.Clamp(ctrl.Padding.Left, 0, 64);
            _chkRtl.Checked = ctrl.RightToLeft == RightToLeft.Yes;
            _chkEnabled.Checked = ctrl.Enabled;
            _syncingPlayground = false;
        }

        RefreshGeneratedCode();
        LogEvent($"Inspector bound to: {target?.GetType().Name ?? "null"}");
    }

    public void RefreshGeneratedCode()
    {
        var format = (CodeOutputFormat)Math.Clamp(_cmbCodeFormat.SelectedIndex, 0, 7);
        _generatedCodeBox.Text = LiveCodeGeneratorEngine.GenerateCode(_inspectedTarget, _activeDemo, format, _mutations);
        _explainerBox.Text = LiveCodeGeneratorEngine.GenerateExplanation(_inspectedTarget, _activeDemo, _mutations);
        _stateViewerBox.Text = LiveCodeGeneratorEngine.GenerateCode(_inspectedTarget, _activeDemo, CodeOutputFormat.ControlStateConfig, _mutations);
    }

    private void ExportCurrentCode()
    {
        var ext = _cmbCodeFormat.SelectedIndex switch
        {
            3 => "json",
            4 => "xml",
            6 => "txt",
            _ => "cs"
        };
        using var sfd = new SaveFileDialog
        {
            Filter = $"Generated File (*.{ext})|*.{ext}|All Files (*.*)|*.*",
            FileName = $"{_activeDemo?.Id ?? "EnterpriseControl"}.Generated.{ext}"
        };
        if (sfd.ShowDialog() == DialogResult.OK)
        {
            File.WriteAllText(sfd.FileName, _generatedCodeBox.Text);
            LogEvent($"Exported generated code to {Path.GetFileName(sfd.FileName)}");
        }
    }

    private void SaveCustomSample()
    {
        var json = LiveCodeGeneratorEngine.GenerateCode(_inspectedTarget, _activeDemo, CodeOutputFormat.JsonConfig, _mutations);
        var path = Path.Combine(Path.GetTempPath(), $"EnterpriseUI_Sample_{_activeDemo?.Id ?? "custom"}.json");
        File.WriteAllText(path, json);
        LogEvent($"Saved sample snapshot: {path}");
        MessageBox.Show($"Saved executable sample configuration to:\n{path}", "Sample Snapshot Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public void LogEvent(string eventDescription)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => LogEvent(eventDescription)));
            return;
        }

        var entry = $"[{DateTime.Now:HH:mm:ss.fff}] {eventDescription}";
        _eventLog.Items.Insert(0, entry);
        if (_eventLog.Items.Count > 100)
        {
            _eventLog.Items.RemoveAt(_eventLog.Items.Count - 1);
        }
    }
}
