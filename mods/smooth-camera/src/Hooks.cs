using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using EntityComponent;
using HarmonyLib;
using JumpKing;
using JumpKing.Level;
using JumpKing.Util.Tags;
using Microsoft.Xna.Framework;

namespace SmoothCamera
{
    internal static class Hooks
    {
        internal const string Id = "smooth-camera.render";
        internal static bool Installed;
        private static IDisposable finalPresentation;
        private static JKRuntime.OwnedPatches patches;
        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        internal static void Install()
        {
            if (Installed) return;
            AssertContracts();
            if (patches != null) { patches.Dispose(); patches = null; }
            if (JKRuntime.OwnedPatches.SharedEngine() != typeof(Harmony).Assembly) throw new InvalidOperationException("Camera Harmony ABI does not match the shared engine");
            var harmony = patches = new JKRuntime.OwnedPatches(Id);
            try
            {
                NativeDrawQueries.Install(harmony);
                harmony.Add(typeof(JumpGame).GetMethod("Draw"), prefix: Method(typeof(Renderer), "BeginFrame"),
                    transpiler: Method(typeof(Hooks), "RewriteDraw"), finalizer: Method(typeof(Renderer), "EndFrame"));
                harmony.Add(typeof(JumpGame).GetMethod("Update"), postfix: Method(typeof(Renderer), "AfterUpdate"));
                harmony.Add(typeof(Game1).GetMethod("Draw", BindingFlags.Instance | BindingFlags.NonPublic), prefix: Method(typeof(Renderer), "BeforeGameDraw"));
                harmony.Add(typeof(Game1).GetMethod("DrawRenderTarget", BindingFlags.Instance | BindingFlags.NonPublic), prefix: Method(typeof(Renderer), "Present"));
                PresentationClock.Ensure();
                finalPresentation = JKRuntime.FrameComposition.RegisterPresenter(delegate { return Renderer.CanPresent; });
                harmony.Add(typeof(EntityManager).GetMethod("Draw"), transpiler: Method(typeof(Hooks), "RewriteEntities"));
                harmony.Add(typeof(Camera).GetProperty("CurrentScreen").GetGetMethod(), prefix: Method(typeof(RenderContext), "CurrentScreen"));
                harmony.Add(typeof(Camera).GetProperty("CurrentScreenIndex1").GetGetMethod(), prefix: Method(typeof(RenderContext), "CurrentScreenIndex1"));
                harmony.Add(typeof(Camera).GetMethod("TransformVector2"), postfix: Method(typeof(RenderContext), "Vector"));
                harmony.Add(typeof(Camera).GetMethod("TransformRect", new[] { typeof(Rectangle) }), postfix: Method(typeof(RenderContext), "RectangleValue"));
                harmony.Add(typeof(Camera).GetMethod("TransformRect", new[] { typeof(Rectangle).MakeByRefType() }), postfix: Method(typeof(RenderContext), "RectangleRef"));
                Installed = true;
            }
            catch (Exception failure)
            {
                var cleanup = new JKRuntime.RuntimeScope();
                cleanup.Defer(delegate { harmony.Dispose(); patches = null; });
                cleanup.Defer(PresentationClock.Release);
                if (finalPresentation != null) cleanup.Own(finalPresentation);
                try { cleanup.Dispose(); } catch (Exception error) { throw new AggregateException("Camera hooks and rollback failed", failure, error); }
                throw;
            }
        }

        internal static void Uninstall()
        {
            var release = new JKRuntime.RuntimeScope();
            release.Defer(delegate { if (patches != null) { patches.Dispose(); patches = null; } });
            release.Defer(PresentationClock.Release);
            release.Defer(delegate { if (finalPresentation != null) { finalPresentation.Dispose(); finalPresentation = null; } });
            release.Dispose(); Installed = false;
        }
        private static MethodInfo Method(Type type, string name) { return type.GetMethod(name, Flags); }
        internal static void AssertContracts()
        {
            PresentationClock.AssertContracts();
            if (RenderContext.NativeScreen == null || RenderContext.NativeScreen.FieldType != typeof(int)
                || Renderer.Screens == null || Renderer.Screens.FieldType != typeof(LevelScreen[]))
                throw new NotSupportedException("Smooth Camera: unsupported native camera/level layout");
        }

        internal static IEnumerable<CodeInstruction> RewriteDraw(IEnumerable<CodeInstruction> instructions)
        {
            var result = new List<CodeInstruction>();
            int backgrounds = 0, entities = 0, foregrounds = 0, overlays = 0;
            foreach (var instruction in instructions)
            {
                var call = instruction.operand as MethodInfo;
                string replacement = null;
                if (call == typeof(LevelScreen).GetMethod("Draw")) { replacement = "Background"; backgrounds++; }
                if (call == typeof(LevelScreen).GetMethod("DrawForeground")) { replacement = "Foreground"; foregrounds++; }
                if (call == typeof(EntityManager).GetMethod("Draw")) { replacement = "Entities"; entities++; }
                if (call == typeof(IForeground).GetMethod("ForegroundDraw")) { replacement = "Overlay"; overlays++; }
                if (replacement != null)
                {
                    // Keep labels and exception regions on the original instruction.
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = typeof(Renderer).GetMethod(replacement, Flags);
                }
                result.Add(instruction);
            }
            if (backgrounds != 1 || foregrounds != 1 || entities != 2 || overlays != 1)
                throw new NotSupportedException("Smooth Camera: unsupported JumpGame.Draw world boundaries");
            return result;
        }

        internal static IEnumerable<CodeInstruction> RewriteEntities(IEnumerable<CodeInstruction> instructions)
        {
            var result = new List<CodeInstruction>();
            int replaced = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.operand as MethodInfo == typeof(Entity).GetMethod("Draw"))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = typeof(Renderer).GetMethod("EntityDraw", Flags);
                    replaced++;
                }
                result.Add(instruction);
            }
            if (replaced != 1) throw new NotSupportedException("Smooth Camera: unsupported entity draw boundary");
            return result;
        }
    }
}
