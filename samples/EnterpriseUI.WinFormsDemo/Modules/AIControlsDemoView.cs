using System;
using System.Drawing;
using System.Windows.Forms;
using EnterpriseUI.WinFormsDemo.Controls;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class AIControlsDemoView : UserControl
{
    private readonly BaseDemoView _baseView;
    private readonly WinFormsAICopilotControl _aiCopilot;

    public AIControlsDemoView(DemoItem metadata)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        _baseView = new BaseDemoView { Dock = DockStyle.Fill };
        _baseView.BindDemoMetadata(metadata);
        Controls.Add(_baseView);

        _aiCopilot = new WinFormsAICopilotControl { Dock = DockStyle.Fill };
        _baseView.DemoContainer.Controls.Add(_aiCopilot);
    }
}

