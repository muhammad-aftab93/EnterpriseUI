using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EnterpriseUI.WinForms;

/// <summary>Drop-down used by the data controls to pick one of a list of values; hosts a themed list in a pop-up.</summary>
internal static class ChoicePopup
{
    public static ToolStripDropDown Show(Control owner, Rectangle anchor, IReadOnlyList<string> choices, string? current, Action<string> picked)
    {
        var list = new EnterpriseListControl { ItemHeight = 26, SelectionMode = ListSelectionMode.One, EmptyText = "No choices" };
        list.AddRange(choices.Cast<object>());
        var currentIndex = current is null ? -1 : choices.ToList().FindIndex(c => string.Equals(c, current, StringComparison.CurrentCultureIgnoreCase));
        list.SelectedIndex = currentIndex;

        var rowHeight = (int)Math.Round(26 * owner.DeviceDpi / 96f);
        var minWidth = (int)Math.Round(120 * owner.DeviceDpi / 96f);
        list.Width = Math.Max(anchor.Width, minWidth);
        list.Height = Math.Min(Math.Max(1, choices.Count), 8) * rowHeight + 2;

        var host = new ToolStripControlHost(list) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = list.Size };
        var drop = new ToolStripDropDown { Padding = Padding.Empty, Margin = Padding.Empty, DropShadowEnabled = true, AutoSize = true };
        drop.Items.Add(host);

        var done = false;
        void Pick(int index)
        {
            if (done || index < 0 || index >= choices.Count) return;
            done = true;
            drop.Close();
            picked(choices[index]);
        }

        list.ItemClick += (_, i) => Pick(i);
        list.ItemActivate += (_, i) => Pick(i);
        drop.Closed += (_, _) => owner.BeginInvoke(new Action(() =>
        {
            drop.Dispose();
            list.Dispose();
        }));
        drop.Show(owner.PointToScreen(new Point(anchor.Left, anchor.Bottom + 1)));
        list.Focus();
        return drop;
    }
}
