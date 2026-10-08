using System;
using System.Collections.Generic;
using System.Reflection;
using EntityComponent;
using JKRuntime;
using JumpKing;
using JumpKing.Level;
using JumpKing.XnaWrappers;
using Microsoft.Xna.Framework;

namespace Prism
{
    internal static class NativeHooks
    {
        private static MethodInfo Hook(string name) { return typeof(NativeHooks).GetMethod(name, OwnedPatches.Members); }
        internal static void Install(RuntimeScope scope)
        {
            GC.KeepAlive(typeof(HarmonyLib.Harmony));
            var patches = scope.Own(new OwnedPatches("prism.render"));
            patches.Add(typeof(LevelScreen).GetMethod("Draw"), prefix: Hook("BeforeDraw"), postfix: Hook("AfterDraw"));
            patches.Add(typeof(LevelScreen).GetMethod("DrawForeground"), prefix: Hook("Foreground"));
            patches.Add(typeof(JumpGame).GetMethod("Update"), postfix: Hook("AfterUpdate"));
            Audio.Validate(); patches.Add(Audio.NativePlay, prefix: Hook("MusicPlay"));
            // Patch draw implementations rather than entity enumeration, so foreground
            // NPCs and Smooth Camera's extra screen passes follow the same switch.
            foreach (Type type in typeof(Game1).Assembly.GetTypes())
            {
                if (!IsScenery(type)) continue;
                foreach (string method in new[] { "Draw", "ForegroundDraw" })
                {
                    var draw = type.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                    if (draw != null && draw.ReturnType == typeof(void)) patches.Add(draw, prefix: Hook("DrawScenery"));
                }
            }
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var host = assembly.GetType("MegaMappingExpansion.SceneHost", false);
                if (host == null) continue;
                foreach (string name in new[] { "DrawLayer", "ComposeScenePass", "DrawSurfaceShadows", "DrawPlayerRim", "DrawScreenTexts" })
                    foreach (var method in host.GetMethods(OwnedPatches.Members))
                        if (method.Name == name && method.ReturnType == typeof(void)) patches.Add(method, prefix: Hook("DrawScenery"));
            }
        }
        internal static bool IsScenery(Type type)
        {
            if (!typeof(Entity).IsAssignableFrom(type)) return false;
            string n = type.FullName;
            return n.StartsWith("JumpKing.Props.", StringComparison.Ordinal)
                || n == "JumpKing.MiscEntities.OldManEntity" || n == "JumpKing.MiscEntities.RavenEntity"
                || n.StartsWith("JumpKing.MiscEntities.Merchant.", StringComparison.Ordinal)
                || n.StartsWith("JumpKing.MiscEntities.DisplayTextEntity.", StringComparison.Ordinal)
                || n == "JumpKing.MiscSystems.ScreenEvents.FlyingGargoyle"
                || (n.StartsWith("JumpKing.GameManager.MultiEnding.", StringComparison.Ordinal) && !n.Contains("KingEntity"));
        }
        private static bool DrawScenery() { return !ModEntry.HideProps; }
        private static bool MusicPlay(IJKSound p_sound)
        { var w = ModEntry.Current; if (w == null) return true; w.Music.Requested(p_sound); return false; }
        private static void AfterUpdate(GameTime __0)
        { var w = ModEntry.Current; if (w != null && !JKRuntime.Gameplay.NativePause.IsPaused) w.Wind.Advance(__0.ElapsedGameTime.TotalSeconds); }
        private static void Configure(World w, LevelScreen screen, ScreenArt art)
        {
            var p = ModEntry.Prefs;
            w.Renderer.Offset = Camera.Offset; w.Renderer.GlowStrength = p.Glow / 100f;
            float velocity = screen.WindEndabled ? WindManager.CurrentVelocityRaw : 0;
            w.Renderer.Wind = w.Wind.Sample(screen.GetIndex0(), velocity);
            w.Renderer.WindVisibility = p.WindVisibility / 100f;
        }
        private static bool BeforeDraw(LevelScreen __instance)
        {
            var w = ModEntry.Current; if (w == null) return true;
            var art = w.Art(__instance); if (art == null || !art.Supported || ModEntry.Prefs.OverlayOnly) return true;
            Configure(w, __instance, art);
            w.Renderer.Background(w.Music.Time, __instance.GetIndex0(), art, ModEntry.Prefs.Gentle); return false;
        }
        private static void AfterDraw(LevelScreen __instance)
        {
            var w = ModEntry.Current; if (w == null) return;
            var art = w.Art(__instance); if (art == null) return;
            Configure(w, __instance, art);
            if (ModEntry.Prefs.OverlayOnly || !art.Supported) w.Renderer.Overlay(w.Music.Time, art, ModEntry.Prefs.Gentle);
        }
        private static bool Foreground(LevelScreen __instance)
        {
            var w = ModEntry.Current; if (w == null) return true;
            var art = w.Art(__instance); if (art == null || !art.Supported || ModEntry.Prefs.OverlayOnly) return true;
            Configure(w, __instance, art);
            w.Renderer.Foreground(w.Music.Time, art, ModEntry.Prefs.Reflections, ModEntry.Prefs.Gentle); return false;
        }
    }
}
