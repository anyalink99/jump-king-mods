using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using BehaviorTree;
using JKRuntime;
using JKRuntime.Simulation;
using JumpKing;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Player;
using JumpKing.MiscEntities.WorldItems;
using JumpKing.MiscEntities.WorldItems.Inventory;

namespace ScreenSolver
{
    internal sealed class SolveCapture : IDisposable
    {
        internal readonly LevelScreen[] Screens;
        internal readonly SimulationSeed Seed;
        internal readonly PrioritySearch Search;
        internal readonly bool Timed;
        private IDisposable registration;
        private SolveCapture(LevelScreen[] screens, SimulationSeed seed, NativeMemory memory, bool snake, double windClock, bool[] verticalWind, CustomWindProfile[] customWind)
        {
            Screens = screens; Seed = seed;
            Timed = screens.Any(s => s.WindEndabled) || customWind.Any(p => p.Enabled);
            var registry = RuntimeApi.Simulation;
            registration = registry.Register(new NativePlayer(screens, memory, snake, windClock, verticalWind, customWind).Provider());
            try { Search = new PrioritySearch(new NativeWorld(screens, seed.Pose.Screen).ExitTargets(),
                targets => new SearchJob(registry.Open(seed), NativeActions.Expand, targets, nodeLimit: 2000, tickLimit: 350000,
                    equivalentState: Timed ? (Func<SimulationSnapshot, string>)null : NativeActions.StaticIdentity)); }
            catch { registration.Dispose(); registration = null; throw; }
        }
        internal static SolveCapture Capture(PlayerEntity player)
        {
            if (player == null || player.m_body == null) throw new NotSupportedException("No active player");
            var body = player.m_body;
            var jump = NativePlayer.Field<JumpState>(player, "m_jump_state");
            var fail = NativePlayer.Field<FailState>(player, "m_fail_state");
            if (!body.Enabled || jump == null || fail == null || jump.IsRunning() || fail.IsRunning() ||
                JumpKing.Controller.ControllerManager.instance.GetPadState().jump)
                throw new NotSupportedException("Release Jump and let the player finish charging or recovering, then Solve again");
            if (!body.IsOnGround || body.IsKnocked)
                throw new NotSupportedException("Start Solve while standing on a platform");
            string hash;
            using (var stream = File.OpenRead(typeof(BodyComp).Assembly.Location))
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            if (hash != NativeWind.AuditedGameSha256) throw new NotSupportedException("This Jump King executable has not been audited");
            var verticalWind = new VerticalWindAdapter();
            var passive = new PassiveAdapters();
            var workshop = new WorkshopCoverage(passive);
            var adapters = new PlayerAdapters();
            Audit(body, player, verticalWind, passive, workshop, adapters);
            var skinManager = typeof(BodyComp).Assembly.GetType("JumpKing.Player.Skins.SkinManager", true);
            if ((bool)skinManager.GetMethod("IsWearingSkin", NativeWorld.Flags).Invoke(null, new object[] { Items.GiantBoots }))
                throw new NotSupportedException("Giant Boots landing pauses need a simulation adapter");
            var screens = NativeWorld.Capture(passive);
            var pose = new SimulationPose { Position = body.Position, Velocity = body.Velocity, Width = body.GetHitbox().Width,
                Height = body.GetHitbox().Height, Grounded = body.IsOnGround, Screen = Camera.CurrentScreen };
            // Native JumpGame.Update hard-codes 1f/60 for gameplay. Wind's
            // achievement clock instead uses TargetElapsedTime. Do not merge them.
            var seed = new SimulationSeed(pose, CurrentTick(), 1.0 / 60,
                hash, new byte[0], new[] { NativePlayer.Requirement });
            var memory = NativePlayer.Capture(body); memory.Subframe = adapters.Subframe;
            memory.InputClock = Game1.instance.TargetElapsedTime.TotalSeconds;
            return new SolveCapture(screens, seed, memory, InventoryManager.HasItemEnabled(Items.SnakeRing),
                Game1.instance.TargetElapsedTime.TotalSeconds, verticalWind.Capture(screens.Length), workshop.CaptureWind(screens.Length));
        }
        internal static long CurrentTick()
        {
            var type = typeof(BodyComp).Assembly.GetType("JumpKing.MiscSystems.Achievements.AchievementManager", true);
            var stats = type.GetMethod("GetCurrentStats", NativeWorld.Flags).Invoke(type.GetField("instance", NativeWorld.Flags).GetValue(null), null);
            return Convert.ToInt64(stats.GetType().GetField("_ticks", NativeWorld.Flags).GetValue(stats));
        }
        private static void Audit(BodyComp body, PlayerEntity player, VerticalWindAdapter verticalWind, PassiveAdapters passive, WorkshopCoverage workshop, PlayerAdapters adapters)
        {
            var game = typeof(BodyComp).Assembly;
            AuditPatches(game, verticalWind, passive, workshop); // Before constructing or invoking any native reference objects.
            adapters.ValidateModes();
            var reference = new BodyComp(body.Position, body.GetHitbox().Width, body.GetHitbox().Height);
            var activePipeline = NativePlayer.Field<LinkedList<IBodyCompBehaviour>>(body, "m_behaviours").Where(b => !adapters.Ignore(b)).ToArray();
            if (!activePipeline.Select(b => b.GetType()).SequenceEqual(
                NativePlayer.Field<LinkedList<IBodyCompBehaviour>>(reference, "m_behaviours").Select(b => b.GetType())))
                throw new NotSupportedException("The active body pipeline differs from vanilla. A gameplay simulation adapter is required.");
            foreach (var b in activePipeline)
                if (b.GetType().Assembly != game) throw new NotSupportedException("Body behaviour needs an adapter: " + b.GetType().FullName);
            foreach (var b in NativePlayer.Field<LinkedList<IBlockBehaviour>>(body, "m_blockBehaviours"))
                if (b.GetType().Assembly != game && !passive.IsAudioBehaviour(b.GetType()) && !workshop.IgnoreInactiveBehaviour(b))
                    throw new NotSupportedException("Block behaviour needs an adapter: " + b.GetType().FullName);
            foreach (var c in player.GetComponents())
                if (c.Enabled && c.GetType().Assembly != game && !adapters.Ignore(c)) throw new NotSupportedException("Player component needs an adapter: " + c.GetType().FullName);
            var tree = player.GetComponent<EntityComponent.BT.BehaviorTreeComp>();
            var visited = new HashSet<IBTnode>();
            var root = NativePlayer.Field<IBTnode>(tree.GetRaw(), "m_root_node");
            AuditNode(root, game, visited, adapters, workshop);
            var blank = (PlayerEntity)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
            var expectedTree = (EntityComponent.BT.BehaviorTreeComp)typeof(PlayerEntity).GetMethod("MakeBT", NativeWorld.Flags).Invoke(blank, null);
            if (TreeShape(root, new Dictionary<IBTnode, int>(), workshop) !=
                TreeShape(NativePlayer.Field<IBTnode>(expectedTree.GetRaw(), "m_root_node"), new Dictionary<IBTnode, int>(), workshop))
                throw new NotSupportedException("The native controller tree was rearranged. A simulation adapter is required.");
            if (EntityComponent.EntityManager.instance != null)
            {
                var unsupportedEntities = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var entity in EntityComponent.EntityManager.instance.Entities)
                    if (entity.IsAlive && entity.GetType().Assembly != game && entity.GetType().Assembly != typeof(RuntimeApi).Assembly &&
                        entity.GetType().Assembly != typeof(ModEntry).Assembly && !adapters.Ignore(entity))
                        unsupportedEntities.Add(entity.GetType().FullName);
                if (unsupportedEntities.Count != 0)
                    throw new NotSupportedException("World entities need simulation adapters:\n" + string.Join("\n", unsupportedEntities));
            }
        }
        internal static void AuditPatches(Assembly game, VerticalWindAdapter verticalWind, PassiveAdapters passive = null, WorkshopCoverage workshop = null)
        {
            passive = passive ?? new PassiveAdapters();
            workshop = workshop ?? new WorkshopCoverage(passive);
            var blockers = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            // Reflection allows coexistence with the game's loaded Harmony versions;
            // Screen Solver does not ship or select a different patch engine.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var harmony = assembly.GetType("HarmonyLib.Harmony", false);
                if (harmony == null) continue;
                var all = harmony.GetMethod("GetAllPatchedMethods", Type.EmptyTypes);
                var info = harmony.GetMethod("GetPatchInfo", new[] { typeof(MethodBase) });
                if (all == null || info == null) throw new NotSupportedException("Cannot inspect this Harmony build");
                foreach (MethodBase method in (IEnumerable)all.Invoke(null, null))
                {
                    string ns = method.DeclaringType == null ? "" : method.DeclaringType.Namespace ?? "";
                    bool adapterMethod = method.DeclaringType != null && (PassiveAdapters.IsAdapterMethod(method.DeclaringType) || WorkshopCoverage.IsKnownAssembly(method.DeclaringType.Assembly) || PlayerAdapters.Known(method.DeclaringType.Assembly));
                    if (!adapterMethod && (method.DeclaringType == null || method.DeclaringType.Assembly != game ||
                        !(ns.StartsWith("JumpKing.Player") || ns.StartsWith("JumpKing.BodyCompBehaviours") ||
                          ns.StartsWith("JumpKing.BlockBehaviours") || ns.StartsWith("JumpKing.Level") || ns == "BehaviorTree" ||
                          method.DeclaringType == typeof(WindManager) || method.DeclaringType == typeof(PlayerValues) ||
                          method.DeclaringType == typeof(InventoryManager) || method.DeclaringType == typeof(JumpKing.Controller.PadInstance)))) continue;
                    var patches = info.Invoke(null, new object[] { method });
                    if (patches == null) continue;
                    foreach (string kind in new[] { "Prefixes", "Postfixes", "Transpilers", "Finalizers" })
                    {
                        var field = patches.GetType().GetField(kind); var property = patches.GetType().GetProperty(kind);
                        var values = (field != null ? field.GetValue(patches) : property == null ? null : property.GetValue(patches, null)) as IEnumerable;
                        if (values == null) throw new NotSupportedException("Unreadable Harmony patch inventory");
                        foreach (var patch in values)
                        {
                            string owner = ReadPatchMember(patch, "owner") as string ?? "Unknown owner";
                            var patchMethod = ReadPatchMember(patch, "PatchMethod") as MethodInfo;
                            if (verticalWind.Accepts(method, kind, owner, patchMethod) || passive.Accepts(method, kind, owner, patchMethod) ||
                                workshop.Accepts(method, kind, owner, patchMethod)) continue;
                            SortedSet<string> targets;
                            if (!blockers.TryGetValue(owner, out targets)) blockers.Add(owner, targets = new SortedSet<string>(StringComparer.Ordinal));
                            targets.Add(method.DeclaringType.Name + "." + method.Name + " [" + kind + "]");
                        }
                    }
                }
            }
            if (blockers.Count == 0) return;
            Console.WriteLine("[Screen Solver] Unsupported gameplay patches:\n" + string.Join("\n", blockers.Select(
                b => b.Key + ": " + string.Join(", ", b.Value))));
            throw new NotSupportedException("Gameplay adapters needed:\n" + string.Join("\n", blockers.Select(
                b => b.Key + " (" + b.Value.Count + ")")) + "\nPatch details are in the game log.");
        }
        private static object ReadPatchMember(object patch, string name)
        {
            var type = patch.GetType(); var field = type.GetField(name); var property = type.GetProperty(name);
            return field != null ? field.GetValue(patch) : property == null ? null : property.GetValue(patch, null);
        }
        private static string TreeShape(IBTnode node, Dictionary<IBTnode, int> seen, WorkshopCoverage workshop)
        {
            int index; if (seen.TryGetValue(node, out index)) return "@" + index;
            seen.Add(node, seen.Count);
            string state = PlayerAdapters.NodeName(node);
            if (node is JumpKing.Util.PauseNode) state += ":" + NativePlayer.Field<JumpKing.Timer>(node, "m_timer").Duration.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            if (node is StaticNode) state += ":" + NativePlayer.Field<BTresult>(node, "m_static_result");
            return state + "(" + string.Join(",", node.GetRelatedNodes().Where(n => !workshop.IsPassiveNode(n)).Select(n => TreeShape(n, seen, workshop))) + ")";
        }
        private static void AuditNode(IBTnode node, Assembly game, HashSet<IBTnode> visited, PlayerAdapters adapters, WorkshopCoverage workshop)
        {
            if (node == null || !visited.Add(node)) return;
            if (workshop.IsPassiveNode(node)) return;
            if (node.GetType().Assembly != game && !adapters.Node(node)) throw new NotSupportedException("Controller node needs an adapter: " + node.GetType().FullName);
            foreach (var child in node.GetRelatedNodes()) AuditNode(child, game, visited, adapters, workshop);
        }
        public void Dispose()
        { if (Search != null) Search.Dispose(); if (registration != null) { registration.Dispose(); registration = null; } }
    }
}
