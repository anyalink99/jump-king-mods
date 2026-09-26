using System;
using System.Reflection;
using JKRuntime;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion
{
    internal static class NativeHooks
    {
        private const string HarmonyId = "mega-mapping-expansion.render";
        private static readonly FieldInfo RenderTarget = typeof(Game1).GetField("m_render_target", BindingFlags.Instance | BindingFlags.NonPublic);
        private static bool installed;
        private static bool saveInstalled;
        private static OwnedPatches renderPatches, savePatches;
        private static MethodInfo Callback(string name) { return typeof(NativeHooks).GetMethod(name, OwnedPatches.Members); }
        private const string SaveHarmonyId = HarmonyId + ".save";
        private static SceneHost updatedThisFrame;
        private static SceneHost ActiveHost { get { return MappingSettings.Enabled ? SceneHost.Current : null; } }
        internal static void ConfigureForScene(bool hasScene)
        { InstallSaveHooks(); if (hasScene) Install(); else UninstallRendering(); }

        internal static void Install()
        {
            InstallSaveHooks();
            if (installed) return;
            AssertContracts();
            if (renderPatches != null) { renderPatches.Dispose(); renderPatches = null; }
            renderPatches = new OwnedPatches(HarmonyId);
            var harmony = renderPatches;
            try
            {
            harmony.Add(typeof(LevelScreen).GetMethod("DrawMG", BindingFlags.Instance | BindingFlags.NonPublic),
                prefix: Callback("BeforeScreenMidground"));
            harmony.Add(typeof(LevelScreen).GetMethod("Draw"), postfix: Callback("AfterScreenDraw"));
            harmony.Add(typeof(LevelScreen).GetMethod("DrawForeground"), postfix: Callback("AfterScreenForeground"));
            harmony.Add(typeof(PlayerEntity).GetMethod("Draw"),
                prefix: Callback("BeforePlayerDraw"));
            harmony.Add(typeof(GameLoop).GetMethod("DrawIngameOverlayItems"), prefix: Callback("BeforeOverlay"));
            harmony.Add(typeof(GameLoop).GetMethod("DrawIngameOverlayItems"), postfix: Callback("AfterOverlay"));
            harmony.Add(typeof(LanguageManager).GetProperty("LEGEND_HAS_IT").GetGetMethod(), postfix: Callback("AfterIntroText"));
            harmony.Add(typeof(Game1).GetMethod("DrawRenderTarget", BindingFlags.Instance | BindingFlags.NonPublic),
                prefix: Callback("BeforeFinalBlit"));
            harmony.Add(typeof(Game1).GetMethod("Draw", BindingFlags.Instance | BindingFlags.NonPublic),
                prefix: Callback("BeginFrameProfile"),postfix:Callback("EndFrameProfile"));
            harmony.Add(typeof(Game).GetMethod("EndDraw",BindingFlags.Instance|BindingFlags.NonPublic),
                prefix:Callback("BeginFrameProfile"),postfix:Callback("EndPresentProfile"));
            harmony.Add(typeof(Game1).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic),
                prefix:Callback("BeginFrameProfile"),postfix:Callback("EndUpdateProfile"));
            harmony.Add(typeof(EntityComponent.EntityManager).GetMethod("Update"),
                postfix: Callback("AfterEntities"));
            harmony.Add(typeof(JumpGame).GetMethod("Update", new[] { typeof(GameTime) }),
                prefix: Callback("BeforeSceneFrame"), postfix: Callback("AfterSceneFrame"));
            installed = true;
            }
            catch (Exception failure) { try { UninstallRendering(); } catch (Exception cleanup) { throw new AggregateException("Scene hook rollback failed", failure, cleanup); } throw; }
        }

        internal static void Uninstall()
        {
            var release = new RuntimeScope(); release.Defer(UninstallRendering); release.Defer(UninstallSave); release.Dispose();
        }
        private static void UninstallRendering()
        { if (renderPatches != null) { renderPatches.Dispose(); renderPatches = null; } installed = false; }
        private static void UninstallSave()
        { if (savePatches != null) { savePatches.Dispose(); savePatches = null; } saveInstalled = false; }
        private static void InstallSaveHooks()
        {
            if (saveInstalled) return;
            if (savePatches != null) { savePatches.Dispose(); savePatches = null; }
            savePatches = new OwnedPatches(SaveHarmonyId);
            var harmony = savePatches;
            try
            {
                var saveType = typeof(Game1).Assembly.GetType("JumpKing.SaveThread.SaveLube", true);
                harmony.Add(saveType.GetMethod("SaveCombinedSaveFile", BindingFlags.Public | BindingFlags.Static),
                    postfix: Callback("AfterNativeSave"));
                harmony.Add(saveType.GetMethod("DeleteSaves", BindingFlags.Public | BindingFlags.Static),
                    postfix: Callback("AfterNativeSaveReset"));
                saveInstalled = true;
            }
            catch (Exception failure) { try { UninstallSave(); } catch (Exception cleanup) { throw new AggregateException("Save hook rollback failed", failure, cleanup); } throw; }
        }

        internal static void AssertContracts()
        {
            NativeMapLayout.AssertContracts();
            NativeSceneAdapter.AssertContracts();
            var update = typeof(EntityComponent.EntityManager).GetMethod("Update", new[] { typeof(float) });
            Type saveType = typeof(Game1).Assembly.GetType("JumpKing.SaveThread.SaveLube", true);
            foreach (string name in new[] { "SaveCombinedSaveFile", "DeleteSaves" })
            { var method = saveType.GetMethod(name, BindingFlags.Public | BindingFlags.Static); if (method == null || method.ReturnType != typeof(void) || method.GetParameters().Length != 0) throw new NotSupportedException("Unsupported native save boundary: " + name); }
            if (update == null || update.ReturnType != typeof(void)) throw new NotSupportedException("Unsupported native entity update boundary");
            if (typeof(JumpGame).GetMethod("Update", new[] { typeof(GameTime) }) == null) throw new NotSupportedException("Unsupported paused scene update boundary");
            if (RenderTarget == null || RenderTarget.FieldType != typeof(RenderTarget2D)
                || typeof(Game1).GetMethod("DrawRenderTarget", BindingFlags.Instance | BindingFlags.NonPublic) == null
                || typeof(Game1).GetMethod("Draw", BindingFlags.Instance | BindingFlags.NonPublic) == null
                || typeof(Game).GetMethod("EndDraw", BindingFlags.Instance | BindingFlags.NonPublic) == null
                || typeof(Game1).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic) == null
                || typeof(LevelScreen).GetMethod("DrawMG", BindingFlags.Instance | BindingFlags.NonPublic) == null
                || typeof(PlayerEntity).GetMethod("Draw") == null
                || typeof(GameLoop).GetMethod("DrawIngameOverlayItems") == null
                || typeof(LanguageManager).GetProperty("LEGEND_HAS_IT") == null)
                throw new InvalidOperationException("Installed Jump King render/text contract is incompatible with Mega Mapping Expansion");
            foreach (string method in new[] { "Draw", "DrawForeground" })
                if (typeof(LevelScreen).GetMethod(method, Type.EmptyTypes) == null)
                    throw new InvalidOperationException("Missing LevelScreen." + method + "()");
        }

        private static void BeforeScreenMidground()
        { SceneHost host = ActiveHost; if (host != null) host.DrawBackground(); }
        private static void BeginFrameProfile(out long __state){__state=ActiveHost != null && ActiveHost.Options.Profiling ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;}
        private static void EndFrameProfile(long __state){if(__state != 0 && ActiveHost!=null)ActiveHost.ProfileStage(0,System.Diagnostics.Stopwatch.GetTimestamp()-__state);}
        private static void EndUpdateProfile(long __state){if(__state != 0 && ActiveHost!=null)ActiveHost.ProfileStage(1,System.Diagnostics.Stopwatch.GetTimestamp()-__state);}
        private static void AfterEntities(EntityComponent.EntityManager __instance, float p_delta)
        { if (ActiveHost != null && ReferenceEquals(__instance, ActiveHost.EntityOwner)) { updatedThisFrame = ActiveHost; ActiveHost.Tick(p_delta); } }
        private static void BeforeSceneFrame() { updatedThisFrame = null; }
        private static void AfterSceneFrame()
        {
            SceneHost current = ActiveHost;
            // JumpGame skips EntityManager entirely during native pause. Its clock is fixed 60 Hz.
            if (current != null && !ReferenceEquals(current, updatedThisFrame) && JKRuntime.Gameplay.NativePause.IsPaused) current.Tick(1f / 60);
        }
        private static void AfterNativeSave() { SceneSaveCoordinator.Save(); }
        private static void AfterNativeSaveReset() { SceneSaveCoordinator.Reset(); }
        private static void EndPresentProfile(long __state)
        {if(__state != 0 && ActiveHost!=null){ActiveHost.ProfileStage(2,System.Diagnostics.Stopwatch.GetTimestamp()-__state);ActiveHost.RecordFrameCost();}}
        private static void AfterScreenDraw()
        { SceneHost host = ActiveHost; if (host != null) host.DrawWorld(); }
        private static void AfterScreenForeground()
        {
            SceneHost host = ActiveHost;
            if (host == null) return;
            host.DrawForeground();
            if (!JKRuntime.FrameComposition.InScreenPass) host.ComposeScenePass((RenderTarget2D)RenderTarget.GetValue(Game1.instance));
        }
        private static void BeforePlayerDraw(PlayerEntity __instance)
        { SceneHost host = ActiveHost; if (host != null) { host.DrawSurfaceShadows(__instance); host.DrawPlayerRim(__instance); } }
        private static bool BeforeOverlay()
        {
            if (!MappingSettings.Enabled) return true;
            SceneOptions options = MappingState.Options;
            return options == null || !string.Equals(options.Timer, "hidden", StringComparison.OrdinalIgnoreCase);
        }
        private static void AfterOverlay() { SceneHost host = ActiveHost; if (host != null) host.DrawScreenTexts(); }
        private static void AfterIntroText(ref string __result)
        {
            if (!MappingSettings.Enabled) return;
            SceneOptions options = MappingState.Options;
            if (options != null && !string.IsNullOrWhiteSpace(options.IntroText))
                __result = options.IntroText.Replace("<newline>", "\n");
        }

        private static bool BeforeFinalBlit(Game1 __instance)
        {
            if (JKRuntime.FrameComposition.HasExternalPresentation) return true;
            if (!MappingSettings.Enabled) return true;
            SceneHost host = ActiveHost;
            if (host != null) host.CapturePreview((RenderTarget2D)RenderTarget.GetValue(__instance));
            SceneOptions options = MappingState.Options;
            if (options == null) return true;
            string mirror = options.Mirror ?? "none";
            bool horizontal = string.Equals(mirror, "horizontal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mirror, "both", StringComparison.OrdinalIgnoreCase);
            bool vertical = string.Equals(mirror, "vertical", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mirror, "both", StringComparison.OrdinalIgnoreCase);
            bool tint = options.TintOpacity > 0f;
            if (!horizontal && !vertical && !tint) return true;

            Rectangle destination = __instance.GetGameRect();
            destination.Location += JumpGame.screenShakeManager.GetOffset();
            SpriteEffects effects = (horizontal ? SpriteEffects.FlipHorizontally : SpriteEffects.None)
                | (vertical ? SpriteEffects.FlipVertically : SpriteEffects.None);
            Color color = tint
                ? Color.Lerp(Color.White, MappingState.FrameTint, options.TintOpacity)
                : Color.White;
            __instance.StartBatch();
            Game1.spriteBatch.Draw((RenderTarget2D)RenderTarget.GetValue(__instance), destination, null, color, 0f,
                Vector2.Zero, effects, 0f);
            __instance.EndBatch();
            return false;
        }
    }
}
