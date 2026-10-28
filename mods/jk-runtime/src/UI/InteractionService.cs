using System;
using System.Collections.Generic;
using EntityComponent;
using JumpKing;
using JumpKing.Controller;
using JumpKing.Util.Tags;
using Microsoft.Xna.Framework;

namespace JKRuntime.UI
{
    internal sealed class InteractionService : Entity, IForeground
    {
        private bool wasHeld;
        private WorldInteraction active;
        private readonly Dictionary<string, bool> actionHeld =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> reportedInputFailures =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> reportedInteractionFailures =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> reportedAvailabilityFailures =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly UiFrame promptFrame = new UiFrame(new Rectangle(0, 0, 1, 1));
        private UiInputActionDefinition pendingSingle;
        private int pendingFrames;

        internal InteractionService()
        {
            GoToFront();
        }

        protected override void Update(float delta)
        {
            BaseBindings.RefreshDevices();
            AutomaticBindings.RefreshDevice();
            WorldInteractionContext.BeginFrame();
            active = null;
            if (!UIApi.IsOpen && !UiInputRouter.IsGamePaused())
            {
                Evaluate(UIApi.GetGlobalInteractionSnapshot());
                Evaluate(UIApi.GetScreenInteractionSnapshot(Camera.CurrentScreenIndex1));
            }
            bool held = IsInteractHeld();
            UiInputActionDefinition inputAction = FindInputAction();
            if (active != null && held && !wasHeld)
            {
                ClearPendingAction();
                try
                {
                    active.Activate();
                    UiSounds.Play(UiSound.Confirm);
                }
                catch (Exception error)
                {
                    Console.WriteLine("[JK Runtime UI] Interaction " + active.Id
                        + " activation failed: " + error.Message);
                    UiSounds.Play(UiSound.Error);
                }
            }
            else if (inputAction != null)
            {
                try { inputAction.Execute(); }
                catch (Exception error)
                {
                    Console.WriteLine("[JK Runtime UI] Input action " + inputAction.Id + " failed: " + error.Message);
                }
            }
            wasHeld = held;
            SynchronizeActionState();
        }

        private void Evaluate(WorldInteraction[] interactions)
        {
            foreach (WorldInteraction interaction in interactions)
            {
                try
                {
                    if (interaction.IsAvailable()
                        && (active == null || interaction.Priority > active.Priority))
                        active = interaction;
                }
                catch (Exception error)
                {
                    if (reportedInteractionFailures.Add(interaction.Id))
                        Console.WriteLine("[JK Runtime UI] Interaction " + interaction.Id
                            + " availability failed: " + error.Message);
                }
            }
        }

        public void ForegroundDraw()
        {
            if (active == null || UIApi.IsOpen || UiInputRouter.IsGamePaused()) return;
            string key = GetInteractLabel();
            string label = UiTheme.FitText(active.Label.ToUpperInvariant(), 116, true);
            int keyWidth = Math.Min(58, Math.Max(30,
                (int)Game1.instance.contentManager.font.LocationFont.MeasureString(UiTheme.NormalizeKey(key)).X + 10));
            int labelWidth = (int)Game1.instance.contentManager.font.MenuFontSmall.MeasureString(label).X;
            Rectangle box = new Rectangle(240 - (keyWidth + labelWidth + 23) / 2, 323, keyWidth + labelWidth + 23, 29);
            promptFrame.Bounds = box;
            promptFrame.Draw();
            UiTheme.Keycap(key, new Rectangle(box.X + 5, box.Y + 3, keyWidth, 23), false);
            UiTheme.TextLine(label, new Vector2(box.X + keyWidth + 12, box.Y + 9), UiTheme.Text, true);
        }

        private static bool IsInteractHeld()
        {
            SettingsStore.EnsureLoaded();
            PadInstance main = ControllerManager.instance.GetMain();
            if (!main.IsValid || !main.IsConnected) return false;
            int[] pressed = main.GetPad().GetPressedButtons();
            return IsAnyChordHeld(SettingsStore.GetInteractChords(), pressed);
        }

        private static string GetInteractLabel()
        {
            PadInstance main = ControllerManager.instance.GetMain();
            UiChord[] chords = SettingsStore.GetInteractChords();
            if (!main.IsValid || chords.Length == 0) return "E";
            return FormatChord(main, chords[0]);
        }

        private UiInputActionDefinition FindInputAction()
        {
            if (TextInputCapture.Active || UIApi.IsOpen || UiInputRouter.IsGamePaused())
            {
                ClearPendingAction();
                return null;
            }
            PadInstance main = ControllerManager.instance.GetMain();
            if (!main.IsValid || !main.IsConnected) return null;
            int[] pressed = main.GetPad().GetPressedButtons();
            UiInputActionDefinition winner = null;
            int winnerSize = 0;
            foreach (UiInputActionDefinition definition in UIApi.GetInputActionSnapshot())
            {
                int matchSize = SafeMatchSize(definition, pressed);
                bool held = matchSize > 0;
                bool previous;
                actionHeld.TryGetValue(definition.Id, out previous);
                if (!held || previous || !SafeAvailable(definition)) continue;
                if (winner == null
                    || matchSize > winnerSize
                    || (matchSize == winnerSize && definition.Priority > winner.Priority))
                {
                    winner = definition;
                    winnerSize = matchSize;
                }
            }
            if (winner != null && winnerSize > 1)
            {
                ClearPendingAction();
                return winner;
            }
            if (pendingSingle != null)
            {
                int heldSize = SafeMatchSize(pendingSingle, pressed);
                pendingFrames--;
                if (heldSize == 0 || pendingFrames <= 0)
                {
                    UiInputActionDefinition delayed = pendingSingle;
                    ClearPendingAction();
                    return SafeAvailable(delayed) ? delayed : null;
                }
                return null;
            }
            if (winner != null && winnerSize == 1
                && HasLongerChordPrefix(winner, pressed))
            {
                pendingSingle = winner;
                pendingFrames = 4;
                return null;
            }
            return winner;
        }

        private bool HasLongerChordPrefix(
            UiInputActionDefinition single,
            int[] pressed)
        {
            int button = MatchingSingleButton(single, pressed);
            if (button < 0) return false;
            foreach (UiInputActionDefinition definition in UIApi.GetInputActionSnapshot())
            {
                foreach (UiChord chord in SafeChords(definition))
                {
                    int[] buttons = chord == null ? new int[0] : chord.Buttons;
                    if (buttons.Length == 2 && Array.IndexOf(buttons, button) >= 0)
                        return true;
                }
            }
            return false;
        }

        private UiChord[] SafeChords(UiInputActionDefinition definition)
        {
            try { return definition.GetChords() ?? new UiChord[0]; }
            catch (Exception error)
            {
                if (reportedInputFailures.Add(definition.Id))
                    Console.WriteLine("[JK Runtime UI] Input bindings " + definition.Id
                        + " failed: " + error.Message);
                return new UiChord[0];
            }
        }

        private int MatchingSingleButton(
            UiInputActionDefinition definition,
            int[] pressed)
        {
            foreach (UiChord chord in SafeChords(definition))
            {
                int[] buttons = chord == null ? new int[0] : chord.Buttons;
                if (buttons.Length == 1 && Array.IndexOf(pressed, buttons[0]) >= 0)
                    return buttons[0];
            }
            return -1;
        }

        private void ClearPendingAction()
        {
            pendingSingle = null;
            pendingFrames = 0;
        }

        private void SynchronizeActionState()
        {
            PadInstance main = ControllerManager.instance.GetMain();
            int[] pressed = main.IsValid && main.IsConnected
                ? main.GetPad().GetPressedButtons()
                : new int[0];
            HashSet<string> current = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (UiInputActionDefinition definition in UIApi.GetInputActionSnapshot())
            {
                current.Add(definition.Id);
                actionHeld[definition.Id] = SafeMatchSize(definition, pressed) > 0;
            }
            List<string> stale = new List<string>();
            foreach (string id in actionHeld.Keys)
                if (!current.Contains(id)) stale.Add(id);
            foreach (string id in stale) actionHeld.Remove(id);
        }

        private static int GetMatchSize(UiInputActionDefinition definition, int[] pressed)
        {
            int result = 0;
            foreach (UiChord chord in definition.GetChords() ?? new UiChord[0])
            {
                if (chord == null || !chord.IsHeld(pressed)) continue;
                result = Math.Max(result, chord.Buttons.Length);
            }
            return result;
        }

        private int SafeMatchSize(UiInputActionDefinition definition, int[] pressed)
        {
            try { return GetMatchSize(definition, pressed); }
            catch (Exception error)
            {
                if (reportedInputFailures.Add(definition.Id))
                    Console.WriteLine("[JK Runtime UI] Input bindings " + definition.Id
                        + " failed: " + error.Message);
                return 0;
            }
        }

        private static bool IsAnyChordHeld(UiChord[] chords, int[] pressed)
        {
            foreach (UiChord chord in chords ?? new UiChord[0])
                if (chord != null && chord.IsHeld(pressed)) return true;
            return false;
        }

        private static string FormatChord(PadInstance main, UiChord chord)
        {
            int[] buttons = chord == null ? new int[0] : chord.Buttons;
            string result = string.Empty;
            foreach (int button in buttons)
            {
                string label = UiTheme.NormalizeKey(main.GetPad().ButtonToString(button));
                result = result.Length == 0 ? label : result + "+" + label;
            }
            return result.Length == 0 ? "-" : result;
        }

        private bool SafeAvailable(UiInputActionDefinition definition)
        {
            try { return definition.IsAvailable(); }
            catch (Exception error)
            {
                if (reportedAvailabilityFailures.Add(definition.Id))
                    Console.WriteLine("[JK Runtime UI] Input action " + definition.Id
                        + " availability failed: " + error.Message);
                return false;
            }
        }
    }
}
