using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BehaviorTree;
using JKRuntime.Gameplay;
using JumpKing.Level;
using JumpKing.Player;

namespace ScreenSolver
{
    // These in-memory modules cannot be checked through Assembly.Location.
    // Pin their actual installed build identities, not just their display names.
    internal sealed class PlayerAdapters
    {
        internal static readonly Dictionary<string, string> Builds = CreateBuilds();
        private static Dictionary<string, string> CreateBuilds()
        {
            var builds = new Dictionary<string, string>(FirstPartyBuilds.Values) {
            { "JumpKingSaveStates", "d12861e9-139b-4699-8320-803364d40c10" },
            { "JumpKingManager", "9d451e62-4b9d-4dd0-b1bc-85c9eaf68fa9" }
            };
            return builds;
        }
        internal bool Subframe;
        private bool megaChecked;
        internal static bool Known(Assembly assembly) { return Builds.ContainsKey(assembly.GetName().Name); }
        private static bool Verified(Type type)
        {
            string id; return Builds.TryGetValue(type.Assembly.GetName().Name, out id) && type.Module.ModuleVersionId.ToString() == id;
        }
        internal static object Read(object value, string name)
        {
            Type type = value as Type ?? value.GetType(); object instance = value is Type ? null : value;
            for (Type t = type; t != null; t = t.BaseType)
            {
                var f = t.GetField(name, NativeWorld.Flags | BindingFlags.DeclaredOnly) ?? t.GetField("<" + name + ">k__BackingField", NativeWorld.Flags | BindingFlags.DeclaredOnly);
                if (f != null) return f.GetValue(instance);
            }
            throw new NotSupportedException("Missing player adapter state: " + type.FullName + "." + name);
        }
        internal void ValidateModes()
        {
            if (GameFeatures.Movement != MovementMode.Vanilla || GameFeatures.IsMorphed || GameFeatures.ThrustEnabled)
                throw new NotSupportedException("Active Casual / Ball / Jetpack movement needs a simulation provider");
        }
        internal bool Node(IBTnode node)
        {
            if (node.GetType().FullName != "SubframeCharge.SubframeChargeState" || !Verified(node.GetType())) return false;
            if ((bool)Read(node, "nativeCharging")) throw new NotSupportedException("Release Jump before Solve");
            Subframe = !(bool)Read(node, "observationOnly");
            return true;
        }
        internal static string NodeName(IBTnode node)
        { return node.GetType().FullName == "SubframeCharge.SubframeChargeState" && Verified(node.GetType()) ? typeof(JumpState).FullName : node.GetType().FullName; }
        internal bool Ignore(object value)
        {
            var type = value.GetType(); if (!Verified(type)) return false;
            switch (type.FullName)
            {
                // Observers are deliberately NOT executed by speculative ticks.
                case "SubframeCharge.SubframeChargeInstaller+JumpLifecycleMarker":
                case "SubframeCharge.ChargeFrameComponents+Observer":
                case "SubframeCharge.JumpTrajectoryProbe":
                case "SubframeCharge.PauseClockObserver":
                case "Replays.ReplayCaptureComponent":
                case "Replays.ReplayRecorder":
                case "Replays.ReplayGhostEntity":
                case "MorphBall.MorphVisualComponent":
                case "JumpKingManager.ManagerBehaviour":
                // Save/load keys are not in the solver's action vocabulary.
                case "JumpKingSaveStates.SavestateBehaviour": return true;
                // Merchant/dispenser interactions are not solver inputs. The
                // guard only enables/disables the merchant's dialogue tree;
                // never run Update/OnDestroy against that live tree in search.
                case "MoreItems.BargainburgMerchantGuard":
                case "MoreItems.ConsumableDispenser":
                // Presentation and run attribution, not movement controllers.
                case "JumpKingJetpack.JetpackAirSpriteComponent":
                case "JumpKingJetpack.JetpackUsageMarker": return true;
                case "MoreItems.RewinderController": return !(bool)Read(value, "rewinding");
                case "MegaGameplayExpansion.WarpController":
                    if (Read(value, "plan") != null) throw new NotSupportedException("Wait for Warp to finish");
                    ValidateMega(type.Assembly); return true;
                case "MegaGameplayExpansion.AirDashController":
                    if ((bool)Read(value,"active")) throw new NotSupportedException("Wait for Air Dash to finish");
                    ValidateMega(type.Assembly); return true;
                case "MegaGameplayExpansion.NoWalkOffController+Phase":
                case "MegaGameplayExpansion.NoWalkOffController+CommitComponent":
                case "MegaGameplayExpansion.WarpVisual":
                case "MegaGameplayExpansion.AirDashVisual": ValidateMega(type.Assembly); return true;
            }
            return false;
        }
        private void ValidateMega(Assembly assembly)
        {
            if (megaChecked) return;
            var settings = Read(assembly.GetType("MegaGameplayExpansion.Settings", true), "current");
            if (settings == null) throw new NotSupportedException("Mega settings are not initialized");
            if ((bool)Read(settings, "WarpJump") || (bool)Read(settings, "NoWalkOff") || (bool)Read(settings,"AirDash"))
                throw new NotSupportedException("Active Warp / No Walk Off / Air Dash needs a simulation provider");
            foreach (string name in new[] { "Warp", "NoWalkOff", "AirDash" })
                if (((IEnumerable)Read(Read(assembly.GetType("MegaGameplayExpansion.MapPixels", true), name), "Screens")).Cast<object>().Any())
                    throw new NotSupportedException("Active Mega screen mechanic: " + name);
            // Solid and zone mechanics are rejected by NativeWorld's geometry audit.
            megaChecked = true;
        }
    }
}
