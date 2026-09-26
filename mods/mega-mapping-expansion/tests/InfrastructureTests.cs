using System;
using System.IO;
using System.Linq;
using MegaMappingExpansion.Api;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void InfrastructureChecks()
        {
            var scene = BehaviorScene();
            scene.Anchors = new[] { new SceneAnchor { Id = "ledge", Screen = 2, X = 100, Y = 120, Width = 40, Height = 8, Kind = "solid" } };
            scene.Regions[0] = new RegionData { Id = "door", Anchor = "ledge", Test = "standing", Enter = "dim", Lifetime = "inside" };
            BehaviorValidation.Validate(scene, 2);
            using (var engine = new SceneBehaviorEngine(scene, null))
            {
                var actor = At(110, 100, 2); actor.Grounded = false;
                engine.Tick(.01, true, true, actor);
                Require(engine.InspectEffects().Length == 0, "Crossing a platform in the air is not standing on it");
                actor.Grounded = true;
                actor.Supports = new[] { new Rectangle(110, 180, 10, 1) };
                engine.Tick(.01, true, true, actor);
                Require(engine.InspectEffects().Length == 0, "A different native support cannot trigger the nearby anchor");
                actor.Supports = new[] { new Rectangle(110, 120, 10, 1) };
                engine.Tick(.01, true, true, actor);
                Require(engine.InspectEffects().Length == 1 && scene.Nodes[0].Opacity == .2f, "Real support on the referenced screen/anchor triggers its effect");
                Require(!engine.MembershipChanged, "A property-only effect does not rebuild light membership");
                actor.Grounded = false;
                engine.Tick(.01, true, true, actor);
                Require(engine.InspectEffects().Length == 0, "Jumping releases inside-owned standing effects");
            }
            scene = BehaviorScene(); scene.Regions = new RegionData[0];
            scene.Rules = new[] { new RuleData { Id = "impact", Event = "landed", Screen = 2, Sound = "block_appear" } };
            using (var engine = new SceneBehaviorEngine(scene, null))
            {
                int played = 0; engine.PlaySound = key => { Require(key == "block_appear", "Native audio cue preserves its event_music ID"); played++; };
                engine.Emit("landed", 2);
                engine.Tick(.01, true, true, At(20, 20, 1));
                Require(played == 1, "Queued native event keeps its original screen across later player movement");
                var state = engine.Capture(); engine.Emit("landed", 2); engine.Restore(state);
                engine.Tick(.01, true, true, At(20, 20, 2));
                Require(played == 1, "Snapshot restore does not replay discarded native audio events");
            }
            scene = BehaviorScene(); scene.Regions = new RegionData[0]; scene.Options.AdvancedLighting = true;
            scene.Waters = new[] { new WaterData { Id = "lake" } };
            scene.Puddles = new[] { new PuddleData { Id = "puddle" } };
            scene.Bushes = new[] { new BushData { Id = "reeds" } };
            scene.Planets = new[] { new PlanetData { Id = "earth" } };
            scene.Surfs = new[] { new SurfData { Id = "ocean" } };
            scene.ShadowSurfaces = new[] { new ShadowSurfaceData { Id = "roof" } };
            scene.Anchors = new[] { new SceneAnchor { Id = "door" } };
            using (var engine = new SceneBehaviorEngine(scene, null))
            {
                var effect = engine.Apply("weather", new EffectDefinition { Id = "storm", Changes = new[] {
                    new SceneChange { Target = "lake", Property = "ripple", Value = "5" },
                    new SceneChange { Target = "puddle", Property = "rainRings", Value = "40" },
                    new SceneChange { Target = "reeds", Property = "sway", Value = "8" },
                    new SceneChange { Target = "earth", Property = "cloudOpacity", Value = "0.3" },
                    new SceneChange { Target = "ocean", Property = "foamDetail", Value = "0.9" },
                    new SceneChange { Target = "roof", Property = "opacity", Value = "0.7" },
                    new SceneChange { Target = "anchor:door", Property = "blocksLight", Value = "true" }
                } });
                Require(scene.Waters[0].Ripple == 5 && scene.Puddles[0].RainRings == 40 && scene.Bushes[0].Sway == 8
                    && scene.Planets[0].CloudOpacity == .3f && scene.Surfs[0].FoamDetail == .9f && scene.ShadowSurfaces[0].Opacity == .7f
                    && scene.Anchors[0].BlocksLight, "One owned effect coordinates water, puddles, plants, planets, surf, shadows and map blockers");
                effect.Dispose();
                Require(scene.Waters[0].Ripple == 2 && !scene.Anchors[0].BlocksLight, "Effect cancellation restores authored material/blocker values");
            }
            scene.Waters[0].CompositeReflection = true; scene.Waters[0].ReflectionObjects = "player;panel";
            ReflectionSelection.Validate(scene);
            scene.Waters[0].ReflectionObjects = "player;missing";
            bool rejected = false; try { ReflectionSelection.Validate(scene); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Unknown reflection participant is an error, not a whole-world reflection");
            var index = new ScreenIndex<PropData>(new[] { new PropData { Screen = 1 }, new PropData { Screen = 300 } }, p => p.Screen);
            Require(index.At(1).Length == 1 && index.At(300).Length == 1 && index.At(2).Length == 0, "Frame work is partitioned by authored screen without 169-screen assumptions");
            scene.Rules = new[] { new RuleData { Id = "native", Event = "hiddenwallenter:80_hidden_wall1" } };
            NativeHiddenWalls.Configure(scene);
            var native = typeof(JumpKing.Game1).Assembly.GetType("JumpKing.Props.RaymanWall.RaymanWallEntity").GetMethod("TouchPlayer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Require(HarmonyLib.Harmony.GetPatchInfo(native).Owners.Contains("mega-mapping-expansion.hidden-walls"), "Hidden-wall rules bind the original native contact method");
            NativeHiddenWalls.Release();
            var remaining = HarmonyLib.Harmony.GetPatchInfo(native);
            Require(remaining == null || !remaining.Owners.Contains("mega-mapping-expansion.hidden-walls"), "No hidden-wall hook survives its owner");
        }
    }
}
