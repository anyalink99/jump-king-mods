using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Security.Cryptography;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Player;

namespace ScreenSolver
{
    // A subscribed block library is not necessarily active on the loaded map.
    // This proof is map-wide, includes persistent flags, and is repeated on Solve.
    internal sealed class WorkshopCoverage
    {
        internal sealed class Contract
        {
            internal string File, Owner, Hash, Targets;
            internal Contract(string file, string owner, string hash, string targets)
            { File = file; Owner = owner; Hash = hash; Targets = targets; }
        }
        internal static readonly Contract[] Contracts = {
            new Contract("JumpKingPlus", "", "ac98bbb0b1ec70ff447dbb4592efe7d921a6f0733a05f107970f6a6bdc8b5bd9", ""),
            new Contract("HighGravityBlockMod", "", "43047f7656020ffe8990e7ccd36dc670b019185e3842145a3f8703f884923867", ""),
            new Contract("SampleJkMod", "", "71d354376fda88c515e41b74448833837c813d1d358e6f43857f11b295caa123", ""),
            new Contract("JumpKing-UpsideDownBlocks", "", "add7b7d2674be8f507d6fb22948b085dac98062f9264ee716ad2c9e65ab579f7", ""),
            new Contract("SwitchBlocks", "Zebra.SwitchBlocks.Harmony", "bbd6a1e0da5653a0082e54aca40c73ce4fa282d92bdefa8e50d4be2f1f599ced",
                "IceBlockBehaviour.get_IsPlayerOnBlock:Postfixes;WaterBlockBehaviour.get_IsPlayerOnBlock:Postfixes;AdvCollisionInfo.get_Sand:Postfixes;ResolveXCollisionBehaviour.ExecuteBehaviour:Prefixes;ResolveYCollisionBehaviour.ExecuteBehaviour:Prefixes;SlopeBlock.JumpKing.Level.IBlock.Intersects:Postfixes;BodyComp.IsOnBlock:Postfixes;WindManager.get_CurrentVelocityRaw:Postfixes"),
            new Contract("JumpKing-Expansion-Blocks", "YutaGoto.JumpKing_Expansion_Blocks", "d2ca8d04d7b444fd7f03b92277f2596f75162f5201a3e3ea1e9acbca8f47fea1",
                "ApplyGravityBehaviour.ExecuteBehaviour:Prefixes;ResolveXCollisionBehaviour.ExecuteBehaviour:Prefixes;BodyComp.IsOnBlock:Postfixes;BodyComp.GetMultipliers:Postfixes;InventoryManager.HasItemEnabled:Postfixes;SkinManager.IsWearingSkin:Postfixes;JumpState.MyRun:Prefixes;JumpState.MyRun:Postfixes;JumpState.DoJump:Prefixes;JumpState.Start:Prefixes;Walk.MyRun:Prefixes"),
            new Contract("JumpKing-GhostOfTheImmortalBabeBlocks", "YutaGoto.JumpKing_GhostOfTheImmortalBabeBlocks", "e4d7ea3e4c9f10082b7d584be166752bb536c49402100366d94f300c3c83fcae",
                "BodyComp.IsOnBlock:Postfixes;BodyComp.GetMultipliers:Postfixes;JumpState.MyRun:Postfixes;InventoryManager.HasItemEnabled:Postfixes;SkinManager.IsWearingSkin:Postfixes"),
            new Contract("AntiBlocks", "Zebra.AntiBlocks.Harmony", "dc7c5434cc2ecd5b9f1fe666e2fa63f7455da39da4b5a092214d9e9fd3df3040",
                "FailState.MyRun:Prefixes;InventoryManager.HasItemEnabled:Postfixes"),
            new Contract("MovementControlBlocks", "Zebra.MovementControlBlocks.Harmony", "121caa1cf6cc5f7fff75346ee7793b76c631ab6613f3f5362111ed0a4f1af03d",
                "JumpState.DoJump:Prefixes;JumpState.DoJump:Postfixes;PadInstance.GetState:Postfixes"),
            new Contract("ConveyorBlockMod", "Mc__Ouille.ConveyorBlockMod", "9ed9caf95217d6d44f5ab580a5d118e4ec6a79ae2b3ca048e97ace13d1709725",
                "ResolveXCollisionBehaviour.ExecuteBehaviour:Prefixes"),
            new Contract("UpSideDownCore", "JeFi.UpsideDownCore.Harmony", "af695a166246d2cd0e597514e5ed9ea4d9e5664f75f6cf5deaf43677b2287b36",
                "SandBlockBehaviour.AdditionalYCollisionCheck:Transpilers;SandBlockBehaviour.ExecuteBlockBehaviour:Transpilers;SandBlockBehaviour.ModifyYVelocity:Transpilers;ApplyGravityBehaviour.ExecuteBehaviour:Transpilers;GuardtowerSoulBugFixBehaviour.ExecuteBehaviour:Transpilers;ResolveXCollisionBehaviour.ExecuteBehaviour:Transpilers;ResolveYCollisionBehaviour.ExecuteBehaviour:Transpilers;WaterParticleSpawningBehaviour.ExecuteBehaviour:Transpilers;LevelScreen.TryCollision:Transpilers;SlopeBlock.JumpKing.Level.IBlock.Intersects:Transpilers;AirAnim.MyRun:Transpilers;FailState.MyRun:Transpilers;JumpState.DoJump:Transpilers;PlayerEntity.Draw:Transpilers;Walk.MyRun:Transpilers"),
            new Contract("CustomWindSwitch", "McOuille.CustomWindSwitch", "8153f2317e58c7c7e42a963301ec7921e96589cea72e03b617a5f9cf0d560edc",
                "WindManager.get_CurrentVelocityRaw:Transpilers"),
            new Contract("Sprinting", "Zebra.Sprinting.Harmony", "8b4f46a03f0505df6437369c446dcbe47ec9be201345e13b203c332c47b8d3ee",
                "PlayerValues.get_WALK_SPEED:Postfixes")
        };
        private readonly Dictionary<Assembly, Contract> verified = new Dictionary<Assembly, Contract>();
        private bool mapChecked;
        private Assembly customWind;
        private readonly PassiveAdapters passive;
        internal WorkshopCoverage(PassiveAdapters value) { passive = value; }
        internal static bool IsKnownAssembly(Assembly assembly)
        { return Contracts.Any(c => string.Equals(c.File, assembly.GetName().Name, StringComparison.OrdinalIgnoreCase)); }
        internal Contract Verify(Assembly assembly)
        {
            Contract result; if (verified.TryGetValue(assembly, out result)) return result;
            result = Contracts.FirstOrDefault(c => string.Equals(c.File, assembly.GetName().Name, StringComparison.OrdinalIgnoreCase));
            if (result == null || string.IsNullOrEmpty(assembly.Location)) return null;
            string hash;
            using (var stream = File.OpenRead(assembly.Location)) using (var sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            if (hash != result.Hash) return null;
            verified.Add(assembly, result); return result;
        }
        internal bool Accepts(MethodBase original, string kind, string owner, MethodInfo patch)
        {
            if (patch == null || original.DeclaringType.Assembly != typeof(BodyComp).Assembly) return false;
            var contract = Verify(patch.DeclaringType.Assembly);
            if (contract == null || owner != contract.Owner || !contract.Targets.Split(';').Contains(original.DeclaringType.Name + "." + original.Name + ":" + kind)) return false;
            if (contract.File == "CustomWindSwitch") { customWind = patch.DeclaringType.Assembly; return true; }
            ValidateMap(); ValidateState(patch.DeclaringType.Assembly, contract);
            return true;
        }
        internal CustomWindProfile[] CaptureWind(int count)
        { return customWind == null ? new CustomWindProfile[count] : CustomWindProfile.Capture(customWind, count); }
        internal bool IsPassiveNode(BehaviorTree.IBTnode node)
        {
            if (node.GetType().FullName != "JumpKingPlus.Nodes.ThinSnowWalkAnimReset" || Verify(node.GetType().Assembly) == null) return false;
            ValidateMap(); return true;
        }
        private void ValidateMap()
        {
            if (mapChecked) return;
            var screens = (LevelScreen[])typeof(LevelManager).GetField("m_screens", NativeWorld.Flags).GetValue(null);
            if (screens == null) throw new NotSupportedException("No loaded collision map");
            foreach (var screen in screens)
            foreach (var block in (IBlock[])typeof(LevelScreen).GetField("m_hitboxes", NativeWorld.Flags).GetValue(screen))
            {
                var type = block.GetType();
                if (type.Assembly != typeof(BodyComp).Assembly && !passive.IsAudioBlock(type) && !passive.IsFixedSlope(type))
                    throw new NotSupportedException("Active map mechanic needs simulation coverage: " + type.FullName);
            }
            mapChecked = true;
        }
        internal bool IgnoreInactiveBehaviour(IBlockBehaviour behaviour)
        {
            var contract = Verify(behaviour.GetType().Assembly); if (contract == null) return false;
            ValidateMap(); ValidateState(behaviour.GetType().Assembly, contract);
            if (behaviour.IsPlayerOnBlock) throw new NotSupportedException("Residual active block state: " + behaviour.GetType().FullName);
            if (contract.File == "ConveyorBlockMod")
                foreach (string name in new[] { "IsPlayerOnBlockLastFrame", "IsPlayerOnBlock2FramesBefore", "IsPlayerOnBlock3FramesBefore" })
                    if ((bool)behaviour.GetType().GetField(name).GetValue(behaviour)) throw new NotSupportedException("Wait until conveyor exit has settled");
            return true;
        }
        private static object Read(Type type, string name)
        {
            var field = type.GetField(name, NativeWorld.Flags) ?? type.GetField("<" + name + ">k__BackingField", NativeWorld.Flags);
            if (field == null) throw new NotSupportedException("Missing Workshop state contract: " + type.FullName + "." + name);
            return field.GetValue(null);
        }
        private static void False(Type type, string name)
        { if ((bool)Read(type, name)) throw new NotSupportedException("Active mechanic needs simulation coverage: " + type.FullName + "." + name); }
        private static void InactiveObject(Type type, string name)
        { var value = Read(type, name) as IBlockBehaviour; if (value != null && value.IsPlayerOnBlock) throw new NotSupportedException("Active movement modifier: " + name); }
        private static void ValidateState(Assembly assembly, Contract contract)
        {
            switch (contract.File)
            {
                case "SwitchBlocks":
                    foreach (string name in new[] { "Auto", "Basic", "Countdown", "Group", "Jump", "Sand", "Sequence", "Threshold" })
                        False(assembly.GetType("SwitchBlocks.Setups.Setup" + name, true), "IsUsed");
                    var post = assembly.GetType("SwitchBlocks.Behaviours.Dummy.BehaviourPost", true);
                    foreach (string name in new[] { "IsPlayerOnIce", "IsPlayerOnSnow", "IsPlayerOnWater", "IsPlayerOnTypeSand", "IsPlayerOnTypeSandUp", "IsPlayerOnMoveUp", "IsPlayerOnInfinityJump" }) False(post, name);
                    False(assembly.GetType("SwitchBlocks.Behaviours.Dummy.BehaviourConveyor", true), "IsPlayerOnConveyor");
                    break;
                case "UpSideDownCore":
                    var controller = assembly.GetType("UpsideDownCore.Controller", true); False(controller, "isReverseGravity");
                    if (Convert.ToInt32(Read(controller, "upsideDownType")) != 0) throw new NotSupportedException("Active UpsideDownCore orientation needs simulation coverage");
                    var manager = assembly.GetType("UpsideDownCore.Models.Manager", true); False(manager, "isReverseGravity"); False(manager, "isUpsideDown"); break;
                case "AntiBlocks":
                    False(assembly.GetType("AntiBlocks.Behaviours.BehaviourAntiSplat", true), "IsOnBlock");
                    False(assembly.GetType("AntiBlocks.Behaviours.BehaviourAntiSnake", true), "IsOnBlock"); break;
                case "MovementControlBlocks":
                    var jump = assembly.GetType("MovementControl.Patches.PatchJumpState", true);
                    InactiveObject(jump, "BehaviourForcedNeutral"); InactiveObject(jump, "BehaviourNoBreaking");
                    InactiveObject(assembly.GetType("MovementControl.Patches.PatchPadInstance", true), "BehaviourInvertInput"); break;
                case "Sprinting": False(assembly.GetType("Sprinting.Patches.PatchControllerManager", true), "IsPressed"); break;
            }
        }
    }
}
