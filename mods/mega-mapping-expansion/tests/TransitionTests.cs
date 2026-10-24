using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JKRuntime.World;
using JumpKing;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static TransitionPlan Routes(string body, string attributes = "")
        { return TransitionPlan.FromText("<Transitions version='1' " + attributes + ">" + body + "</Transitions>", 300); }
        private static TransitionPlan DestinationRoutes(string mode = "random", int history = 1, string extra = "")
        { return Routes("<Pool id='trials' screens='10,15,20,25' mode='" + mode + "' avoidLast='" + history + "' " + extra + "/><Exit screen='5' side='right' pool='trials'/><Exit screen='6' side='left' pool='trials'/>"); }
        private static int Cross(TransitionEngine engine, int source = 4, int side = 1)
        { var decision = engine.Prepare(source, side); int target = decision.Target; decision.Commit(); return target; }
        private static byte[] RouteBytes(TransitionEngine engine)
        { return new WorldStateCodec(typeof(TransitionState), true).Capture(engine.State); }
        private static void BadRoute(Action action, string message)
        { bool failed = false; try { action(); } catch (Exception e) { if (!(e is InvalidDataException || e is InvalidOperationException || e is System.Xml.XmlException || e is ArgumentException || e is NotSupportedException)) throw; failed = true; } Require(failed, message); }
        private static void TransitionChecks(string directory)
        {
            var fixedPlan = Routes("<Pool id='fixed' screens='2,3,4'/><Exit screen='1' side='right' pool='fixed'/>", "seed='0'");
            var fixedA = new TransitionEngine(fixedPlan, 123); var fixedB = new TransitionEngine(fixedPlan, 999);
            Require(fixedA.Seed == 0 && Enumerable.Range(0, 20).Select(n => Cross(fixedA, 0)).SequenceEqual(Enumerable.Range(0, 20).Select(n => Cross(fixedB, 0))), "fixed XML seed including zero overrides automatic seed inputs");
            foreach (var mode in new[] { "random", "weighted", "shuffle", "sequence" }) foreach (int history in new[] { 0, 1, 3 })
            {
                var plan = DestinationRoutes(mode, history, mode == "weighted" ? "weights='1,2,3,4'" : "");
                var first = new TransitionEngine(plan, 0); var second = new TransitionEngine(plan, 0);
                var previous = new Queue<int>(); var bag = new HashSet<int>();
                for (int n = 0; n < 160; n++)
                {
                    int target = Cross(first, n % 2 == 0 ? 4 : 5, n % 2 == 0 ? 1 : 0);
                    Require(target == Cross(second), "fixed seeds reproduce choices across exits of the same pool");
                    Require(!previous.Contains(target), "avoidLast excludes recent choices in " + mode);
                    previous.Enqueue(target); while (previous.Count > history) previous.Dequeue();
                    if (mode == "shuffle") { Require(bag.Add(target), "shuffle visits every destination exactly once per bag"); if (bag.Count == 4) bag.Clear(); }
                    if (mode == "sequence") Require(target == plan.Pools[0].Screens[n % 4], "sequence follows authored order and cycles");
                }
            }
            var unrestricted = new TransitionEngine(DestinationRoutes("random", 0), 19); int last = -1; bool repeated = false;
            for (int n = 0; n < 200; n++) { int target = Cross(unrestricted); repeated |= target == last; last = target; }
            Require(repeated, "unrestricted random permits consecutive repeats");
            var weighted = new TransitionEngine(DestinationRoutes("weighted", 0, "weights='1000,1,1,1'"), 2); int favoured = 0;
            for (int n = 0; n < 1000; n++) if (Cross(weighted) == 10) favoured++;
            Require(favoured > 950, "weighted selection respects relative positive weights");
            var solo = new TransitionEngine(DestinationRoutes(), 7);
            var extra = Routes("<Pool id='unrelated' screens='10,20'/><Exit screen='8' side='right' pool='unrelated'/><Pool id='trials' screens='10,15,20,25'/><Exit screen='5' side='right' pool='trials'/>");
            var independent = new TransitionEngine(extra, 7);
            for (int n = 0; n < 100; n++) { Cross(independent, 7); Require(Cross(solo) == Cross(independent), "another pool's crossings cannot advance this stream"); }
            var engine = new TransitionEngine(DestinationRoutes("shuffle", 1), 123); var before = RouteBytes(engine);
            var canceled = engine.Prepare(4, 1); BadRoute(() => engine.Capture(), "half-completed crossings cannot be captured"); canceled.Cancel();
            Require(before.SequenceEqual(RouteBytes(engine)), "canceled decisions don't spend RNG, history or bag");
            var retry = engine.Prepare(4, 1); Require(retry.Target == canceled.Target, "canceled selection remains next"); retry.Commit(); BadRoute(retry.Commit, "double commit is rejected");
            for (int n = 0; n < 7; n++) Cross(engine);
            var codec = new WorldStateCodec(typeof(TransitionState), true); var packet = codec.Capture(engine.State);
            int[] expected = Enumerable.Range(0, 40).Select(n => Cross(engine)).ToArray(); engine.Restore(codec.Decode(packet));
            Require(expected.SequenceEqual(Enumerable.Range(0, 40).Select(n => Cross(engine))), "save/load restores seed, bag, history and next choices exactly");
            var invalid = engine.State; invalid.Pools[0].Random = 0; before = RouteBytes(engine);
            BadRoute(() => engine.Restore(invalid), "zero RNG state is rejected before mutation"); Require(before.SequenceEqual(RouteBytes(engine)), "invalid restore doesn't mutate routes");
            var formatted = Routes("<!-- mapper note --><Pool screens='10,15,20,25' avoidLast='1' mode='random' id='trials'/><Exit side='right' screen='5' pool='trials'/><Exit side='left' screen='6' pool='trials'/>");
            Require(formatted.Identity == DestinationRoutes().Identity, "comments, whitespace and attribute order don't invalidate saves");
            BadRoute(() => Routes("<Pool id='a' screens='2,3' avoidLast='2'/><Exit screen='1' side='right' pool='a'/>"), "impossible histories fail during validation");
            foreach (string body in new[] {
                "<Pool id='a' screens='2,2'/><Exit screen='1' side='right' pool='a'/>",
                "<Pool id='a' screens='1,2'/><Exit screen='1' side='right' pool='a'/>",
                "<Exit screen='1' side='right' pool='missing'/>",
                "<Pool id='a' screens='2,3' weights='1,2'/><Exit screen='1' side='right' pool='a'/>",
                "<Pool id='a' screens='2,3' mode='weighted' weights='1,0'/><Exit screen='1' side='right' pool='a'/>",
                "<SideLink screen='1' side='right' target='2'/><Pool id='a' screens='2,3'/><Exit screen='1' side='right' pool='a'/>",
                "<Pool id='a' screens='2,3'/><Exit screen='1' side='right' pool='a'/><Reroll id='r' on='event' event='test' pools='a'/>",
                "<SchemePool id='s'><Scheme id='a'><SideLink screen='1' side='right' target='2'/></Scheme><Scheme id='b'><SideLink screen='1' side='left' target='3'/></Scheme></SchemePool>" }) BadRoute(() => Routes(body), "invalid transition definitions fail before activation");
            TriggerRoutes(); RouteWorldState(); RoutePersistence(directory); NativeRouteCrossings();
            Console.WriteLine("[OK] Dynamic transitions: all selection modes, histories, seeds, independent pools, triggers, schemes, native crossings, saves and world state");
        }
        private static void TriggerRoutes()
        {
            var attempt = new TransitionEngine(DestinationRoutes("sequence", 1, "refresh='attempt'"), 1);
            Require(Cross(attempt) == 10 && Cross(attempt) == 10, "attempt refresh keeps the chosen destination until a new run");
            var plan = Routes("<SchemePool id='pair' mode='sequence'><Scheme id='a'><SideLink screen='1' side='right' target='10'/><SideLink screen='10' side='left' target='1'/></Scheme><Scheme id='b'><SideLink screen='1' side='right' target='20'/><SideLink screen='10' side='left' target='20'/></Scheme></SchemePool><Reroll id='switch' screen='3' x='0' y='0' width='24' height='24' pools='pair'/><Reroll id='arrival' on='screen-enter' screen='4' pools='pair'/><Reroll id='script' on='event' event='enter:trial' pools='pair'/>");
            var engine = new TransitionEngine(plan, 1); int notified = 0;
            engine.LinksChanged = () => { notified++; Require(engine.Links().All(l => l.Target == 20) || engine.Links().Select(l => l.Target).SequenceEqual(new[] { 10, 1 }), "schemes publish all links atomically"); };
            engine.Tick(2, new Rectangle(5, 5, 18, 26)); Require(notified == 1 && Cross(engine, 0) == 20, "touch rerolls a scheme without moving the player");
            engine.Tick(2, new Rectangle(5, 5, 18, 26)); Require(notified == 2, "standing on a trigger doesn't keep rerolling");
            engine.Tick(2, new Rectangle(100, 100, 18, 26)); engine.Tick(2, new Rectangle(5, 5, 18, 26)); Require(engine.Links().First().Target == 10, "leaving rearms touch rerolls");
            engine.Tick(3, new Rectangle(100, 100, 18, 26)); Require(engine.Links().First().Target == 20, "screen-entry rerolls once on arrival");
            var saved = engine.State; engine.Tick(3, new Rectangle(100, 100, 18, 26)); engine.Restore(saved); engine.Tick(3, new Rectangle(100, 100, 18, 26)); Require(engine.Links().First().Target == 20, "restored trigger contacts don't fire again on resume");
            engine.Emit("unrelated"); Require(engine.Links().First().Target == 20, "unrelated events leave routes alone"); engine.Emit("enter:trial"); Require(engine.Links().First().Target == 10, "named scene events can reroll without a special collision RGB");
        }
        private static void RouteWorldState()
        {
            var engine = new TransitionEngine(DestinationRoutes("shuffle"), 1);
            using (WorldRegistry.Shared.Bind<TransitionState>("test.transitions", () => engine.State, state => engine.Restore(state), state => engine.Validate(state)))
            {
                byte[] initial = WorldRegistry.Shared.Capture(); Cross(engine); byte[] host = WorldRegistry.Shared.Capture(); WorldRegistry.Shared.Apply(initial);
                using (var receiver = WorldControl.Begin("test.routes", "map", 1, WorldRole.Playback))
                {
                    receiver.Receive(host); Require(engine.State.Pools[0].Current >= 0, "playback receives committed choices");
                    BadRoute(() => Cross(engine), "receivers cannot roll independently"); var before = RouteBytes(engine); engine.Tick(4, new Rectangle()); Require(before.SequenceEqual(RouteBytes(engine)), "playback doesn't advance contacts or RNG");
                }
                Require(WorldRegistry.Shared.Capture().SequenceEqual(initial), "leaving playback restores the local randomizer");
            }
            using (JKRuntime.State.GameState.Snapshots.Register(engine))
            {
                var checkpoint = JKRuntime.State.GameState.Snapshots.Capture(); int next = Cross(engine); JKRuntime.State.GameState.Snapshots.Restore(checkpoint); Require(Cross(engine) == next, "practice checkpoints restore the exact next choice");
            }
        }
        private static void RoutePersistence(string directory)
        {
            string root = Path.Combine(directory, "transition-save"); Directory.CreateDirectory(root);
            var plan = DestinationRoutes("shuffle"); var native = new NativeTransitions(plan, () => root);
            for (int n = 0; n < 9; n++) Cross(native.Engine);
            var codec = new WorldStateCodec(typeof(TransitionState), true); string text = Convert.ToBase64String(codec.Capture(native.Engine.State));
            ScenePersistence store; Dictionary<string, string> flags; int revision;
            TransitionSaveCoordinator.Load(plan.SaveId, () => root, out store, out flags, out revision);
            var values = new Dictionary<string, string>(); for (int n = 0; n < text.Length; n += 250) values.Add((n / 250).ToString("D3"), text.Substring(n, Math.Min(250, text.Length - n)));
            TransitionSaveCoordinator.Publish(store, values); values.Clear(); TransitionSaveCoordinator.Save();
            var resumed = new NativeTransitions(plan, () => root); Require(RouteBytes(resumed.Engine).SequenceEqual(RouteBytes(native.Engine)), "native saves use an immutable route packet and resume without a scene");
            SceneSaveCoordinator.Reset(root); TransitionSaveCoordinator.Reset(root);
            resumed = new NativeTransitions(plan, () => root); Require(resumed.Engine.State.Pools[0].Current == -1, "new-game reset discards saved choice history");
            TransitionSaveCoordinator.Publish(store, new Dictionary<string, string> { { "000", "stale" } }); TransitionSaveCoordinator.Save();
            resumed = new NativeTransitions(plan, () => root); Require(resumed.Engine.State.Pools[0].Current == -1, "stale save context cannot resurrect a previous run");
        }
        private static bool CancelNativeRoute() { return false; }
        private static void ThrowNativeRoute() { throw new InvalidOperationException("route fixture"); }
        private sealed class RouteFlightWorld : JKRuntime.Simulation.INativeFlightWorld, JKRuntime.Simulation.INativeFlightTeleports
        {
            internal int Index, Calls;
            public int Screen { get { return Index; } }
            public bool HasTeleport { get { return true; } }
            public void ValidateScreen() { }
            public void AdvanceCamera(BodyComp body) { }
            public void HandleTeleport(BehaviourContext context) { Calls++; }
            public AdvCollisionInfo GetCollisionInfo(Rectangle box) { throw new NotSupportedException(); }
            public bool CheckCollision(Rectangle box, out Rectangle overlap, out AggregateCollisionInfo info) { throw new NotSupportedException(); }
            public bool CheckCollision(Rectangle box, out Rectangle overlap, out AdvCollisionInfo info) { throw new NotSupportedException(); }
            public bool IsInWater(Rectangle box) { throw new NotSupportedException(); }
        }
        private sealed class RoutePlayer : PlayerEntity { protected override void Update(float delta) { } protected override void OnDestroy() { } }
        private sealed class RouteBrain : BehaviorTree.IBTnode
        { protected override BehaviorTree.BTresult MyRun(BehaviorTree.TickData data) { return BehaviorTree.BTresult.Running; } }
        private static void ActivatedRoutes(LevelScreen[] screens)
        {
            string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "active-route-save"); Directory.CreateDirectory(directory);
            var native = new NativeTransitions(DestinationRoutes("shuffle"), () => directory);
            var player = (RoutePlayer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(RoutePlayer));
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(EntityComponent.Entity).GetField("m_components", fields).SetValue(player, new List<EntityComponent.Component>());
            player.m_body = new BodyComp(new Vector2(100, -1340), 18, 26);
            var brain = new EntityComponent.BT.BehaviorTreeComp(new RouteBrain()); typeof(PlayerEntity).GetField("m_bt", fields).SetValue(player, brain);
            player.AddComponents(false, player.m_body, new InputComponent(), brain);
            try
            {
                using (NativeSideLinks.Apply("mega-mapping-expansion", native.Engine.Links().Select(l => new WorldSideLink(l.Screen, l.Side, l.Target)), 300, 0))
                using (native.Activate(player))
                {
                    Require(NativeTransitions.Current == native, "scene-free activation owns its player callback and router");
                    Cross(native.Engine); var checkpoint = JKRuntime.State.GameState.Snapshots.Capture(); var saved = RouteBytes(native.Engine);
                    Cross(native.Engine); JKRuntime.State.GameState.Snapshots.Restore(checkpoint); TransitionSaveCoordinator.Save();
                    Require(RouteBytes(new NativeTransitions(native.Engine.Plan, () => directory).Engine).SequenceEqual(saved), "practice restore republishes restored routes for the next native save");
                    var host = WorldRegistry.Shared.Capture();
                    using (var playback = WorldControl.Begin("test.active-routes", "map", 1, WorldRole.Replica)) { playback.Receive(host); BadRoute(() => Cross(native.Engine), "activated receiver cannot spend local randomness"); }
                    Require(RouteBytes(native.Engine).SequenceEqual(saved), "activated world restoration keeps local choices and native links consistent");
                    SceneSaveCoordinator.Reset(directory); TransitionSaveCoordinator.Reset(directory); player.UpdateComponents(.01f);
                    Require(native.Engine.State.Pools[0].Current == -1 && native.Engine.State.Pools[0].Bag.Length == 0, "native new-game reset refreshes the active randomizer on the next player phase");
                    TransitionSaveCoordinator.Save(); Require(new NativeTransitions(native.Engine.Plan, () => directory).Engine.State.Pools[0].Current == -1, "active reset publishes into the new native save epoch");
                }
                Require(NativeTransitions.Current == null && !WorldRegistry.Shared.Inspect().Any(x => x.Id == "mega.mapping.transitions"), "teardown removes transition callbacks and shared state");
            }
            finally { player.Destroy(); }
        }
        private static void NativeRouteCrossings()
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var screenField = typeof(LevelManager).GetField("m_screens", flags); var countField = typeof(LevelManager).GetField("_total_screens", flags); var cameraField = typeof(Camera).GetField("_current_screen", flags);
            object oldScreens = screenField.GetValue(null), oldCount = countField.GetValue(null), oldCamera = cameraField.GetValue(null);
            var screens = Enumerable.Range(0, 300).Select(i => new LevelScreen(i, new IBlock[0], new LevelScreen.Graphics(), false, new[] { new TeleportLink(), new TeleportLink() }, 0, null)).ToArray();
            var original = screens[4].teleport[1]; var engine = new TransitionEngine(DestinationRoutes("shuffle"), 12);
            var fixture = new HarmonyLib.Harmony("mapping.route-fixture"); var method = typeof(HandlePlayerTeleportBehaviour).GetMethod("ExecuteBehaviour");
            try
            {
                screenField.SetValue(null, screens); countField.SetValue(null, screens.Length);
                using (NativeSideLinks.Apply("test.routes", engine.Links().Select(l => new WorldSideLink(l.Screen, l.Side, l.Target)), 300, 0))
                using (NativeSideLinks.RegisterRouter("test.routes", engine))
                {
                    engine.LinksChanged = () => { foreach (var link in engine.Links()) NativeSideLinks.UpdateOwned("test.routes", link.Screen, link.Side, link.Target); };
                    var teleport = new HandlePlayerTeleportBehaviour();
                    for (int n = 0; n < 30; n++)
                    {
                        cameraField.SetValue(null, 4); var body = new BodyComp(new Vector2(n % 2 == 0 ? 480 : -30, -1340), 18, 26) { Velocity = new Vector2(3, -4) };
                        var context = new BehaviourContext(body); var expected = new TransitionEngine(engine.Plan, engine.Seed); expected.Restore(engine.State); int target = Cross(expected);
                        teleport.ExecuteBehaviour(context);
                        Require(context.ContainsKey(HandlePlayerTeleportBehaviour.TeleportedPlayerFlag) && Camera.CurrentScreen == target - 1, "real native crossing uses the committed destination on either edge of a one-link screen");
                        Require(body.Velocity == new Vector2(3, -4) && body.Position.X == (n % 2 == 0 ? 0 : 450), "native movement keeps velocity and opposite-edge arrival");
                        Require(RouteBytes(engine).SequenceEqual(RouteBytes(expected)), "a real crossing advances exactly one choice");
                    }
                    foreach (string name in new[] { "CancelNativeRoute", "ThrowNativeRoute" })
                    {
                        var prefix = new HarmonyLib.HarmonyMethod(typeof(Tests).GetMethod(name, flags)) { priority = HarmonyLib.Priority.Last };
                        fixture.Patch(method, prefix: prefix); var before = RouteBytes(engine); var previous = screens[4].teleport[1]; cameraField.SetValue(null, 4);
                        var body = new BodyComp(new Vector2(480, -1340), 18, 26);
                        try { teleport.ExecuteBehaviour(new BehaviourContext(body)); } catch (InvalidOperationException) { if (name != "ThrowNativeRoute") throw; }
                        Require(RouteBytes(engine).SequenceEqual(before) && ReferenceEquals(previous, screens[4].teleport[1]), "canceled or throwing native execution rolls back its pending choice and temporary link");
                        fixture.UnpatchAll(fixture.Id);
                    }
                    var guard = typeof(NativeSideLinks).GetMethod("CheckForecast", flags); var untouched = RouteBytes(engine);
                    bool refused = false; try { guard.Invoke(null, new object[] { 4, 0 }); } catch (TargetInvocationException e) { refused = e.InnerException is NotSupportedException; }
                    Require(refused && RouteBytes(engine).SequenceEqual(untouched), "Warp forecasts fail explicitly at dynamic edges without spending live randomness"); guard.Invoke(null, new object[] { 1, 1 });
                    var world = new RouteFlightWorld { Index = 4 };
                    var adapter = (JumpKing.API.IBodyCompBehaviour)Activator.CreateInstance(typeof(JKRuntime.Simulation.NativeFlightSimulation).GetNestedType("Teleport", BindingFlags.NonPublic), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { world }, null);
                    var shadow = new BodyComp(new Vector2(-30, -1340), 18, 26);
                    BadRoute(() => adapter.ExecuteBehaviour(new BehaviourContext(shadow)), "actual native flight adapter rejects dynamic crossing");
                    Require(world.Calls == 0 && RouteBytes(engine).SequenceEqual(untouched), "rejected forecast never calls the teleport model or live router");
                    world.Index = 1; adapter.ExecuteBehaviour(new BehaviourContext(shadow)); Require(world.Calls == 1, "ordinary flight crossings still delegate to their adapter");
                }
                Require(ReferenceEquals(original, screens[4].teleport[1]), "route teardown restores original native links after all rerolls");
                ActivatedRoutes(screens);
            }
            finally { fixture.UnpatchAll(fixture.Id); screenField.SetValue(null, oldScreens); countField.SetValue(null, oldCount); cameraField.SetValue(null, oldCamera); JKRuntime.Geometry.MapTopology.Invalidate(); }
        }
    }
}
