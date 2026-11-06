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
public class EnterprisePropertyInspectorControl : UserControl, IThemeableControl
{

    private static readonly object[] CachedValues2 = new object[]
        {
            "C# Imperative Code",
            "WinForms Designer Code",
            "Fluent Builder API",
            "JSON Configuration",
            "XML Configuration",
            "Theme Configuration",
            "Control State Snapshot",
            "DI Service Registration"
        };

    private static readonly object[] CachedValues1 = new object[]
        {
            "Default Dataset",
            "High-Volume (100K+)",
            "Filtered Audit Subset",
            "Regional EMEA/APAC"
        };
    private readonly PropertyGrid _propertyGrid=new PropertyGrid();
    private readonly ListBox _eventLog=new ListBox();
    private readonly TabControl _tabControl=new TabControl();
    private readonly ComboBox _cmbCodeFormat=new ComboBox();
    private readonly RichTextBox _generatedCodeBox=new RichTextBox();
    private readonly RichTextBox _explainerBox=new RichTextBox();
    private readonly ComboBox _cmbDock=new ComboBox();
    private readonly CheckBox _chkRtl=new CheckBox();
    private readonly CheckBox _chkEnabled=new CheckBox();
    private readonly NumericUpDown _numPadding=new NumericUpDown();
    private readonly ComboBox _cmbPlaygroundTheme=new ComboBox();
    private readonly ComboBox _cmbDataSourcePreset=new ComboBox();
    private readonly CheckBox _chkAllowDrop=new CheckBox();
    private readonly CheckBox _chkCausesValidation=new CheckBox();
    private readonly RichTextBox _stateViewerBox=new RichTextBox();
    private readonly List<PropertyMutationRecord> _mutations = new();

    private object? _inspectedTarget;
    private DemoItem? _activeDemo;
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private bool _syncingPlayground;

    public string? ThemeOverride { get; set; }
    public event Action<PropertyMutationRecord>? PropertyMutated;
    public event Action? ResetRequested;

    public IReadOnlyList<PropertyMutationRecord> Mutations => _mutations;

    public EnterprisePropertyInspectorControl()
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        _tabControl.            Dock = DockStyle.Fill;

        _tabControl.            Padding = new Point(6, 4)
;

        // 1. Live Property Grid Tab
        var tabProps = new TabPage("Property Grid");
        _propertyGrid.            Dock = DockStyle.Fill;
        _propertyGrid.            HelpVisible = true;
        _propertyGrid.            ToolbarVisible = true;
        _propertyGrid.            Font = new Font("Segoe UI", 9f)
;
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
        _cmbDock.            DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbDock.            Location = new Point(130, 38);
        _cmbDock.            Width = 260;
        _cmbDock.            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
;
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
        _numPadding.            Minimum = 0;
        _numPadding.            Maximum = 64;
        _numPadding.            Value = 10;
        _numPadding.            Location = new Point(130, 74);
        _numPadding.            Width = 260;
        _numPadding.            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
;
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

        _chkRtl.            Text = "Enable Right-To-Left (RTL) Mirroring";

        _chkRtl.            Location = new Point(12, 112);

        _chkRtl.            AutoSize = true
;
        _chkRtl.CheckedChanged += (_, _) =>
        {
            if (_syncingPlayground || _inspectedTarget is not Control ctrl) return;
            var oldVal = ctrl.RightToLeft.ToString();
            ctrl.RightToLeft = _chkRtl.Checked ? RightToLeft.Yes : RightToLeft.No;
            ctrl.Invalidate(true);
            RecordMutation(ctrl.GetType().Name, "RightToLeft", oldVal, ctrl.RightToLeft.ToString());
        };

        _chkEnabled.            Text = "Control Enabled (Interactive State)";

        _chkEnabled.            Location = new Point(12, 142);

        _chkEnabled.            Checked = true;

        _chkEnabled.            AutoSize = true
;
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
        _cmbPlaygroundTheme.            DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbPlaygroundTheme.            Location = new Point(130, 214);
        _cmbPlaygroundTheme.            Width = 260;
        _cmbPlaygroundTheme.            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
;
        foreach (var t in ThemeManager.Instance.AvailableThemes) _cmbPlaygroundTheme.Items.Add(t.Name);
        if (_cmbPlaygroundTheme.Items.Count > 0) _cmbPlaygroundTheme.SelectedIndex = 0;
        _cmbPlaygroundTheme.SelectedIndexChanged += (_, _) =>
        {
            if (_syncingPlayground || _cmbPlaygroundTheme.SelectedItem is not string tName) return;
            ThemeManager.Instance.ApplyTheme(tName);
        };

        var lblDataSource = new Label { Text = "Data Source:", Location = new Point(12, 250), AutoSize = true, Font = new Font("Segoe UI", 8.75f, FontStyle.Bold) };
        _cmbDataSourcePreset.            DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbDataSourcePreset.            Location = new Point(130, 246);
        _cmbDataSourcePreset.            Width = 260;
        _cmbDataSourcePreset.            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
;
        _cmbDataSourcePreset.Items.AddRange(CachedValues1);
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

        _chkAllowDrop.Text = "Allow Drag & Drop Operations";

        _chkAllowDrop.Location = new Point(12, 304);

        _chkAllowDrop.AutoSize = true ;
        _chkAllowDrop.CheckedChanged += (_, _) =>
        {
            if (_syncingPlayground || _inspectedTarget is not Control ctrl) return;
            var oldVal = ctrl.AllowDrop.ToString();
            ctrl.AllowDrop = _chkAllowDrop.Checked;
            RecordMutation(ctrl.GetType().Name, "AllowDrop", oldVal, ctrl.AllowDrop.ToString());
        };

        _chkCausesValidation.Text = "Causes Input Validation";

        _chkCausesValidation.Location = new Point(12, 328);

        _chkCausesValidation.Checked = true;

        _chkCausesValidation.AutoSize = true ;
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

        _stateViewerBox.            Location = new Point(12, 378);

        _stateViewerBox.            Size = new Size(378, 150);

        _stateViewerBox.            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

        _stateViewerBox.            ReadOnly = true;

        _stateViewerBox.            Font = new Font("Consolas", 8.25f);

        _stateViewerBox.            BackColor = Color.FromArgb(248, 250, 252)
;

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

        _cmbCodeFormat.            DropDownStyle = ComboBoxStyle.DropDownList;

        _cmbCodeFormat.            Width = 175
;
        _cmbCodeFormat.Items.AddRange(CachedValues2);
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

        _generatedCodeBox.            Dock = DockStyle.Fill;

        _generatedCodeBox.            ReadOnly = true;

        _generatedCodeBox.            Font = new Font("Consolas", 8.75f);

        _generatedCodeBox.            BackColor = Color.FromArgb(250, 250, 252);

        _generatedCodeBox.            BorderStyle = BorderStyle.None;

        _generatedCodeBox.            WordWrap = false
;
        tabCode.Controls.Add(_generatedCodeBox);
        tabCode.Controls.Add(codeToolbar);

        // 4. Code Explainer Tab
        var tabExplain = new TabPage("Code Explainer");
        _explainerBox.            Dock = DockStyle.Fill;
        _explainerBox.            ReadOnly = true;
        _explainerBox.            Font = new Font("Segoe UI", 9f);
        _explainerBox.            BackColor = Color.FromArgb(250, 250, 252);
        _explainerBox.            BorderStyle = BorderStyle.None
;
        tabExplain.Controls.Add(_explainerBox);

        // 5. Event Monitor Tab
        var tabEvents = new TabPage("Events");
        _eventLog.            Dock = DockStyle.Fill;
        _eventLog.            Font = new Font("Consolas", 8.5f);
        _eventLog.            IntegralHeight = false
;
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

        RecordMutation(targetName, propName ?? "Property", oldVal, newVal);
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
        EnterpriseThemeEngine.ApplyToControl(this, newTheme);
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
