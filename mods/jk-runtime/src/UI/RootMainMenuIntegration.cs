using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BehaviorTree;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using LanguageJK;

namespace JKRuntime.UI
{
    internal static class RootMainMenuIntegration
    {
        private sealed class State
        {
            internal readonly List<IBTnode> Injected = new List<IBTnode>();
            internal bool HasOriginalLayout;
            internal float OriginalAnchorY;
            internal int OriginalAnchorBoundsY;
            internal int OriginalBottom;
        }

        private static readonly ConditionalWeakTable<object, State> States =
            new ConditionalWeakTable<object, State>();

        internal static void Apply(object factory)
        {
            MenuSelector menu = VanillaMenuAdapter.FindRootMainMenu(factory);
            if (menu == null) return;
            State state = States.GetValue(
                factory,
                delegate { return new State(); });
            GuiFormat format = VanillaMenuAdapter.GetFormat(menu);
            if (!state.HasOriginalLayout)
            {
                state.HasOriginalLayout = true;
                state.OriginalAnchorY = format.anchor.Y;
                state.OriginalAnchorBoundsY = format.anchor_bounds.Y;
                state.OriginalBottom = menu.GetBounds().Bottom;
            }
            List<IBTnode> children = new List<IBTnode>();
            foreach (IBTnode child in VanillaMenuAdapter.ChildrenForEdit(menu))
                if (!state.Injected.Contains(child)) children.Add(child);

            int extras = FindExtras(children);
            if (extras < 0)
            {
                Console.WriteLine(
                    "[JK Runtime UI] Root menu item registration requires the vanilla Extras entry");
                return;
            }

            state.Injected.Clear();
            foreach (UiMainMenuItemDefinition definition in UIApi.GetMainMenuItems())
            {
                if (definition.Placement != UiMainMenuPlacement.BeforeExtras)
                    continue;
                IBTnode action = definition.CreateNode(factory);
                if (action == null) continue;
                TextButton button = new TextButton(definition.Label, action);
                children.Insert(extras++, button);
                state.Injected.Add(button);
            }
            format.anchor.Y = state.OriginalAnchorY;
            format.anchor_bounds.Y = state.OriginalAnchorBoundsY;
            VanillaMenuAdapter.SetFormat(menu, format);
            VanillaMenuAdapter.SetChildren(menu, children.ToArray());
            if (state.Injected.Count == 0) return;

            int overflow = menu.GetBounds().Bottom - state.OriginalBottom;
            if (overflow == 0) return;
            format.anchor_bounds.Y = state.OriginalAnchorBoundsY - overflow;
            VanillaMenuAdapter.SetFormat(menu, format);
            VanillaMenuAdapter.Refresh(menu);
        }

        private static int FindExtras(IList<IBTnode> children)
        {
            for (int index = 0; index < children.Count; index++)
            {
                TextButton button = children[index] as TextButton;
                if (button != null
                    && string.Equals(
                        button.Text,
                        language.GAMETITLESCREEN_EXTRAS,
                        StringComparison.Ordinal))
                {
                    return index;
                }
            }
            return -1;
        }
    }
}
