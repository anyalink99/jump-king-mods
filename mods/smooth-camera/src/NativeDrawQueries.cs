using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using JumpKing;
using Microsoft.Xna.Framework;

namespace SmoothCamera
{
    // Patch the actual callers as well as the getters. A getter already inlined
    // into a native NPC's Draw before camera installation bypasses a getter hook.
    internal static class NativeDrawQueries
    {
        internal static void Install(JKRuntime.OwnedPatches harmony)
        {
            foreach (string name in new[] { "JumpKing.MiscEntities.OldManEntity", "JumpKing.MiscEntities.Merchant.MerchantEntity", "JumpKing.Props.LoopingProp",
                "JumpKing.Player.PlayerEntity", "JumpKing.Props.RattmanText.RattmanEntity", "JumpKing.MiscSystems.ScreenEvents.FlyingGargoyle",
                "JumpKing.MiscEntities.OldMan.SpeechBubbleFormat" })
            {
                Type type = typeof(Game1).Assembly.GetType(name, true);
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    if (method.Name == "Draw" || method.Name == "DrawText" || method.Name == "DrawHitbox" || method.Name == "ForegroundDraw")
                        harmony.Add(method, transpiler: typeof(NativeDrawQueries).GetMethod("Rewrite", JKRuntime.OwnedPatches.Members));
            }
        }
        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                var method = instruction.operand as MethodInfo;
                if (method != null && method.DeclaringType == typeof(Camera))
                {
                    string replacement = method.Name == "get_CurrentScreen" ? "Screen0" : method.Name == "get_CurrentScreenIndex1" ? "Screen1"
                        : method.Name == "TransformVector2" ? "Vector" : method.Name == "TransformRect" ? (method.GetParameters()[0].ParameterType.IsByRef ? "RectRef" : "Rect") : null;
                    if (replacement != null)
                    { instruction.opcode = OpCodes.Call; instruction.operand = typeof(NativeDrawQueries).GetMethod(replacement, BindingFlags.Static | BindingFlags.NonPublic); }
                }
                yield return instruction;
            }
        }
        [MethodImpl(MethodImplOptions.NoInlining)] private static int Screen0() { return RenderContext.Active ? RenderContext.Screen : Camera.CurrentScreen; }
        [MethodImpl(MethodImplOptions.NoInlining)] private static int Screen1() { return RenderContext.Active ? RenderContext.Screen + 1 : Camera.CurrentScreenIndex1; }
        [MethodImpl(MethodImplOptions.NoInlining)] private static Vector2 Vector(Vector2 value)
        { if (!RenderContext.Active) return Camera.TransformVector2(value); return value + Camera.Offset + new Vector2(RenderContext.HorizontalCorrection, RenderContext.VerticalTranslation); }
        [MethodImpl(MethodImplOptions.NoInlining)] private static Rectangle Rect(Rectangle value)
        { if (!RenderContext.Active) return Camera.TransformRect(value); value.Offset(Camera.Offset.ToPoint()); value.Offset(RenderContext.HorizontalCorrection, RenderContext.VerticalTranslation); return value; }
        [MethodImpl(MethodImplOptions.NoInlining)] private static void RectRef(ref Rectangle value) { value = Rect(value); }
    }
}
