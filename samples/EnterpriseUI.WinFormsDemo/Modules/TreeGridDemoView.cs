using System;
using System.Drawing;
using System.Windows.Forms;
using EnterpriseUI.WinFormsDemo.Controls;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class TreeGridDemoView : UserControl
{
    private readonly BaseDemoView _baseView;
    private readonly WinFormsTreeListControl _treeControl;

    public TreeGridDemoView(DemoItem metadata)
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

        var btnExpandAll = new Button
        {
            Text = "Expand All",
            Location = new Point(8, 8),
            Size = new Size(110, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnExpandAll.Click += (_, _) =>
        {
            foreach (var item in _treeControl.Items) item.IsExpanded = true;
            _treeControl.Invalidate();
        };

        var btnCollapseAll = new Button
        {
            Text = "Collapse All",
            Location = new Point(126, 8),
            Size = new Size(110, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnCollapseAll.Click += (_, _) =>
        {
            foreach (var item in _treeControl.Items) item.IsExpanded = false;
            _treeControl.Invalidate();
        };

        toolbar.Controls.AddRange(new Control[] { btnExpandAll, btnCollapseAll });

        _treeControl = new WinFormsTreeListControl { Dock = DockStyle.Fill };

        _baseView.DemoContainer.Controls.Add(_treeControl);
        _baseView.DemoContainer.Controls.Add(toolbar);
    }
}

