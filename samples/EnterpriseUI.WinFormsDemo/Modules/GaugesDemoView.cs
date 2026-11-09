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
    private readonly BaseDemoView _baseView=new BaseDemoView();
    private readonly EnterpriseGaugeControl _radialGauge=new EnterpriseGaugeControl();
    private readonly EnterpriseGaugeControl _linearGauge=new EnterpriseGaugeControl();
    private readonly EnterpriseBulletGraphControl _bulletGraph=new EnterpriseBulletGraphControl();
    private readonly EnterpriseDigitalGauge _digitalGaugeModel = new();
    private readonly Label _lblDigitalReadout=new Label();
    private readonly System.Windows.Forms.Timer _telemetryTimer=new System.Windows.Forms.Timer();
    private readonly TrackBar _slider=new TrackBar();

    public GaugesDemoView(DemoItem metadata)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        _baseView.Dock = DockStyle.Fill ;
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

        _slider.            Minimum = 0;

        _slider.            Maximum = 100;

        _slider.            Value = 74;

        _slider.            Location = new Point(150, 6);

        _slider.            Width = 200;

        _slider.            TickStyle = TickStyle.None
;

        var btnSimulate = new Button
        {
            Text = "Toggle Telemetry Stream",
            Location = new Point(365, 8),
            Size = new Size(175, 28),
            FlatStyle = FlatStyle.Flat
        };

        _lblDigitalReadout.            Text = "DIGITAL LED: 074.0%";

        _lblDigitalReadout.            Location = new Point(555, 10);

        _lblDigitalReadout.            AutoSize = true;

        _lblDigitalReadout.            Font = new Font("Consolas", 11f, FontStyle.Bold);

        _lblDigitalReadout.            BackColor = Color.FromArgb(20, 26, 32);

        _lblDigitalReadout.            ForeColor = Color.FromArgb(0, 230, 118);

        _lblDigitalReadout.            Padding = new Padding(8, 4, 8, 4)
;

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

        _bulletGraph.            Dock = DockStyle.Bottom;

        _bulletGraph.            Height = 76;

        _bulletGraph.            Title = "EnterpriseBulletGraph KPI — Server Cluster Throughput vs SLA Target"
;

        var split = new System.Windows.Forms.SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 350
        };

        _radialGauge.Dock = DockStyle.Fill;

        _radialGauge.IsLinear = false ;
        _radialGauge.AriaLabel = "Server CPU Load";

        _linearGauge.Dock = DockStyle.Fill;

        _linearGauge.IsLinear = true ;
        _linearGauge.AriaLabel = "Memory Allocation";

        split.Panel1.Controls.Add(_radialGauge);
        split.Panel2.Controls.Add(_linearGauge);

        _baseView.DemoContainer.Controls.Add(split);
        _baseView.DemoContainer.Controls.Add(_bulletGraph);
        _baseView.DemoContainer.Controls.Add(toolbar);

        SyncAllGauges(74);

        _telemetryTimer.Interval = 120 ;
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
