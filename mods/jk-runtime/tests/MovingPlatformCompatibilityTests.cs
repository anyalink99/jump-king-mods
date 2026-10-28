using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using EntityComponent;
using JKRuntime.Compatibility;
using JKRuntime.Gameplay;
using JKRuntime.UI;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.GameManager;
using JumpKing.Mods;
using JumpKing.Player;
using JumpKing.SaveThread.SaveComponents;
using Microsoft.Xna.Framework;

namespace JKRuntime
{
    internal static class MovingPlatformCompatibilityTests
    {
        private static Assembly foreign;
        private static MethodInfo start;
        private static int checks, runs, peerCalls;
        private sealed class Marker : IBodyCompBehaviour
        { public bool ExecuteBehaviour(BehaviourContext context) { return true; } }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        private static void Peer() { peerCalls++; }
        private static void Apply(bool enabled)
        { SettingsStore.Current.ModCompatibilityFixes = enabled; MovingPlatformCompatibility.Apply(); }
        private static uint Count(BodyComp body) { return ModifierRegistrationEvidence.ReadExternal(body); }
        private static PlayerEntity Player(bool empty)
        {
            RunModifiers.Finish();
            var manager = EntityManager.instance ?? new EntityManager();
            ((List<Entity>)typeof(EntityManager).GetField("entities", OwnedPatches.Members).GetValue(manager)).Clear();
            var loop = new GameLoop();
            var completion = typeof(GameLoop).GetField("m_ending_body_modifiers", OwnedPatches.Members);
            completion.SetValue(loop, Activator.CreateInstance(completion.FieldType, true));
            typeof(BodyComp).Assembly.GetType("JumpKing.SaveThread.SaveLube").GetProperty("CombinedSave").SetValue(null,
                new JumpKing.SaveThread.CombinedSaveFile { full_run = new SaveCompCushion<FullRunSave> { initialized = true } }, null);
            var player = (PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
            GC.SuppressFinalize(player);
            typeof(Entity).GetField("m_components", OwnedPatches.Members).SetValue(player, new List<Component>());
            player.m_body = new BodyComp(Vector2.Zero, 18, 26);
            var list = new LinkedList<IBodyCompBehaviour>();
            if (!empty) list.AddLast(new Marker());
            typeof(BodyComp).GetField("m_behaviours", OwnedPatches.Members).SetValue(player.m_body, list);
            manager.AddObject(player);
            RunModifiers.Begin(player.m_body, "platform-" + (++runs), 0,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "attempt-" + Guid.NewGuid().ToString("N") + ".xml"));
            return player;
        }
        private static void Start() { start.Invoke(null, null); RunModifiers.Observe(); }
        private static IBodyCompBehaviour Updater(BodyComp body)
        { return body.GetBehaviourList().Single(b => b.GetType().FullName == "MovingPlatformMod.MovingPlatformUpdater"); }
        private static void Movement(BodyComp body)
        {
            var entity = foreign.GetType("MovingPlatformMod.MovingPlatformEntity", true);
            var axis = foreign.GetType("MovingPlatformMod.MovingAxis", true);
            var texture = foreign.GetType("MovingPlatformMod.MovingPlatformTextureType", true);
            var range = foreign.GetType("MovingPlatformMod.MovingRangeBlock", true);
            var platform = Activator.CreateInstance(entity, new object[] { new Rectangle(100, 100, 20, 8), Enum.ToObject(texture, 0), 0, Enum.ToObject(axis, 0) });
            Activator.CreateInstance(range, new object[] { new Rectangle(120, 100, 3, 8), 0, Enum.ToObject(axis, 0) });
            foreign.GetType("MovingPlatformMod.MovingPlatformGroup", true).GetMethod("RebuildGroups").Invoke(null, null);
            var updater = Updater(body);
            foreach (int x in new[] { 101, 102, 103, 103, 102 })
            {
                Check(updater.ExecuteBehaviour(null), "Original platform updater still runs");
                Check(((Rectangle)entity.GetField("Hitbox").GetValue(platform)).X == x, "Original travel and turnaround preserved");
            }
            foreign.GetType("MovingPlatformMod.MovingBlockFactory", true).GetMethod("ResetAll").Invoke(null, null);
        }
        public static int Main(string[] args)
        {
            try
            {
                SettingsStore.EnsureLoaded(); Apply(true);
                Check(MovingPlatformCompatibility.Status.StartsWith("Not needed:"), "Absent mod isn't loaded by adapter");
                if (args[0] != "none") Assembly.LoadFrom(args[0]);
                foreign = Assembly.LoadFrom(args[1]);
                ModLoader.Instance.LoadedMods.Add(new ModAssembly(foreign, new JumpKingModAttribute("MovingPlatformMod")));
                start = foreign.GetType("MovingPlatformMod.ModEntry", true).GetMethod("OnLevelStart");
                if (args.Length > 2 && args[2] == "refuse")
                {
                    Apply(true);
                    Check(MovingPlatformCompatibility.Status.StartsWith("Unavailable:") || MovingPlatformCompatibility.Status.StartsWith("Unsupported:"), "Unreviewed build or missing engine refused");
                    var refused = Player(false); Start();
                    Check(Count(refused.m_body) == 1, "Refusal preserves native modifier registration");
                    Console.WriteLine("[OK] Moving platform refusal: " + MovingPlatformCompatibility.Status);
                    return 0;
                }
                bool lateObserver = args.Length > 2 && args[2] == "adapter-first";
                if (!lateObserver) ModifierRegistrationObserver.TryInstall();
                Apply(true);
                Check(MovingPlatformCompatibility.Status.StartsWith("Active:"), MovingPlatformCompatibility.Status);
                if (lateObserver) ModifierRegistrationObserver.TryInstall();
                using (var peer = new OwnedPatches("test.moving-platform-peer"))
                {
                    peer.Add(start, postfix: typeof(MovingPlatformCompatibilityTests).GetMethod("Peer", OwnedPatches.Members));
                    foreach (bool empty in new[] { false, true })
                    {
                        var player = Player(empty); var body = player.m_body;
                        var previous = body.GetBehaviourList().ToArray();
                        Start();
                        Check(Count(body) == 0 && RunModifiers.ReadNativePeak() == 0 && RunModifiers.GetContributors().Length == 0, "Adapted registration leaves native count and evidence clean");
                        Check(body.GetBehaviourList().First() == Updater(body) && body.GetBehaviourList().Skip(1).SequenceEqual(previous), "Original updater and placement retained, including empty-list fallback");
                        Check(body.GetBehaviourList().Count == previous.Length + 1, "Exactly one updater installed");
                        Start();
                        Check(body.GetBehaviourList().Count == previous.Length + 1, "Repeated foreign startup does not duplicate updater");
                        Movement(body);
                        var marker = new Marker(); body.RegisterBehaviour(marker); RunModifiers.Observe();
                        Check(Count(body) == 1 && RunModifiers.ReadNativePeak() == 1 && RunModifiers.GetContributors().Length == 1, "Unrelated modifiers still counted and reported");
                        body.RemoveBehaviour(marker); RunModifiers.Observe();
                        Check(Count(body) == 0 && RunModifiers.GetContributors().Length == 1, "Unrelated removal retains history without touching platform slot");
                    }
                    Apply(false);
                    var nativePlayer = Player(false); Start();
                    Check(Count(nativePlayer.m_body) == 1 && RunModifiers.GetContributors().Single() == "MovingPlatformMod", "Disabled adapter restores native registration and attribution");
                    Apply(true); Start();
                    Check(Count(nativePlayer.m_body) == 1 && RunModifiers.GetContributors().Single() == "MovingPlatformMod", "Enabling doesn't erase existing attempt history");
                    var cleanPlayer = Player(false); Start();
                    Check(Count(cleanPlayer.m_body) == 0 && RunModifiers.GetContributors().Length == 0, "Next player uses enabled adapter");
                    var direct = (IBodyCompBehaviour)Activator.CreateInstance(foreign.GetType("MovingPlatformMod.MovingPlatformUpdater"));
                    cleanPlayer.m_body.RegisterBehaviour(direct); RunModifiers.Observe();
                    Check(Count(cleanPlayer.m_body) == 1 && RunModifiers.GetContributors().Single() == "MovingPlatformMod", "Same foreign type registered outside patched caller is still counted");
                    cleanPlayer = Player(false); Start();
                    Apply(false); Start();
                    Check(Count(cleanPlayer.m_body) == 0, "Disabling doesn't retroactively reclassify an existing slot");
                    int beforePeer = peerCalls; Player(false); Start();
                    Check(peerCalls == beforePeer + 1, "Removing adapter preserves foreign patches");
                }
                Check(RunModifiers.WaitForWrites(5000), "Evidence writes finish inside fixture directory");
                Console.WriteLine("[OK] Moving platform compatibility: " + checks + " checks");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
