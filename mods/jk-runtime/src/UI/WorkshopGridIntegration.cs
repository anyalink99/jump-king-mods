using System;
using System.Collections;
using System.Collections.Generic;
using BehaviorTree;
using JumpKing.PauseMenu.BT;

namespace JKRuntime.UI
{
    internal static class WorkshopGridIntegration
    {
        private sealed class FactoryCache
        {
            internal WeakReference Factory;
            internal readonly Dictionary<TextButton, IBTnode> OriginalMenus =
                new Dictionary<TextButton, IBTnode>();
            internal readonly Dictionary<TextButton, SquareGridSelector> Grids =
                new Dictionary<TextButton, SquareGridSelector>();
        }

        private static readonly List<FactoryCache> Caches = new List<FactoryCache>();

        internal static void Apply(object factory, bool enabled)
        {
            IList drawables = VanillaMenuAdapter.GetDrawables(factory);
            if (drawables == null) return;
            FactoryCache cache = GetCache(factory);
            List<TextButton> buttons = FindWorkshopGridButtons(drawables);
            foreach (TextButton known in cache.OriginalMenus.Keys)
                if (!buttons.Contains(known)) buttons.Add(known);
            foreach (TextButton button in buttons)
            {
                IBTnode original;
                if (!cache.OriginalMenus.TryGetValue(button, out original))
                {
                    original = button.Child;
                    cache.OriginalMenus[button] = original;
                }
                if (!enabled)
                {
                    if (!ReferenceEquals(button.Child, original)) button.SetChild(original);
                    SquareGridSelector oldGrid;
                    if (cache.Grids.TryGetValue(button, out oldGrid))
                    {
                        drawables.Remove(oldGrid);
                        cache.Grids.Remove(button);
                    }
                    continue;
                }
                if (button.Child is SquareGridSelector) continue;
                List<GridMenuItem> cards = CollectCards(original);
                if (cards.Count == 0) continue;
                SquareGridSelector grid = new SquareGridSelector(cards);
                button.SetChild(grid);
                cache.Grids[button] = grid;
                int layer = drawables.IndexOf(original);
                drawables.Insert(layer < 0 ? 0 : layer, grid);
            }
        }

        private static FactoryCache GetCache(object factory)
        {
            for (int i = Caches.Count - 1; i >= 0; i--)
            {
                object current = Caches[i].Factory.Target;
                if (current == null)
                {
                    Caches.RemoveAt(i);
                    continue;
                }
                if (ReferenceEquals(current, factory)) return Caches[i];
            }
            FactoryCache cache = new FactoryCache { Factory = new WeakReference(factory) };
            Caches.Add(cache);
            return cache;
        }

        private static List<TextButton> FindWorkshopGridButtons(IList drawables)
        {
            List<TextButton> result = new List<TextButton>();
            HashSet<TextButton> seen = new HashSet<TextButton>();
            foreach (object drawable in drawables)
            {
                MenuSelector menu = drawable as MenuSelector;
                if (menu == null) continue;
                bool workshopRoot = Array.Exists(
                    menu.Children,
                    delegate(IBTnode child) { return child is LinkButton; })
                    && Array.Exists(
                        menu.Children,
                        delegate(IBTnode child)
                        {
                            TextButton category = child as TextButton;
                            return category != null && category.Child is MenuSelectorClosePopup;
                        });
                foreach (IBTnode child in menu.Children)
                {
                    TextButton button = child as TextButton;
                    if (button == null) continue;
                    bool workshopCategory = workshopRoot && button.Child is MenuSelectorClosePopup;
                    bool pauseMods = string.Equals(
                        button.Text,
                        "Mods",
                        StringComparison.OrdinalIgnoreCase);
                    if ((workshopCategory || pauseMods) && seen.Add(button)) result.Add(button);
                }
            }
            return result;
        }

        private static List<GridMenuItem> CollectCards(IBTnode root)
        {
            List<GridMenuItem> result = new List<GridMenuItem>();
            HashSet<IBTnode> visited = new HashSet<IBTnode>();
            CollectCards(root, result, visited);
            return result;
        }

        private static void CollectCards(
            IBTnode node,
            List<GridMenuItem> result,
            HashSet<IBTnode> visited)
        {
            MenuSelector menu = node as MenuSelector;
            if (menu == null || !visited.Add(menu)) return;
            bool detailed = Array.Exists(
                menu.Children,
                delegate(IBTnode child) { return child is DoubleTextInfoButton; });
            foreach (IBTnode child in menu.Children)
            {
                DoubleTextInfoButton details = child as DoubleTextInfoButton;
                if (details != null)
                {
                    string label = details.Texts != null && details.Texts.Length > 0
                        ? NativeMenuText.Original(details.Texts[0])
                        : "Item";
                    var metadata = new List<string>();
                    if (details.Texts != null)
                        for (int i = 1; i < details.Texts.Length; i++)
                            if (details.Texts[i] != null) metadata.Add(NativeMenuText.Original(details.Texts[i]));
                    result.Add(new GridMenuItem(
                        label,
                        details,
                        GridSubtitle.ForText(string.Join("\n", metadata))));
                    continue;
                }
                TextButton text = child as TextButton;
                if (text == null || IsBackButton(text)) continue;
                if (detailed) CollectCards(text.Child, result, visited);
                else result.Add(new GridMenuItem(text.Text, text));
            }
        }

        private static bool IsBackButton(TextButton button)
        {
            return button.Child == null
                || button.Child.GetType().FullName == "JumpKing.PauseMenu.BT.MenuSelectorBack";
        }
    }
}
