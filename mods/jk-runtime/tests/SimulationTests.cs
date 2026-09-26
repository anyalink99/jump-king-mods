using System;
using System.Linq;
using System.Threading;
using JKRuntime.Simulation;
using Microsoft.Xna.Framework;

namespace JKRuntime
{
    internal static class SimulationTests
    {
        private static SimulationRequirement Claim(string id, int version = 1)
        { return new SimulationRequirement(id, new Version(version, 0)); }
        private static SimulationSeed Seed(params SimulationRequirement[] claims)
        { return new SimulationSeed(new SimulationPose { Width = 18, Height = 26 }, 0, 1.0 / 60, "test-only", new byte[0], claims); }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private static void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected rejection"); }
        private static void Main()
        {
            try
            {
                DormancyAndIsolation(); CoverageAndOrder(); LifetimeAndFailures(); WindParity(); FirstDivergence();
                InventoryReadCache();
                NativeInventoryReads();
                Console.WriteLine("[OK] Simulation: dormant callbacks=0; exact coverage/order; branch isolation; cancellation, budgets and lifetime");
            }
            catch (Exception error) { Console.Error.WriteLine(error); Environment.Exit(1); }
        }
        private static void NativeInventoryReads()
        {
            var save = typeof(JumpKing.Player.BodyComp).Assembly.GetType("JumpKing.SaveThread.SaveLube", true);
            var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var field = save.GetField("loaded_objects", flags);
            object oldCache = field.GetValue(null);
            string oldDirectory = System.IO.Directory.GetCurrentDirectory();
            string directory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "item-cache-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            System.IO.Directory.SetCurrentDirectory(directory);
            try
            {
                field.SetValue(null, new System.Collections.Generic.Dictionary<string, object>());
                var inventory = new JumpKing.MiscEntities.WorldItems.Inventory.Inventory().GetDefault();
                inventory.items.Add(new JumpKing.MiscEntities.WorldItems.Inventory.InventoryItem {
                    item = JumpKing.MiscEntities.WorldItems.Items.SnakeRing, count = 1 });
                var property = save.GetProperty("inventory", flags);
                property.SetValue(null, inventory, null);
                save.GetProperty("generalSettings", flags).SetValue(null, new JumpKing.SaveThread.GeneralSettings().GetDefault(), null);
                field.SetValue(null, new System.Collections.Generic.Dictionary<string, object>());
                string inventoryPath = System.IO.Path.Combine(directory, "Content/SavesPerma/inventory.inv");
                byte[] before = System.IO.File.ReadAllBytes(inventoryPath);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 100; i++) Check(JumpKing.MiscEntities.WorldItems.Inventory.InventoryManager.HasItem(JumpKing.MiscEntities.WorldItems.Items.SnakeRing), "Native disk inventory read");
                watch.Stop(); double uncached = watch.Elapsed.TotalMilliseconds;
                NativeItemReadCache.Ensure();
                watch.Restart();
                for (int i = 0; i < 100; i++) Check(JumpKing.MiscEntities.WorldItems.Inventory.InventoryManager.HasItem(JumpKing.MiscEntities.WorldItems.Items.SnakeRing), "Cached native inventory result unchanged");
                watch.Stop();
                Check(before.SequenceEqual(System.IO.File.ReadAllBytes(inventoryPath)), "Priming never writes inventory");
                inventory.items.Clear(); property.SetValue(null, inventory, null);
                NativeItemReadCache.Ensure();
                Check(!JumpKing.MiscEntities.WorldItems.Inventory.InventoryManager.HasItem(JumpKing.MiscEntities.WorldItems.Items.SnakeRing), "Native inventory writes remain authoritative");
                field.SetValue(null, new System.Collections.Generic.Dictionary<string, object>());
                NativeItemReadCache.Ensure();
                Check(!JumpKing.MiscEntities.WorldItems.Inventory.InventoryManager.HasItem(JumpKing.MiscEntities.WorldItems.Items.SnakeRing), "Restart reloads the new native inventory");
                Console.WriteLine("[PERF] 100 actual native inventory reads: disk=" + uncached.ToString("F3") + " ms, primed=" + watch.Elapsed.TotalMilliseconds.ToString("F3") + " ms");
            }
            finally { field.SetValue(null, oldCache); System.IO.Directory.SetCurrentDirectory(oldDirectory); }
        }
        private static void InventoryReadCache()
        {
            var cache = new System.Collections.Generic.Dictionary<string, object>();
            int reads = 0;
            object current = new object();
            Func<object> read = () => { reads++; return current; };
            for (int i=0;i<1000;i++) NativeItemReadCache.WarmMissing(cache,"inventory",read);
            Check(reads==1 && ReferenceEquals(cache["inventory"],current),"Cold inventory was read repeatedly");
            object equipped = new object(); cache["inventory"] = equipped;
            NativeItemReadCache.WarmMissing(cache,"inventory",read);
            Check(reads==1 && ReferenceEquals(cache["inventory"],equipped),"Native equipment update overwritten");
            cache.Clear(); current = new object();
            NativeItemReadCache.WarmMissing(cache,"inventory",read);
            Check(reads==2 && ReferenceEquals(cache["inventory"],current),"Cleared cache retained old inventory");
            cache = new System.Collections.Generic.Dictionary<string, object>(); current = new object();
            NativeItemReadCache.WarmMissing(cache,"inventory",read);
            Check(reads==3 && ReferenceEquals(cache["inventory"],current),"Restart retained old cache");
            NativeItemReadCache.WarmMissing(cache,"new-folder/inventory",read);
            Check(reads==4,"Save-folder change reused old data");
            NativeItemReadCache.WarmMissing(cache,"settings",() => { cache["settings"]=equipped; return current; });
            Check(ReferenceEquals(cache["settings"],equipped),"Read callback update overwritten");
            Throws(() => NativeItemReadCache.WarmMissing(cache,"failed",() => { throw new Exception("read failed"); }));
            Check(!cache.ContainsKey("failed"),"Failed read polluted cache");
            Console.WriteLine("[OK] Native item read cache: one cold read; existing values, equipment writes, clear/restart, paths and failures preserved");
        }
        private static void DormancyAndIsolation()
        {
            var registry = new SimulationRegistry(); int captures = 0, ticks = 0;
            byte[] source = { 0 }; SimulationTick retained = null;
            var provider = new SimulationProvider("native", "fixture", new[] { Claim("movement") }, new[] { SimulationPhase.Controls },
                seed => { captures++; return source; }, frame => {
                    ticks++; retained = frame; var data = frame.State; data[0]++; frame.State = data; data[0] = 250;
                    var pose = frame.Pose; pose.Position.X += frame.Input.Direction; frame.Pose = pose;
                });
            using (registry.Register(provider))
            {
                for (int i = 0; i < 10000; i++) Check(registry.ActiveSessions == 0, "Unexpected background session");
                Check(registry.CheckCoverage(new[] { Claim("movement") }).Length == 0, "Coverage");
                Check(captures == 0 && ticks == 0, "Idle registry executed simulation work");
                using (var session = registry.Open(Seed(Claim("movement"))))
                {
                    source[0] = 99;
                    Check(session.Initial.Read("native")[0] == 0, "Capture shared live bytes");
                    var initial = session.Initial;
                    var left = session.Step(initial, new SimulationInput(-1, false)).State;
                    var right = session.Step(initial, new SimulationInput(1, false)).State;
                    Check(initial.Read("native")[0] == 0 && left.Read("native")[0] == 1 && right.Read("native")[0] == 1, "Branches shared state");
                    var leak = left.Read("native"); leak[0] = 42;
                    Check(left.Read("native")[0] == 1, "Snapshot exposed mutable bytes");
                    Check(left.Pose.Position.X == -1 && right.Pose.Position.X == 1 && left.Key != right.Key, "Pose branches / key");
                    var wait = session.Step(initial, new SimulationInput(0, false)).State;
                    Check(wait.Key != initial.Key, "Time omitted from state identity");
                    Throws(() => retained.State = new byte[0]);
                    using (var other = registry.Open(Seed(Claim("movement")))) Throws(() => other.Step(initial, new SimulationInput()));
                }
                int before = ticks;
                for (int i = 0; i < 10000; i++) Check(registry.ActiveSessions == 0, "Leaked active session");
                Check(ticks == before, "Work continued after disposal");
            }
        }
        private static SimulationProvider Provider(string id, string claim, Action<SimulationTick> tick, params string[] after)
        { return new SimulationProvider(id, "fixture", new[] { Claim(claim) }, new[] { SimulationPhase.Controls }, s => new byte[0], tick, after); }
        private static void CoverageAndOrder()
        {
            foreach (bool reverse in new[] { false, true })
            {
                string order = ""; var registry = new SimulationRegistry();
                var a = Provider("a", "one", f => order += "a");
                var b = Provider("b", "two", f => order += "b", "a");
                using (registry.Register(reverse ? b : a)) using (registry.Register(reverse ? a : b))
                using (var session = registry.Open(Seed(Claim("two"))))
                { session.Step(session.Initial, new SimulationInput()); Check(order == "ab", "Dependency order changed with registration"); }
            }
            var r = new SimulationRegistry();
            Check(r.CheckCoverage(new[] { Claim("missing") }).Single().Contains("Unsupported"), "Unknown accepted");
            Throws(() => r.Open(Seed()));
            using (r.Register(Provider("a", "one", f => { })))
            {
                Throws(() => r.Open(Seed(Claim("one", 2))));
                using (r.Register(Provider("b", "one", f => { }))) Throws(() => r.Open(Seed(Claim("one"))));
            }
            using (r.Register(Provider("a", "one", f => { }, "b")))
            using (r.Register(Provider("b", "two", f => { }, "a"))) Throws(() => r.Open(Seed(Claim("one"))));
            using (r.Register(Provider("a", "one", f => { }, "absent"))) Throws(() => r.Open(Seed(Claim("one"))));
        }
        private static void LifetimeAndFailures()
        {
            var registry = new SimulationRegistry();
            var registration = registry.Register(Provider("a", "one", f => { }));
            var session = registry.Open(Seed(Claim("one"))); var initial = session.Initial;
            var cancel = new CancellationTokenSource(); cancel.Cancel();
            Throws(() => session.Step(initial, new SimulationInput(), cancel.Token));
            Check(session.Step(initial, new SimulationInput()).State.Tick == 1, "Cancelled step changed initial state");
            registration.Dispose(); Check(registry.ActiveSessions == 0, "Provider unload left session alive");
            Throws(() => session.Step(initial, new SimulationInput()));
            using (registry.Register(Provider("bad", "bad", f => { f.State = new byte[SimulationSession.MaxStateBytes + 1]; })))
            using (var bad = registry.Open(Seed(Claim("bad"))))
            { Throws(() => bad.Step(bad.Initial, new SimulationInput())); Check(registry.ActiveSessions == 0, "Failed session not disposed"); }
            using (registry.Register(Provider("pose", "pose", f => { var p = f.Pose; p.Position = new Vector2(float.NaN, 0); f.Pose = p; })))
            using (var bad = registry.Open(Seed(Claim("pose")))) Throws(() => bad.Step(bad.Initial, new SimulationInput()));
            using (registry.Register(new SimulationProvider("capture", "fixture", new[] { Claim("capture") }, new[] { SimulationPhase.World },
                s => { registry.Invalidate(); return new byte[0]; }, f => { }))) Throws(() => registry.Open(Seed(Claim("capture"))));
            Check(registry.ActiveSessions == 0, "Failed capture leaked session");
        }
        private static void WindParity()
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance;
            var assembly = typeof(JumpKing.Game1).Assembly;
            var managerType = assembly.GetType("JumpKing.MiscSystems.Achievements.AchievementManager", true);
            var statsType = assembly.GetType("JumpKing.MiscSystems.Achievements.PlayerStats", true);
            var managerField = managerType.GetField("instance", flags);
            var gameField = typeof(JumpKing.Game1).GetField("_instance", flags);
            var screensField = typeof(JumpKing.Level.LevelManager).GetField("m_screens", flags);
            var screenField = typeof(JumpKing.Camera).GetField("_current_screen", flags);
            var totalField = typeof(JumpKing.Level.LevelManager).GetField("_total_screens", flags);
            var fields = new[] { managerField, gameField, screensField, screenField, totalField };
            var saved = fields.Select(f => f.GetValue(null)).ToArray();
            try
            {
                var game = (JumpKing.Game1)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(JumpKing.Game1));
                var manager = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(managerType);
                managerField.SetValue(null, manager); gameField.SetValue(null, game); screenField.SetValue(null, 0); totalField.SetValue(null, 1);
                int cases = 0;
                foreach (long clockTicks in new long[] { 166667, 170000 })
                foreach (bool enabled in new[] { false, true })
                foreach (float intensity in new[] { 0f, 1f, 8f, 16f })
                foreach (bool? direction in new bool?[] { null, false, true })
                {
                    // The public setter notifies the graphics platform; this
                    // headless fixture only supplies the getter's clock field.
                    typeof(Microsoft.Xna.Framework.Game).GetField("_targetElapsedTime", flags).SetValue(game, TimeSpan.FromTicks(clockTicks));
                    screensField.SetValue(null, new[] { new JumpKing.Level.LevelScreen(0, new JumpKing.Level.IBlock[0],
                        new JumpKing.Level.LevelScreen.Graphics(), enabled, new JumpKing.Level.TeleportLink[0], intensity, direction) });
                    for (int ticks = 0; ticks < 2500; ticks += 7)
                    {
                        var stats = Activator.CreateInstance(statsType); statsType.GetField("_ticks", flags).SetValue(stats, ticks);
                        managerType.GetField("m_all_time_stats", flags).SetValue(manager, stats);
                        float actual = JumpKing.WindManager.CurrentVelocityRaw;
                        float predicted = NativeWind.Velocity(ticks, game.TargetElapsedTime.TotalSeconds, 0, enabled, intensity, direction);
                        Check(actual == predicted, "Native wind mismatch at tick " + ticks); cases++;
                    }
                }
                Console.WriteLine("[OK] " + cases + " exact installed-game wind comparisons: clock rounding, reversals, intensity and fixed direction");
            }
            finally { for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, saved[i]); }
        }
        private static void FirstDivergence()
        {
            int reference = 0, candidate = 0;
            var result = SimulationConformance.Compare("intentional mismatch fixture", Enumerable.Repeat(new SimulationInput(1, false), 10),
                () => new System.Collections.Generic.SortedDictionary<string, string> { { "x", reference.ToString() } },
                () => new System.Collections.Generic.SortedDictionary<string, string> { { "x", candidate.ToString() } },
                input => reference++, input => candidate += candidate == 3 ? 2 : 1);
            Check(!result.Matches && result.Tick == 4 && result.Differences.Length == 1 && result.Differences[0].Contains("x: expected=4; actual=5"), "First divergent tick/field lost");
            Check(reference == 4, "Conformance continued after mismatch");
        }
    }
}
