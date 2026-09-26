using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using EntityComponent;
using JKRuntime.Gameplay;
using JKRuntime.Input;
using JKRuntime.State;
using Microsoft.Xna.Framework;

namespace JKRuntime
{
    internal static class ExtensibilityTests
    {
        private sealed class TestComponent : Component { }
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected rejection"); }
        private static void SharedInput()
        {
            var sampler = new HighRateInputSampler(true);
            var first = SharedActionSampler.Acquire("test.first", "test.jump", true, () => sampler);
            var second = SharedActionSampler.Acquire("test.second", "test.jump");
            Throws(() => SharedActionSampler.Acquire("test.third", "test.jump", true));
            first.Start();
            first.Configure(new[] { new[] { 32 } }, new XInputJumpBinding[0], new DirectInputJumpBinding[0], IntPtr.Zero, true);
            second.Reset();
            sampler.ObserveKey(32, true, 10); sampler.ObserveKey(32, false, 40);
            JumpInputTransition a, b;
            Check(first.TryDequeue(out a) && second.TryDequeue(out b) && a.Timestamp == b.Timestamp && a.IsDown && b.IsDown, "Shared press differs");
            first.Reset();
            Check(second.TryDequeue(out b) && b.Timestamp == 40 && !b.IsDown, "One reader cleared another queue");
            Check(!first.TryDequeue(out a), "Own reset failed");
            for (int i = 0; i < 800; i++) { sampler.ObserveKey(32, i % 2 == 0, 100 + i); first.TryDequeue(out a); }
            bool invalid = false; int count = 0;
            while (second.TryDequeue(out b)) { invalid |= !b.Reliable; count++; }
            Check(invalid && count <= HighRateInputSampler.MaximumQueuedEdges, "Slow reader received false precision or unbounded queue");
            first.Dispose();
            Check(second.TryDequeue(out b) && !b.Reliable && !second.Enabled, "Binding authority teardown retained stale evidence");
            second.Dispose(); Check(SharedActionSampler.ActiveStreams == 0, "Input stream leaked");
        }
        private static void Events()
        {
            var observed = new List<GameplayEvent>();
            int brokenCalls = 0;
            using (GameplayEvents.Subscribe("test.good", e => observed.Add(e)))
            using (GameplayEvents.Subscribe("test.bad", e => { brokenCalls++; throw new Exception("bad observer"); }))
            {
                GameplayEvents.BeginTick(new AttemptStamp { Session = 7, Attempt = 9 });
                GameplayEvents.NotifyTeleport("test.teleport", Vector2.One, Vector2.Zero, 2);
                GameplayEvents.NotifyTeleport("test.teleport", Vector2.One, Vector2.Zero, 3);
                Check(observed.Count == 2 && brokenCalls == 1, "Observer failure was not isolated");
                Check(observed[0].Sequence < observed[1].Sequence && observed[0].Tick == observed[1].Tick && observed[0].Attempt.Attempt == 9, "Event identity/order lost");
            }
            Check(!GameplayEvents.Requested, "Event listeners leaked");
        }
        private static void Suspension()
        {
            var component = new TestComponent { Enabled = true };
            var one = ComponentSuspension.Acquire("test.one", component);
            var two = ComponentSuspension.Acquire("test.two", component);
            Check(!component.Enabled, "Suspension not applied");
            one.Dispose(); Check(!component.Enabled, "First owner resumed another's component");
            two.Dispose(); Check(component.Enabled && !ComponentSuspension.IsSuspended(component), "Final owner did not restore original state");
            component.Enabled = false;
            using (ComponentSuspension.Acquire("test.one", component)) { }
            Check(!component.Enabled, "Originally disabled component was enabled");
        }
        private static void Frames()
        {
            bool down = false; int reads = 0;
            using (ActionInputs.Register("test.input", "test.frame", () => { reads++; return down; }))
            {
                Check(!ActionInputs.Read("test.frame").Pressed, "Initial frame invented an edge");
                ActionInputs.Read("test.frame"); Check(reads == 1, "Frame input read twice");
                down = true; ActionInputs.BeginTick(); Check(ActionInputs.Read("test.frame").Pressed, "Missing press");
                down = false; ActionInputs.BeginTick(); Check(ActionInputs.Read("test.frame").Released, "Missing release");
            }
        }
        private static void NativeObservation()
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
            var refs = new GameClock.Contract();
            object oldManager = refs.ManagerInstance.GetValue(null);
            var manager = FormatterServices.GetUninitializedObject(refs.ManagerInstance.FieldType);
            refs.AllTime.SetValue(manager, Activator.CreateInstance(refs.AllTime.FieldType));
            refs.Snapshot.SetValue(manager, Activator.CreateInstance(refs.Snapshot.FieldType));
            refs.ManagerInstance.SetValue(null, manager);
            var player = (JumpKing.Player.PlayerEntity)FormatterServices.GetUninitializedObject(typeof(JumpKing.Player.PlayerEntity));
            typeof(Entity).GetField("m_components", flags).SetValue(player, new List<Component>());
            player.m_body = new JumpKing.Player.BodyComp(Vector2.Zero, 18, 26);
            var nativeJump = (JumpKing.Player.JumpState)FormatterServices.GetUninitializedObject(typeof(JumpKing.Player.JumpState));
            typeof(JumpKing.Player.PlayerEntity).GetField("m_jump_state", flags).SetValue(player, nativeJump);
            var tail = new TestComponent(); player.AddComponents(player.m_body, tail);
            var results = new List<GameplayEvent>(); int legacy = 0;
            Action tick = null;
            try
            {
                using (var observer = NativeGameplayObserver.Install(player))
                using (GameplayEvents.Subscribe("test.native", e => results.Add(e)))
                using (JumpEvents.Subscribe(e => legacy++))
                {
                    var parts = (List<Component>)typeof(Entity).GetField("m_components", flags).GetValue(player);
                    Check(parts[1] == player.m_body && parts[3] == tail, "Native observer reordered existing components");
                    Action begin = () => observer.GetType().GetMethod("Begin", flags).Invoke(observer, null);
                    Action end = () => observer.GetType().GetMethod("End", flags).Invoke(observer, null);
                    tick = () => { begin(); end(); };
                    tick(); Check(legacy == 0, "Idle tick invented jump");
                    begin(); typeof(JumpKing.Player.JumpState).GetField("m_timer", flags).SetValue(nativeJump, .1f); end();
                    Check(results.Last().Kind == GameplayEventKind.ChargeStarted, "Native charge not observed without SFC");
                    begin(); player.m_body.Velocity = new Vector2(0, -4); JumpKing.Player.PlayerEntity.OnJumpCall();
                    typeof(JumpKing.Player.JumpState).GetField("m_timer", flags).SetValue(nativeJump, 0f); end();
                    Check(legacy == 1 && results.Count(e => e.Kind == GameplayEventKind.Jump) == 1, "Native jump missing without SFC");
                    begin(); player.m_body.Velocity = new Vector2(0, -5); JumpKing.Player.PlayerEntity.OnJumpCall();
                    var timing = new JumpResult("subframe-charge", JumpEvidence.PhysicalHold, 68, 4, 4, .066f, false, false, 0, -5);
                    JumpEvents.Publish(timing); end();
                    Check(legacy == 2 && results.Count(e => e.Kind == GameplayEventKind.Jump) == 2 && results.Last(e => e.Kind == GameplayEventKind.Jump).Jump == timing,
                        "SFC/native event duplicated or lost physical timing");
                }
                Check(!GameplayEvents.Requested, "Native observer subscriptions leaked");
            }
            finally { refs.ManagerInstance.SetValue(null, oldManager); }
        }
        private static void CatalogAndComposition()
        {
            Check(Geometry.BlockCatalog.FindOwners(new Color(173, 47, 211)).Contains("mega-gameplay-expansion"), "Bundled colour reservation missing");
            Throws(() => Geometry.BlockCatalog.Register("test.steal", new[] { new Geometry.BlockDeclaration("test.rule", new Color(173, 47, 211), Geometry.BlockVariant.Zone, false) }));
            var registry = new MechanicRegistry();
            Func<MechanicState> active = () => new MechanicState(true, true, true, MechanicSource.Controller, "fixture");
            using (registry.Register("test.a", new MechanicDefinition("test.a", new Version(1,0), MechanicEffects.Movement, MechanicComposition.Exclusive, "controller"), active))
            using (registry.Register("test.b", new MechanicDefinition("test.b", new Version(1,0), MechanicEffects.Movement, MechanicComposition.Additive, "controller"), active))
            {
                var rows = registry.Inspect();
                Check(rows.All(r => r.Conflicts.Length == 1 && r.State.Active), "Conflict hidden or inventory changed gameplay activation");
            }
        }
        public static int Main()
        {
            try {
                var integer = new JumpResult("test.jump", JumpEvidence.PhysicalHold, 204, 12, 12, 13f/60, false, false, 0, -3);
                Check(integer.CorrectedFrameCount == 12 && integer.PredictedFrameCount == 12, "Old constructor lost exact integer counts");
                var fractional = new JumpResult("test.jump", JumpEvidence.PhysicalHold, 208.25, null, null, 6.625f/60, false, false, 0, -3, 12.25f, 12.25f);
                Check(fractional.CorrectedFrameCount == 12.25f && fractional.PredictedFrameCount == 12.25f
                    && fractional.CorrectedFrames == null && fractional.PredictedFrames == null, "Fractional event rounded or fabricated an integer count");
                Check((int)JumpEvidence.BufferedNative == 2 && (int)JumpEvidence.BufferedHold == 3, "Buffered evidence changed a shipped enum value");
                Throws(() => new JumpResult("test.jump", JumpEvidence.PhysicalHold, 208.25, 12, null, .1f, false, false, 0, -3, 12.25f, null));
                Throws(() => new JumpResult("test.jump", JumpEvidence.PhysicalHold, 208.25, null, null, .1f, false, false, 0, -3, float.NaN, null));
                SharedInput(); Events(); Suspension(); Frames(); NativeObservation(); CatalogAndComposition();
                for (int i = 0; i < 400; i++) RuntimeJournal.Record("test", "bounded", i.ToString());
                Check(RuntimeJournal.Read().Length == 256, "Journal unbounded");
                Check(RuntimeResources.Inspect().Length == 0, "Owned resources leaked");
                Console.WriteLine("[OK] Shared action timestamps/queues, fault-isolated ordered events, frame actions, nested suspension and bounded diagnostics"); return 0;
            } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
