using System;
using System.Drawing;
using System.Windows.Forms;
using EnterpriseUI.WinFormsDemo.Controls;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class SpecializedControlsDemoView : UserControl
{
    private readonly BaseDemoView _baseView;
    private readonly WinFormsGanttControl _gantt;
    private readonly WinFormsBarcodeControl _barcode;
    private readonly WinFormsKanbanBoardControl _kanban;
    private readonly WinFormsTreeMapControl _treeMap;
    private readonly WinFormsSignaturePadControl _signaturePad;
    private readonly WinFormsVirtualKeyboardControl _virtualKeyboard;
    private readonly TabControl _tabs;

    public SpecializedControlsDemoView(DemoItem metadata)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        _baseView = new BaseDemoView { Dock = DockStyle.Fill };
        _baseView.BindDemoMetadata(metadata);
        Controls.Add(_baseView);

        _tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5f),
            Padding = new Point(10, 5)
        };

        // Tab 1: Gantt Chart (RadGanttView equivalent)
        var tabGantt = new TabPage("📅 Gantt Project View");
        var ganttToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(8, 5, 8, 5),
            BackColor = Color.FromArgb(245, 247, 250)
        };

        var chkCritPath = new CheckBox
        {
            Text = "Highlight Critical Path",
            Checked = true,
            AutoSize = true,
            Padding = new Padding(4, 4, 8, 0)
        };

        var btnAdvanceProgress = new Button
        {
            Text = "⚡ Advance Tasks (+10%)",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat
        };

        _gantt = new WinFormsGanttControl { Dock = DockStyle.Fill };

        chkCritPath.CheckedChanged += (_, _) =>
        {
            _gantt.HighlightCriticalPath = chkCritPath.Checked;
            _gantt.Invalidate();
            _baseView.LogEvent("Gantt.HighlightCriticalPath", chkCritPath.Checked.ToString());
        };

        btnAdvanceProgress.Click += (_, _) =>
        {
            foreach (var t in _gantt.Tasks)
            {
                t.ProgressPercent = Math.Min(100, t.ProgressPercent + 10);
            }
            _gantt.Invalidate();
            _baseView.LogEvent("Gantt.AdvanceProgress", "+10% across all WBS tasks");
        };

        ganttToolbar.Controls.Add(chkCritPath);
        ganttToolbar.Controls.Add(btnAdvanceProgress);
        tabGantt.Controls.Add(_gantt);
        tabGantt.Controls.Add(ganttToolbar);

        // Tab 2: Barcode & QR Generator (RadBarcode equivalent)
        var tabBarcode = new TabPage("🔳 Barcode & QR Engine");
        var barcodeToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            Padding = new Padding(8, 5, 8, 5),
            BackColor = Color.FromArgb(245, 247, 250)
        };

        var lblSymbology = new Label { Text = "Symbology:", AutoSize = true, Padding = new Padding(2, 6, 4, 0) };
        var cmbSymbology = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 150
        };
        cmbSymbology.Items.AddRange(new object[] { "QrCode2D", "Code128Linear", "DataMatrix2D" });
        cmbSymbology.SelectedIndex = 0;

        var lblPayload = new Label { Text = "Payload:", AutoSize = true, Padding = new Padding(8, 6, 4, 0) };
        var txtPayload = new TextBox
        {
            Text = "ENTERPRISE-UI-2026-PRO",
            Width = 280
        };

        _barcode = new WinFormsBarcodeControl { Dock = DockStyle.Fill };

        cmbSymbology.SelectedIndexChanged += (_, _) =>
        {
            if (Enum.TryParse<BarcodeSymbology>(cmbSymbology.SelectedItem?.ToString(), out var sym))
            {
                _barcode.Symbology = sym;
                _baseView.LogEvent("Barcode.Symbology", sym.ToString());
            }
        };

        txtPayload.TextChanged += (_, _) =>
        {
            _barcode.Payload = txtPayload.Text;
            _baseView.LogEvent("Barcode.Payload", txtPayload.Text);
        };

        barcodeToolbar.Controls.Add(lblSymbology);
        barcodeToolbar.Controls.Add(cmbSymbology);
        barcodeToolbar.Controls.Add(lblPayload);
        barcodeToolbar.Controls.Add(txtPayload);
        tabBarcode.Controls.Add(_barcode);
        tabBarcode.Controls.Add(barcodeToolbar);

        // Tab 3: Kanban TaskBoard (RadTaskBoard / RadCardView equivalent)
        var tabKanban = new TabPage("📋 Kanban TaskBoard");
        var kanbanToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(8, 5, 8, 5),
            BackColor = Color.FromArgb(245, 247, 250)
        };

        var btnAddTask = new Button
        {
            Text = "+ New Sprint Card (Click Any Card to Advance Stage)",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat
        };

        _kanban = new WinFormsKanbanBoardControl { Dock = DockStyle.Fill };
        _kanban.CardAdvanced += card =>
        {
            _baseView.LogEvent("KanbanBoard.CardAdvanced", $"{card.Id} moved to {card.Lane}");
        };

        int cardSeq = 110;
        btnAddTask.Click += (_, _) =>
        {
            var id = $"K-{++cardSeq}";
            _kanban.Cards.Add(new WinFormsKanbanBoardControl.KanbanCard
            {
                Id = id,
                Title = $"Automated Regression Verification #{cardSeq}",
                Lane = "Backlog",
                Owner = "QA Lead",
                Priority = "Normal"
            });
            _kanban.Invalidate();
            _baseView.LogEvent("KanbanBoard.AddCard", $"{id} added to Backlog");
        };

        kanbanToolbar.Controls.Add(btnAddTask);
        tabKanban.Controls.Add(_kanban);
        tabKanban.Controls.Add(kanbanToolbar);

        // Tab 4: Hierarchical TreeMap (RadTreeMap equivalent)
        var tabTreeMap = new TabPage("🗺️ TreeMap Analytics");
        _treeMap = new WinFormsTreeMapControl { Dock = DockStyle.Fill };
        _treeMap.NodeSelected += leaf =>
        {
            _baseView.LogEvent("TreeMap.SelectedNode", $"{leaf.Label} (${leaf.Weight}M)");
        };
        tabTreeMap.Controls.Add(_treeMap);

        // Tab 5: Digital Signature Pad (RadSignaturePad equivalent)
        var tabSig = new TabPage("✍️ Signature Pad");
        var sigToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(8, 5, 8, 5),
            BackColor = Color.FromArgb(245, 247, 250)
        };
        var btnClearSig = new Button { Text = "Clear Ink Signature", AutoSize = true, FlatStyle = FlatStyle.Flat };
        _signaturePad = new WinFormsSignaturePadControl { Dock = DockStyle.Fill };
        _signaturePad.SignatureChanged += strokes =>
        {
            _baseView.LogEvent("SignaturePad.StrokeCount", strokes.ToString());
        };
        btnClearSig.Click += (_, _) => _signaturePad.ClearSignature();
        sigToolbar.Controls.Add(btnClearSig);
        tabSig.Controls.Add(_signaturePad);
        tabSig.Controls.Add(sigToolbar);

        // Tab 6: Touch Virtual Keyboard (RadVirtualKeyboard equivalent)
        var tabKeyboard = new TabPage("⌨️ Virtual Keyboard");
        _virtualKeyboard = new WinFormsVirtualKeyboardControl { Dock = DockStyle.Fill };
        _virtualKeyboard.KeyPressed += key =>
        {
            _baseView.LogEvent("VirtualKeyboard.TypedBuffer", _virtualKeyboard.TypedBuffer);
        };
        tabKeyboard.Controls.Add(_virtualKeyboard);

        _tabs.TabPages.Add(tabGantt);
        _tabs.TabPages.Add(tabBarcode);
        _tabs.TabPages.Add(tabKanban);
        _tabs.TabPages.Add(tabTreeMap);
        _tabs.TabPages.Add(tabSig);
        _tabs.TabPages.Add(tabKeyboard);

        _baseView.DemoContainer.Controls.Add(_tabs);
    }
}
