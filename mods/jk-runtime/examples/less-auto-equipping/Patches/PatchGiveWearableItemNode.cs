// Derived from Zebra's LessAutoEquipping (MIT); see LICENSE.md.
namespace LessAutoEquipping.Patches
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using HarmonyLib;
    using JumpKing.GameManager.MultiEnding;

    [HarmonyPatch(typeof(GiveWearableItemNode), "MyRun")]
    public static class PatchGiveWearableItemNode
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var code = new List<CodeInstruction>(instructions);

            var insertionIndex = -1;
            var continueFound = false;
            var continueLabel = il.DefineLabel();
            var enableSkin = AccessTools.Method(
                AccessTools.TypeByName("JumpKing.Player.Skins.SkinManager"),
                "EnableSkin");

            int i;
            // Find the first part, that is where we want to insert out own IL instructions.
            for (i = 0; i < code.Count - 2; i++)
            {
                if (code[i].opcode != OpCodes.Ldarg_0
                    || code[i + 1].opcode != OpCodes.Ldfld
                    || code[i + 2].opcode != OpCodes.Call
                    || code[i + 2].operand as MethodInfo != enableSkin)
                {
                    continue;
                }

                insertionIndex = i;
                break;
            }

            // Find the second part, that is where we want to jump to in case of auto equipping being disabled.
            for (; i < code.Count - 1; i++)
            {
                if (code[i].opcode != OpCodes.Ldc_I4_1 || code[i + 1].opcode != OpCodes.Ret)
                {
                    continue;
                }

                continueFound = true;
                code[i].labels.Add(continueLabel);
                break;
            }

            if (insertionIndex == -1 || !continueFound)
            {
                throw new System.NotSupportedException("LessAutoEquipping: expected native auto-equip sequence is missing");
            }

            var insert = new List<CodeInstruction>
            {
                new CodeInstruction(
                    OpCodes.Call,
                    AccessTools.PropertyGetter(typeof(ModEntry), "Preferences")),
                new CodeInstruction(
                    OpCodes.Callvirt,
                    AccessTools.PropertyGetter(typeof(Preferences), "ShouldPreventAutoEquip")),
                new CodeInstruction(OpCodes.Brtrue_S, continueLabel),
            };
            code.InsertRange(insertionIndex, insert);

            return code.AsEnumerable();
        }
    }

}
