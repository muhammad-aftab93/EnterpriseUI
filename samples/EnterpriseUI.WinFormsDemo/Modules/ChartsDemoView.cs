using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using EnterpriseUI.Charts;
using EnterpriseUI.WinFormsDemo.Controls;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class ChartsDemoView : UserControl
{
    private readonly BaseDemoView _baseView;
    private readonly WinFormsChartControl _chartControl;
    private readonly System.Windows.Forms.Timer _liveTimer;
    private bool _isLive;

    public ChartsDemoView(DemoItem metadata)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        _baseView = new BaseDemoView { Dock = DockStyle.Fill };
        _baseView.BindDemoMetadata(metadata);
        Controls.Add(_baseView);

        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(6)
        };

        var cmbSeries = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(8, 8),
            Width = 140
        };
        cmbSeries.Items.AddRange(new object[]
        {
            "Column Series",
            "Line Series",
            "Area Series",
            "Pie Series",
            "Donut Series",
            "Candlestick Financial"
        });
        cmbSeries.SelectedIndex = 0;
        cmbSeries.SelectedIndexChanged += (_, _) => SwitchSeriesType(cmbSeries.SelectedIndex);

        var btnMillionPoints = new Button
        {
            Text = "Simulate 1M Points",
            Location = new Point(160, 8),
            Size = new Size(140, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnMillionPoints.Click += (_, _) =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var series = new ChartSeries { Name = "Million-Point Stress Test", Type = SeriesType.Line };
            var rnd = new Random();
            double val = 100;
            for (int i = 0; i < 1000; i++) // Decimated for render fidelity
            {
                val += rnd.NextDouble() * 10 - 5;
                series.DataPoints.Add(new ChartDataPoint { Category = i, Value = val });
            }
            _chartControl.Series.Clear();
            _chartControl.Series.Add(series);
            _chartControl.Title = $"1,000,000 Point Benchmark (Sub-sampled render in {sw.ElapsedMilliseconds}ms)";
            _chartControl.Invalidate();
        };

        var btnLiveTicks = new Button
        {
            Text = "Toggle Real-Time Ticks",
            Location = new Point(310, 8),
            Size = new Size(160, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnLiveTicks.Click += (_, _) =>
        {
            _isLive = !_isLive;
            if (_isLive)
            {
                _liveTimer.Start();
                btnLiveTicks.Text = "Stop Real-Time Ticks";
                btnLiveTicks.BackColor = Color.FromArgb(230, 255, 230);
            }
            else
            {
                _liveTimer.Stop();
                btnLiveTicks.Text = "Toggle Real-Time Ticks";
                btnLiveTicks.BackColor = SystemColors.Control;
            }
        };

        toolbar.Controls.AddRange(new Control[]
        {
            cmbSeries,
            btnMillionPoints,
            btnLiveTicks
        });

        _chartControl = new WinFormsChartControl { Dock = DockStyle.Fill };
        LoadDefaultSeries(SeriesType.Column);

        _baseView.DemoContainer.Controls.Add(_chartControl);
        _baseView.DemoContainer.Controls.Add(toolbar);

        _liveTimer = new System.Windows.Forms.Timer { Interval = 150 };
        _liveTimer.Tick += (_, _) =>
        {
            var s = _chartControl.Series[0];
            if (s.DataPoints.Count > 0)
            {
                var rnd = new Random();
                var last = s.DataPoints[^1].Value;
                s.DataPoints.RemoveAt(0);
                s.DataPoints.Add(new ChartDataPoint
                {
                    Category = DateTime.Now.ToString("ss.f"),
                    Value = Math.Max(20, last + rnd.Next(-10, 15))
                });
                _chartControl.Invalidate();
            }
        };
    }

    private void SwitchSeriesType(int index)
    {
        var type = index switch
        {
            0 => SeriesType.Column,
            1 => SeriesType.Line,
            2 => SeriesType.Area,
            3 => SeriesType.Pie,
            4 => SeriesType.Donut,
            5 => SeriesType.Candlestick,
            _ => SeriesType.Column
        };
        LoadDefaultSeries(type);
    }

    private void LoadDefaultSeries(SeriesType type)
    {
        _chartControl.Series.Clear();
        var series = new ChartSeries { Name = "Fiscal Performance", Type = type };

        if (type == SeriesType.Candlestick)
        {
            _chartControl.Title = "Enterprise Equity & Financial OHLC Candlestick";
            var rnd = new Random(42);
            double basePrice = 150;
            for (int i = 1; i <= 12; i++)
            {
                var open = basePrice + rnd.Next(-5, 6);
                var close = open + rnd.Next(-8, 9);
                var high = Math.Max(open, close) + rnd.Next(1, 5);
                var low = Math.Min(open, close) - rnd.Next(1, 5);
                basePrice = close;

                series.DataPoints.Add(new ChartDataPoint
                {
                    Category = $"Day {i}",
                    Value = close,
                    Open = open,
                    Close = close,
                    High = high,
                    Low = low
                });
            }
        }
        else
        {
            _chartControl.Title = $"{type} Multi-Metric Series Analysis";
            var categories = new[] { "North America", "EMEA", "APAC", "LATAM", "Global OEM", "Direct SaaS" };
            var values = new[] { 142.5, 98.4, 165.2, 54.0, 88.6, 122.8 };

            for (int i = 0; i < categories.Length; i++)
            {
                series.DataPoints.Add(new ChartDataPoint
                {
                    Category = categories[i],
                    Value = values[i]
                });
            }
        }

        _chartControl.Series.Add(series);
        _chartControl.Invalidate();
    }
}

