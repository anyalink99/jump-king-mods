using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using HarmonyLib;

namespace WorldsmithExtension
{
    internal static class EditorTypography
    {
        internal static void Install(Harmony harmony)
        {
            harmony.Patch(AccessTools.PropertySetter(typeof(ResourceDictionary), "Source"),
                postfix: new HarmonyMethod(typeof(EditorTypography), "FontsLoaded"));
        }

        static void FontsLoaded(ResourceDictionary __instance)
        {
            var source = __instance.Source;
            if (source == null || !String.Equals(source.OriginalString,
                "pack://application:,,,/JKWorldsmith;component/Style/Fonts.xaml", StringComparison.OrdinalIgnoreCase))
                return;

            // WPF's font loader uses the entry assembly even after ResourceAssembly is set.
            // Qualify these before other dictionaries capture them in StaticResource setters.
            const string prefix = "pack://application:,,,/Style/Assets/Fonts/";
            foreach (object key in __instance.Keys.Cast<object>().ToArray())
            {
                var font = __instance[key] as FontFamily;
                if (font != null && font.Source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    __instance[key] = new FontFamily(new Uri("pack://application:,,,/JKWorldsmith;component/"), "./Style/Assets/Fonts/" + font.Source.Substring(prefix.Length));
            }
        }
    }

    internal sealed class EditorText : System.Windows.Controls.TextBlock
    {
        internal EditorText()
        {
            SetResourceReference(StyleProperty, "TextBlockStyle");
        }
    }
}
