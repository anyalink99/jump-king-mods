using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BehaviorTree;
using JKRuntime.Settings;
using JKRuntime.UI;
using JumpKing;
using JumpKing.Mods;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using JumpKing.PauseMenu.BT.Actions;

namespace MegaGameplayExpansion
{
    internal sealed class GimmickPin : SettingToggle
    {
        private sealed class Binding { internal GimmickEntry Entry; internal bool Visible; }
        private readonly Binding binding;
        internal GimmickPin(GimmickEntry entry) : this(new Binding { Entry = entry }) { }
        private GimmickPin(Binding source) : base(new Setting<bool>("mega.pin." + Stable(source.Entry.Id), source.Entry.Label,
            () => source.Visible && Gimmicks.Enabled(source.Entry), value => { if (value != Gimmicks.Enabled(source.Entry)) Gimmicks.Toggle(source.Entry); }), Gimmicks.Short(source.Entry.Label, 28))
        { binding = source; }
        public override void Draw(int x, int y, bool selected) { binding.Visible = true; base.Draw(x, y, selected); }
        protected override void OnToggle()
        { try { binding.Visible = true; base.OnToggle(); } catch (Exception error) { Gimmicks.Status = error.GetBaseException().Message; } }
        private static string Stable(string value)
        { using (var hash = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant(); }
    }
    public sealed class GimmickLibraryButton : TextButton
    {
        public GimmickLibraryButton(object factory, GuiFormat format, bool pause)
            : base("Gimmick library", UIApi.CreateMenuPage(factory, new GimmickPage(factory, format, pause)))
        { GimmickMenu.Remember(factory, format, pause); }
    }
    internal static class GimmickMenu
    {
        private sealed class MenuContext { internal WeakReference Factory; internal GuiFormat Format; internal bool Pause, SettingsReady; }
        private static readonly List<MenuContext> menus = new List<MenuContext>();
        internal static void Remember(object factory, GuiFormat format, bool pause)
        {
            menus.RemoveAll(m => !m.Factory.IsAlive);
            if (!menus.Any(m => ReferenceEquals(m.Factory.Target, factory))) menus.Add(new MenuContext { Factory = new WeakReference(factory), Format = format, Pause = pause });
            Schedule(factory);
        }
        internal static void RefreshAll()
        {
            foreach (var menu in menus.ToArray()) if (menu.Factory.IsAlive) Schedule(menu.Factory.Target);
        }
        internal static void Schedule(object factory)
        {
            Gimmicks.Initialize();
            if (Game1.callbackManager != null) Game1.callbackManager.CreateRoutine(1, () => {
                try
                {
                    using (JKRuntime.RuntimeApi.MeasureStartup("mega-gameplay.refresh-pins")) Refresh(factory);
                }
                catch (Exception error) { Gimmicks.Status = "Pins: " + error.GetBaseException().Message; }
            });
        }
        internal static void Refresh(object factory)
        {
            var drawables = factory.GetType().GetProperty("Drawables", Gimmicks.Members);
            var all = drawables == null ? null : drawables.GetValue(factory, null) as IEnumerable;
            if (all == null) return;
            foreach (object raw in all)
            {
                var menu = raw as MenuSelector;
                if (menu == null || !menu.Children.Any(c => c is GimmickLibraryButton)) continue;
                var children = menu.Children.Where(c => !(c is GimmickPin) && !(c is WarpJumpOption) && !(c is NoWalkOffOption) && !(c is AirDashOption)).ToList();
                int index = children.FindIndex(c => c is GimmickLibraryButton);
                foreach (string id in Gimmicks.Pins.Distinct())
                {
                    GimmickEntry entry;
                    string key = id;
                    Gimmicks.Entries.TryGetValue(id, out entry);
                    var projection = new GimmickEntry { Id = id, Label = entry == null ? "Unavailable: " + id.Split(':').Last() : entry.Label,
                        Read = () => { var resolved = ResolvePin(factory, key); return resolved != null && Gimmicks.Enabled(resolved); },
                        Write = value => {
                            var resolved = ResolvePin(factory, key);
                            if (resolved == null) throw new InvalidOperationException("Open the library to inspect this unavailable provider.");
                            if (value != Gimmicks.Enabled(resolved)) Gimmicks.Toggle(resolved);
                        } };
                    children.Insert(index++, new GimmickPin(projection));
                }
                typeof(IBTcomposite).GetField("m_children", Gimmicks.Members).SetValue(menu, children.ToArray());
                typeof(MenuSelector).GetField("_index", Gimmicks.Members).SetValue(menu, Math.Min(index, children.Count - 1));
                typeof(MenuSelector).GetMethod("FetchMenuItems", Gimmicks.Members).Invoke(menu, null);
                typeof(MenuSelector).GetMethod("CalculateBounds", Gimmicks.Members).Invoke(menu, null);
            }
        }
        private static GimmickEntry ResolvePin(object factory, string id)
        {
            var context = menus.FirstOrDefault(m => ReferenceEquals(m.Factory.Target, factory));
            if (id.StartsWith("setting:", StringComparison.Ordinal) && context != null && !context.SettingsReady)
            {
                // Called only when a pin is actually drawn/used in a visible menu,
                // never by construction of the dormant pause menu at handoff.
                DiscoverSettings(factory, context.Format, context.Pause);
                Schedule(factory);
            }
            GimmickEntry entry;
            if (!Gimmicks.Entries.TryGetValue(id, out entry) && id.StartsWith("state:", StringComparison.Ordinal) && Gimmicks.Session != null)
            { Gimmicks.Session.EnsureStates(); Gimmicks.Entries.TryGetValue(id, out entry); }
            return entry;
        }
        internal static void DiscoverSettings(object factory, GuiFormat format, bool pause)
        {
            var context = menus.FirstOrDefault(m => ReferenceEquals(m.Factory.Target, factory));
            if (context != null) context.SettingsReady = true;
            var weak = new WeakReference(factory);
            var resolve = typeof(JKRuntime.PackageHost).GetMethod("MenuImplementation", Gimmicks.Members);
            foreach (ModAssembly mod in ModLoader.Instance.LoadedMods)
            {
                var methods = (pause ? mod.PauseMenuItemSettingMethods : mod.MainMenuItemSettingMethods).ToArray();
                var implementations = new Dictionary<MethodInfo, MethodInfo>();
                foreach (var method in methods)
                    try { implementations[method] = resolve == null ? method : (MethodInfo)resolve.Invoke(null, new object[] { mod.Assembly, method }); } catch { }
                foreach (var method in methods)
                {
                    try
                    {
                        MethodInfo implementation; if (!implementations.TryGetValue(method, out implementation)) continue;
                        if (implementation == null || implementation.DeclaringType.Assembly == typeof(Gimmicks).Assembly
                            || !typeof(IToggle).IsAssignableFrom(implementation.ReturnType)) continue;
                        MethodInfo menuMethod = method;
                        Func<IToggle> create = () => {
                            if (!weak.IsAlive) throw new InvalidOperationException("Reopen the current mod menu to refresh settings");
                            var option = menuMethod.Invoke(null, new object[] { weak.Target, format }) as IToggle;
                            if (option == null) throw new InvalidOperationException("This setting is unavailable in the current context");
                            return option;
                        };
                        create();
                        string identity = implementation.ReturnType.FullName;
                        if (implementations.Values.Count(m => m != null && m.DeclaringType == implementation.DeclaringType && m.ReturnType == implementation.ReturnType) > 1)
                            identity += ":" + implementation.Name;
                        Gimmicks.Add(new GimmickEntry { Id = "setting:" + Gimmicks.TypeId(implementation.DeclaringType) + ":" + identity,
                            Label = Gimmicks.Human(implementation.ReturnType.Name), Owner = mod.ModName, Kind = "Setting",
                            Detail = "Uses the mod's own toggle callback and persistence. This enables its setting, which may still require its original input.",
                            Read = () => create().toggle,
                            Write = value => {
                                IToggle option = create();
                                if (!(bool)option.GetType().GetMethod("CanChange", Gimmicks.Members).Invoke(option, null)) throw new InvalidOperationException("The original mod currently restricts this setting");
                                option.OverrideToggle(value); option.GetType().GetMethod("OnToggle", Gimmicks.Members).Invoke(option, null);
                            } });
                    }
                    catch (Exception error) { Gimmicks.Status = "Setting discovery: " + error.GetBaseException().Message; }
                }
            }
        }
    }
}
