using System;
using System.Drawing;
using System.Windows.Forms;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class ReportingDemoView : UserControl
{
    private readonly BaseDemoView _baseView;
    private readonly ComboBox _cmbReportType;
    private readonly Panel _reportCanvas;
    private readonly Label _lblTitle;
    private readonly Label _lblContent;

    public ReportingDemoView(DemoItem metadata)
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

        _cmbReportType = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(8, 8),
            Width = 200
        };
        _cmbReportType.Items.AddRange(new object[]
        {
            "Invoice & Billing Statement",
            "Purchase Order Confirmation",
            "Healthcare Patient Diagnostic Summary",
            "Executive Quarterly Sales Report"
        });
        _cmbReportType.SelectedIndex = 0;
        _cmbReportType.SelectedIndexChanged += (_, _) => RenderReport(_cmbReportType.SelectedIndex);

        var btnPrint = new Button
        {
            Text = "Simulate Print Preview",
            Location = new Point(220, 8),
            Size = new Size(160, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnPrint.Click += (_, _) => MessageBox.Show("Spooling report to EnterpriseUI GDI+ PrintPipeline... 1 page rendered.", "Print Engine", MessageBoxButtons.OK, MessageBoxIcon.Information);

        var btnExportPdf = new Button
        {
            Text = "Export Report (PDF)",
            Location = new Point(390, 8),
            Size = new Size(150, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnExportPdf.Click += (_, _) => MessageBox.Show("Report successfully exported to Vector PDF with high-DPI font kerning.", "Report Exporter", MessageBoxButtons.OK, MessageBoxIcon.Information);

        toolbar.Controls.AddRange(new Control[] { _cmbReportType, btnPrint, btnExportPdf });

        var scrollContainer = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(240, 240, 242) };
        _reportCanvas = new Panel
        {
            Size = new Size(580, 720),
            BackColor = Color.White,
            Location = new Point(40, 20),
            BorderStyle = BorderStyle.FixedSingle
        };

        _lblTitle = new Label
        {
            Location = new Point(30, 24),
            AutoSize = true,
            Font = new Font("Segoe UI", 14f, FontStyle.Bold)
        };

        _lblContent = new Label
        {
            Location = new Point(30, 70),
            Size = new Size(520, 620),
            Font = new Font("Consolas", 9.5f)
        };

        _reportCanvas.Controls.Add(_lblTitle);
        _reportCanvas.Controls.Add(_lblContent);
        scrollContainer.Controls.Add(_reportCanvas);

        _baseView.DemoContainer.Controls.Add(scrollContainer);
        _baseView.DemoContainer.Controls.Add(toolbar);

        RenderReport(0);
    }

    private void RenderReport(int index)
    {
        switch (index)
        {
            case 0:
                _lblTitle.Text = "INVOICE #INV-2026-904";
                _lblContent.Text =
                    "============================================================\n" +
                    "Billed To: Contoso Enterprises, Inc.\n" +
                    "Billing Date: November 4, 2026       Due Date: Net 30\n" +
                    "============================================================\n\n" +
                    "ITEM DESCRIPTION                     QTY   UNIT       TOTAL\n" +
                    "------------------------------------------------------------\n" +
                    "EnterpriseUI Commercial Site License   1  $9,500.00  $9,500.00\n" +
                    "24/7 Mission-Critical SLA Support      1  $2,400.00  $2,400.00\n" +
                    "Onsite Architecture Workshop (2 Days)  2  $1,800.00  $3,600.00\n" +
                    "------------------------------------------------------------\n" +
                    "Subtotal:                                       $15,500.00\n" +
                    "Tax (8.25%):                                     $1,278.75\n" +
                    "------------------------------------------------------------\n" +
                    "TOTAL DUE (USD):                                $16,778.75\n\n" +
                    "Thank you for your business! EnterpriseUI Engine Banded Report.";
                break;
            case 1:
                _lblTitle.Text = "PURCHASE ORDER #PO-8812";
                _lblContent.Text =
                    "============================================================\n" +
                    "Vendor: Fabrikam Semiconductor Corp.\n" +
                    "Ship To: Silicon Valley Logistics Gate 4\n" +
                    "============================================================\n\n" +
                    "PART NO.   DESCRIPTION             QTY   UNIT COST     TOTAL\n" +
                    "------------------------------------------------------------\n" +
                    "FPGA-800   Edge Acceleration Card   50    $420.00  $21,000.00\n" +
                    "QSFP-100G  Optical Transceiver     100     $95.00   $9,500.00\n" +
                    "------------------------------------------------------------\n" +
                    "Total PO Value:                                    $30,500.00\n" +
                    "Approved By: VP Infrastructure Procurement";
                break;
            case 2:
                _lblTitle.Text = "CLINICAL DIAGNOSTIC SUMMARY";
                _lblContent.Text =
                    "============================================================\n" +
                    "Patient: Doe, Jane       DOB: 1984-06-12       MRN: #994821\n" +
                    "Attending Physician: Dr. Marcus Vance, MD (Cardiology)\n" +
                    "============================================================\n\n" +
                    "TEST PANEL                 RESULT     REFERENCE RANGE  STATUS\n" +
                    "------------------------------------------------------------\n" +
                    "High-Sensitivity Troponin  0.01 ng/mL  < 0.04 ng/mL    NORMAL\n" +
                    "Serum Potassium            4.2 mmol/L  3.5 - 5.0       NORMAL\n" +
                    "Total Cholesterol          182 mg/dL   < 200 mg/dL     DESIRABLE\n" +
                    "------------------------------------------------------------\n" +
                    "Assessment & Plan:\n" +
                    "Patient demonstrates stable biomarker metrics post-intervention.\n" +
                    "Follow-up scheduled in 6 weeks.";
                break;
            case 3:
                _lblTitle.Text = "EXECUTIVE SALES ANALYTICS";
                _lblContent.Text =
                    "============================================================\n" +
                    "Reporting Period: Q3 Fiscal 2026\n" +
                    "Prepared For: Board of Directors & Executive Leadership\n" +
                    "============================================================\n\n" +
                    "TERRITORY         TARGET REVENUE   ACTUAL REVENUE   VARIANCE\n" +
                    "------------------------------------------------------------\n" +
                    "North America       $12,000,000      $13,420,000     +11.8%\n" +
                    "EMEA                 $8,500,000       $8,940,000      +5.2%\n" +
                    "Asia-Pacific        $14,000,000      $16,810,000     +20.1%\n" +
                    "Latin America        $3,000,000       $3,150,000      +5.0%\n" +
                    "------------------------------------------------------------\n" +
                    "Consolidated Net:   $37,500,000      $42,320,000     +12.9%";
                break;
        }
    }
}

