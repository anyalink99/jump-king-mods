using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MegaMappingExpansion;

namespace WorldsmithExtension
{
    internal sealed class TransitionsWindow : Window
    {
        private readonly string root;
        private readonly int screens;
        private readonly TextBox xml, seed, preview;
        private readonly ComboBox pool;
        internal TransitionsWindow(string projectRoot)
        {
            root = projectRoot;
            if (string.IsNullOrEmpty(root) || !File.Exists(Path.Combine(root, "level_settings.xml"))) throw new InvalidOperationException("Open an editable level project first.");
            int inferred = Collision.SourceScreens(root);
            screens = Layout.Read(root, inferred == 0 ? 169 : inferred).Screens;
            Title = "Transitions"; Width = 1000; Height = 760; MinWidth = 760; MinHeight = 500;
            EditorUI.Theme(this);
            var body = new DockPanel { Margin = new Thickness(16) }; Content = body;
            var heading = new EditorText { Text = "Edit transitions.xml, then validate and save. Preview uses a separate randomizer and never changes the game.\nScreen numbers start at 1; left/right name the outgoing edge.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
            DockPanel.SetDock(heading, Dock.Top); body.Children.Add(heading);
            var actions = new WrapPanel(); DockPanel.SetDock(actions, Dock.Bottom); body.Children.Add(actions);
            var panels = new Grid(); panels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }); panels.ColumnDefinitions.Add(new ColumnDefinition()); body.Children.Add(panels);
            xml = new TextBox { AcceptsReturn = true, AcceptsTab = true, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(0, 0, 12, 0) };
            string path = Path.Combine(root, MappingFiles.Transitions);
            xml.Text = File.Exists(path) ? File.ReadAllText(path) : "<Transitions version=\"1\" seed=\"auto\">\n</Transitions>\n";
            panels.Children.Add(xml);
            var right = new DockPanel(); Grid.SetColumn(right, 1); panels.Children.Add(right);
            var settings = new StackPanel(); DockPanel.SetDock(settings, Dock.Top); right.Children.Add(settings);
            settings.Children.Add(new EditorText { Text = "Preview pool" }); pool = new ComboBox { Margin = new Thickness(0, 4, 0, 8) }; settings.Children.Add(pool);
            settings.Children.Add(new EditorText { Text = "Preview seed (used when XML says auto)" }); seed = new TextBox { Text = "1", Margin = new Thickness(0, 4, 0, 8) }; settings.Children.Add(seed);
            settings.Children.Add(EditorUI.Button("Preview 30 selections", () => {
                var plan = Validate(); ulong number;
                if (!ulong.TryParse(seed.Text, NumberStyles.None, CultureInfo.InvariantCulture, out number)) throw new InvalidDataException("Preview seed must be an unsigned 64-bit integer.");
                preview.Text = TransitionAuthoring.Preview(plan, number, Convert.ToString(pool.SelectedItem, CultureInfo.InvariantCulture), 30);
            }));
            preview = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(0, 8, 0, 0) }; right.Children.Add(preview);
            actions.Children.Add(EditorUI.Button("Validate", () => { var plan = Validate(); preview.Text = "Valid: " + plan.Pools.Count + " pools, " + plan.Exits.Count + " exits, " + plan.Triggers.Count + " reroll triggers."; }));
            actions.Children.Add(EditorUI.Button("Add random pool example", () => {
                if (screens < 3) throw new InvalidDataException("This example needs at least 3 screens.");
                var document = XDocument.Parse(xml.Text); var element = document.Root;
                if (element.Name != "Transitions") throw new InvalidDataException("Expected a Transitions root.");
                string id = "trials"; int suffix = 1; while (element.Elements().Any(e => (string)e.Attribute("id") == id)) id = "trials" + suffix++;
                element.Add(new XElement("Pool", new XAttribute("id", id), new XAttribute("screens", string.Join(",", Enumerable.Range(2, Math.Min(4, screens - 1)))), new XAttribute("mode", "random"), new XAttribute("avoidLast", 1)));
                element.Add(new XElement("Exit", new XAttribute("screen", 1), new XAttribute("side", "right"), new XAttribute("pool", id))); xml.Text = document.ToString();
                preview.Text = "Example added. Set the exit screen and destinations, then validate before saving.";
            }));
            actions.Children.Add(EditorUI.Button("Save", () => { TransitionAuthoring.Save(root, xml.Text, screens); Validate(); preview.Text = "Saved " + MappingFiles.Transitions + ". The previous file is kept as .worldsmith-backup."; }));
            actions.Children.Add(EditorUI.Button("Guide", () => System.Diagnostics.Process.Start("https://github.com/anyalink99/jump-king-mods/blob/main/mods/mega-mapping-expansion/docs/teleports.md")));
            actions.Children.Add(EditorUI.Button("Close", Close));
            try { Validate(); } catch (Exception error) { preview.Text = error.Message; }
        }
        private TransitionPlan Validate()
        {
            var plan = TransitionAuthoring.Validate(root, xml.Text, screens); string selected = pool.SelectedItem as string;
            pool.ItemsSource = plan.Pools.Select(p => p.Id).ToArray(); pool.SelectedItem = selected;
            if (pool.SelectedIndex < 0 && pool.Items.Count > 0) pool.SelectedIndex = 0;
            return plan;
        }
    }
}
