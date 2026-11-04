using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EnterpriseUI.Data;
using EnterpriseUI.Exporting;
using EnterpriseUI.Grids;
using EnterpriseUI.Theming;
using EnterpriseUI.WinForms;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class GridDemoRecord
{
    public int Id { get; set; }
    public string Customer { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal Total => UnitPrice * Quantity;
    public string Priority { get; set; } = "Normal";
    public string Status { get; set; } = "Completed";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class GridDemoView : UserControl
{
    private readonly BaseDemoView _baseView;
    private readonly WinFormsDataGrid<GridDemoRecord> _grid;
    private readonly List<GridDemoRecord> _allRecords;
    private readonly System.Windows.Forms.Timer _signalRTimer;
    private readonly Label _lblMetrics;
    private ColumnLayoutProfile? _savedProfile;
    private bool _isLiveStreaming;

    public GridDemoView(DemoItem metadata)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        _baseView = new BaseDemoView { Dock = DockStyle.Fill };
        _baseView.BindDemoMetadata(metadata);
        Controls.Add(_baseView);

        _allRecords = GenerateSampleData(100);

        // Control Panel
        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 74,
            Padding = new Padding(6)
        };

        // Row 1 Buttons
        var btnSimulate1M = new Button
        {
            Text = "1,000,000 Rows",
            Location = new Point(8, 6),
            Size = new Size(125, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnSimulate1M.Click += async (_, _) =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _grid.Engine.DataSource = GenerateSampleData(1000000);
            await _grid.Engine.RefreshDataAsync();
            sw.Stop();
            _grid.Invalidate();
            _lblMetrics.Text = $"Active Records: 1,000,000 | Virtualization: Active | Bind Time: {sw.ElapsedMilliseconds} ms";
        };

        var btnReset = new Button
        {
            Text = "100 Rows",
            Location = new Point(138, 6),
            Size = new Size(80, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnReset.Click += async (_, _) =>
        {
            _grid.Engine.DataSource = _allRecords;
            await _grid.Engine.RefreshDataAsync();
            _grid.Invalidate();
            _lblMetrics.Text = $"Active Records: {_allRecords.Count} | Normal Virtualized Mode";
        };

        var btnMultiSort = new Button
        {
            Text = "Sort: Region ▲ + Total ▼",
            Location = new Point(224, 6),
            Size = new Size(165, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnMultiSort.Click += (_, _) =>
        {
            _grid.Engine.SortBy("Region", System.ComponentModel.ListSortDirection.Ascending);
            _grid.Engine.CycleSort("Total", true);
            _grid.Invalidate();
            _lblMetrics.Text = "Multi-Column Sorting Applied: [Region ASC, Total DESC]";
        };

        var btnGroupRegion = new Button
        {
            Text = "+ Group by Region",
            Location = new Point(395, 6),
            Size = new Size(130, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnGroupRegion.Click += (_, _) =>
        {
            _grid.Engine.GroupBy("Region");
            _grid.Invalidate();
            _lblMetrics.Text = "Group by Region Active";
        };

        var btnClearGroup = new Button
        {
            Text = "Clear Groups",
            Location = new Point(530, 6),
            Size = new Size(100, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnClearGroup.Click += (_, _) =>
        {
            _grid.Engine.ClearGroups();
            _grid.Invalidate();
            _lblMetrics.Text = "Grouping Cleared";
        };

        var btnLiveSignalR = new Button
        {
            Text = "Toggle Real-Time Ticks",
            Location = new Point(636, 6),
            Size = new Size(155, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnLiveSignalR.Click += (_, _) =>
        {
            _isLiveStreaming = !_isLiveStreaming;
            if (_isLiveStreaming)
            {
                _signalRTimer.Start();
                btnLiveSignalR.Text = "Stop Real-Time Ticks";
                btnLiveSignalR.BackColor = Color.FromArgb(230, 255, 230);
            }
            else
            {
                _signalRTimer.Stop();
                btnLiveSignalR.Text = "Toggle Real-Time Ticks";
                btnLiveSignalR.BackColor = SystemColors.Control;
            }
        };

        // Row 2 Controls (Filtering, Presets, Export)
        var txtSearch = new TextBox
        {
            Location = new Point(8, 40),
            Width = 160,
            PlaceholderText = "Search Panel / Filter..."
        };
        txtSearch.TextChanged += (_, _) =>
        {
            _grid.Engine.SearchPanelText = txtSearch.Text;
            _grid.Invalidate();
        };

        var btnColumnChooser = new Button
        {
            Text = "Column Chooser (Toggle Priority)",
            Location = new Point(174, 38),
            Size = new Size(200, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnColumnChooser.Click += (_, _) =>
        {
            var col = _grid.Engine.Columns.FirstOrDefault(c => c.Field == "Priority");
            if (col != null)
            {
                _grid.Engine.SetColumnVisibility("Priority", !col.Visible);
                _grid.Invalidate();
                btnColumnChooser.Text = col.Visible ? "Hide Priority Column" : "Show Priority Column";
            }
        };

        var btnSavePreset = new Button
        {
            Text = "Save Layout Profile",
            Location = new Point(380, 38),
            Size = new Size(140, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnSavePreset.Click += (_, _) =>
        {
            _savedProfile = _grid.Engine.CreateLayoutProfile("MyCustomLayout");
            MessageBox.Show("Column layout profile (visibility, widths, sort order) saved to memory!", "Column Profile Presets", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        var btnLoadPreset = new Button
        {
            Text = "Restore Profile",
            Location = new Point(526, 38),
            Size = new Size(110, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnLoadPreset.Click += (_, _) =>
        {
            if (_savedProfile != null)
            {
                _grid.Engine.ApplyLayoutProfile(_savedProfile);
                _grid.Invalidate();
            }
            else
            {
                MessageBox.Show("Please save a layout profile first.", "Column Profile Presets", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };

        var btnFilterRow = new Button
        {
            Text = "Filter Row",
            Location = new Point(642, 38),
            Size = new Size(85, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnFilterRow.Click += (_, _) =>
        {
            _grid.ShowFilterRow = !_grid.ShowFilterRow;
            _grid.Invalidate();
            _baseView.LogEvent("FilterRowToggled", $"ShowFilterRow: {_grid.ShowFilterRow}");
        };

        var btnMasterDetail = new Button
        {
            Text = "Master-Detail",
            Location = new Point(732, 38),
            Size = new Size(100, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnMasterDetail.Click += (_, _) =>
        {
            _grid.Engine.EnableMasterDetail = !_grid.Engine.EnableMasterDetail;
            if (_grid.Engine.EnableMasterDetail && _grid.Engine.RenderedItems.Count > 0)
            {
                _grid.Engine.ToggleDetailExpansion(_grid.Engine.RenderedItems[0]);
            }
            _grid.Invalidate();
            _baseView.LogEvent("MasterDetailToggled", $"EnableMasterDetail: {_grid.Engine.EnableMasterDetail}");
        };

        var btnBanded = new Button
        {
            Text = "Stacked Bands",
            Location = new Point(837, 38),
            Size = new Size(105, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnBanded.Click += (_, _) =>
        {
            _grid.ShowBandedHeaders = !_grid.ShowBandedHeaders;
            _grid.Invalidate();
            _baseView.LogEvent("BandedHeadersToggled", $"ShowBandedHeaders: {_grid.ShowBandedHeaders}");
        };

        var btnExport = new Button
        {
            Text = "Export All",
            Location = new Point(947, 38),
            Size = new Size(88, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnExport.Click += async (_, _) =>
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "Excel XML (*.xml)|*.xml|CSV file (*.csv)|*.csv|PDF Document (*.pdf)|*.pdf|XML Dataset (*.xml)|*.xml|JSON (*.json)|*.json",
                FileName = "GridExport.xml"
            };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                var format = sfd.FilterIndex switch
                {
                    1 => ExportFormat.ExcelXml,
                    2 => ExportFormat.Csv,
                    3 => ExportFormat.Pdf,
                    4 => ExportFormat.Xml,
                    _ => ExportFormat.Json
                };

                const int exportLimit = 100_000;
                var rows = await _grid.Engine.ReadAllAsync(exportLimit);
                using var fs = File.Create(sfd.FileName);
                await ExportEngine.ExportAsync(rows, format, fs);
                var suffix = rows.Count >= exportLimit ? $" (limited to the first {exportLimit:N0} rows)" : string.Empty;
                _baseView.LogEvent("DataExported", $"Exported {rows.Count:N0} filtered rows to {Path.GetFileName(sfd.FileName)} as {format}");
                MessageBox.Show($"Exported {rows.Count:N0} rows to {Path.GetFileName(sfd.FileName)}{suffix}.", "EnterpriseUI Export Engine", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };

        var btnPrint = new Button
        {
            Text = "Print Preview",
            Location = new Point(1040, 38),
            Size = new Size(98, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnPrint.Click += (_, _) =>
        {
            _baseView.LogEvent("PrintPreviewOpened", "Opened GDI+ PrintPreviewDialog");
            _grid.ShowPrintPreviewDialog();
        };

        _lblMetrics = new Label
        {
            Text = $"Records: {_allRecords.Count} | Multi-Sort & Inline Edit Ready",
            Location = new Point(798, 12),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        toolbar.Controls.AddRange(new Control[]
        {
            btnSimulate1M,
            btnReset,
            btnMultiSort,
            btnGroupRegion,
            btnClearGroup,
            btnLiveSignalR,
            txtSearch,
            btnColumnChooser,
            btnSavePreset,
            btnLoadPreset,
            btnFilterRow,
            btnMasterDetail,
            btnBanded,
            btnExport,
            btnPrint,
            _lblMetrics
        });

        // DataGrid Setup
        _grid = new WinFormsDataGrid<GridDemoRecord> { Dock = DockStyle.Fill };
        _grid.Engine.Columns.Add(new GridColumn { Field = "Id", Title = "Record ID", Width = 90 });
        _grid.Engine.Columns.Add(new GridColumn { Field = "Customer", Title = "Enterprise Customer", Width = 180 });
        _grid.Engine.Columns.Add(new GridColumn { Field = "Product", Title = "Product Line", Width = 170 });
        _grid.Engine.Columns.Add(new GridColumn { Field = "Region", Title = "Region", Width = 110 });
        _grid.Engine.Columns.Add(new GridColumn { Field = "UnitPrice", Title = "Unit Price", FormatString = "C2", Width = 100 });
        _grid.Engine.Columns.Add(new GridColumn { Field = "Quantity", Title = "Qty", Width = 70 });
        _grid.Engine.Columns.Add(new GridColumn { Field = "Total", Title = "Gross Amount", FormatString = "C2", Width = 110 });
        _grid.Engine.Columns.Add(new GridColumn { Field = "Priority", Title = "Priority", Width = 90 });
        _grid.Engine.Columns.Add(new GridColumn { Field = "Status", Title = "Status", Width = 100 });

        var orderBand = new GridColumnBand { Title = "Order" };
        var commercialBand = new GridColumnBand { Title = "Commercial" };
        var pricingBand = new GridColumnBand { Title = "Pricing" };
        var workflowBand = new GridColumnBand { Title = "Workflow" };
        commercialBand.ChildBands.Add(pricingBand);
        _grid.Engine.Bands.AddRange(new[] { orderBand, commercialBand, workflowBand });

        var bandByField = new Dictionary<string, GridColumnBand>
        {
            ["Id"] = orderBand, ["Customer"] = orderBand, ["Product"] = orderBand, ["Region"] = orderBand,
            ["UnitPrice"] = pricingBand, ["Quantity"] = pricingBand, ["Total"] = pricingBand,
            ["Priority"] = workflowBand, ["Status"] = workflowBand
        };
        foreach (var column in _grid.Engine.Columns)
        {
            column.BandId = bandByField[column.Field].Id;
        }
        _grid.Engine.FormatRules.Add(new GridFormatRule
        {
            Name = "Gross amount data bar",
            ColumnField = "Total",
            RuleType = FormatRuleType.DataBar,
            Value1 = 0m,
            Value2 = 60000m,
            BackgroundColorHex = "#5088d7"
        });
        _grid.Engine.FormatRules.Add(new GridFormatRule
        {
            Name = "Critical priority highlight",
            ColumnField = "Priority",
            RuleType = FormatRuleType.HighlightCell,
            Operator = FormatConditionOperator.Equal,
            Value1 = "Critical",
            BackgroundColorHex = "#fff0c2",
            ForegroundColorHex = "#713f12"
        });
        _grid.Engine.FormatRules.Add(new GridFormatRule
        {
            Name = "Quantity color scale",
            ColumnField = "Quantity",
            RuleType = FormatRuleType.ColorScale,
            Value1 = 1,
            Value2 = 20,
            BackgroundColorHex = "#f4f8fc",
            ForegroundColorHex = "#6aa86e"
        });

        _grid.Engine.DataSource = _allRecords;
        _grid.SelectionChanged += (_, _) => _baseView.LogEvent("RowSelectionChanged", $"SelectedRowIndex: {_grid.SelectedRowIndex}, TotalSelected: {_grid.SelectedIndices.Count}");
        _grid.CellEdited += (_, details) => _baseView.LogEvent("CellEdited", details);

        _baseView.DemoContainer.Controls.Add(_grid);
        _baseView.DemoContainer.Controls.Add(toolbar);

        // SignalR Simulation Timer
        _signalRTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _signalRTimer.Tick += async (_, _) =>
        {
            var rnd = new Random();
            var target = _allRecords[rnd.Next(_allRecords.Count)];
            target.UnitPrice = Math.Max(50, target.UnitPrice + rnd.Next(-15, 25));
            target.Timestamp = DateTime.UtcNow;
            await _grid.Engine.RefreshDataAsync();
            _grid.Invalidate();
        };
    }

    private static List<GridDemoRecord> GenerateSampleData(int count)
    {
        var customers = new[] { "Acme Corporation", "Contoso Global", "Fabrikam Aerospace", "Northwind Logistics", "Tailspin Technologies", "AdventureWorks Financial" };
        var products = new[] { "Cloud Infrastructure Suite", "Real-Time Telemetry Node", "Enterprise Security Agent", "Workflow Execution Core", "High-Throughput Analytics" };
        var regions = new[] { "North America", "EMEA", "APAC", "LATAM" };
        var priorities = new[] { "Normal", "High", "Critical", "Low" };
        var statuses = new[] { "Completed", "Processing", "Pending", "Verified" };

        var list = new List<GridDemoRecord>(count);
        var rng = new Random(1337);

        for (int i = 1; i <= count; i++)
        {
            list.Add(new GridDemoRecord
            {
                Id = 10000 + i,
                Customer = customers[rng.Next(customers.Length)],
                Product = products[rng.Next(products.Length)],
                Region = regions[rng.Next(regions.Length)],
                UnitPrice = 450m + (rng.Next(1, 100) * 25m),
                Quantity = rng.Next(1, 20),
                Priority = priorities[rng.Next(priorities.Length)],
                Status = statuses[rng.Next(statuses.Length)]
            });
        }
        return list;
    }
}
