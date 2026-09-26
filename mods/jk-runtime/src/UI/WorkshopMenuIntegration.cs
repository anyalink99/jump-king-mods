using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BehaviorTree;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using LanguageJK;

namespace JKRuntime.UI
{
    internal static class WorkshopMenuIntegration
    {
        private static readonly ConditionalWeakTable<MenuSelector, List<IBTnode>> Injected =
            new ConditionalWeakTable<MenuSelector, List<IBTnode>>();

        internal static void Apply(object factory)
        {
            var drawables = VanillaMenuAdapter.GetDrawables(factory);
            if (drawables == null) return;
            // CreateWorkshop is identified by its native Browse link, including
            // empty catalogs and localized menus. Snapshot before page factories add drawables.
            foreach (var menu in drawables.Cast<object>().OfType<MenuSelector>().ToArray())
            {
                if (!menu.Children.OfType<LinkButton>().Any(x => string.Equals(x.Text, language.WORKSHOP_BROWSE, StringComparison.Ordinal))) continue;
                var injected = Injected.GetValue(menu, key => new List<IBTnode>());
                var children = VanillaMenuAdapter.ChildrenForEdit(menu).Where(x => !injected.Contains(x)).ToList();
                injected.Clear();
                int position = children.FindIndex(x => x is IBTMenuDecorator && ((IBTMenuDecorator)x).Child is MenuSelectorBack);
                if (position < 0) position = children.Count;
                foreach (var definition in UIApi.GetMainMenuItems().Where(x => x.Placement == UiMainMenuPlacement.Workshop))
                {
                    var action = definition.CreateNode(factory);
                    if (action == null) continue;
                    var button = new TextButton(definition.Label, action);
                    children.Insert(position++, button); injected.Add(button);
                }
                VanillaMenuAdapter.SetChildren(menu, children.ToArray());
            }
        }
    }
}
