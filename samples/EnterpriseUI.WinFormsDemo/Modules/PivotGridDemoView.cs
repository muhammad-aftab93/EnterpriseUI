using System;
using System.Drawing;
using System.Windows.Forms;
using EnterpriseUI.WinFormsDemo.Controls;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class PivotGridDemoView : UserControl
{
    private readonly BaseDemoView _baseView;
    private readonly EnterprisePivotGridControl _pivotGrid;

    public PivotGridDemoView(DemoItem metadata)
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

        var lblInfo = new Label
        {
            Text = "Multi-Dimensional OLAP Analysis: Region (Rows) × Quarter (Columns) | Aggregation: SUM(Revenue)",
            Location = new Point(8, 12),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        toolbar.Controls.Add(lblInfo);

        _pivotGrid = new EnterprisePivotGridControl { Dock = DockStyle.Fill };

        _baseView.DemoContainer.Controls.Add(_pivotGrid);
        _baseView.DemoContainer.Controls.Add(toolbar);
    }
}

