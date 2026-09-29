using System;
using System.Collections.Generic;
using BehaviorTree;
using JumpKing;
using JumpKing.Controller;
using JumpKing.PauseMenu;
using JumpKing.Util;
using Microsoft.Xna.Framework;

namespace JKRuntime.UI
{
    internal sealed class DebugActionsPageNode : IBTnode, JumpKing.Util.IDrawable
    {
        private readonly IList<UiDebugActionDefinition> actions;
        private readonly UiFrame frame = new UiFrame(new Rectangle(58, 34, 364, 292));
        private readonly HashSet<string> reportedAvailabilityFailures =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private int index;
        private readonly UiListViewport viewport = new UiListViewport();
        private bool entering = true;

        private DebugActionsPageNode()
        {
            actions = UIApi.GetDebugActions();
        }

        internal static DebugActionsPageNode Create(object factory, GuiFormat format)
        {
            DebugActionsPageNode page = new DebugActionsPageNode();
            VanillaMenuAdapter.AddDrawable(factory, page);
            return page;
        }

        protected override BTresult MyRun(TickData data)
        {
            PadInstance main = ControllerManager.instance.GetMain();
            if (entering)
            {
                UiInputRouter.Consume();
                if (main.GetPad().GetPressedButtons().Length == 0) entering = false;
                return BTresult.Running;
            }
            UiAction action = UiPointer.Read(this, UiInputRouter.Read(
                ControllerManager.instance.MenuController.GetPadState(),
                false)).Action;
            if (action != UiAction.None) UiInputRouter.Consume();
            if (action == UiAction.Cancel)
            {
                entering = true;
                UiSounds.Play(UiSound.Back);
                return BTresult.Success;
            }
            if (actions.Count == 0) return BTresult.Running;
            if (action == UiAction.Up)
            {
                UiSounds.Select(ref index, (index + actions.Count - 1) % actions.Count);
                viewport.FollowSelection(index, actions.Count, 7);
            }
            else if (action == UiAction.Down)
            {
                UiSounds.Select(ref index, (index + 1) % actions.Count);
                viewport.FollowSelection(index, actions.Count, 7);
            }
            else if (action == UiAction.Confirm || action == UiAction.Left || action == UiAction.Right)
            {
                UiDebugActionDefinition selected = actions[index];
                try
                {
                    InvokeAction(selected, action);
                }
                catch (Exception error)
                {
                    Console.WriteLine("[JK Runtime UI] Debug action " + selected.Id + " failed: " + error.Message);
                    UiSounds.Play(UiSound.Error);
                }
            }
            return BTresult.Running;
        }

        public void Draw()
        {
            if (last_result != BTresult.Running) return;
            UiPointer.BeginSurface(this);
            frame.Draw();
            UiTheme.TextLine("MODS DEBUG ACTIONS", new Vector2(74, 49), UiTheme.Text, false);
            if (actions.Count == 0)
            {
                UiTheme.TextLine("NO ACTIONS REGISTERED", new Vector2(76, 157), UiTheme.Muted, true);
            }
            else
            {
                const int visible = 7;
                int first = viewport.FirstVisible(actions.Count, visible, index);
                int last = Math.Min(actions.Count, first + visible);
                int y = 86;
                for (int i = first; i < last; i++)
                {
                    UiDebugActionDefinition action = actions[i];
                    bool available = SafeAvailable(action);
                    Rectangle row = new Rectangle(72, y - 3, 336, 27);
                    int hoverIndex = i;
                    UiPointer.ActionRegion(row, UiAction.Confirm, () => UiSounds.Select(ref index, hoverIndex));
                    if (i == index) UiTheme.Panel(row, new Color(29, 36, 38), UiTheme.Gold);
                    UiTheme.TextLine(UiTheme.FitText(action.Group.ToUpperInvariant(), 105, true), new Vector2(80, y + 2), UiTheme.Gold, true);
                    UiTheme.TextLine(UiTheme.FitText(SafeLabel(action), action.Adjust == null ? 205 : 169, true), new Vector2(190, y + 2), available ? UiTheme.Text : UiTheme.Disabled, true);
                    if (action.Adjust != null)
                    {
                        UiTheme.TextLine("<", new Vector2(366, y + 2), available ? UiTheme.Gold : UiTheme.Disabled, true);
                        UiTheme.TextLine(">", new Vector2(390, y + 2), available ? UiTheme.Gold : UiTheme.Disabled, true);
                        UiPointer.ActionRegion(new Rectangle(360, y - 3, 22, 27), UiAction.Left, () => UiSounds.Select(ref index, hoverIndex));
                        UiPointer.ActionRegion(new Rectangle(384, y - 3, 22, 27), UiAction.Right, () => UiSounds.Select(ref index, hoverIndex));
                    }
                    y += 29;
                }
            }
            UiPointer.ScrollRegion(new Rectangle(72, 80, 336, 205), delta => UiSounds.Select(ref index, viewport.Scroll(-delta, actions.Count, 7, index)));
            UiDebugActionDefinition current = actions.Count == 0 ? null : actions[index];
            if (current != null && current.Adjust != null)
                UiTheme.CommandBar(UiTheme.FooterRow(frame.Bounds),
                    new UiCommand(UiInputHints.Key(UiAction.Left) + "/" + UiInputHints.Key(UiAction.Right), "SET"),
                    UiInputHints.Command(UiAction.Confirm, current.ConfirmLabel), UiInputHints.Command(UiAction.Cancel, "BACK"));
            else UiTheme.CommandBar(UiTheme.FooterRow(frame.Bounds),
                UiInputHints.Command(UiAction.Confirm, current == null ? "RUN" : current.ConfirmLabel),
                UiInputHints.Command(UiAction.Cancel, "BACK"));
        }

        private string SafeLabel(UiDebugActionDefinition action)
        {
            try { return action.GetLabel() ?? action.Label; }
            catch (Exception error)
            {
                if (reportedAvailabilityFailures.Add("label:" + action.Id))
                    Console.WriteLine("[JK Runtime UI] Debug label " + action.Id + " failed: " + error.Message);
                return action.Label;
            }
        }

        internal static bool InvokeAction(UiDebugActionDefinition action, UiAction input)
        {
            if (input != UiAction.Confirm && input != UiAction.Left && input != UiAction.Right) return false;
            if (!action.IsAvailable()) return false;
            if (input == UiAction.Confirm) { action.Execute(); UiSounds.Play(UiSound.Confirm); }
            else if (action.Adjust != null && (input == UiAction.Left || input == UiAction.Right))
            { action.Adjust(input == UiAction.Left ? -1 : 1); UiSounds.Play(UiSound.Change); }
            else return false;
            return true;
        }

        private bool SafeAvailable(UiDebugActionDefinition action)
        {
            try { return action.IsAvailable(); }
            catch (Exception error)
            {
                if (reportedAvailabilityFailures.Add(action.Id))
                    Console.WriteLine("[JK Runtime UI] Debug action " + action.Id
                        + " availability failed: " + error.Message);
                return false;
            }
        }
    }
}
