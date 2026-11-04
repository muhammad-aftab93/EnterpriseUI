using System;
using System.Drawing;
using System.Windows.Forms;
using EnterpriseUI.Gauges;
using EnterpriseUI.WinForms;
using EnterpriseUI.WinFormsDemo.Controls;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class GaugesDemoView : UserControl
{
    private readonly BaseDemoView _baseView;
    private readonly EnterpriseGaugeControl _radialGauge;
    private readonly EnterpriseGaugeControl _linearGauge;
    private readonly EnterpriseBulletGraphControl _bulletGraph;
    private readonly EnterpriseDigitalGauge _digitalGaugeModel = new();
    private readonly Label _lblDigitalReadout;
    private readonly System.Windows.Forms.Timer _telemetryTimer;
    private readonly TrackBar _slider;

    public GaugesDemoView(DemoItem metadata)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        _baseView = new BaseDemoView { Dock = DockStyle.Fill };
        _baseView.BindDemoMetadata(metadata);
        Controls.Add(_baseView);

        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(8)
        };

        var lblSlider = new Label
        {
            Text = "Manual Gauge Value:",
            Location = new Point(8, 14),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        _slider = new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 74,
            Location = new Point(150, 6),
            Width = 200,
            TickStyle = TickStyle.None
        };

        var btnSimulate = new Button
        {
            Text = "Toggle Telemetry Stream",
            Location = new Point(365, 8),
            Size = new Size(175, 28),
            FlatStyle = FlatStyle.Flat
        };

        _lblDigitalReadout = new Label
        {
            Text = "DIGITAL LED: 074.0%",
            Location = new Point(555, 10),
            AutoSize = true,
            Font = new Font("Consolas", 11f, FontStyle.Bold),
            BackColor = Color.FromArgb(20, 26, 32),
            ForeColor = Color.FromArgb(0, 230, 118),
            Padding = new Padding(8, 4, 8, 4)
        };

        _slider.ValueChanged += (_, _) =>
        {
            SyncAllGauges(_slider.Value);
            _baseView.LogEvent("Gauges.Value", $"{_slider.Value}%");
        };

        btnSimulate.Click += (_, _) =>
        {
            _telemetryTimer.Enabled = !_telemetryTimer.Enabled;
            btnSimulate.BackColor = _telemetryTimer.Enabled ? Color.FromArgb(230, 255, 230) : SystemColors.Control;
            _baseView.LogEvent("Gauges.LiveTelemetry", _telemetryTimer.Enabled.ToString());
        };

        toolbar.Controls.AddRange(new Control[] { lblSlider, _slider, btnSimulate, _lblDigitalReadout });

        _bulletGraph = new EnterpriseBulletGraphControl
        {
            Dock = DockStyle.Bottom,
            Height = 76,
            Title = "RadBulletGraph KPI — Server Cluster Throughput vs SLA Target"
        };

        var split = new System.Windows.Forms.SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 350
        };

        _radialGauge = new EnterpriseGaugeControl { Dock = DockStyle.Fill, IsLinear = false };
        _radialGauge.AriaLabel = "Server CPU Load";

        _linearGauge = new EnterpriseGaugeControl { Dock = DockStyle.Fill, IsLinear = true };
        _linearGauge.AriaLabel = "Memory Allocation";

        split.Panel1.Controls.Add(_radialGauge);
        split.Panel2.Controls.Add(_linearGauge);

        _baseView.DemoContainer.Controls.Add(split);
        _baseView.DemoContainer.Controls.Add(_bulletGraph);
        _baseView.DemoContainer.Controls.Add(toolbar);

        SyncAllGauges(74);

        _telemetryTimer = new System.Windows.Forms.Timer { Interval = 120 };
        _telemetryTimer.Tick += (_, _) =>
        {
            var rnd = new Random();
            var val = Math.Clamp(_radialGauge.Value + rnd.Next(-4, 5), 0, 100);
            _slider.Value = (int)val;
            SyncAllGauges(val);
        };
    }

    private void SyncAllGauges(double val)
    {
        _radialGauge.Value = val;
        _linearGauge.Value = val;
        _bulletGraph.FeaturedMeasure = val;
        _digitalGaugeModel.DisplayText = $"{val:000.0}%";
        _lblDigitalReadout.Text = $"DIGITAL LED: {_digitalGaugeModel.DisplayText}";
        _radialGauge.Invalidate();
        _linearGauge.Invalidate();
        _bulletGraph.Invalidate();
    }
}
