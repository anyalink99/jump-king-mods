using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

internal static class Typography
{
    internal static void Run(Assembly engine, Assembly native)
    {
        // only load resources here, running the app would start Steam and open the editor
        var app = (Application)Activator.CreateInstance(native.GetType("JKWorldsmith.App"));
        app.GetType().GetMethod("InitializeComponent").Invoke(app, null);
        foreach (string name in new[] { "EnterCommand", "LitterLover2", "Default", "Gargoyle" })
        {
            var font = (FontFamily)app.FindResource(name);
            RequireEmbeddedFont(font, FontWeights.Normal);
        }
        var nativeText = new TextBlock { Style = (Style)app.FindResource("TextBlockStyle") };
        var text = (TextBlock)Activator.CreateInstance(engine.GetType("WorldsmithExtension.EditorText"), true);

        var button = (Button)engine.GetType("WorldsmithExtension.EditorUI").GetMethod("Button", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { "Extension updates", new Action(() => { }) });
        var input = (TextBox)Activator.CreateInstance(Assembly.Load("Wpf.Ui").GetType("Wpf.Ui.Controls.TextBox"));
        var nativeInput = (TextBox)Activator.CreateInstance(input.GetType());
        nativeInput.Style = (Style)app.FindResource("DefaultTextBoxStyle");
        var layout = new StackPanel();
        text.Text = "Workshop project";
        layout.Children.Add(nativeText);
        layout.Children.Add(text);
        layout.Children.Add(button);
        layout.Children.Add(input);
        layout.Children.Add(nativeInput);
        layout.Measure(new Size(700, 800));
        layout.Arrange(new Rect(0, 0, 700, 800));
        if (!text.FontFamily.Equals(nativeText.FontFamily) || text.FontSize != nativeText.FontSize || text.FontWeight != nativeText.FontWeight)
            throw new Exception("Extension text does not use the editor's body typography");
        RequireEmbeddedFont(text.FontFamily, text.FontWeight);

        RequireEmbeddedFont(button.FontFamily, button.FontWeight);
        if (!input.FontFamily.Equals(nativeInput.FontFamily) || input.FontSize != nativeInput.FontSize)
            throw new Exception("Extension input differs from the editor's native input style");
        if (button.DesiredSize.Height < 1 || text.DesiredSize.Height < 1)
            throw new Exception("Editor typography produced empty controls");

        app.MainWindow = new TestWindow();
        new System.Windows.Interop.WindowInteropHelper(app.MainWindow).EnsureHandle();
        var dialog = new Window();
        engine.GetType("WorldsmithExtension.EditorUI").GetMethod("Theme", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { dialog });
        if (!dialog.FontFamily.Equals(text.FontFamily) || dialog.FontSize != text.FontSize || dialog.FontWeight != text.FontWeight)
            throw new Exception("Extension dialog inherited window-chrome typography instead of body typography");
        RequireEmbeddedFont(dialog.FontFamily, dialog.FontWeight);

        var unrelated = new ResourceDictionary();
        var system = new FontFamily("Segoe UI");
        unrelated["Font"] = system;
        engine.GetType("WorldsmithExtension.EditorTypography").GetMethod("FontsLoaded", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { unrelated });
        if (!Object.ReferenceEquals(system, unrelated["Font"]))
            throw new Exception("Font fix changed an unrelated resource dictionary");
        Console.WriteLine("[OK] Embedded Worldsmith glyphs, extension body text, buttons and native input typography.");
    }

    sealed class TestWindow : Window
    {
        public Control RootContent { get { return null; } }
    }

    static void RequireEmbeddedFont(FontFamily font, FontWeight weight)
    {
        GlyphTypeface glyph;
        if (!new Typeface(font, FontStyles.Normal, weight, FontStretches.Normal).TryGetGlyphTypeface(out glyph)
            || glyph.FontUri.Scheme != "pack" || glyph.FontUri.OriginalString.IndexOf("/JKWorldsmith;component/", StringComparison.OrdinalIgnoreCase) < 0)
            throw new Exception("Font fell back instead of using Worldsmith's embedded glyphs: " + font);
    }
}
