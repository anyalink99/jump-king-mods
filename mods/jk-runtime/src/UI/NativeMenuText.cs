using System.Runtime.CompilerServices;
using JumpKing.PauseMenu.BT;

namespace JKRuntime.UI
{
    // keep the source with the exact fitted string instance, even if native completed-level
    // icons wrap it in a new TextInfo. Weak keys follow the menu lifetime
    // identical truncated labels from different items still need separate sources
    internal static class NativeMenuText
    {
        private sealed class Source { internal string Value; }
        private static readonly ConditionalWeakTable<string, Source> sources = new ConditionalWeakTable<string, Source>();
        internal static void Remember(string original, TextInfo fitted)
        {
            if (fitted == null || string.IsNullOrEmpty(fitted.Text) || original == fitted.Text) return;
            sources.GetOrCreateValue(fitted.Text).Value = original;
        }
        internal static string Original(TextInfo text)
        {
            if (text == null) return "Item";
            Source source;
            return text.Text != null && sources.TryGetValue(text.Text, out source) ? source.Value : text.Text;
        }
    }
}
