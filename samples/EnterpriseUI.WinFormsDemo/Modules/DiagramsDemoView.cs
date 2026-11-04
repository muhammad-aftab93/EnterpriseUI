using System;
using System.Drawing;
using System.Windows.Forms;
using EnterpriseUI.Diagrams;
using EnterpriseUI.WinFormsDemo.Controls;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class DiagramsDemoView : UserControl
{
    private readonly BaseDemoView _baseView;
    private readonly WinFormsDiagramControl _diagramControl;

    public DiagramsDemoView(DemoItem metadata)
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

        var btnAddNode = new Button
        {
            Text = "+ Add Microservice Node",
            Location = new Point(8, 8),
            Size = new Size(180, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnAddNode.Click += (_, _) =>
        {
            var id = Guid.NewGuid().ToString("N");
            var count = _diagramControl.Model.Nodes.Count;
            _diagramControl.Model.Nodes.Add(new DiagramNode
            {
                Id = id,
                Text = $"Microservice #{count + 1}",
                X = 40 + (count * 30),
                Y = 220,
                Width = 140,
                Height = 55
            });
            _diagramControl.Invalidate();
        };

        var btnReset = new Button
        {
            Text = "Reset Architecture",
            Location = new Point(196, 8),
            Size = new Size(140, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnReset.Click += (_, _) =>
        {
            SetupDefaultArchitecture();
            _diagramControl.Invalidate();
        };

        var lblTip = new Label
        {
            Text = "Tip: Click and drag any node to test dynamic orthogonal connector routing",
            Location = new Point(345, 12),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Italic),
            ForeColor = Color.FromArgb(80, 80, 80)
        };

        toolbar.Controls.AddRange(new Control[] { btnAddNode, btnReset, lblTip });

        _diagramControl = new WinFormsDiagramControl { Dock = DockStyle.Fill };
        SetupDefaultArchitecture();

        _baseView.DemoContainer.Controls.Add(_diagramControl);
        _baseView.DemoContainer.Controls.Add(toolbar);
    }

    private void SetupDefaultArchitecture()
    {
        _diagramControl.Model.Nodes.Clear();
        _diagramControl.Model.Connectors.Clear();

        var node1 = new DiagramNode { Id = "n1", Text = "API Gateway\n(Envoy / YARP)", X = 40, Y = 100, Width = 130, Height = 60 };
        var node2 = new DiagramNode { Id = "n2", Text = "Auth & Identity\n(OAuth / JWT)", X = 240, Y = 40, Width = 130, Height = 60 };
        var node3 = new DiagramNode { Id = "n3", Text = "Order Engine\n(High-Throughput)", X = 240, Y = 160, Width = 140, Height = 60 };
        var node4 = new DiagramNode { Id = "n4", Text = "Distributed DB\n(CockroachDB)", X = 450, Y = 160, Width = 130, Height = 60 };

        _diagramControl.Model.Nodes.AddRange(new[] { node1, node2, node3, node4 });

        _diagramControl.Model.Connectors.Add(new DiagramConnector { SourceNodeId = "n1", TargetNodeId = "n2", Label = "Validate" });
        _diagramControl.Model.Connectors.Add(new DiagramConnector { SourceNodeId = "n1", TargetNodeId = "n3", Label = "Dispatch" });
        _diagramControl.Model.Connectors.Add(new DiagramConnector { SourceNodeId = "n3", TargetNodeId = "n4", Label = "ACID Commit" });
    }
}

