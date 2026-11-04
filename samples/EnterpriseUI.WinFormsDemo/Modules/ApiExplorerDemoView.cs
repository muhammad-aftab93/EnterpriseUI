using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class ApiExplorerDemoView : UserControl
{
    private readonly BaseDemoView _baseView;
    private readonly ComboBox _cmbAssemblies;
    private readonly ListBox _classList;
    private readonly ListView _memberList;
    private Assembly? _currentAssembly;

    public ApiExplorerDemoView(DemoItem metadata)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        _baseView = new BaseDemoView { Dock = DockStyle.Fill };
        _baseView.BindDemoMetadata(metadata);
        Controls.Add(_baseView);

        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(6)
        };

        var lblSelect = new Label
        {
            Text = "Target Assembly:",
            Location = new Point(8, 12),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };

        _cmbAssemblies = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(130, 8),
            Width = 260
        };
        _cmbAssemblies.Items.AddRange(new object[]
        {
            "EnterpriseUI.Grids",
            "EnterpriseUI.Charts",
            "EnterpriseUI.Editors",
            "EnterpriseUI.Scheduling",
            "EnterpriseUI.Reporting",
            "EnterpriseUI.Dashboard",
            "EnterpriseUI.Diagrams",
            "EnterpriseUI.Theming",
            "EnterpriseUI.Data",
            "EnterpriseUI.Accessibility",
            "EnterpriseUI.AI",
            "EnterpriseUI.WinForms"
        });
        _cmbAssemblies.SelectedIndex = 0;
        _cmbAssemblies.SelectedIndexChanged += (_, _) => LoadAssembly(_cmbAssemblies.SelectedItem?.ToString() ?? "");

        topPanel.Controls.AddRange(new Control[] { lblSelect, _cmbAssemblies });

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 260
        };

        _classList = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5f),
            IntegralHeight = false
        };
        _classList.SelectedIndexChanged += (_, _) =>
        {
            if (_classList.SelectedItem is string className && _currentAssembly != null)
            {
                var type = _currentAssembly.GetTypes().FirstOrDefault(t => t.Name == className || t.FullName == className);
                if (type != null) InspectType(type);
            }
        };

        _memberList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Consolas", 9f)
        };
        _memberList.Columns.Add("Member Type", 110);
        _memberList.Columns.Add("Name", 220);
        _memberList.Columns.Add("Return / Data Type", 200);
        _memberList.Columns.Add("Accessibility / Signature", 280);

        split.Panel1.Controls.Add(_classList);
        split.Panel2.Controls.Add(_memberList);

        _baseView.DemoContainer.Controls.Add(split);
        _baseView.DemoContainer.Controls.Add(topPanel);

        LoadAssembly("EnterpriseUI.Grids");
    }

    private void LoadAssembly(string assemblyName)
    {
        _classList.Items.Clear();
        _memberList.Items.Clear();

        try
        {
            _currentAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName) ?? Assembly.Load(assemblyName);

            if (_currentAssembly != null)
            {
                var publicTypes = _currentAssembly.GetExportedTypes().OrderBy(t => t.Name).ToList();
                foreach (var t in publicTypes)
                {
                    _classList.Items.Add(t.Name);
                }

                if (_classList.Items.Count > 0)
                {
                    _classList.SelectedIndex = 0;
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to inspect assembly {assemblyName}: {ex.Message}");
        }
    }

    private void InspectType(Type type)
    {
        _memberList.Items.Clear();

        // Properties
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            _memberList.Items.Add(new ListViewItem(new[] { "Property", p.Name, p.PropertyType.Name, $"{(p.CanRead ? "get; " : "")}{(p.CanWrite ? "set; " : "")}" }));
        }

        // Methods
        foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Where(m => !m.IsSpecialName))
        {
            var pars = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
            _memberList.Items.Add(new ListViewItem(new[] { "Method", m.Name, m.ReturnType.Name, $"({pars})" }));
        }

        // Events
        foreach (var ev in type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            _memberList.Items.Add(new ListViewItem(new[] { "Event", ev.Name, ev.EventHandlerType?.Name ?? "", "event dispatch" }));
        }
    }
}

