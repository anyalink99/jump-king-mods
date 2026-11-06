using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BehaviorTree;
using JumpKing.PauseMenu.BT;
using LanguageJK;

namespace JKRuntime.UI
{
    internal static class RootPauseMenuIntegration
    {
        private sealed class State
        {
            internal readonly List<IBTnode> Injected = new List<IBTnode>();
        }

        private static readonly ConditionalWeakTable<object, State> States =
            new ConditionalWeakTable<object, State>();

        internal static void Apply(object factory)
        {
            MenuSelector menu;
            if (!VanillaMenuAdapter.TryGetPauseMainMenu(factory, out menu)) return;
            State state = States.GetValue(
                factory,
                delegate { return new State(); });
            List<IBTnode> children = new List<IBTnode>();
            foreach (IBTnode child in VanillaMenuAdapter.ChildrenForEdit(menu))
                if (!state.Injected.Contains(child)) children.Add(child);
            state.Injected.Clear();

            int saveAndExit = FindSaveAndExit(children);
            if (saveAndExit < 0)
            {
                VanillaMenuAdapter.SetChildren(menu, children.ToArray());
                return;
            }
            foreach (UiPauseMenuItemDefinition definition in UIApi.GetPauseMenuItems())
            {
                if (definition.Placement != UiPauseMenuPlacement.BeforeSaveAndExit)
                    continue;
                IBTnode action = definition.CreateNode(factory);
                if (action == null) continue;
                UiMenuFeedbackActionNode feedback =
                    action as UiMenuFeedbackActionNode;
                TextButton button = feedback == null
                    ? new TextButton(definition.Label, action)
                    : new UiFeedbackTextButton(definition.Label, feedback);
                children.Insert(saveAndExit++, button);
                state.Injected.Add(button);
            }
            VanillaMenuAdapter.SetChildren(menu, children.ToArray());
        }

        private static int FindSaveAndExit(IList<IBTnode> children)
        {
            for (int index = 0; index < children.Count; index++)
            {
                TextButton button = children[index] as TextButton;
                if (button != null
                    && string.Equals(
                        button.Text,
                        language.PAUSEMANAGER_SAVEEXIT,
                        StringComparison.Ordinal))
                {
                    return index;
                }
            }
            return -1;
        }
    }
}
