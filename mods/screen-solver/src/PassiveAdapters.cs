using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using JKRuntime;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;

namespace ScreenSolver
{
    // Exact, audited exceptions for code which the isolated model never runs.
    // This is not an owner allowlist: a different target/kind/method still fails.
    internal sealed class PassiveAdapters
    {
        internal const string JumpHash = "e7fd77e35380e6552df67890063424f2f0963cdc008e9359e880fc42583c0538";
        internal const string MuteHash = "a90e615e3675192f2751484ad7be46ff6d6dfe6ee9ab5baf1e1545899f345026";
        internal const string SizesHash = "2318f08edd9d7ea8b039d3271e53c4320ce20280a8897dbf19d4259d009619cf";
        internal const string SlopeHash = "5af16339e2d0e20e49569727a78006fc58e20e156b722313f52ccb73e7844c2d";
        private readonly Dictionary<Assembly, string> hashes = new Dictionary<Assembly, string>();
        private bool Verified(Type type, string name, string hash)
        {
            var assembly = type.Assembly;
            if (assembly.GetName().Name != name || string.IsNullOrEmpty(assembly.Location)) return false;
            string actual;
            if (!hashes.TryGetValue(assembly, out actual))
            {
                using (var stream = File.OpenRead(assembly.Location))
                using (var sha = SHA256.Create()) actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                hashes.Add(assembly, actual);
            }
            return actual == hash;
        }
        internal static bool IsAdapterMethod(Type type)
        {
            return new[] { "VerticalWindMod", "JumpKingLastJumpValue", "MuteJumpSfxBlock", "MoreBlockSizes", "ForcedSlopeBlocks" }.Contains(type.Assembly.GetName().Name)
                || type.FullName == "JKRuntime.Gameplay.ModifierRegistrationObserver";
        }
        internal bool Accepts(MethodBase target, string kind, string owner, MethodInfo patch)
        {
            if (patch == null || patch.DeclaringType == null) return false;
            var type = patch.DeclaringType;
            if (owner == "jk.runtime.run-modifiers" && target.DeclaringType == typeof(BodyComp) &&
                new[] { "RegisterBehaviour", "RegisterBehaviourBefore", "RegisterBehaviourAfter", "RemoveBehaviour" }.Contains(target.Name) &&
                type.Assembly == typeof(RuntimeApi).Assembly && type.FullName == "JKRuntime.Gameplay.ModifierRegistrationObserver")
            {
                // Read the loaded constant, not the compiler-inlined SDK value.
                // 1.3/1.4/1.5 retain the audited observer implementation. Geometry,
                // inventory and presentation services are not invoked here.
                object version = typeof(RuntimeApi).GetField("Version").GetRawConstantValue();
                if (!Equals(version, "1.2.0") && !Equals(version, "1.3.0") && !Equals(version, "1.4.0") && !Equals(version, "1.5.0")) return false;
                return kind == "Prefixes" && patch.Name == "Prefix" || kind == "Postfixes" && patch.Name == "Postfix" ||
                    kind == "Finalizers" && patch.Name == "Finalizer";
            }
            if (owner == "Phoenixx19.LastJumpValue.Harmony" && kind == "Postfixes" &&
                target == typeof(JumpState).GetMethod("MyRun", NativeWorld.Flags) &&
                type.FullName == "JumpKingLastJumpValue.Models.JumpChargeCalc" && patch.Name == "Run")
                return Verified(type, "JumpKingLastJumpValue", JumpHash);
            if (owner == "Zebra.MuteJumpSfxBlock.Harmony" && kind == "Transpilers" && patch.Name == "Transpiler")
            {
                string expected = target == typeof(PlayBumpSFXBehaviour).GetMethod("ExecuteBehaviour") ? "PatchPlayBumpSfxBehaviour" :
                    target == typeof(JumpState).GetMethod("HandleSounds", NativeWorld.Flags) ? "PatchJumpState" :
                    target == typeof(FailState).GetMethod("HandleSounds", NativeWorld.Flags) ? "PatchFailState" :
                    target == typeof(IsOnGround).GetMethod("HandleSounds", NativeWorld.Flags) ? "PatchIsOnGround" : null;
                return expected != null && type.FullName == "MuteJumpSfxBlock.Patches." + expected && Verified(type, "MuteJumpSfxBlock", MuteHash);
            }
            if (owner == "Zebra.MoreBlockSizes.Harmony")
            {
                bool match = target.DeclaringType == typeof(LevelManager) && target.Name == "LoadScreens" &&
                    type.FullName == "MoreBlockSizes.Patches.PatchLoadScreens" &&
                    (kind == "Prefixes" && patch.Name == "Prefix" || kind == "Postfixes" && patch.Name == "Postfix") ||
                    target.DeclaringType == typeof(LevelManager) && target.Name == "LoadBlocksInterval" && target.GetParameters().Length == 7 &&
                    type.FullName == "MoreBlockSizes.Patches.PatchLoadBlocksInterval" && kind == "Prefixes" && patch.Name == "Prefix" ||
                    target == typeof(LevelScreen).GetMethod("DebugDraw", NativeWorld.Flags) &&
                    type.FullName == "MoreBlockSizes.Patches.PatchDebugDraw" && kind == "Prefixes" && patch.Name == "Prefix";
                return match && Verified(type, "MoreBlockSizes", SizesHash);
            }
            if (owner == "ForcedSlopeBlocks.Harmony" && kind == "Postfixes" && patch.Name == "Postfix" &&
                target == typeof(SlopeBlock).GetConstructor(new[] { typeof(Microsoft.Xna.Framework.Rectangle), typeof(SlopeType) }) &&
                type.FullName == "ForcedSlopeBlocks.Patches.PatchSlopeBlock") return Verified(type, "ForcedSlopeBlocks", SlopeHash);
            return false;
        }
        internal bool IsAudioBlock(Type type)
        { return type.FullName == "MuteJumpSfxBlock.Blocks.BlockMuteJumpSfx" && Verified(type, "MuteJumpSfxBlock", MuteHash); }
        internal bool IsAudioBehaviour(Type type)
        { return type.FullName == "MuteJumpSfxBlock.Behaviours.BehaviourMuteJumpSfx" && Verified(type, "MuteJumpSfxBlock", MuteHash); }
        internal bool IsFixedSlope(Type type)
        { return type.FullName == "ForcedSlopeBlocks.Blocks.SlopeBottomLeft" && Verified(type, "ForcedSlopeBlocks", SlopeHash); }
    }
}
