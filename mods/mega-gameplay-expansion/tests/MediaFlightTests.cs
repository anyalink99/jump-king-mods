using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JKRuntime.Simulation;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal static partial class Tests
    {
        private static void InstalledMediaRoutes(string gameDir)
        {
            var screens = ReadNativeScreens(gameDir, 150);
            foreach (var sample in new[] {
                new Vector3(209.25f, -31978, 89), new Vector3(447, -52642, 147) })
            {
                int screen = (int)sample.Z;
                var source = new BodyComp(new Vector2(sample.X, sample.Y), 18, 26) {
                    Velocity = new Vector2(screen == 89 ? 3.5f : -3.5f, -8.742858f) };
                var clock = Stopwatch.StartNew();
                BodyComp landing; int ticks; string reason;
                bool ok = NativeFlight.TryPredict(source, new FlightWorld(screens, screen, 0), out landing, out ticks, out reason);
                Require(ok, "Recorded media route failed: " + reason);
                Console.WriteLine("[OK] Recorded media route screen=" + (screen+1) + " ticks=" + ticks + " totalMs=" + clock.Elapsed.TotalMilliseconds.ToString("F2"));
            }
        }

        private static void PendingForecastLifecycle()
        {
            var screens = Scene(new IBlock[] { new BoxBlock(new Rectangle(0,320,480,40)) });
            typeof(LevelManager).GetField("m_screens",Flags).SetValue(null,screens);
            typeof(LevelManager).GetField("_total_screens",Flags).SetValue(null,1);
            typeof(Camera).GetField("_current_screen",Flags).SetValue(null,0);
            bool added = MegaBlockFactory.WarpScreens.Add(0);
            try
            {
                var player = ResumePlayer();
                player.m_body.Position = new Vector2(180,-8000); player.m_body.Velocity = new Vector2(0,4);
                NativeFlight.Set(player.m_body,"_is_on_ground",false);
                var origin = player.m_body.Position;
                using (var controller = new WarpController(player))
                {
                    typeof(BodyComp).GetMethod("UpdateInternal",Flags).Invoke(player.m_body,new object[]{1f/60f});
                    var forecastField = typeof(WarpController).GetField("forecast",Flags);
                    var pending = (FlightJob)forecastField.GetValue(controller);
                    Require(pending != null && pending.Landing == null && !player.m_body.Enabled,"Long forecast did not yield with player suspended");
                    object snapshot = controller.Capture();
                    var savedPlan=snapshot.GetType().GetField("Plan",Flags).GetValue(snapshot);
                    var savedImage=(WarpImage)savedPlan.GetType().GetField("Image",Flags).GetValue(savedPlan);
                    int ticks = pending.Ticks;
                    controller.Advance(0);
                    Require(pending.Ticks == ticks,"Pause advanced pending forecast");
                    for (int i=0; i<160 && !player.m_body.Enabled; i++) controller.Advance(1f/60f);
                    Require(player.m_body.Enabled && player.m_body.Position.Y==294,"Sliced controller did not finish real transfer");
                    Require(savedImage.ArrivalSprite==Game1.instance.contentManager.playerSprites.idle,"Completing a forecast mutated a pending snapshot's arrival pose");
                    Require(typeof(PlayerEntity).GetField("m_sprite",Flags).GetValue(player)==Game1.instance.contentManager.playerSprites.splat,"Long fall did not assemble into splat");
                    controller.Restore(snapshot);
                    Require(!player.m_body.Enabled && player.m_body.Position==origin && forecastField.GetValue(controller)!=null,"Pending snapshot did not restart isolated forecast");
                    pending=(FlightJob)forecastField.GetValue(controller);
                    for(int i=0;i<=FlightJob.MaxSlices && !pending.Done;i++) pending.Step(1);
                    Require(pending.Failure!=null,"Pending budget fixture did not expire");
                    controller.Advance(1f/60f);
                    Require(player.m_body.Enabled && player.m_body.Position==origin
                        && !JKRuntime.Gameplay.PresentationActivity.IsActive(player.m_body),"Expired forecast left the real player frozen or moved");
                }
                Require(player.m_body.Enabled && player.m_body.Position==origin && player.m_body.Velocity==new Vector2(0,4),"Pending cancellation lost launch state");
                Require(!JKRuntime.Gameplay.PresentationActivity.IsActive(player.m_body),"Pending cancellation leaked presentation ownership");
                Console.WriteLine("[OK] Pending forecast: cooperative yield, pause, completion, snapshot restart and cancellation restore controls/position");
            }
            finally { if (added) MegaBlockFactory.WarpScreens.Remove(0); }
        }

        private sealed class CaptureAtX : IBodyCompBehaviour
        {
            internal FlightWorld World;
            internal BodyComp Shadow;
            internal FlightJob Job;
            public bool ExecuteBehaviour(BehaviourContext context)
            {
                if (Shadow == null)
                {
                    Shadow = NativeFlight.CreateShadow(context.BodyComp, World);
                    Job = new FlightJob(new FlightJob.Seed(context.BodyComp, World));
                }
                return true;
            }
        }
        private static void WindAndContinuation()
        {
            var managerType = typeof(Game1).Assembly.GetType("JumpKing.MiscSystems.Achievements.AchievementManager", true);
            var managerField = managerType.GetField("instance", Flags);
            var allTime = managerType.GetField("m_all_time_stats", Flags);
            var oldManager = managerField.GetValue(null);
            try
            {
                using (new SpriteGameFixture())
                {
                    var manager = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(managerType);
                    managerField.SetValue(null, manager);
                    typeof(Microsoft.Xna.Framework.Game).GetField("_targetElapsedTime", Flags).SetValue(Game1.instance, TimeSpan.FromMilliseconds(17));
                    int compared = 0;
                    foreach (int start in new[] { 0, 180, 380, 1000 })
                    foreach (bool? direction in new bool?[] { null, false, true })
                    foreach (bool grounded in new[] { false, true })
                    foreach (int material in new[] { 0, 1, 2, 3 })
                    {
                        var blocks = new List<IBlock> { new BoxBlock(new Rectangle(0,320,480,40)), new NoWindBlock(new Rectangle(225,100,80,220)) };
                        if (material == 1) blocks.Add(new WaterBlock(new Rectangle(0,0,480,320)));
                        if (material == 2) blocks.Add(new SandBlock(new Rectangle(0,240,480,80)));
                        if (material == 3) { blocks.Clear(); blocks.Add(new SnowBlock(new Rectangle(0,320,480,40))); }
                        var screens = new[] { new LevelScreen(0, blocks.ToArray(), new LevelScreen.Graphics(), true, new TeleportLink[0], 8, direction) };
                        typeof(LevelManager).GetField("m_screens",Flags).SetValue(null,screens);
                        typeof(LevelManager).GetField("_total_screens",Flags).SetValue(null,1);
                        typeof(Camera).GetField("_current_screen",Flags).SetValue(null,0);
                        var expected = new BodyComp(new Vector2(180,294),18,26) { Velocity = new Vector2(3.5f,-6) };
                        NativeFlight.Set(expected,"_is_on_ground",grounded);
                        if (material == 3) NativeFlight.Get<Dictionary<Type,IBlockBehaviour>>(expected,"m_blockBehaviourLookup")[typeof(SnowBlock)].IsPlayerOnBlock = true;
                        var pipeline = NativeFlight.Get<LinkedList<IBodyCompBehaviour>>(expected,"m_behaviours");
                        foreach (var stage in pipeline.ToArray()) if (stage.GetType().Name == "WaterParticleSpawningBehaviour" || stage.GetType().Name == "PlayBumpSFXBehaviour") pipeline.Remove(stage);
                        var world = new FlightWorld(screens,0,0); world.SetWindClock(start,.017,0);
                        var probe = new CaptureAtX { World = world };
                        pipeline.AddBefore(pipeline.Find(pipeline.Single(s => s is UpdateXPositionFromVelocityBehaviour)),probe);
                        int ticks = 0;
                        do
                        {
                            var stats = Activator.CreateInstance(allTime.FieldType);
                            stats.GetType().GetField("_ticks",Flags).SetValue(stats,start+ticks); allTime.SetValue(manager,stats);
                            typeof(BodyComp).GetMethod("UpdateInternal",Flags).Invoke(expected,new object[] {1f/60f});
                            if (ticks == 0) NativeFlightSimulation.AdvanceShadowFromX(probe.Shadow);
                            else NativeFlightSimulation.AdvanceShadow(probe.Shadow,1f/60f);
                            ticks++;
                            var diff = SimulationConformance.CompareFields("wind/media continuation",ticks,new SimulationInput(1,false),SimulationConformance.ReadBody(expected),SimulationConformance.ReadBody(probe.Shadow));
                            Require(diff.Matches,"Wind/media tick " + ticks + " material="+material+": " + string.Join("; ",diff.Differences));
                            var water = NativeFlight.Get<Dictionary<Type,IBlockBehaviour>>(expected,"m_blockBehaviourLookup")[typeof(WaterBlock)];
                            var copyWater = NativeFlight.Get<Dictionary<Type,IBlockBehaviour>>(probe.Shadow,"m_blockBehaviourLookup")[typeof(WaterBlock)];
                            Require(NativeFlight.Get<bool>(water,"<PrevIsPlayerOnBlock>k__BackingField") == NativeFlight.Get<bool>(copyWater,"<PrevIsPlayerOnBlock>k__BackingField"),"First-tick water history changed");
                            world.AdvanceCamera(probe.Shadow);
                        } while (!(expected.LastVelocity.Y >= 0 && (expected.IsOnGround || expected.IsOnBlock(typeof(SandBlock)))) && ticks < 1200);
                        Require(ticks < 1200,"Native media fixture did not land");
                        int slices = 0;
                        while (!probe.Job.Done && slices++ < 1200)
                        {
                            int before = probe.Job.Ticks; probe.Job.Step(128,2);
                            Require(probe.Job.Ticks - before <= 128,"Cooperative tick bound ignored");
                        }
                        Require(probe.Job.Landing != null && probe.Job.Ticks == ticks && probe.Job.Landing.Position == expected.Position && probe.Job.Landing.Velocity == expected.Velocity,
                            "Sliced media/wind endpoint mismatch: " + probe.Job.Failure);
                        NativeFlight.Commit(probe.Job.Landing, expected, NativeFlight.Get<BehaviourContext>(expected,"m_behaviourContext"));
                        compared += ticks;
                    }
                    Console.WriteLine("[OK] Wind + mid-tick continuation: " + compared + " exact native ticks across water/sand/snow, fixed/reversing wind, NoWind, activation latch; sliced landing parity");
                }
            }
            finally { managerField.SetValue(null, oldManager); }
        }
    }
}
