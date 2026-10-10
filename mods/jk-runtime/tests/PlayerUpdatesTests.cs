using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using EntityComponent;
using EntityComponent.BT;
using JKRuntime.Gameplay;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace JKRuntime
{
    internal static class PlayerUpdatesTests
    {
        private sealed class Player : PlayerEntity { protected override void Update(float delta) { } protected override void OnDestroy() { } }
        private sealed class Empty : BehaviorTree.IBTnode { protected override BehaviorTree.BTresult MyRun(BehaviorTree.TickData data) { return BehaviorTree.BTresult.Running; } }
        private sealed class Marker : Component
        {
            internal string Name;
            protected override void Update(float delta) { trace.Add(Name); }
        }
        private static readonly List<string> trace = new List<string>();
        private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
        private static void Throws(Action action) { try { action(); } catch (InvalidOperationException) { return; } throw new Exception("Expected refusal"); }
        private static Player Make()
        {
            var player = (Player)FormatterServices.GetUninitializedObject(typeof(Player));
            typeof(Entity).GetField("m_components", OwnedPatches.Members).SetValue(player, new List<Component>());
            player.m_body = new BodyComp(Vector2.Zero, 18, 26);
            var brain = new BehaviorTreeComp(new Empty());
            typeof(PlayerEntity).GetField("m_bt", OwnedPatches.Members).SetValue(player, brain);
            player.AddComponents(false, player.m_body, new InputComponent(), brain);
            player.AddComponents(new Marker { Name = "tail" });
            return player;
        }
        private static void Main(string[] args)
        {
            Assembly.LoadFrom(args[0]);
            using (var world = new RuntimeScope())
            {
                PlayerUpdates.Prepare(world);
                var player = Make(); var other = Make();
                Component[] initial = player.GetComponents();
                using (var b = PlayerUpdates.Register(player, "test.b", PlayerUpdatePhase.BeforeInput, d => trace.Add("before-b")))
                using (var a = PlayerUpdates.Register(player, "test.a", PlayerUpdatePhase.BeforeInput, d => trace.Add("before-a")))
                using (var after = PlayerUpdates.Register(player, "test.after", PlayerUpdatePhase.AfterInput, d => trace.Add("after")))
                {
                    trace.Clear(); player.UpdateComponents(.01f);
                    Check(trace.SequenceEqual(new[] { "before-a", "before-b", "after", "tail" }), "Stable ordering and disabled input still publishes the native slot");
                    Check(initial.SequenceEqual(player.GetComponents()), "No components inserted or reordered");
                    trace.Clear(); other.UpdateComponents(.01f);
                    Check(trace.SequenceEqual(new[] { "tail" }), "Registrations are player-specific");
                    Throws(() => PlayerUpdates.Register(player, "test.after", PlayerUpdatePhase.AfterInput, d => { }));
                    var list = (List<Component>)typeof(Entity).GetField("m_components", OwnedPatches.Members).GetValue(player);
                    var input = player.GetComponent<InputComponent>(); list.Remove(input); list.Insert(0, input);
                    Throws(() => player.UpdateComponents(.01f));
                    list.Remove(input); list.Insert(1, input);
                    trace.Clear(); player.UpdateComponents(.01f);
                    Check(trace.Count == 4, "Valid ordering can resume after refusal");
                }
                trace.Clear(); player.UpdateComponents(.01f);
                Check(trace.SequenceEqual(new[] { "tail" }), "Dispose removes callbacks without changing enumeration");
                PlayerUpdates.Registration self = null, victim = null;
                self = PlayerUpdates.Register(player, "test.self", PlayerUpdatePhase.AfterInput, d => { trace.Add("self"); self.Dispose(); victim.Dispose(); }, -1);
                victim = PlayerUpdates.Register(player, "test.victim", PlayerUpdatePhase.AfterInput, d => trace.Add("victim"));
                trace.Clear(); player.UpdateComponents(.01f); player.UpdateComponents(.01f);
                Check(trace.SequenceEqual(new[] { "self", "tail", "tail" }), "Disposal during dispatch neither mutates native iteration nor calls a removed peer");
                using (var fail = PlayerUpdates.Register(player, "test.failure", PlayerUpdatePhase.AfterInput, d => { throw new InvalidOperationException("fixture"); }))
                {
                    Throws(() => player.UpdateComponents(.01f));
                    Check(fail.LastError == "fixture", "Gameplay failure propagates and remains diagnosable");
                }
                var dying = PlayerUpdates.Register(player, "test.destroy", PlayerUpdatePhase.AfterInput, d => trace.Add("dead"));
                player.Destroy();
                Check(dying.Disposed, "Native destruction releases phase ownership");
                dying.Dispose();
                other.Destroy();
            }
            // No preparation lease is required for correctness, and the last callback can remove itself.
            var fallback = Make(); PlayerUpdates.Registration last = null;
            last = PlayerUpdates.Register(fallback, "test.last", PlayerUpdatePhase.AfterInput, d => last.Dispose());
            fallback.UpdateComponents(.01f); fallback.UpdateComponents(.01f); fallback.Destroy();
            Check(last.Disposed, "Fallback activation and final removal inside dispatch");
            Console.WriteLine("[OK] Player phases: actual native enumeration, ordering, lifetime, self-removal and failure propagation");
        }
    }
}
