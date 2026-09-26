using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BehaviorTree;
using EntityComponent;
using EntityComponent.BT;
using JKRuntime.Gameplay;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Player;

namespace ScreenSolver
{
    internal static partial class Tests
    {
        private static MethodInfo installedQuantizer, installedBeforeRelease;
        private static float InstalledSubframeTimer(double seconds, float multiplier)
        {
            var result = installedQuantizer.Invoke(null, new object[] { seconds, multiplier });
            return (float)installedBeforeRelease.Invoke(null, new object[] { result, (double)((1f / 60) * multiplier) });
        }
        private static void PlayerIntegrationParity()
        {
            var modules = PlayerAdapters.Builds.ToDictionary(p => p.Key, p => Fixture(p.Key));
            foreach (var pair in modules) Check(pair.Value.ManifestModule.ModuleVersionId.ToString() == PlayerAdapters.Builds[pair.Key], "Player fixture build changed: " + pair.Key);
            var sfc = modules["SubframeCharge.Module"];
            var quantizer = sfc.GetType("SubframeCharge.ChargeQuantizer", true);
            installedQuantizer = quantizer.GetMethod("QuantizeRelease", NativeWorld.Flags);
            installedBeforeRelease = quantizer.GetMethods(NativeWorld.Flags).Single(m => m.Name == "TimerBeforeNativeRelease" && m.GetParameters()[0].ParameterType.Name == "ChargeResult");
            for (int ms = 0; ms <= 1500; ms++) foreach (float multiplier in new[] { .5f, 1f })
                Check(SubframeTimer.BeforeRelease(ms / 1000.0, multiplier, (double)((1f / 60) * multiplier)) == InstalledSubframeTimer(ms / 1000.0, multiplier), "SFC release timer differs from installed quantizer");
            var mega = modules["MegaGameplayExpansion.Module"];
            var setting = mega.GetType("MegaGameplayExpansion.Settings", true).GetField("current", NativeWorld.Flags);
            var oldSettings = setting.GetValue(null);
            var preferences = Activator.CreateInstance(mega.GetType("MegaGameplayExpansion.Preferences", true));
            setting.SetValue(null, preferences);
            var moreItems = modules["MoreItems.Module"];
            var merchantTree = (BehaviorTreeComp)Empty(typeof(BehaviorTreeComp)); merchantTree.Enabled = false;
            Entity merchantGuard = null;
            MoreItemsEntityCoverage(moreItems);
            try
            {
                capturePlayer = player => {
                    var tree = player.GetComponent<BehaviorTreeComp>();
                    var original = NativePlayer.Field<JumpState>(player, "m_jump_state");
                    // Copy native base fields without starting SFC's real input sampler.
                    var replacement = (JumpState)Empty(sfc.GetType("SubframeCharge.SubframeChargeState", true));
                    for (Type t = typeof(JumpState); t != null; t = t.BaseType)
                        foreach (var f in t.GetFields(NativeWorld.Flags | BindingFlags.DeclaredOnly).Where(f => !f.IsStatic)) f.SetValue(replacement, f.GetValue(original));
                    JumpNodeBindings.Replace(tree.GetRaw(), player, original, replacement);
                    var pipeline = NativePlayer.Field<LinkedList<IBodyCompBehaviour>>(player.m_body, "m_behaviours");
                    var marker = (IBodyCompBehaviour)Empty(sfc.GetType("SubframeCharge.SubframeChargeInstaller+JumpLifecycleMarker", true));
                    marker.GetType().GetField("state", NativeWorld.Flags).SetValue(marker, replacement);
                    pipeline.AddBefore(pipeline.Find(pipeline.Single(b => b is WindVelocityUpdateBehaviour)), marker);
                    var x = pipeline.Find(pipeline.Single(b => b is UpdateXPositionFromVelocityBehaviour));
                    pipeline.AddBefore(x, (IBodyCompBehaviour)Empty(mega.GetType("MegaGameplayExpansion.WarpController", true)));
                    var dash=(IBodyCompBehaviour)Empty(mega.GetType("MegaGameplayExpansion.AirDashController",true));
                    pipeline.AddBefore(x,dash);
                    var phase = mega.GetType("MegaGameplayExpansion.NoWalkOffController+Phase", true);
                    pipeline.AddBefore(x, (IBodyCompBehaviour)Empty(phase));
                    pipeline.AddAfter(pipeline.Find(pipeline.Single(b => b is ResolveXCollisionBehaviour)), (IBodyCompBehaviour)Empty(phase));
                    foreach (var p in new[] { new[] { "JumpKingSaveStates", "JumpKingSaveStates.SavestateBehaviour" }, new[] { "JumpKingManager", "JumpKingManager.ManagerBehaviour" } })
                        pipeline.AddLast((IBodyCompBehaviour)Empty(modules[p[0]].GetType(p[1], true)));
                    foreach (string name in new[] { "SubframeCharge.JumpTrajectoryProbe", "SubframeCharge.ChargeFrameComponents+Observer" })
                        player.AddComponents((Component)Empty(sfc.GetType(name, true)));
                    foreach (string name in new[] { "MegaGameplayExpansion.WarpVisual", "MegaGameplayExpansion.NoWalkOffController+CommitComponent", "MegaGameplayExpansion.AirDashVisual" })
                        player.AddComponents((Component)Empty(mega.GetType(name, true)));
                    player.AddComponents((Component)Empty(modules["Replays.Module"].GetType("Replays.ReplayCaptureComponent", true)));
                    var entities = (List<Entity>)typeof(EntityManager).GetField("entities", NativeWorld.Flags).GetValue(EntityManager.instance);
                    foreach (string name in new[] { "Replays.ReplayRecorder", "Replays.ReplayGhostEntity" })
                        entities.Add((Entity)Empty(modules["Replays.Module"].GetType(name, true)));
                    entities.Add((Entity)Empty(modules["MoreItems.Module"].GetType("MoreItems.RewinderController", true)));
                    merchantGuard = (Entity)Empty(moreItems.GetType("MoreItems.BargainburgMerchantGuard", true));
                    merchantGuard.GetType().GetField("merchantTree", NativeWorld.Flags).SetValue(merchantGuard, merchantTree);
                    entities.Add(merchantGuard);
                    entities.Add((Entity)Empty(moreItems.GetType("MoreItems.ConsumableDispenser", true)));
                    entities.Add((Entity)Empty(moreItems.GetType("JumpKingJetpack.JetpackUsageMarker", true)));
                    player.AddComponents((Component)Empty(moreItems.GetType("JumpKingJetpack.JetpackAirSpriteComponent", true)));
                    foreach (var component in player.GetComponents()) component.Enabled = true;
                    Check(entities.All(e => e.IsAlive), "Inactive entity fixture would bypass the capture audit");
                    var adapter = new PlayerAdapters(); Check(adapter.Node(replacement) && adapter.Subframe, "SFC policy capture");
                    preferences.GetType().GetProperty("WarpJump").SetValue(preferences, true, null);
                    bool rejected = false;
                    try { adapter.Ignore(pipeline.Single(b => b.GetType().FullName == "MegaGameplayExpansion.WarpController")); }
                    catch (NotSupportedException) { rejected = true; }
                    finally { preferences.GetType().GetProperty("WarpJump").SetValue(preferences, false, null); }
                    Check(rejected, "Active Warp was mistaken for an observer");
                    preferences.GetType().GetProperty("AirDash").SetValue(preferences,true,null);
                    rejected=false;
                    try { new PlayerAdapters().Ignore(dash); } catch(NotSupportedException) { rejected=true; }
                    finally { preferences.GetType().GetProperty("AirDash").SetValue(preferences,false,null); }
                    Check(rejected,"Enabled Air Dash was mistaken for an observer");
                    dash.GetType().GetField("active",NativeWorld.Flags).SetValue(dash,true);
                    rejected=false;
                    try { new PlayerAdapters().Ignore(dash); } catch(NotSupportedException) { rejected=true; }
                    finally { dash.GetType().GetField("active",NativeWorld.Flags).SetValue(dash,false); }
                    Check(rejected,"Running Air Dash was mistaken for an observer");
                };
                ControlParity(false, true);
                Check(merchantGuard != null && merchantGuard.IsAlive && !merchantTree.Enabled &&
                    ReferenceEquals(PlayerAdapters.Read(merchantGuard, "merchantTree"), merchantTree),
                    "Capture/search/cancel ran the merchant guard or changed its live dialogue tree");
                NativeRoute(true);
                Console.WriteLine("[OK] Installed SFC quantizer: 3002 release values and 141120 exact native-tree ticks with SFC timer preloads, buffers and auto-max; combined player pipeline capture");
            }
            finally { capturePlayer = null; setting.SetValue(null, oldSettings); }
        }
        private static void MoreItemsEntityCoverage(Assembly assembly)
        {
            var expected = new Dictionary<string, bool> {
                { "MoreItems.BargainburgMerchantGuard", true },
                { "MoreItems.ConsumableDispenser", true },
                { "MoreItems.RewinderController", true },
                { "JumpKingJetpack.JetpackUsageMarker", true },
                { "JumpKingJetpack.JetpackAirSpriteComponent", true },
                { "MoreItems.ConsumablePickup", false },
                { "MoreItems.LooseConsumablePickup", false }
            };
            var actual = assembly.GetTypes().Where(t => !t.IsAbstract &&
                (typeof(Entity).IsAssignableFrom(t) || typeof(Component).IsAssignableFrom(t))).ToArray();
            Check(actual.Select(t => t.FullName).OrderBy(n => n).SequenceEqual(expected.Keys.OrderBy(n => n)),
                "More Items entity/component inventory changed; review new types before claiming coverage");
            var adapter = new PlayerAdapters();
            foreach (var type in actual)
                Check(adapter.Ignore(Empty(type)) == expected[type.FullName], "Wrong More Items coverage: " + type.FullName);
            var rewinder = Empty(assembly.GetType("MoreItems.RewinderController", true));
            rewinder.GetType().GetField("rewinding", NativeWorld.Flags).SetValue(rewinder, true);
            Check(!adapter.Ignore(rewinder), "Active rewind was classified as observation");
            Console.WriteLine("[OK] Complete installed More Items entity/component inventory: manual tools and observers accepted; automatic pickups and active rewind rejected");
        }
    }
}
