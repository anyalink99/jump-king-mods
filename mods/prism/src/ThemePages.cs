using System;
using System.Collections.Generic;
using JKRuntime.UI;
using Microsoft.Xna.Framework;

namespace Prism
{
    internal abstract class PrismListPage : ScopedUiPage
    {
        protected readonly UiPageStack Pages;
        protected readonly UiFrame Frame = new UiFrame(new Rectangle(8, 8, 464, 344));
        internal readonly UiList List = new UiList();
        private readonly UiPageCommand[] commands;
        private int revision = -1;
        private string error = "";
        protected abstract string Title { get; }
        protected abstract IEnumerable<UiListItem> Rows();
        protected PrismListPage(UiPageStack pages)
        {
            Pages = pages;
            commands = new[] { new UiPageCommand(UiAction.Confirm, "Select", () => List.Update(new UiInput { Action = UiAction.Confirm })), new UiPageCommand(UiAction.Cancel, "Back", () => WantsClose = true) };
        }
        protected UiListItem Row(string id, string label, string description, Action action)
        {
            return new UiListItem(id, label, delegate {
                try { action(); error = ""; Refresh(); }
                catch (Exception e) { error = e.GetBaseException().Message; UiSounds.Play(UiSound.Error); }
            }, description);
        }
        internal void Refresh() { List.SetItems(Rows()); revision = Settings.Revision; }
        protected override void OpenPage(JKRuntime.RuntimeScope resources) { Refresh(); }
        public override void Update(UiInput input, float delta)
        {
            if (revision != Settings.Revision) Refresh();
            // UiList owns confirmation feedback; the command only supplies its hint.
            if (input.Action == UiAction.Confirm) { List.Update(input); return; }
            foreach (var command in commands) if (command.Handle(input)) return;
            List.Update(input);
        }
        public override void Draw()
        {
            Frame.Draw(); var layout = new UiPageLayout(Frame.Bounds, commands, 3);
            UiTheme.TextLine(Title, layout.Title.Location.ToVector2(), UiTheme.Gold, false);
            List.Draw(layout.Content, "No themes installed", 23);
            UiTheme.WrappedText(error.Length > 0 ? error : List.Description, layout.Status, error.Length > 0 ? UiTheme.Red : UiTheme.Muted);
            layout.DrawCommands();
        }
        protected static string On(bool value) { return value ? "On" : "Off"; }
    }
    internal sealed class ThemesPage : PrismListPage
    {
        internal ThemesPage(UiPageStack pages) : base(pages) { }
        protected override string Title { get { return "PRISM / THEMES"; } }
        protected override IEnumerable<UiListItem> Rows()
        {
            yield return Row("original", "Original map" + (!Settings.Current.Enabled ? "  [Active]" : ""), "Restore the map's artwork, props and music. Theme settings are kept.", () => Settings.Change(p => p.Enabled = false));
            foreach (var theme in ThemeCatalog.All)
            {
                var selected = theme;
                yield return Row(theme.Id, theme.Name + (Settings.Current.Enabled && Settings.Current.SelectedTheme == theme.Id ? "  [Active]" : "") + " >", theme.Description,
                    delegate { Settings.Change(p => { p.SelectedTheme = selected.Id; p.Enabled = true; }); Pages.Push(new ThemePage(Pages, selected)); });
            }
        }
    }
    internal sealed class ThemePage : PrismListPage
    {
        private readonly ThemeDefinition theme;
        internal ThemePage(UiPageStack pages, ThemeDefinition theme) : base(pages) { this.theme = theme; }
        protected override string Title { get { return theme.Name.ToUpperInvariant(); } }
        private ThemePreferences Current { get { return Settings.Current.For(theme.Id); } }
        private void Edit(Action<ThemePreferences> change) { Settings.ChangeTheme(theme.Id, change); }
        protected override IEnumerable<UiListItem> Rows()
        {
            yield return Row("active", "Theme: " + On(Settings.Current.Enabled && Settings.Current.SelectedTheme == theme.Id), "Changes take effect immediately. Turning this off restores the original map.", () => Settings.Change(p => { p.Enabled = !(p.Enabled && p.SelectedTheme == theme.Id); p.SelectedTheme = theme.Id; }));
            yield return Row("disable-props", "Disable props: " + On(Current.DisableProps), "Hide map props, NPCs and Hidden Walls. Keep the King, pickups, UI and map events.", () => Edit(p => p.DisableProps = !p.DisableProps));
            yield return Row("artwork", "Keep map artwork: " + On(Current.OverlayOnly), "Add neon edges to original map layers instead of replacing the scenery.", () => Edit(p => p.OverlayOnly = !p.OverlayOnly));
            yield return Row("reflections", "Reflections: " + On(Current.Reflections), "Reflect the world and King inside glass surfaces.", () => Edit(p => p.Reflections = !p.Reflections));
            yield return Row("gentle", "Gentle motion: " + On(Current.Gentle), "Reduce beat pulses and remove note trails. Wind direction stays visible.", () => Edit(p => p.Gentle = !p.Gentle));
            yield return Row("volume", "Music volume: " + Current.MusicVolume + "% >", "Theme volume, multiplied by the game's master volume and Music preference.", () => Number("MUSIC VOLUME", "Adjust with arrows, wheel or drag. Changes are live.", 0, 100, () => Current.MusicVolume, v => Edit(p => p.MusicVolume = v)));
            yield return Row("glow", "Glow: " + Current.Glow + "% >", "Set the brightness of platform halos. Collision edges remain readable at zero.", () => Number("PLATFORM GLOW", "The solid edge remains visible at every brightness.", 0, 150, () => Current.Glow, v => Edit(p => p.Glow = v)));
            yield return Row("wind", "Wind visibility: " + Current.WindVisibility + "% >", "Make cosmic wind trails quieter or brighter. Their direction follows the actual wind.", () => Number("WIND VISIBILITY", "Trail direction and speed come from the game, not the music.", 50, 150, () => Current.WindVisibility, v => Edit(p => p.WindVisibility = v)));
        }
        private void Number(string title, string description, int min, int max, Func<int> read, Action<int> write)
        { Pages.Push(new ThemeNumberPage(title, description, min, max, read, write)); }
    }
    internal sealed class ThemeNumberPage : ScopedUiPage
    {
        private readonly UiFrame frame = new UiFrame(new Rectangle(20, 62, 440, 236));
        internal readonly UiNumberControl Number;
        private readonly UiPageCommand[] commands;
        private readonly string title, description;
        private string error = "";
        internal ThemeNumberPage(string title, string description, int min, int max, Func<int> read, Action<int> write)
        {
            this.title = title; this.description = description;
            Number = new UiNumberControl(min, max, 1, () => read(), value => {
                try { write((int)value); error = ""; }
                catch (Exception e) { error = e.GetBaseException().Message; UiSounds.Play(UiSound.Error); }
            });
            commands = new[] { new UiPageCommand(UiAction.Cancel, "Back", () => WantsClose = true) };
        }
        public override void Update(UiInput input, float delta)
        { foreach (var command in commands) if (command.Handle(input)) return; Number.Update(input); }
        public override void Draw()
        {
            frame.Draw(); var layout = new UiPageLayout(frame.Bounds, commands, 3);
            UiTheme.TextLine(title, layout.Title.Location.ToVector2(), UiTheme.Gold, false);
            Number.Draw(layout.Content);
            UiTheme.WrappedText(error.Length > 0 ? error : description, layout.Status, error.Length > 0 ? UiTheme.Red : UiTheme.Muted);
            layout.DrawCommands();
        }
    }
}
