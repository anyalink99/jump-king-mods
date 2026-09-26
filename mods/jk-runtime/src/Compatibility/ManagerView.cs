using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace JKRuntime.Compatibility
{
    internal sealed class ManagerView
    {
        private readonly Form form;
        private readonly ComboBox screens;
        private readonly object[] originalScreens;
        private readonly Control[] originalAreas;
        private readonly FlowLayoutPanel areas;
        private readonly string originalTitle;
        private string originalSavedLabel;
        private ManagerMap map;
        private object originalSelection;
        internal bool Enabled { get; private set; }

        internal ManagerView(Form value)
        {
            form = value;
            screens = (ComboBox)form.Controls.Find("cboSpecificLevel", true).Single();
            originalScreens = screens.Items.Cast<object>().ToArray();
            originalAreas = form.Controls.Cast<Control>().Where(c => c.Bottom < screens.Top).ToArray();
            originalTitle = form.Text;
            areas = new FlowLayoutPanel { Location = new Point(12, 9), Size = new Size(352, 282),
                AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Visible = false };
            form.Controls.Add(areas);
        }
        internal void Refresh(ManagerMap value, bool enabled)
        {
            if (form.InvokeRequired) { form.BeginInvoke(new Action(delegate { Refresh(value, enabled); })); return; }
            if (Enabled == enabled && ReferenceEquals(map, value)) return;
            var selected = screens.SelectedItem as ManagerMap.Entry;
            if (!Enabled && enabled)
            {
                originalSelection = screens.SelectedItem;
                originalSavedLabel = form.Controls.Find("lblSavedPosition", true).Single().Text;
            }
            Enabled = enabled; map = value;
            form.SuspendLayout(); screens.BeginUpdate(); areas.SuspendLayout();
            try
            {
                foreach (Control control in originalAreas) control.Visible = !enabled;
                areas.Visible = enabled;
                while (areas.Controls.Count != 0) areas.Controls[0].Dispose();
                screens.Items.Clear();
                if (!enabled)
                {
                    screens.Items.AddRange(originalScreens); screens.SelectedItem = originalSelection;
                    if (originalSavedLabel != null) form.Controls.Find("lblSavedPosition", true).Single().Text = originalSavedLabel;
                    form.Text = originalTitle; return;
                }
                form.Text = originalTitle + " - Loaded map";
                areas.Controls.Add(new Label { Text = "Loaded map areas", AutoSize = true, Margin = new Padding(3, 3, 3, 8) });
                if (value == null || value.Screens.Length == 0)
                    areas.Controls.Add(new Label { Text = "No map loaded", AutoSize = true });
                else
                {
                    foreach (var area in value.Areas)
                    {
                        var target = area;
                        var button = new Button { Text = area.Name + "  (" + (area.First + 1) + "-" + (area.Last + 1) + ")",
                            Width = 326, Height = 40, UseMnemonic = false };
                        button.Click += delegate { JumpKingManagerCompatibility.Navigate(form, value, target.First); };
                        areas.Controls.Add(button);
                    }
                    if (value.Areas.Length == 0)
                        areas.Controls.Add(new Label { Text = "No named areas. Select a screen below.", AutoSize = true });
                    screens.Items.AddRange(value.Screens);
                    screens.SelectedIndex = selected != null && selected.First < value.Screens.Length ? selected.First : 0;
                }
                areas.BringToFront();
            }
            finally { areas.ResumeLayout(); screens.EndUpdate(); form.ResumeLayout(); }
        }
        internal void NavigateSelected()
        {
            var entry = screens.SelectedItem as ManagerMap.Entry;
            if (entry != null) JumpKingManagerCompatibility.Navigate(form, map, entry.First);
        }
        internal void RefreshSavedLabel()
        {
            if (!Enabled || map == null) return;
            var type = form.GetType();
            int index = Convert.ToInt32(type.GetField("PScreen", OwnedPatches.Members).GetValue(form));
            var label = form.Controls.Find("lblSavedPosition", true).Single();
            originalSavedLabel = label.Text;
            string coordinates = label.Text.Contains("\n") ? label.Text.Substring(label.Text.IndexOf('\n')) : "";
            label.Text = (index >= 0 && index < map.Screens.Length ? map.Screens[index].Name : "Screen " + (index + 1)) + coordinates;
        }
    }
}
