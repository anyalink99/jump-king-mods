using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using JumpKing;
using JumpKing.Level;
using JumpKing.Mods;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal static partial class Tests
    {
        private static Action<float> GimmickTick(GimmickSession session)
        { return (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), session, typeof(GimmickSession).GetMethod("Update", Flags)); }
        private static GimmickAttempt PreparedGimmicks()
        { return (GimmickAttempt)typeof(ModEntry).GetField("preparedGimmicks", Flags).GetValue(null); }
        private static void GimmickStartupRegression()
        {
            var previousRules = Settings.Current.GimmickRules;
            using (new SpriteGameFixture())
            using (new WalkInputFixture())
            try
            {
                IBlock[] original = { new BoxBlock(new Rectangle(0, 320, 480, 40)) };
                var screens = Scene(original); GimmickBlocks.Screens.SetValue(null, screens);
                typeof(LevelManager).GetField("_total_screens", Flags).SetValue(null, 1);
                typeof(Camera).GetField("_current_screen", Flags).SetValue(null, 0);
                Settings.Current.GimmickRules = new[] { new GimmickRule { Id = "disabled:missing", Enabled = false } };
                using (var scope = new JKRuntime.RuntimeScope())
                {
                    ModEntry.PrepareAttempt(scope);
                    var plan = PreparedGimmicks();
                    Require(plan != null && plan.Rules.Length == 0, "Empty/disabled preferences prepare without discovering providers");
                    using (var session = new GimmickSession(ResumePlayer(), plan))
                    {
                        long catalogue = Gimmicks.Generation; int compilations = GimmickSpace.Compilations; object snapshot = session.Capture();
                        var tick = GimmickTick(session); for (int i = 0; i < 120; i++) tick(1f / 60f);
                        session.Apply(new GimmickRule[0]); session.Validate(snapshot);
                        Require(catalogue == Gimmicks.Generation && ReferenceEquals(GimmickBlocks.Hitboxes.GetValue(screens[0]), original), "Idle handoff does not discover states or rewrite world geometry");
                        Require(GimmickSpace.Compilations == compilations, "Idle handoff never compiles empty-space masks");
                    }
                }
                Require(PreparedGimmicks() == null, "Attempt scope clears prepared resources");
                int reads = 0;
                new GimmickPin(new GimmickEntry { Id = "test:cold-pin", Label = "Probe", Read = () => { reads++; return false; } });
                Require(reads == 0, "Dormant pin construction does not read foreign settings");

                string staticId = "state:static:" + Gimmicks.TypeId(typeof(UnknownProvider.Control)) + ".<Held>k__BackingField";
                string nestedId = "state:instance:" + Gimmicks.TypeId(typeof(UnknownProvider.Behaviour)) + ":0.transition.target";
                string contract = typeof(UnknownProvider.Control).Module.ModuleVersionId.ToString("D") + ":System.Boolean";
                Settings.Current.GimmickRules = new[] {
                    new GimmickRule { Id = staticId, Enabled = true, Value = "True", Contract = contract },
                    new GimmickRule { Id = nestedId, Enabled = true, Value = "True", Contract = contract }
                };
                var previousPlayer = ResumePlayer(); var previousBehaviour = new UnknownProvider.Behaviour(); previousPlayer.m_body.RegisterBehaviour(previousBehaviour);
                UnknownProvider.Control.Held = false;
                for (int attempt = 0; attempt < 2; attempt++)
                using (var scope = new JKRuntime.RuntimeScope())
                {
                    ModEntry.PrepareAttempt(scope); var plan = PreparedGimmicks();
                    Require(plan.Error == null && !UnknownProvider.Control.ReadHeld() && !previousBehaviour.transition.target, "Preparation warms metadata/hooks without touching previous-player state: " + plan.Error);
                    var nextPlayer = ResumePlayer(); var nextBehaviour = new UnknownProvider.Behaviour(); nextPlayer.m_body.RegisterBehaviour(nextBehaviour);
                    using (var session = new GimmickSession(nextPlayer, plan))
                    {
                        GimmickTick(session)(1f / 60f);
                        Require(UnknownProvider.Control.ReadHeld() && nextBehaviour.transition.target && !previousBehaviour.transition.target, "Cold/restart binding applies only selected states to the new player: " + Gimmicks.Status);
                        var bound = Gimmicks.Entries[nestedId].Slot; session.EnsureStates();
                        Require(ReferenceEquals(bound, Gimmicks.Entries[nestedId].Slot) && session.IsEnabled(nestedId), "Opening the full catalogue preserves selected state leases");
                    }
                    Require(!UnknownProvider.Control.ReadHeld() && !nextBehaviour.transition.target, "Prepared state activation releases its owned values");
                }
                GimmickAttempt cancelled;
                using (var scope = new JKRuntime.RuntimeScope()) { ModEntry.PrepareAttempt(scope); cancelled = PreparedGimmicks(); }
                Require(cancelled.Disposed && PreparedGimmicks() == null && !UnknownProvider.Control.ReadHeld(), "Cancelled intro releases preparation without activating states");
                using (var scope = new JKRuntime.RuntimeScope())
                {
                    ModEntry.PrepareAttempt(scope); var plan = PreparedGimmicks(); Settings.Current.GimmickRules = new GimmickRule[0];
                    using (var session = new GimmickSession(ResumePlayer(), plan))
                    {
                        GimmickTick(session)(1f / 60f);
                        Require(!UnknownProvider.Control.ReadHeld() && Gimmicks.Status.Contains("Prepared settings changed"), "Stale preparation cannot apply old settings");
                    }
                }
                var entry = new GimmickEntry { Id = "startup:material", Kind = "Block", Factory = new UnknownProvider.PortableFactory(), Colour = UnknownProvider.PortableFactory.Code };
                Gimmicks.Add(entry);
                Settings.Current.GimmickRules = new[] { new GimmickRule { Id = entry.Id, Enabled = true, Application = GimmickApplication.Overlay } };
                using (var scope = new JKRuntime.RuntimeScope())
                {
                    ModEntry.PrepareAttempt(scope); var plan = PreparedGimmicks();
                    Require(entry.Template != null && ReferenceEquals(GimmickBlocks.Hitboxes.GetValue(screens[0]), original), "Saved factory recipe is prepared before player attachment without changing the map");
                    using (var session = new GimmickSession(ResumePlayer(), plan))
                    { GimmickTick(session)(1f / 60f); Require(session.IsEnabled(entry.Id), "Prepared block override activates on new player"); }
                    Require(ReferenceEquals(GimmickBlocks.Hitboxes.GetValue(screens[0]), original), "Prepared block release restores world");
                }
                var medium = new GimmickEntry { Id = "startup:water", Label = "Water", Kind = "Block", Template = new WaterBlock(Rectangle.Empty) };
                Gimmicks.Add(medium);
                Settings.Current.GimmickRules = new[] { new GimmickRule { Id = medium.Id, Enabled = true, Application = GimmickApplication.FillEmpty } };
                using (var scope = new JKRuntime.RuntimeScope()) {
                    int before = GimmickSpace.Compilations;
                    ModEntry.PrepareAttempt(scope); var plan = PreparedGimmicks();
                    Require(plan.Error == null && GimmickSpace.Compilations > before && plan.Geometry != null, "Selected empty-space geometry compiles in BeforeAttempt");
                    int after = GimmickSpace.Compilations;
                    using (var session = new GimmickSession(ResumePlayer(), plan)) {
                        GimmickTick(session)(1f / 60f);
                        Require(session.IsEnabled(medium.Id) && GimmickSpace.Compilations == after, "Handoff consumes prepared fill geometry without recomputing masks");
                    }
                    using (var session = new GimmickSession(ResumePlayer(), plan)) {
                        var changed = Gimmicks.Copy(plan.Rules[0]); changed.Application = GimmickApplication.Overlay;
                        session.Apply(new[] { changed });
                        Require(((IBlock[])GimmickBlocks.Hitboxes.GetValue(screens[0])).Last().GetRect().Height == 360, "Explicit edited rules cannot consume a stale fill plan");
                    }
                }
                Gimmicks.Entries.Remove(medium.Id);
            }
            finally { Settings.Current.GimmickRules = previousRules; }
            Console.WriteLine("[OK] Idle 120-frame handoff, lazy pins, selected saved-state binding, prepared factories, restart/cancel and stale-settings refusal");
        }
        private static object startupSnapshot;
        private static void CaptureStartup(object snapshot) { startupSnapshot = snapshot; }
        private static int GimmickStartupAudit(string gameDirectory)
        {
            // Load metadata only. No installed mod startup callbacks or save IO.
            string workshop = Path.Combine(Directory.GetParent(gameDirectory).Parent.FullName, "workshop/content/1061090");
            foreach (string path in Directory.GetFiles(workshop, "*.dll", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(path) == "MegaGameplayExpansion.dll" || Path.GetFileName(path) == "0Harmony.dll") continue;
                try {
                    var assembly = Assembly.LoadFrom(path);
                    using (var stream = assembly.GetManifestResourceStream("JKRuntime.Module"))
                        if (stream != null) { var bytes = new byte[stream.Length]; stream.Read(bytes, 0, bytes.Length); Assembly.Load(bytes); }
                } catch { }
            }
            var lines = new List<string>();
            var trace = typeof(JKRuntime.RuntimeApi).Assembly.GetType("JKRuntime.StartupTrace", true);
            var configure = trace.GetMethod("ConfigureForTest", Flags);
            configure.Invoke(null, new object[] { Delegate.CreateDelegate(configure.GetParameters()[0].ParameterType, typeof(Tests).GetMethod("CaptureStartup", Flags)) });
            using (new SpriteGameFixture())
            using (new WalkInputFixture())
            {
                Settings.Current.GimmickRules = new GimmickRule[0]; Settings.Current.GimmickPins = null;
                var blocks = Enumerable.Range(0, 2700).Select(i => (IBlock)new BoxBlock(new Rectangle((i % 60) * 8, (i / 60) * 8, 8, 8))).ToArray();
                GimmickBlocks.Screens.SetValue(null, Scene(blocks));
                typeof(LevelManager).GetField("_total_screens", Flags).SetValue(null, 1);
                typeof(Camera).GetField("_current_screen", Flags).SetValue(null, 0);
                LevelManager.RegisterBlockFactory(new UnknownProvider.Factory());
                var loader = ModLoader.Instance;
                var mod = new ModAssembly(typeof(UnknownProvider.Factory).Assembly, new JumpKingModAttribute("Handoff probe"));
                mod.PauseMenuItemSettingMethods.Add(typeof(UnknownProvider.MenuProbe).GetMethod("Create"));
                loader.LoadedMods.Add(mod);
                var previousCallbacks = Game1.callbackManager;
                try
                {
                    typeof(Game1).GetField("_callback_manager", Flags).SetValue(null, new TimerCallback.CallbackManager());
                    for (int attempt = 0; attempt < 3; attempt++)
                    using (var scope = new JKRuntime.RuntimeScope())
                    {
                        trace.GetMethod("BeginAttempt", Flags).Invoke(null, null);
                        ModEntry.PrepareAttempt(scope);
                        trace.GetMethod("EndPreparation", Flags).Invoke(null, null);
                        trace.GetMethod("BeginGameplay", Flags).Invoke(null, null);
                        var player = ResumePlayer(); var timer = Stopwatch.StartNew();
                        using (var session = new GimmickSession(player, PreparedGimmicks()))
                        {
                            lines.Add("attempt " + attempt + " attach_ms=" + timer.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                            var tick = GimmickTick(session);
                            long generation = Gimmicks.Generation; object snapshot = session.Capture();
                            timer.Restart();
                            using (JKRuntime.RuntimeApi.MeasureStartup("fixture.mega-gameplay.first-tick")) tick(1f / 60f);
                            lines.Add("attempt " + attempt + " first_tick_ms=" + timer.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                            timer.Restart(); for (int frame = 1; frame < 120; frame++) tick(1f / 60f);
                            lines.Add("attempt " + attempt + " remaining_119_ticks_ms=" + timer.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                            Require(Gimmicks.Generation == generation, "Idle gameplay must not discover the installed state catalogue"); session.Validate(snapshot);
                            var menu = new LibraryFactory(); GimmickMenu.Remember(menu, new JumpKing.PauseMenu.GuiFormat(), true);
                            timer.Restart(); Game1.callbackManager.Update(1); Game1.callbackManager.Update(1);
                            lines.Add("attempt " + attempt + " menu_callbacks_ms=" + timer.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                        }
                        trace.GetMethod("Finish", Flags).Invoke(null, new object[] { "isolated MGE handoff fixture" });
                        string formatted = (string)trace.GetMethod("Format", Flags).Invoke(null, new[] { startupSnapshot });
                        Require(formatted.IndexOf("mega-gameplay.prepare-gimmicks", StringComparison.Ordinal) < formatted.IndexOf("runtime.gameplay-handoff", StringComparison.Ordinal), "Runtime trace places MGE preparation before handoff");
                        Require(!formatted.Contains("browse-live-states") && !formatted.Contains("bind-saved-overrides"), "Empty startup trace has no discovery or override binding");
                        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gimmick-startup-trace-" + attempt + ".txt"), formatted);
                    }
                    lines.Add("foreign_menu_factory_calls=" + UnknownProvider.MenuProbe.Calls);
                    Require(UnknownProvider.MenuProbe.Calls == 0, "Deferred pin callbacks must not invoke foreign menu factories");
                }
                finally { typeof(Game1).GetField("_callback_manager", Flags).SetValue(null, previousCallbacks); loader.LoadedMods.Remove(mod); trace.GetMethod("ResetForTest", Flags).Invoke(null, null); }
            }
            foreach (string line in lines) Console.WriteLine(line);
            return 0;
        }
    }
}
