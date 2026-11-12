using System;
using System.Collections.Generic;
using BehaviorTree;
using JumpKing;
using JumpKing.Controller;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using JumpKing.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JKRuntime.UI
{
    internal sealed class PinnedSettingsBrowser : MenuSelector
    {
        private sealed class ModGroup
        {
            internal string Name;
            internal readonly List<ModSettingDescriptor> Settings =
                new List<ModSettingDescriptor>();
        }

        private const int Columns = 2;
        private const int Rows = 4;
        private const int PageSize = Columns * Rows;
        private readonly List<ModGroup> groups = new List<ModGroup>();
        private readonly GuiFrame frame = new GuiFrame(new Rectangle(15, 10, 450, 340));
        private int pane;
        private int modIndex;
        private int settingIndex;

        internal PinnedSettingsBrowser(IList<ModSettingDescriptor> descriptors)
            : base(new GuiFormat())
        {
            Dictionary<string, ModGroup> byName =
                new Dictionary<string, ModGroup>(StringComparer.OrdinalIgnoreCase);
            foreach (ModSettingDescriptor descriptor in descriptors)
            {
                ModGroup group;
                if (!byName.TryGetValue(descriptor.Info.ModName, out group))
                {
                    group = new ModGroup { Name = descriptor.Info.ModName };
                    byName[group.Name] = group;
                    groups.Add(group);
                }
                group.Settings.Add(descriptor);
            }
            groups.Sort(delegate(ModGroup left, ModGroup right)
            {
                return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        protected override BTresult MyRun(TickData data)
        {
            PadState input = ControllerManager.instance.MenuController.GetPadState();
            input = UiInputRouter.ToPadState(UiPointer.Read(this, UiInputRouter.Read(input, false)));
            if (input.cancel || input.pause)
            {
                ControllerManager.instance.MenuController.ConsumePadPresses();
                if (pane == 1)
                {
                    pane = 0;
                    UiSounds.Play(UiSound.Back);
                    return BTresult.Running;
                }
                UiSounds.Play(UiSound.Back); return BTresult.Failure;
            }
            if (groups.Count == 0) return BTresult.Running;
            int previousMod = modIndex;
            int previousSetting = settingIndex;
            if (pane == 0)
            {
                if (input.left) modIndex = GridNavigation.MoveHorizontal(modIndex, groups.Count, -1, Columns);
                else if (input.right) modIndex = GridNavigation.MoveHorizontal(modIndex, groups.Count, 1, Columns);
                else if (input.up) modIndex = GridNavigation.MoveVertical(modIndex, groups.Count, -1, Columns);
                else if (input.down) modIndex = GridNavigation.MoveVertical(modIndex, groups.Count, 1, Columns);
                else if (input.confirm)
                {
                    pane = 1;
                    settingIndex = Math.Min(settingIndex, Math.Max(0, CurrentSettings.Count - 1));
                    ControllerManager.instance.MenuController.ConsumePadPresses();
                    SelectSound();
                }
                if (modIndex != previousMod)
                {
                    settingIndex = 0;
                    MoveSound();
                }
            }
            else
            {
                int count = CurrentSettings.Count;
                if (count == 0)
                {
                    pane = 0;
                    return BTresult.Running;
                }
                if (input.left) settingIndex = GridNavigation.MoveHorizontal(settingIndex, count, -1, Columns);
                else if (input.right) settingIndex = GridNavigation.MoveHorizontal(settingIndex, count, 1, Columns);
                else if (input.up) settingIndex = GridNavigation.MoveVertical(settingIndex, count, -1, Columns);
                else if (input.down) settingIndex = GridNavigation.MoveVertical(settingIndex, count, 1, Columns);
                else if (input.boots)
                {
                    ModSettingDescriptor setting = CurrentSettings[settingIndex];
                    ControllerManager.instance.MenuController.ConsumePadPresses();
                    if (setting.Info.IsToggle)
                    {
                        ModSettingsCatalog.SetBindable(
                            setting.Info.Id,
                            !setting.Info.IsBindable);
                        SelectSound();
                    }
                    else UiSounds.Play(UiSound.Error);
                }
                else if (input.confirm)
                {
                    ModSettingDescriptor setting = CurrentSettings[settingIndex];
                    ModSettingsCatalog.SetPinned(
                        setting.Info.Id,
                        !SettingsStore.IsSettingPinned(setting.Info.Id));
                    ControllerManager.instance.MenuController.ConsumePadPresses();
                    SelectSound();
                }
                if (settingIndex != previousSetting) MoveSound();
            }
            return BTresult.Running;
        }

        protected override void OnNewRun()
        {
            pane = 0;
            modIndex = 0;
            settingIndex = 0;
            ControllerManager.instance.MenuController.ConsumePadPresses();
        }

        protected override void ResumeRun()
        {
            ControllerManager.instance.MenuController.ConsumePadPresses();
        }

        public override void Draw()
        {
            UiPointer.BeginSurface(this);
            UiPointer.ScrollRegion(new Rectangle(32, 43, 158, 247), delta => Hover(0, Math.Max(0, Math.Min(groups.Count - 1, modIndex - delta * Columns))));
            UiPointer.ScrollRegion(new Rectangle(206, 43, 242, 247), delta => Hover(1, Math.Max(0, Math.Min(CurrentSettings.Count - 1, settingIndex - delta * Columns))));
            if (last_result != BTresult.Running) return;
            frame.Draw();
            UiTheme.TextLine("MODS", new Vector2(32, 23), pane == 0 ? UiTheme.Gold : UiTheme.Muted, true);
            UiTheme.TextLine("SETTINGS", new Vector2(206, 23), pane == 1 ? UiTheme.Gold : UiTheme.Muted, true);
            DrawMods();
            DrawSettings();
            DrawCommands();
        }

        private IList<ModSettingDescriptor> CurrentSettings
        {
            get
            {
                return groups.Count == 0
                    ? (IList<ModSettingDescriptor>)new ModSettingDescriptor[0]
                    : groups[modIndex].Settings;
            }
        }

        private void Hover(int nextPane, int nextIndex)
        {
            if (pane == nextPane && (nextPane == 0 ? modIndex : settingIndex) == nextIndex) return;
            pane = nextPane;
            if (pane == 0) { if (modIndex != nextIndex) settingIndex = 0; modIndex = nextIndex; }
            else settingIndex = nextIndex;
            MoveSound();
        }

        private void DrawMods()
        {
            if (groups.Count == 0)
            {
                UiTheme.WrappedText(
                    "No mod settings found.",
                    new Rectangle(32, 48, 158, 80),
                    UiTheme.Muted);
                return;
            }
            int page = modIndex / PageSize;
            int first = page * PageSize;
            int count = Math.Min(PageSize, groups.Count - first);
            for (int visible = 0; visible < count; visible++)
            {
                int index = first + visible;
                Rectangle card = CardBounds(32, visible, 75);
                int selectedMod = index;
                UiPointer.ActionRegion(card, UiAction.Confirm, () => Hover(0, selectedMod));
                bool selected = pane == 0 && index == modIndex;
                UiTheme.Panel(
                    card,
                    selected ? new Color(38, 33, 18) : UiTheme.PanelFill,
                    selected ? UiTheme.Gold : UiTheme.Border);
                DrawCentered(groups[index].Name, card, selected ? UiTheme.Gold : UiTheme.Text);
            }
            DrawPage(page, groups.Count, new Rectangle(32, 290, 158, 14));
        }

        private void DrawSettings()
        {
            IList<ModSettingDescriptor> settings = CurrentSettings;
            if (settings.Count == 0)
            {
                UiTheme.WrappedText(
                    "This mod has no pinnable settings.",
                    new Rectangle(206, 48, 242, 80),
                    UiTheme.Muted);
                return;
            }
            int page = settingIndex / PageSize;
            int first = page * PageSize;
            int count = Math.Min(PageSize, settings.Count - first);
            for (int visible = 0; visible < count; visible++)
            {
                int index = first + visible;
                Rectangle card = CardBounds(206, visible, 117);
                int selectedSetting = index;
                UiPointer.ActionRegion(card, UiAction.Confirm, () => Hover(1, selectedSetting));
                bool selected = pane == 1 && index == settingIndex;
                bool pinned = SettingsStore.IsSettingPinned(settings[index].Info.Id);
                bool bindable = settings[index].Info.IsBindable;
                UiTheme.Panel(
                    card,
                    selected ? new Color(15, 42, 47) : UiTheme.PanelFill,
                    selected ? UiTheme.Cyan : UiTheme.Border);
                DrawCentered(
                    settings[index].Info.Label,
                    new Rectangle(card.X + 5, card.Y + 4, card.Width - 28, card.Height - 8),
                    selected ? UiTheme.Cyan : UiTheme.Text);
                (pinned
                    ? Game1.instance.contentManager.gui.CheckBoxTrue
                    : Game1.instance.contentManager.gui.CheckBoxFalse).Draw(
                        new Vector2(card.Right - 18, card.Y + 7));
                if (bindable)
                    UiTheme.DrawText(
                        Game1.instance.contentManager.font.LocationFont,
                        "BIND",
                        new Vector2(card.X + 5, card.Bottom - 12),
                        selected ? UiTheme.Cyan : UiTheme.Muted);
            }
            DrawPage(page, settings.Count, new Rectangle(206, 290, 242, 14));
        }

        private static Rectangle CardBounds(int x, int visible, int width)
        {
            int row = visible / Columns;
            int column = visible % Columns;
            return new Rectangle(x + column * (width + 8), 43 + row * 61, width, 53);
        }

        private static void DrawCentered(string text, Rectangle bounds, Color color)
        {
            SpriteFont font = Game1.instance.contentManager.font.MenuFontSmall;
            string[] lines = SquareGridSelector.SplitLabel(text, bounds.Width - 8, font);
            int lineHeight = font.LineSpacing;
            int capacity = Math.Max(1, bounds.Height / lineHeight);
            if (lines.Length > capacity) { Array.Resize(ref lines, capacity); lines[capacity - 1] = UiTheme.FitText(lines[capacity - 1] + "...", bounds.Width - 8, true); }
            int y = bounds.Y + (bounds.Height - lines.Length * lineHeight) / 2;
            foreach (string line in lines)
            {
                float width = font.MeasureString(line).X;
                UiTheme.DrawText(
                    font,
                    line,
                    new Vector2(bounds.X + (bounds.Width - width) / 2f, y),
                    color);
                y += lineHeight;
            }
        }

        private static void DrawPage(int page, int count, Rectangle bounds)
        {
            int pages = (count + PageSize - 1) / PageSize;
            if (pages <= 1) return;
            string text = (page + 1) + "/" + pages;
            SpriteFont font = Game1.instance.contentManager.font.LocationFont;
            float width = font.MeasureString(text).X;
            UiTheme.DrawText(
                font,
                text,
                new Vector2(bounds.Right - width, bounds.Y),
                UiTheme.Muted);
        }

        private void DrawCommands()
        {
            Rectangle bounds = UiTheme.FooterRow(frame.GetBounds());
            if (pane == 0)
            {
                UiTheme.CommandBar(
                    bounds,
                    UiInputHints.Command(JKpadButtons.Confirm, "Settings"),
                    UiInputHints.Command(UiAction.Cancel, "Back"));
                return;
            }
            ModSettingDescriptor setting = CurrentSettings.Count == 0
                ? null
                : CurrentSettings[settingIndex];
            if (setting == null || !setting.Info.IsToggle)
            {
                UiTheme.CommandBar(
                    bounds,
                    UiInputHints.Command(JKpadButtons.Confirm, setting != null && setting.Info.IsPinned ? "Unpin" : "Pin"),
                    UiInputHints.Command(UiAction.Cancel, "Mods"));
                return;
            }
            UiTheme.CommandBar(
                bounds,
                UiInputHints.Command(JKpadButtons.Confirm, setting.Info.IsPinned ? "Unpin" : "Pin"),
                UiInputHints.Command(UiAction.Secondary, setting.Info.IsBindable ? "Stop binding" : "Make bindable"),
                UiInputHints.Command(UiAction.Cancel, "Mods"));
        }

        private static void MoveSound()
        {
            UiSounds.Play(UiSound.Move);
        }

        private static void SelectSound()
        {
            UiSounds.Play(UiSound.Confirm);
        }
    }
}
