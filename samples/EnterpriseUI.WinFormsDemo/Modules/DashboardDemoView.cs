using System;
using System.Drawing;
using System.Windows.Forms;
using EnterpriseUI.Charts;
using EnterpriseUI.WinFormsDemo.Controls;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class DashboardDemoView : UserControl
{
    private readonly BaseDemoView _baseView;
    private readonly EnterpriseChartControl _chart;

    public DashboardDemoView(DemoItem metadata)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        _baseView = new BaseDemoView { Dock = DockStyle.Fill };
        _baseView.BindDemoMetadata(metadata);
        Controls.Add(_baseView);

        var mainPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        // 1. KPI Cards Row
        var kpiRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 110,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(4)
        };
        kpiRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        kpiRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        kpiRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        kpiRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

        kpiRow.Controls.Add(CreateKpiCard("TOTAL ARR REVENUE", "$48.6M", "+18.4% YoY", Color.FromArgb(16, 124, 65)), 0, 0);
        kpiRow.Controls.Add(CreateKpiCard("ACTIVE ENTERPRISE SEATS", "124,800", "+9.2% MoM", Color.FromArgb(0, 120, 215)), 1, 0);
        kpiRow.Controls.Add(CreateKpiCard("AVG RENDER LATENCY", "3.8 ms", "60+ FPS GDI+", Color.FromArgb(136, 23, 152)), 2, 0);
        kpiRow.Controls.Add(CreateKpiCard("NET PROMOTER SCORE", "79 NPS", "Industry Leader", Color.FromArgb(255, 140, 0)), 3, 0);

        // 2. EnterpriseChart Component
        _chart = new EnterpriseChartControl { Dock = DockStyle.Fill };
        _chart.Title = "Global Revenue Trend & Forecast";
        var s = new ChartSeries { Name = "Revenue", Type = SeriesType.Area };
        s.DataPoints.Add(new ChartDataPoint { Category = "Jan", Value = 320 });
        s.DataPoints.Add(new ChartDataPoint { Category = "Feb", Value = 365 });
        s.DataPoints.Add(new ChartDataPoint { Category = "Mar", Value = 410 });
        s.DataPoints.Add(new ChartDataPoint { Category = "Apr", Value = 390 });
        s.DataPoints.Add(new ChartDataPoint { Category = "May", Value = 460 });
        s.DataPoints.Add(new ChartDataPoint { Category = "Jun", Value = 520 });
        _chart.Series.Add(s);

        mainPanel.Controls.Add(_chart);
        mainPanel.Controls.Add(kpiRow);

        _baseView.DemoContainer.Controls.Add(mainPanel);
    }

    private static Panel CreateKpiCard(string title, string value, string change, Color accent)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(6),
            BackColor = Color.FromArgb(250, 252, 255),
            BorderStyle = BorderStyle.FixedSingle
        };

        var bar = new Panel { Dock = DockStyle.Top, Height = 4, BackColor = accent };
        var lblTitle = new Label { Text = title, Location = new Point(12, 12), AutoSize = true, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = Color.FromArgb(100, 100, 100) };
        var lblVal = new Label { Text = value, Location = new Point(10, 32), AutoSize = true, Font = new Font("Segoe UI", 16f, FontStyle.Bold), ForeColor = Color.FromArgb(30, 30, 30) };
        var lblChange = new Label { Text = change, Location = new Point(12, 68), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = accent };

        card.Controls.AddRange(new Control[] { bar, lblTitle, lblVal, lblChange });
        return card;
    }
}

