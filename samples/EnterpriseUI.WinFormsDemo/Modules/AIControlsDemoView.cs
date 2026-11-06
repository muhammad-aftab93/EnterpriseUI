using System;
using System.Drawing;
using System.Windows.Forms;
using EnterpriseUI.WinFormsDemo.Controls;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class AIControlsDemoView : UserControl
{
    private readonly BaseDemoView _baseView=new BaseDemoView();
    private readonly EnterpriseAICopilotControl _aiCopilot=new EnterpriseAICopilotControl();

    public AIControlsDemoView(DemoItem metadata)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        _baseView.Dock = DockStyle.Fill ;
        _baseView.BindDemoMetadata(metadata);
        Controls.Add(_baseView);

        _aiCopilot.Dock = DockStyle.Fill ;
        _baseView.DemoContainer.Controls.Add(_aiCopilot);
    }
}

