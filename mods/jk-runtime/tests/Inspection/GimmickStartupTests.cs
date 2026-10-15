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

namespace JKRuntime.Inspection
{
    internal static partial class Tests
    {
        private static Action<float> GimmickTick(GimmickSession session)
        { return (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), session, typeof(GimmickSession).GetMethod("Update", Flags)); }
        private static GimmickAttempt PreparedGimmicks()
        { return (GimmickAttempt)preparedInspector; }
        private static void GimmickStartupRegression()
        {
            var previousRules = InspectorSettings.Current.GimmickRules;
            using (new SpriteGameFixture())
            using (new WalkInputFixture())
            try
            {
                IBlock[] original = { new BoxBlock(new Rectangle(0, 320, 480, 40)) };
                var screens = Scene(original); GimmickBlocks.Screens.SetValue(null, screens);
                typeof(LevelManager).GetField("_total_screens", Flags).SetValue(null, 1);
                typeof(Camera).GetField("_current_screen", Flags).SetValue(null, 0);
                InspectorSettings.Current.GimmickRules = new[] { new GimmickRule { Id = "disabled:missing", Enabled = false } };
                using (var scope = new JKRuntime.RuntimeScope())
                {
                    PrepareInspector(scope);
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

                string staticId = "state:static:" + Gimmicks.TypeId(typeof(UnknownProvider.Control)) + ".<Held>k__BackingField";
                string nestedId = "state:instance:" + Gimmicks.TypeId(typeof(UnknownProvider.Behaviour)) + ":0.transition.target";
                string contract = typeof(UnknownProvider.Control).Module.ModuleVersionId.ToString("D") + ":System.Boolean";
                InspectorSettings.Current.GimmickRules = new[] {
                    new GimmickRule { Id = staticId, Enabled = true, Value = "True", Contract = contract },
                    new GimmickRule { Id = nestedId, Enabled = true, Value = "True", Contract = contract }
                };
                var previousPlayer = ResumePlayer(); var previousBehaviour = new UnknownProvider.Behaviour(); previousPlayer.m_body.RegisterBehaviour(previousBehaviour);
                UnknownProvider.Control.Held = false;
                for (int attempt = 0; attempt < 2; attempt++)
                using (var scope = new JKRuntime.RuntimeScope())
                {
                    PrepareInspector(scope); var plan = PreparedGimmicks();
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
                using (var scope = new JKRuntime.RuntimeScope()) { PrepareInspector(scope); cancelled = PreparedGimmicks(); }
                Require(cancelled.Disposed && PreparedGimmicks() == null && !UnknownProvider.Control.ReadHeld(), "Cancelled intro releases preparation without activating states");
                using (var scope = new JKRuntime.RuntimeScope())
                {
                    PrepareInspector(scope); var plan = PreparedGimmicks(); InspectorSettings.Current.GimmickRules = new GimmickRule[0];
                    using (var session = new GimmickSession(ResumePlayer(), plan))
                    {
                        GimmickTick(session)(1f / 60f);
                        Require(!UnknownProvider.Control.ReadHeld() && Gimmicks.Status.Contains("Prepared settings changed"), "Stale preparation cannot apply old settings");
                    }
                }
                var entry = new GimmickEntry { Id = "startup:material", Kind = "Block", Factory = new UnknownProvider.PortableFactory(), Colour = UnknownProvider.PortableFactory.Code };
                Gimmicks.Add(entry);
                InspectorSettings.Current.GimmickRules = new[] { new GimmickRule { Id = entry.Id, Enabled = true, Application = GimmickApplication.Overlay } };
                using (var scope = new JKRuntime.RuntimeScope())
                {
                    PrepareInspector(scope); var plan = PreparedGimmicks();
                    Require(entry.Template != null && ReferenceEquals(GimmickBlocks.Hitboxes.GetValue(screens[0]), original), "Saved factory recipe is prepared before player attachment without changing the map");
                    using (var session = new GimmickSession(ResumePlayer(), plan))
                    { GimmickTick(session)(1f / 60f); Require(session.IsEnabled(entry.Id), "Prepared block override activates on new player"); }
                    Require(ReferenceEquals(GimmickBlocks.Hitboxes.GetValue(screens[0]), original), "Prepared block release restores world");
                }
                var medium = new GimmickEntry { Id = "startup:water", Label = "Water", Kind = "Block", Template = new WaterBlock(Rectangle.Empty) };
                Gimmicks.Add(medium);
                InspectorSettings.Current.GimmickRules = new[] { new GimmickRule { Id = medium.Id, Enabled = true, Application = GimmickApplication.FillEmpty } };
                using (var scope = new JKRuntime.RuntimeScope()) {
                    int before = GimmickSpace.Compilations;
                    PrepareInspector(scope); var plan = PreparedGimmicks();
                    Require(plan.Error == null && GimmickSpace.Compilations == before && plan.Geometry == null, "BeforeAttempt doesn't compile geometry before native correction callbacks");
                    plan.FinalizeGeometry();
                    Require(GimmickSpace.Compilations > before && plan.Geometry != null, "Finalized geometry compiles before activation");
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
            finally { InspectorSettings.Current.GimmickRules = previousRules; }
            Console.WriteLine("[OK] Idle 120-frame handoff, deferred geometry, selected saved-state binding, prepared factories, restart/cancel and stale-settings refusal");
        }
    }
}
