using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using BehaviorTree;
using EntityComponent;
using EntityComponent.BT;
using JumpKing;
using JumpKing.Controller;
using JumpKing.Level;
using JumpKing.Player;
using JumpKing.XnaWrappers;
using Microsoft.Xna.Framework;

namespace HammerKing
{
    internal static partial class Tests
    {
        private static bool equipmentIntegration;
        private sealed class PassiveBlock : JumpKing.API.IBlockBehaviour
        {
            public float BlockPriority { get { return 0f; } }
            public bool IsPlayerOnBlock { get; set; }
            public float ModifyXVelocity(float value, JumpKing.BodyCompBehaviours.BehaviourContext context) { return value; }
            public float ModifyYVelocity(float value, JumpKing.BodyCompBehaviours.BehaviourContext context) { return value; }
            public float ModifyGravity(float value, JumpKing.BodyCompBehaviours.BehaviourContext context) { return value; }
            public bool AdditionalXCollisionCheck(AdvCollisionInfo info, JumpKing.BodyCompBehaviours.BehaviourContext context) { return false; }
            public bool AdditionalYCollisionCheck(AdvCollisionInfo info, JumpKing.BodyCompBehaviours.BehaviourContext context) { return false; }
            public bool ExecuteBlockBehaviour(JumpKing.BodyCompBehaviours.BehaviourContext context) { IsPlayerOnBlock = context.BodyComp.IsOnGround; return true; }
        }
        private sealed class CountingSound : IJKSound
        {
            internal int Plays;
            public bool IsLooped { get; set; }
            public JKSoundState State { get { return JKSoundState.Stopped; } }
            public float Volume { get; set; }
            public TimeSpan Duration { get { return TimeSpan.Zero; } }
            public void Play() { Plays++; }
            public void Pause() { } public void Resume() { } public void Stop() { }
        }

        private static HashSet<IBTnode> Nodes(BehaviorTreeComp tree)
        {
            var visited = new HashSet<IBTnode>();
            var queue = new Queue<IBTnode>();
            queue.Enqueue((IBTnode)typeof(BTmanager).GetField("m_root_node", Flags).GetValue(tree.GetRaw()));
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (node == null || !visited.Add(node)) continue;
                foreach (var child in node.GetRelatedNodes()) queue.Enqueue(child);
            }
            return visited;
        }

        private static void LandingPipeline()
        {
            var instance = typeof(Game1).GetField("_instance", Flags);
            var previous = instance.GetValue(null);
            var oldInput = ControllerManager.instance;
            var oldSplat = PlayerEntity.OnSplatCall;
            var game = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
            game.contentManager = (JKContentManager)FormatterServices.GetUninitializedObject(typeof(JKContentManager));
            game.contentManager.playerSprites = new JKContentManager.PlayerSprites { _CurrentSprites = new JumpKing.JKMemory.LayeredKingSprites(null) };
            game.contentManager.playerSprites.AddLayer(new JumpKing.JKMemory.KingSprites(null));
            instance.SetValue(null, game);
            ControllerManager.instance = (ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager));
            typeof(ControllerManager).GetField("m_pads", Flags).SetValue(ControllerManager.instance, new List<PadInstance>());
            try
            {
                Scene(new BoxBlock(new Rectangle(0, 300, 480, 60)));
                var player = (PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
                typeof(Entity).GetField("m_components", Flags).SetValue(player, new List<Component>());
                player.m_body = new BodyComp(new Vector2(150, 268), 18, 26) { Velocity = new Vector2(0, 2) };
                player.m_body.RegisterBlockBehaviour<BoxBlock>(new PassiveBlock());
                typeof(PlayerEntity).GetField("m_screen_shake", Flags).SetValue(player,
                    FormatterServices.GetUninitializedObject(typeof(JumpKing.MiscSystems.ScreenShakeController)));
                // Construct the installed game's complete, unmodified BT graph.
                var tree = (BehaviorTreeComp)typeof(PlayerEntity).GetMethod("MakeBT", Flags).Invoke(player, null);
                player.AddComponents(player.m_body, new InputComponent(), tree);
                var before = Nodes(tree);
                var jump = before.OfType<JumpState>().Single();
                var ground = before.OfType<IsOnGround>().Single();
                var fail = before.OfType<FailState>().Single();
                var land = new CountingSound(); var splat = new CountingSound();
                int particles = 0, events = 0;
                player.RegisterLandSound<BoxBlock>(land);
                player.RegisterFailSound<BoxBlock>(splat);
                player.RegisterLandParticleSpawningAction<BoxBlock>(delegate { particles++; });
                PlayerEntity.OnSplatCall = delegate { events++; };
                var visual = new HammerVisual(player); player.AddComponents(false, visual);
                using (Responses(player.m_body))
                using (var control = new HammerNativeControl(player, tree))
                {
                    Check(tree.Enabled && Nodes(tree).Contains(ground) && Nodes(tree).Contains(fail), "Original native landing and failure nodes stay active");
                    Check(!Nodes(tree).Contains(jump) && !Nodes(tree).OfType<Walk>().Any(), "All shared jump and walk execution paths are blocked");
                    visual.Attach(Pose(player.m_body.Position + new Vector2(9, 13), new Vector2(40, -20)));
                    for (int landing = 1; landing <= 2; landing++)
                    {
                        player.m_body.Position = new Vector2(150, 268); player.m_body.Velocity = new Vector2(0, 2);
                        for (int frame = 0; frame < 90; frame++)
                        { Tick(player.m_body); tree.GetRaw().Run(1f / 60f); visual.Refresh(); }
                        Check(land.Plays == landing && particles == landing, "One native sound/particle callback per landing, no repeats at rest: " + landing + ", sounds=" + land.Plays + ", particles=" + particles);
                        Check(splat.Plays == 0 && events == 0, "Short falls do not trigger splat");
                    }
                    player.m_body.Position = new Vector2(150, 220); player.m_body.Velocity = new Vector2(0, PlayerValues.MAX_FALL);
                    int frames = 0;
                    do { Tick(player.m_body); tree.GetRaw().Run(1f / 60f); visual.Refresh(); frames++; }
                    while (!player.m_body.IsOnGround && frames < 60);
                    Check(land.Plays == 3 && particles == 3 && splat.Plays == 1 && events == 1 && fail.IsRunning(), "Hard landing runs native ground, splat sound and OnSplat exactly once");
                    var wrapper = (HammerSprite)typeof(PlayerEntity).GetField("m_sprite", Flags).GetValue(player);
                    Check(ReferenceEquals(wrapper.Source, game.contentManager.playerSprites.splat), "Hammer preserves the native splat animation");
                    Check(!control.AllowsHammer(new Vector2(20, 0)), "Hammer cannot bypass the native splat timer");
                    frames = 0;
                    while (!(bool)typeof(FailState).GetField("m_wait_done", Flags).GetValue(fail) && frames++ < 300)
                    { Tick(player.m_body); tree.GetRaw().Run(1f / 60f); visual.Refresh(); }
                    Check(frames > 0 && frames < 300 && events == 1 && splat.Plays == 1, "Native timer progresses without replaying failure effects");
                    Check(!control.AllowsHammer(Vector2.Zero), "Splat waits for intentional input after its timer");
                    Check(control.AllowsHammer(new Vector2(2, 0)), "Mouse can get up after the native timer");
                    Tick(player.m_body); tree.GetRaw().Run(1f / 60f); visual.Refresh();
                    Check(!fail.IsRunning() && events == 1, "Mouse recovery resumes native idle without a second splat");
                    wrapper = (HammerSprite)typeof(PlayerEntity).GetField("m_sprite", Flags).GetValue(player);
                    var latest = wrapper.Source;
                    visual.Enabled = false;
                    Check(ReferenceEquals(typeof(PlayerEntity).GetField("m_sprite", Flags).GetValue(player), latest), "Disabling restores the latest native pose");
                    visual.Attach(new HammerPhysics()); visual.Enabled = false;
                    Check(player.GetComponents().Count(c => c is HammerVisual) == 1, "Toggles reuse the visual component");
                }
                Check(before.SetEquals(Nodes(tree)), "Disabling restores the exact native graph and shared jump references");
                Check(ReferenceEquals(typeof(PlayerEntity).GetField("m_jump_state", Flags).GetValue(player), jump), "Native jump discovery restored");
                CleanupOwnership(player, tree);
                if (equipmentIntegration) EquippedLifecycle(player, tree);
                if (chargeAssembly != null) ChargeComposition(player, tree);
            }
            finally { PlayerEntity.OnSplatCall = oldSplat; ControllerManager.instance = oldInput; instance.SetValue(null, previous); }
            Console.WriteLine("[OK] Installed native BT: landing SFX/particles, repeated landings, splat event/timer/mouse recovery, animation and graph restoration");
        }

        private static void CleanupOwnership(PlayerEntity player, BehaviorTreeComp tree)
        {
            var before = Nodes(tree);
            var parent = before.OfType<IBTcomposite>().Single(node => node.Children.Any(child => child is Walk));
            int slot = Array.FindIndex(parent.Children, node => node is Walk);
            HammerNativeControl controller = null;
            bool chargeActive = false;
            using (JKRuntime.Gameplay.JumpSlot.RegisterChargePolicy(delegate { chargeActive = true; }, delegate { chargeActive = false; }))
            {
                JKRuntime.Gameplay.JumpSlot.Recompose(delegate { controller = new HammerNativeControl(player, tree); });
                var owned = parent.Children[slot];
                parent.Children[slot] = new BTselector();
                bool rejected = false;
                try { JKRuntime.Gameplay.JumpSlot.Recompose(controller.Dispose); }
                catch (AggregateException) { rejected = true; }
                Check(rejected && !chargeActive && JKRuntime.Gameplay.JumpSlot.ChargePolicySuspended,
                    "Failed graph cleanup cannot resume charge on a partially restored controller");
                parent.Children[slot] = owned;
                JKRuntime.Gameplay.JumpSlot.Recompose(controller.Dispose);
                Check(chargeActive && before.SetEquals(Nodes(tree)), "Retry restores graph before releasing charge reservation");
            }
        }

        private static void ImpactSounds()
        {
            var world = new World(); world.Blocks.Add(new Rectangle(-100, 25, 200, 1));
            HammerImpacts impacts;
            HammerPhysics.Sweep(Vector2.Zero, new Vector2(0, 10), new Vector2(0, 20), world, out impacts);
            Check(impacts.Stone > 1 && impacts.Wood == 0, "Head collision selects stone audio");
            world = new World(); world.Blocks.Add(new Rectangle(22, 9, 3, 3));
            HammerPhysics.Sweep(Vector2.Zero, new Vector2(45, 0), new Vector2(0, 35), world, out impacts);
            Check(impacts.Wood > 1 && impacts.Stone == 0, "Shaft-only collision selects wood audio");
            var gate = new HammerImpactGate();
            Check(gate.Step(8f) > 0, "First strong impact plays");
            for (int i = 0; i < 600; i++) Check(gate.Step(8f) == 0, "Sustained pressure does not loop the hit");
            gate.Step(0); gate.Step(0);
            Check(gate.Step(8f) == 0, "One/two-frame contact jitter does not retrigger");
            for (int i = 0; i < 6; i++) gate.Step(0);
            Check(gate.Step(8f) > 0, "A new swing after separation plays again");
            gate.Suppress();
            Check(gate.Step(8f) == 0, "Pause/restore suppresses stale contact");
            for (int i = 0; i < 10; i++) gate.Step(0);
            Check(gate.Step(.3f) == 0, "Resting gravity is inaudible");
            Console.WriteLine("[OK] Shaft/head routing, sustained pressure, contact jitter, separation, pause and low-speed filtering");
        }
    }
}
