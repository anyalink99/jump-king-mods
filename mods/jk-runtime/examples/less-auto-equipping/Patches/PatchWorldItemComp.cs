// Derived from Zebra's LessAutoEquipping (MIT); see LICENSE.md.
namespace LessAutoEquipping.Patches
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using HarmonyLib;

    [HarmonyPatch("JumpKing.MiscEntities.WorldItems.WorldItemComp", "OnPickup")]
    public static class PatchWorldItemComp
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var code = new List<CodeInstruction>(instructions);

            // Because we want to insert at two places.
            var firstInsertionIndex = -1;
            var firstContinueFound = false;
            var firstContinueLabel = il.DefineLabel();

            var secondInsertionIndex = -1;
            var secondContinueFound = false;
            var secondContinueLabel = il.DefineLabel();

            var skinManager = AccessTools.TypeByName("JumpKing.Player.Skins.SkinManager");
            var setSkinEnabled = AccessTools.Method(skinManager, "SetSkinEnabled");
            var enableSkin = AccessTools.Method(skinManager, "EnableSkin");

            var removeWorldItem = AccessTools.Method(
                AccessTools.TypeByName("JumpKing.SaveThread.SaveLube"),
                "RemoveWorldItem");

            int i;
            // Find the first part, that is where we want to insert out own IL instructions.
            for (i = 0; i < code.Count - 4; i++)
            {
                if (code[i].opcode != OpCodes.Ldarg_0
                    || code[i + 1].opcode != OpCodes.Ldflda
                    || code[i + 2].opcode != OpCodes.Ldfld
                    || code[i + 3].opcode != OpCodes.Ldc_I4_1
                    || code[i + 4].opcode != OpCodes.Call
                    || code[i + 4].operand as MethodInfo != setSkinEnabled)
                {
                    continue;
                }

                firstInsertionIndex = i;
                break;
            }

            // Find the second part, that is where we want to jump to in case of auto equipping being disabled.
            for (; i < code.Count - 2; i++)
            {
                if (code[i].opcode != OpCodes.Ldarg_0
                    || code[i + 1].opcode != OpCodes.Ldfld
                    || code[i + 2].opcode != OpCodes.Call
                    || code[i + 2].operand as MethodInfo != removeWorldItem)
                {
                    continue;
                }

                firstContinueFound = true;
                code[i].labels.Add(firstContinueLabel);
                break;
            }

            // Find the first(third total) part, that is where we want to insert out own IL instructions.
            for (i = 0; i < code.Count - 3; i++)
            {
                if (code[i].opcode != OpCodes.Ldarg_0
                    || code[i + 1].opcode != OpCodes.Ldflda
                    || code[i + 2].opcode != OpCodes.Ldfld
                    || code[i + 3].opcode != OpCodes.Call
                    || code[i + 3].operand as MethodInfo != enableSkin)
                {
                    continue;
                }

                secondInsertionIndex = i;
                break;
            }

            // Find the second(fourth total) part, that is where we want to jump to in case of auto equipping being disabled.
            for (; i < code.Count - 2; i++)
            {
                if (code[i].opcode != OpCodes.Ldarg_0
                    || code[i + 1].opcode != OpCodes.Call
                    || code[i + 2].opcode != OpCodes.Brfalse_S)
                {
                    continue;
                }

                secondContinueFound = true;
                code[i].labels.Add(secondContinueLabel);
                break;
            }

            if (firstInsertionIndex == -1
                || !firstContinueFound
                || secondInsertionIndex == -1
                || !secondContinueFound)
            {
                throw new System.NotSupportedException("LessAutoEquipping: expected native auto-equip sequence is missing");
            }

            var firstInsert = new List<CodeInstruction>
            {
                new CodeInstruction(
                    OpCodes.Call,
                    AccessTools.PropertyGetter(typeof(ModEntry), "Preferences")),
                new CodeInstruction(
                    OpCodes.Callvirt,
                    AccessTools.PropertyGetter(typeof(Preferences), "ShouldPreventAutoEquip")),
                new CodeInstruction(OpCodes.Brtrue_S, firstContinueLabel),
            };
            code.InsertRange(firstInsertionIndex, firstInsert);

            var secondInsert = new List<CodeInstruction>
            {
                new CodeInstruction(
                    OpCodes.Call,
                    AccessTools.PropertyGetter(typeof(ModEntry), "Preferences")),
                new CodeInstruction(
                    OpCodes.Callvirt,
                    AccessTools.PropertyGetter(typeof(Preferences), "ShouldPreventAutoEquip")),
                new CodeInstruction(OpCodes.Brtrue_S, secondContinueLabel),
            };
            code.InsertRange(secondInsertionIndex + firstInsert.Count, secondInsert);

            return code.AsEnumerable();
        }
    }
}
