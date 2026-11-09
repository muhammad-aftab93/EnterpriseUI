using System;
using System.Drawing;
using System.Windows.Forms;
using EnterpriseUI.Grids;
using EnterpriseUI.WinForms;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class PivotGridDemoView : UserControl
{
    private readonly BaseDemoView _baseView = new BaseDemoView();
    private readonly EnterprisePivotGridControl _pivotGrid = new EnterprisePivotGridControl();

    public PivotGridDemoView(DemoItem metadata)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        _baseView.Dock = DockStyle.Fill;
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
            Text = "Multi-dimensional analysis: Region and Product (rows) × Status (columns) | Aggregation: SUM(Revenue)",
            Location = new Point(8, 12),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        toolbar.Controls.Add(lblInfo);

        _pivotGrid.Dock = DockStyle.Fill;
        _pivotGrid.RowFields.Add("Region");
        _pivotGrid.RowFields.Add("Product");
        _pivotGrid.ColumnFields.Add("Status");
        _pivotGrid.Values.Add(new PivotValueField("Total", EnterpriseUI.Data.AggregateFunction.Sum, "Revenue", "C0"));
        _pivotGrid.DataSource = GridScenarioData.Records(600);
        _pivotGrid.SelectionChanged += (_, _) => _baseView.LogEvent("PivotGrid.SelectedValue", Convert.ToString(_pivotGrid.SelectedValue) ?? string.Empty);

        _baseView.DemoContainer.Controls.Add(_pivotGrid);
        _baseView.DemoContainer.Controls.Add(toolbar);
    }
}
