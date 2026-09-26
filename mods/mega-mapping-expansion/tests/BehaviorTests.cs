using System;
using System.Linq;
using MegaMappingExpansion.Api;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static SceneFile BehaviorScene()
        {
            return new SceneFile {
                VectorAssets = new[] { new VectorAssetData { Id = "blue" }, new VectorAssetData { Id = "gold" } },
                Nodes = new[] { new PropData { Id = "panel", Asset = "blue", Opacity = .8f } },
                LightTemplates = new[] { new LightData { Id = "lantern", Attach = "player.center" } },
                Flags = new[] { new FlagData { Id = "powered" } },
                Effects = new[] {
                    new EffectDefinition { Id = "lantern", Duration = 30,
                        Changes = new[] { new SceneChange { Target = "panel", Property = "asset", Value = "gold" } },
                        Lights = new[] { new LightSpawn { Template = "lantern" } } },
                    new EffectDefinition { Id = "dim", Duration = 1, Priority = 10,
                        Changes = new[] { new SceneChange { Target = "panel", Property = "opacity", Value = ".2" } } }
                },
                Regions = new[] { new RegionData { Id = "door", X = 100, Y = 100, Width = 40, Height = 40, Enter = "lantern" } }
            };
        }
        private static SceneActor At(int x, int y, int screen = 1)
        { return new SceneActor { Present = true, Screen = screen, Bounds = new Rectangle(x, y, 10, 20) }; }
        private static void BehaviorChecks()
        {
            SceneFile scene = BehaviorScene();
            using (var external = new SceneBehaviorEngine(BehaviorScene(), null)) global::SceneExample.Verify(external);
            using (var engine = new SceneBehaviorEngine(scene, null))
            {
                engine.Tick(0, true, true, At(105, 105));
                Require(scene.Nodes[0].Asset == "gold" && scene.Lights.Length == 1, "area entry commits texture variant and spawned light together");
                Require(engine.InspectEffects()[0].RemainingSeconds == 30, "new effect starts with full lifetime");
                for (int i = 0; i < 600; i++) engine.Tick(1d / 60, false, true, At(105, 105));
                Require(engine.InspectEffects()[0].RemainingSeconds == 30, "UI/gameplay pause freezes gameplay clock");
                for (int i = 0; i < 1200; i++) engine.Tick(1d / 60, true, true, At(0, 0));
                engine.Tick(0, true, true, At(105, 105));
                Require(engine.InspectEffects().Length == 1 && Math.Abs(engine.InspectEffects()[0].RemainingSeconds - 30) < 1e-8, "re-entry refreshes one lamp instead of leaking another");
                object snapshot = engine.Capture();
                for (int i = 0; i < 1800; i++) engine.Tick(1d / 60, true, true, At(0, 0, 2));
                Require(scene.Lights.Length == 0 && scene.Nodes[0].Asset == "blue", "1800 gameplay ticks expire light and restore texture across screens");
                engine.Restore(snapshot);
                Require(scene.Lights.Length == 1 && scene.Nodes[0].Asset == "gold", "snapshot restores active semantic effects");
                engine.Tick(0, true, true, At(105, 105));
                Require(engine.InspectEffects().Length == 1, "restored membership does not duplicate entry");
                var high = engine.Apply("other", new EffectDefinition { Id = "override", Priority = 100,
                    Changes = new[] { new SceneChange { Target = "panel", Property = "asset", Value = "blue" } } });
                var low = engine.Activate("another", "lantern");
                low.Dispose(); Require(scene.Nodes[0].Asset == "blue", "lower owner release preserves higher priority");
                high.Dispose(); Require(scene.Nodes[0].Asset == "gold", "release reveals remaining effect rather than obsolete captured value");
                int before = engine.InspectEffects().Length;
                bool rejected = false;
                try { engine.Apply("bad", new EffectDefinition { Id = "bad", Changes = new[] { new SceneChange { Target = "panel", Property = "asset", Value = "missing" } }, Lights = new[] { new LightSpawn { Template = "lantern" } } }); }
                catch (System.IO.InvalidDataException) { rejected = true; }
                Require(rejected && engine.InspectEffects().Length == before, "failed action validation does not partially spawn");
                var old = engine.Activate("external", "dim"); object second = engine.Capture(); engine.Restore(second);
                Require(!old.Active, "snapshot restore invalidates pre-restore external handles");
                old.Dispose(); Require(scene.Nodes[0].Opacity == .2f, "stale handle cannot cancel restored effect");
            }
            Require(scene.Lights.Length == 0 && scene.Nodes[0].Asset == "blue" && scene.Nodes[0].Opacity == .8f, "teardown restores authored baselines");
            scene = BehaviorScene(); scene.Regions[0].Lifetime = "inside";
            using (var engine = new SceneBehaviorEngine(scene, null))
            {
                engine.Tick(0, true, true, At(105, 105)); engine.Tick(0, true, true, At(0, 0));
                Require(scene.Lights.Length == 0, "inside-scoped effect releases on exit");
            }
            scene = BehaviorScene(); scene.Regions[0].SpawnInside = "baseline"; scene.Regions[0].Hysteresis = 5;
            using (var engine = new SceneBehaviorEngine(scene, null))
            {
                engine.Tick(0, true, true, At(105, 105)); Require(scene.Lights.Length == 0, "spawn-inside baseline emits no entry");
                engine.Tick(0, true, true, At(141, 105)); Require(scene.Lights.Length == 0, "hysteresis retains membership near border");
                engine.Tick(0, true, true, At(150, 105)); engine.Tick(0, true, true, At(105, 105)); Require(scene.Lights.Length == 1, "outside then inside fires after baseline");
            }
            scene = BehaviorScene(); scene.Regions = new RegionData[0];
            scene.Rules = new[] { new RuleData { Id = "land", Event = "landed", Effect = "dim", SetFlag = "powered", Value = "true", Once = true } };
            using (var engine = new SceneBehaviorEngine(scene, null))
            {
                engine.Emit("landed"); engine.Tick(0, true, true, At(0, 0));
                Require(engine.GetFlag("powered") == "true" && scene.Nodes[0].Opacity == .2f, "native/custom event can activate effect and flag");
                engine.Tick(1, true, true, At(0, 0)); engine.Emit("landed"); engine.Tick(0, true, true, At(0, 0));
                Require(scene.Nodes[0].Opacity == .8f, "once rule stays consumed after expiry");
                using (engine.Apply("negative", new EffectDefinition { Id = "subtract", Changes = new[] { new SceneChange { Target = "panel", Property = "opacity", Value = "-.3", Mode = "add" } } }))
                    Require(Math.Abs(scene.Nodes[0].Opacity - .5f) < 1e-6, "negative additive operands use output clamping, not set-value bounds");
                var fading = engine.Apply("fade", new EffectDefinition { Id = "fade", Duration = 2, FadeIn = 1, FadeOut = 1,
                    Changes = new[] { new SceneChange { Target = "panel", Property = "opacity", Value = "0" } } });
                engine.Tick(.5, true, true, At(0, 0)); Require(Math.Abs(scene.Nodes[0].Opacity - .4f) < 1e-5, "numeric override blends from current lower layer");
                engine.Tick(.5, true, true, At(0, 0)); Require(scene.Nodes[0].Opacity == 0, "fade reaches target");
                engine.Tick(1, true, true, At(0, 0)); Require(!fading.Active && scene.Nodes[0].Opacity == .8f, "fade expiry restores exact baseline");
            }
            scene = BehaviorScene(); scene.Regions = new RegionData[0];
            using (var engine = new SceneBehaviorEngine(scene, null))
            {
                var definition = new EffectDefinition { Id = "stack", Repeat = "stack", MaxStacks = 2, Duration = 1, Clock = "presentation", Changes = new[] { new SceneChange { Target = "panel", Property = "opacity", Value = ".5", Mode = "multiply" } } };
                var a = engine.Apply("test", definition); var b = engine.Apply("test", definition);
                Require(Math.Abs(scene.Nodes[0].Opacity - .2f) < 1e-6, "stacked multipliers compose deterministically");
                bool rejected = false; try { engine.Apply("test", definition); } catch (InvalidOperationException) { rejected = true; }
                Require(rejected && a.Active && b.Active, "stack budget failure preserves existing effects");
                engine.Tick(1, false, true, At(0, 0)); Require(!a.Active && !b.Active, "presentation clock continues while gameplay is held");
                engine.Tick(0, true, true, At(0, 0)); // Drain start; paused ticks intentionally retain queued events.
                for (int i = 0; i < 256; i++) engine.Emit("queued");
                rejected = false; try { engine.SetFlag("powered", "true"); } catch (InvalidOperationException) { rejected = true; }
                Require(rejected && engine.GetFlag("powered") == "false", "full event queue cannot partially change a flag");
            }
            scene = BehaviorScene(); scene.Regions = new RegionData[0];
            scene.Rules = new[] { new RuleData { Id = "on", Event = "flag:powered", RequiresFlag = "powered", EqualsValue = "false", SetFlag = "powered", Value = "true" }, new RuleData { Id = "off", Event = "flag:powered", RequiresFlag = "powered", EqualsValue = "true", SetFlag = "powered", Value = "false" } };
            using (var engine = new SceneBehaviorEngine(scene, null))
            { engine.Emit("flag:powered"); engine.Tick(0, true, true, At(0, 0)); Require(engine.InspectErrors().Length > 0, "cyclic rules are bounded and surfaced as diagnostics"); engine.Tick(0, true, true, At(0, 0)); }
            scene = BehaviorScene(); scene.Effects[0].Group = scene.Effects[1].Group = "room"; scene.Regions[0].Owner = "room";
            scene.Regions = new[] { scene.Regions[0], new RegionData { Id = "night", Owner = "room", X = 300, Y = 100, Width = 40, Height = 40, Enter = "dim" } };
            using (var engine = new SceneBehaviorEngine(scene, null))
            {
                engine.Tick(0, true, true, At(105, 105)); engine.Tick(0, true, true, At(305, 105));
                Require(scene.Lights.Length == 0 && scene.Nodes[0].Asset == "blue" && scene.Nodes[0].Opacity == .2f && engine.InspectEffects().Length == 1, "shared room owner replaces the old grouped state across regions");
                engine.SetFlag("powered", "true"); engine.ResetRun();
                Require(engine.GetFlag("powered") == "false" && engine.InspectEffects().Length == 0 && scene.Nodes[0].Opacity == .8f, "native reset clears effects, flags and semantic state");
            }
            foreach (var outside in new[] { At(80, 105), At(145, 105), At(105, 70), At(105, 145) })
            {
                scene = BehaviorScene(); scene.Regions[0].Dwell = .25;
                using (var engine = new SceneBehaviorEngine(scene, null))
                {
                    engine.Tick(.1, true, true, outside); engine.Tick(.1, true, true, At(105, 105));
                    Require(scene.Lights.Length == 0, "entry from each side respects dwell");
                    engine.Tick(.15, true, true, At(105, 105)); Require(scene.Lights.Length == 1, "continuous dwell activates from each side");
                }
            }
            scene = BehaviorScene(); scene.Regions = new RegionData[0]; scene.ScreenLooks = new[] { new ScreenLook { Screen = 1 } };
            using (var engine = new SceneBehaviorEngine(scene, null))
            {
                using (engine.Apply("screen", new EffectDefinition { Id = "ambient", Changes = new[] { new SceneChange { Target = "screen:1", Property = "ambientIntensity", Value = "0.5" } } }))
                    Require(scene.ScreenLooks[0].AmbientIntensity == .5f, "screen override replaces inherited intensity");
                Require(scene.ScreenLooks[0].AmbientIntensity == -1, "screen override disposal restores inheritance sentinel");
                bool rejected = false;
                try { engine.Apply("screen", new EffectDefinition { Id = "invalid-inherit", Changes = new[] { new SceneChange { Target = "screen:1", Property = "ambientIntensity", Value = "-0.5" } } }); } catch (System.IO.InvalidDataException) { rejected = true; }
                Require(rejected, "fractional negative intensity cannot masquerade as inheritance");
                rejected = false;
                try { engine.Apply("offset", new EffectDefinition { Id = "overflow", Lights = new[] { new LightSpawn { Template = "lantern", OffsetX = float.MaxValue } } }); } catch (System.IO.InvalidDataException) { rejected = true; }
                Require(rejected && scene.Lights.Length == 0, "finite but overflowing attachment operands are rejected atomically");
            }
        }
        private static void PersistenceChecks(string root)
        {
            string directory = System.IO.Path.Combine(root, "scene-save-test-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory); string context = directory;
            var store = new ScenePersistence("test.map", () => context);
            store.Save(new System.Collections.Generic.Dictionary<string, string> { { "old-power", "true" } });
            Require(store.Load()["old-power"] == "true", "scene flags round trip through atomic sidecar");
            SceneSaveCoordinator.Publish(store, new System.Collections.Generic.Dictionary<string, string> { { "old-power", "true" } });
            Exception workerError = null; var worker = new System.Threading.Thread(() => { try { SceneSaveCoordinator.Save(); } catch (Exception e) { workerError = e; } }); worker.Start(); worker.Join();
            Require(workerError == null && store.Load()["old-power"] == "true", "native save worker consumes an immutable flag snapshot");
            using (var entered = new System.Threading.ManualResetEvent(false))
            using (var resume = new System.Threading.ManualResetEvent(false))
            {
                int block = 0;
                var delayed = new ScenePersistence("delayed.map", () => {
                    if (System.Threading.Volatile.Read(ref block) != 0) { entered.Set(); if (!resume.WaitOne(5000)) throw new Exception("Storage test release timed out"); }
                    return directory;
                });
                SceneSaveCoordinator.Publish(delayed, new System.Collections.Generic.Dictionary<string, string> { { "value", "first" } });
                System.Threading.Volatile.Write(ref block, 1);
                worker = new System.Threading.Thread(SceneSaveCoordinator.Save); worker.Start();
                Require(entered.WaitOne(5000), "Save entered controlled slow storage");
                bool published = false;
                var publisher = new System.Threading.Thread(() => { SceneSaveCoordinator.Publish(delayed,
                    new System.Collections.Generic.Dictionary<string, string> { { "value", "latest" } }); published = true; });
                publisher.Start();
                bool responsive;
                try { responsive = publisher.Join(1000); }
                finally { System.Threading.Volatile.Write(ref block, 0); resume.Set(); worker.Join(); publisher.Join(); }
                Require(responsive && published, "Flag publication does not wait for a blocked disk write");
                SceneSaveCoordinator.Save();
                Require(delayed.Load()["value"] == "latest", "An older completed write cannot acknowledge a newer pending packet");
                string savedFile = System.IO.Directory.GetFiles(System.IO.Path.Combine(directory, "MegaMapping"), "*.xml")
                    .Single(file => System.IO.File.ReadAllText(file).Contains("delayed.map"));
                SceneSaveCoordinator.Publish(delayed, new System.Collections.Generic.Dictionary<string, string> { { "value", "retry" } });
                using (var locked = System.IO.File.Open(savedFile, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.None)) SceneSaveCoordinator.Save();
                SceneSaveCoordinator.Save();
                Require(delayed.Load()["value"] == "retry", "Storage recovery retries the retained dirty packet without another flag change");
            }
            Require(new ScenePersistence("different.map", () => context).Load().Count == 0, "map identities isolate flags within one save context");
            var scene = BehaviorScene(); scene.Flags[0].Scope = "save"; scene.Flags[0].PreviousId = "old-power";
            using (var engine = new SceneBehaviorEngine(scene, null)) { engine.LoadPersistentFlags(store.Load()); Require(engine.GetFlag("powered") == "true", "renamed flags migrate from explicit old IDs"); }
            context = directory + "-different-slot";
            bool rejected = false; try { store.Save(new System.Collections.Generic.Dictionary<string, string>()); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "context switch rejects stale save writes");
            context = directory; SceneSaveCoordinator.Reset(directory);
            ScenePersistence resetStore; int resetRevision;
            using (var locked = System.IO.File.Open(System.IO.Path.Combine(directory, "MegaMapping/epoch.txt"), System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.None))
            {
                resetStore = SceneSaveCoordinator.ResetStore(store, out resetRevision);
                Require(resetStore != null && resetRevision == SceneSaveCoordinator.Revision,
                    "Gameplay reset receives a ready epoch without reading locked storage");
            }
            resetStore.Save(new System.Collections.Generic.Dictionary<string, string> { { "after-reset", "true" } });
            Require(resetStore.Load()["after-reset"] == "true", "Reset store commits only new progress with the worker-owned epoch");
            SceneSaveCoordinator.Reset(directory);
            Require(new ScenePersistence("test.map", () => context).Load().Count == 0, "native reset invalidates all old map flags without deleting recoverable files");
            rejected = false; try { store.Save(new System.Collections.Generic.Dictionary<string, string>()); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "pre-reset store cannot resurrect old progress");
        }
    }
}
