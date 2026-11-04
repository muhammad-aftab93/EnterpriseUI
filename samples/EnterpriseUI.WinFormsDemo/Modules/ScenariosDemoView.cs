using System;
using System.Drawing;
using System.Windows.Forms;
using EnterpriseUI.Charts;
using EnterpriseUI.WinFormsDemo.Controls;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class ScenariosDemoView : UserControl
{
    private readonly BaseDemoView _baseView;
    private readonly TabControl _scenarioTabs;

    public ScenariosDemoView(DemoItem metadata)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        _baseView = new BaseDemoView { Dock = DockStyle.Fill };
        _baseView.BindDemoMetadata(metadata);
        Controls.Add(_baseView);

        _scenarioTabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 5) };

        // 1. CRM Scenario
        _scenarioTabs.TabPages.Add(CreateCrmTab());

        // 2. ERP Scenario
        _scenarioTabs.TabPages.Add(CreateErpTab());

        // 3. Healthcare Scenario
        _scenarioTabs.TabPages.Add(CreateHealthcareTab());

        // 4. Financial Trading Scenario
        _scenarioTabs.TabPages.Add(CreateTradingTab());

        // 5. HR & Project Management
        _scenarioTabs.TabPages.Add(CreateHrAndProjectTab());

        _baseView.DemoContainer.Controls.Add(_scenarioTabs);
    }

    private TabPage CreateCrmTab()
    {
        var tab = new TabPage("CRM & Lead Tracking");
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

        var lbl = new Label
        {
            Text = "Customer Relationship Management (CRM) Pipeline & Conversion Funnel",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 28
        };

        var grid = new DataGridView { Dock = DockStyle.Fill, BackgroundColor = Color.White, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        grid.Columns.Add("LeadId", "Lead #");
        grid.Columns.Add("Company", "Target Enterprise");
        grid.Columns.Add("Contact", "Primary Contact");
        grid.Columns.Add("Value", "Expected Value");
        grid.Columns.Add("Probability", "Win Probability");
        grid.Columns.Add("Stage", "Pipeline Stage");

        grid.Rows.Add("LD-901", "Global Aerospace Ltd", "Sarah Jenkins", "$180,000", "75%", "Proposal Sent");
        grid.Rows.Add("LD-902", "Nordic Energy Grid", "Bjorn Lindqvist", "$350,000", "90%", "Contract Review");
        grid.Rows.Add("LD-903", "Apex FinTech Core", "David Chen", "$95,000", "40%", "Discovery Call");
        grid.Rows.Add("LD-904", "Pacific Marine Logistics", "Elena Rostova", "$520,000", "60%", "Technical Evaluation");

        panel.Controls.Add(grid);
        panel.Controls.Add(lbl);
        tab.Controls.Add(panel);
        return tab;
    }

    private TabPage CreateErpTab()
    {
        var tab = new TabPage("ERP & Manufacturing");
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

        var lbl = new Label
        {
            Text = "Enterprise Resource Planning (ERP): Inventory, Work Orders & Supply Chain",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 28
        };

        var grid = new DataGridView { Dock = DockStyle.Fill, BackgroundColor = Color.White, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        grid.Columns.Add("PartNo", "SKU / Part #");
        grid.Columns.Add("Desc", "Material Specification");
        grid.Columns.Add("OnHand", "Stock On-Hand");
        grid.Columns.Add("Reorder", "Reorder Level");
        grid.Columns.Add("Supplier", "Primary Vendor");
        grid.Columns.Add("Status", "Inventory State");

        grid.Rows.Add("IC-7702", "Server Motherboard Dual Xeon", "142", "50", "Intel Foundry", "Adequate");
        grid.Rows.Add("DDR5-64G", "ECC Registered Memory Module", "28", "80", "Micron Technology", "REORDER REQUIRED");
        grid.Rows.Add("NVME-8TB", "Enterprise Gen5 U.2 SSD", "95", "40", "Samsung Semiconductor", "Adequate");
        grid.Rows.Add("PSU-2000W", "Titanium Redundant Power Supply", "64", "30", "Delta Electronics", "Adequate");

        panel.Controls.Add(grid);
        panel.Controls.Add(lbl);
        tab.Controls.Add(panel);
        return tab;
    }

    private TabPage CreateHealthcareTab()
    {
        var tab = new TabPage("Healthcare & Clinical EHR");
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

        var lbl = new Label
        {
            Text = "Patient Clinical Dashboard & Telemetry EHR Portal",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 28
        };

        var grid = new DataGridView { Dock = DockStyle.Fill, BackgroundColor = Color.White, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        grid.Columns.Add("MRN", "Medical Record #");
        grid.Columns.Add("Patient", "Patient Full Name");
        grid.Columns.Add("Ward", "Unit / Room");
        grid.Columns.Add("HeartRate", "Pulse (BPM)");
        grid.Columns.Add("SpO2", "Blood Oxygen (%)");
        grid.Columns.Add("Acuity", "Triage Acuity");

        grid.Rows.Add("MRN-4412", "Eleanor Vance", "ICU Bed 04", "78", "99%", "Level 2 (Emergent)");
        grid.Rows.Add("MRN-4413", "Marcus Brody", "Cardiology 208", "84", "97%", "Level 3 (Urgent)");
        grid.Rows.Add("MRN-4414", "Sophia Nguyen", "Surgical Stepdown 12", "72", "98%", "Level 4 (Standard)");
        grid.Rows.Add("MRN-4415", "Arthur Pendelton", "Telemetry Ward 05", "112", "92%", "ALERT - Tachycardia");

        panel.Controls.Add(grid);
        panel.Controls.Add(lbl);
        tab.Controls.Add(panel);
        return tab;
    }

    private TabPage CreateTradingTab()
    {
        var tab = new TabPage("Financial Trading & Real-Time Books");
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

        var lbl = new Label
        {
            Text = "Institutional Algorithmic Trading Order Book & Volatility Spreads",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 28
        };

        var grid = new DataGridView { Dock = DockStyle.Fill, BackgroundColor = Color.White, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        grid.Columns.Add("Symbol", "Ticker");
        grid.Columns.Add("Bid", "Bid Price");
        grid.Columns.Add("Ask", "Ask Price");
        grid.Columns.Add("Spread", "Spread (bps)");
        grid.Columns.Add("Volume", "24h Volume");
        grid.Columns.Add("VWAP", "VWAP ($)");

        grid.Rows.Add("US-TREAS-10Y", "98.421", "98.425", "0.4", "4.8B", "98.423");
        grid.Rows.Add("EUR/USD", "1.0845", "1.0846", "0.1", "12.4B", "1.0844");
        grid.Rows.Add("BRENT-CRUDE", "74.80", "74.83", "3.0", "850M", "74.78");
        grid.Rows.Add("SPX-INDEX", "5,820.50", "5,821.00", "0.5", "18.2B", "5,818.20");

        panel.Controls.Add(grid);
        panel.Controls.Add(lbl);
        tab.Controls.Add(panel);
        return tab;
    }

    private TabPage CreateHrAndProjectTab()
    {
        var tab = new TabPage("HR & Project Management");
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

        var lbl = new Label
        {
            Text = "Human Resources Performance Reviews & Agile Sprint Task Planning",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 28
        };

        var grid = new DataGridView { Dock = DockStyle.Fill, BackgroundColor = Color.White, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        grid.Columns.Add("Task", "Sprint Deliverable");
        grid.Columns.Add("Assignee", "Lead Engineer");
        grid.Columns.Add("Points", "Story Points");
        grid.Columns.Add("Estimate", "Estimate (Hrs)");
        grid.Columns.Add("Progress", "Completion");
        grid.Columns.Add("Status", "Workflow State");

        grid.Rows.Add("Virtual Grid SIMD Acceleration", "Devon Miles", "8", "32", "90%", "In Code Review");
        grid.Rows.Add("WCAG 2.2 AA Focus Visualizer", "Claire Bennett", "5", "20", "100%", "Closed / Verified");
        grid.Rows.Add("Banded Report PDF Vector Exporter", "Kenji Sato", "13", "52", "60%", "In Progress");
        grid.Rows.Add("Design Tokens Real-Time Sync", "Maria Garcia", "3", "12", "100%", "Closed / Verified");

        panel.Controls.Add(grid);
        panel.Controls.Add(lbl);
        tab.Controls.Add(panel);
        return tab;
    }
}

