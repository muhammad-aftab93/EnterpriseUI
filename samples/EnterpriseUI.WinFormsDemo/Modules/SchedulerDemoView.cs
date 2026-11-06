using System;
using System.Drawing;
using System.Windows.Forms;
using EnterpriseUI.Scheduling;
using EnterpriseUI.WinFormsDemo.Controls;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class SchedulerDemoView : UserControl
{

    private static readonly object[] CachedValues1 = new object[] { "Work Week View", "Day View" };
    private readonly BaseDemoView _baseView=new BaseDemoView();
    private readonly EnterpriseSchedulerControl _schedulerControl=new EnterpriseSchedulerControl();

    public SchedulerDemoView(DemoItem metadata)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        _baseView.Dock = DockStyle.Fill ;
        _baseView.BindDemoMetadata(metadata);
        Controls.Add(_baseView);

        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(6)
        };

        var cmbView = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(8, 8),
            Width = 140
        };
        cmbView.Items.AddRange(CachedValues1);
        cmbView.SelectedIndex = 0;
        cmbView.SelectedIndexChanged += (_, _) =>
        {
            _schedulerControl.ActiveView = cmbView.SelectedIndex == 0 ? SchedulerViewType.Week : SchedulerViewType.Day;
            _schedulerControl.Invalidate();
        };

        var btnAddAppt = new Button
        {
            Text = "+ Add Sprint Review",
            Location = new Point(160, 8),
            Size = new Size(160, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnAddAppt.Click += (_, _) =>
        {
            _schedulerControl.Appointments.Add(new Appointment
            {
                Subject = "Client Discovery & Demo",
                Start = DateTime.Today.AddHours(14),
                End = DateTime.Today.AddHours(15.5),
                Color = "#ffb900",
                Location = "Executive Briefing Center"
            });
            _schedulerControl.Invalidate();
        };

        var btnToday = new Button
        {
            Text = "Jump to Today",
            Location = new Point(330, 8),
            Size = new Size(120, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnToday.Click += (_, _) =>
        {
            _schedulerControl.SelectedDate = DateTime.Today;
            _schedulerControl.Invalidate();
        };

        toolbar.Controls.AddRange(new Control[] { cmbView, btnAddAppt, btnToday });

        _schedulerControl.Dock = DockStyle.Fill ;

        _baseView.DemoContainer.Controls.Add(_schedulerControl);
        _baseView.DemoContainer.Controls.Add(toolbar);
    }
}

