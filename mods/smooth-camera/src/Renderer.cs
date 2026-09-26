using System;
using System.Reflection;
using EntityComponent;
using HarmonyLib;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.Level;
using JumpKing.Util.Tags;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SmoothCamera
{
    internal static class Renderer
    {
        internal static readonly FieldInfo Screens = typeof(LevelManager).GetField("m_screens", BindingFlags.Static | BindingFlags.NonPublic);
        internal static readonly CameraMotion Motion = new CameraMotion();
        internal static readonly HorizontalMotion Horizontal = new HorizontalMotion();
        internal static readonly CameraView View = new CameraView();
        private static readonly ScreenCompositor compositor = new ScreenCompositor();
        private static bool running, frameActive, worldPending;
        private static bool deferredFrame, canPresent, paused;
        private static float drawDelta;
        private static long previousDrawStamp;
        private static int revision;
        private static long nextMappingScan;
        internal static int CompatibilityScans { get; private set; }
        internal static bool CanPresent { get { return canPresent; } }
        internal static bool Running { get { return running; } }
        private static Func<bool> mappingEnabled;
        private static bool legacyMapping;
        private static readonly MethodInfo ScreenForeground = typeof(LevelScreen).GetMethod("DrawForeground");
        private static readonly FieldInfo NativeTarget = typeof(Game1).GetField("m_render_target", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Action<Game1> CalculateRect = (Action<Game1>)Delegate.CreateDelegate(typeof(Action<Game1>),
            typeof(Game1).GetMethod("CalculateGameRect", BindingFlags.Instance | BindingFlags.NonPublic));
        internal static bool Blocked { get; private set; }
        internal static bool HighRefreshRequested { get { return running && Settings.Current.Smooth && Settings.Current.HighRefresh && !Blocked; } }

        internal static void Start() { PresentationClock.Ensure(); running = true; Blocked = false; InvalidateCompatibility(); previousDrawStamp = 0; revision++; Motion.Reset(); Horizontal.Reset(); View.Reset(); CameraControls.Gestures.Reset(); }
        internal static void InvalidateCompatibility() { nextMappingScan = 0; PortalOpenings.Reset(); }
        internal static void HorizontalChanged() { View.Rebase(-Horizontal.Translation, 0); Horizontal.Reset(); canPresent = false; revision++; }
        internal static void SettingsChanged()
        { Motion.Reset(); Horizontal.Reset(); View.Reset(); CameraControls.Gestures.Reset(); InvalidateCompatibility(); previousDrawStamp = 0; canPresent = false; revision++; }
        internal static void RefreshCompatibility(long now)
        {
            if (now < nextMappingScan) return;
            CacheMapping(Harmony.GetPatchInfo(ScreenForeground)); CompatibilityScans++;
            nextMappingScan = now + System.Diagnostics.Stopwatch.Frequency;
        }
        internal static void Stop() { running = frameActive = worldPending = Blocked = canPresent = false; Motion.Reset(); Horizontal.Reset(); View.Reset(); CameraControls.Gestures.Reset(); PortalOpenings.Reset(); compositor.Dispose(); }
        internal static void AfterUpdate(JumpGame __instance = null)
        {
            revision++;
            PortalOpenings.Reset();
            if (!running || !Settings.Current.Smooth || GameLoop.m_player == null) return;
            paused = JKRuntime.Gameplay.NativePause.IsPaused;
            CameraControls.Update(!paused && !Blocked && (__instance == null || __instance.IsPlaying()));
            ObservePlayer();
        }

        private static void ObservePlayer()
        {
            var screens = (LevelScreen[])Screens.GetValue(null);
            int current = (int)RenderContext.NativeScreen.GetValue(null), anchor = current;
            float influence = 0;
            if (Settings.Current.Horizontal)
                FindPortalNeighborhood(screens, current, CameraControls.Gestures.Current == CameraMode.Normal ? Motion.Translation : View.Y, out anchor, out influence);
            float oldX = Horizontal.Translation;
            float rebase = Horizontal.Observe(GameLoop.m_player.m_body.GetHitbox().Center.X, current, screens,
                Settings.Current.Horizontal, paused, anchor, influence);
            Motion.Rebase(rebase);
            View.Rebase(Horizontal.Translation - oldX, rebase);
            Motion.Observe(PlayerCenter(), LevelManager.TotalScreens, paused, GameLoop.m_player.m_body.Velocity.Y * 60f);
        }

        internal static void FindPortalNeighborhood(LevelScreen[] screens, int current, float translation, out int anchor, out float influence)
        {
            anchor = current; influence = 0;
            if (screens == null) return;
            int bottom = Math.Max(0, (int)Math.Floor(translation / 360f));
            for (int row = bottom; row <= bottom + 1 && row < screens.Length; row++)
            {
                if (PortalOpenings.Destination(screens, row, true) < 0 && PortalOpenings.Destination(screens, row, false) < 0) continue;
                float top = translation - row * 360;
                float overlap = Math.Max(0, Math.Min(360, top + 360) - Math.Max(0, top)) / 360;
                if (overlap > influence) { anchor = row; influence = overlap; }
            }
        }

        private static float PlayerCenter()
        { return GameLoop.m_player.m_body.Position.Y + GameLoop.m_player.m_body.GetHitbox().Height * .5f; }

        internal static void BeforeGameDraw()
        {
            // Predicted actors must be redrawn into the world atlas at each
            // presentation position, preserving native foreground occlusion.
            if (ModEntry.ExternalInterpolation) revision++;
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            drawDelta = previousDrawStamp == 0 ? 0 : (float)((now - previousDrawStamp) / (double)System.Diagnostics.Stopwatch.Frequency);
            previousDrawStamp = now;
            deferredFrame = true; canPresent = false;
        }

        internal static void BeginFrame()
        {
            frameActive = worldPending = false;
            if (!running || !Settings.Current.Smooth || GameLoop.m_player == null) return;
            // This renderer redirects the game's native render target from its own
            // hooks. Combining that compositor with ours needs an explicit adapter.
            // Cache reflection/patch deserialization across simulation ticks.
            // Read the bound property every frame so known toggles stay immediate;
            // rescan once per second for independently installed/removed hooks.
            RefreshCompatibility(System.Diagnostics.Stopwatch.GetTimestamp());
            bool conflict = legacyMapping;
            try { if (mappingEnabled != null) conflict = mappingEnabled(); } catch { conflict = true; }
            if (conflict && !Blocked) Console.WriteLine("[Smooth Camera] Using native screens: Mega Mapping Expansion compositor is active.");
            Blocked = conflict;
            if (Blocked) return;
            ObservePlayer();
            Motion.Advance(drawDelta);
            Horizontal.Advance(drawDelta);
            View.Advance(CameraControls.Gestures.Current, Horizontal.Translation, Motion.Translation, PlayerCenter(),
                GameLoop.m_player.m_body.Velocity.Y * 60f, (int)RenderContext.NativeScreen.GetValue(null), LevelManager.TotalScreens, drawDelta, paused);
            frameActive = LevelManager.TotalScreens > 0;
        }

        private static void CacheMapping(Patches patches)
        {
            mappingEnabled = null; legacyMapping = false;
            if (patches == null) return;
            foreach (var patch in patches.Postfixes)
            {
                if (patch.owner != "mega-mapping-expansion.render") continue;
                var entry = patch.PatchMethod.DeclaringType.Assembly.GetType("MegaMappingExpansion.ModEntry");
                var adapter = entry == null ? null : entry.GetProperty("SupportsCameraComposition");
                if (adapter != null && adapter.PropertyType == typeof(bool) && (bool)adapter.GetValue(null, null)) continue;
                var enabled = entry == null ? null : entry.GetProperty("IsCompositorActive", BindingFlags.Public | BindingFlags.Static)
                    ?? entry.GetProperty("IsPresentationEnabled", BindingFlags.Public | BindingFlags.Static);
                if (enabled == null || enabled.PropertyType != typeof(bool)) { legacyMapping = true; return; }
                mappingEnabled = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), enabled.GetGetMethod());
            }
        }

        internal static bool MappingConflict(Patches patches)
        {
            if (patches == null) return false;
            foreach (var patch in patches.Postfixes)
            {
                if (patch.owner != "mega-mapping-expansion.render") continue;
                // Optional discovery contract: older Mapping releases do not expose
                // a switch and must continue to block the competing compositor.
                var entry = patch.PatchMethod.DeclaringType.Assembly.GetType("MegaMappingExpansion.ModEntry");
                var adapter = entry == null ? null : entry.GetProperty("SupportsCameraComposition");
                if (adapter != null && adapter.PropertyType == typeof(bool) && (bool)adapter.GetValue(null, null)) continue;
                var enabled = entry == null ? null : entry.GetProperty("IsCompositorActive", BindingFlags.Public | BindingFlags.Static)
                    ?? entry.GetProperty("IsPresentationEnabled", BindingFlags.Public | BindingFlags.Static);
                if (enabled == null || enabled.PropertyType != typeof(bool)) return true;
                try { if ((bool)enabled.GetValue(null, null)) return true; }
                catch { return true; }
            }
            return false;
        }
        internal static void EndFrame(Exception __exception)
        { frameActive = worldPending = false; deferredFrame = false; drawDelta = 0; if (__exception != null) canPresent = false; }

        internal static bool Present(Game1 __instance)
        {
            if (!canPresent) return true;
            CalculateRect(__instance);
            Rectangle destination = __instance.GetGameRect();
            if (JumpGame.screenShakeManager != null) destination.Location += JumpGame.screenShakeManager.GetOffset();
            var overlay = (RenderTarget2D)NativeTarget.GetValue(__instance);
            if (JKRuntime.FrameComposition.CaptureRequested) compositor.Capture(overlay, View.Y, View.X);
            compositor.Present(destination, overlay, View.Y, View.X, JKRuntime.FrameComposition.Style);
            return false;
        }

        internal static void Background(LevelScreen screen)
        { if (frameActive) worldPending = true; else screen.Draw(); }
        internal static void Foreground(LevelScreen screen)
        { if (!frameActive) screen.DrawForeground(); }
        internal static void Entities(EntityManager manager)
        {
            if (!frameActive || !worldPending) { manager.Draw(); return; }
            worldPending = false;
            var screens = (LevelScreen[])Screens.GetValue(null);
            compositor.Draw(View.Y, screens.Length, delegate(int index)
            {
                screens[index].Draw();
                manager.Draw();
                screens[index].DrawForeground();
                if (JKRuntime.FrameComposition.InScreenPass) JKRuntime.FrameComposition.ComposeScreen();
                foreach (var entity in manager.Entities)
                {
                    var foreground = entity as IForeground;
                    if (foreground != null && IsWorldForeground(foreground)) foreground.ForegroundDraw();
                }
            }, deferredFrame, revision, View.X, Horizontal.Left, Horizontal.Right, Horizontal.Anchor);
            canPresent = deferredFrame;
            foreach (var entity in manager.Entities)
                if (IsViewportEntity(entity)) entity.Draw();
        }

        internal static bool IsViewportEntity(Entity entity)
        {
            string name = entity.GetType().FullName;
            return name == "JumpKing.PauseMenu.PauseManager"
                || name == "JumpKing.GameManager.FadeTextEntity"
                || name.StartsWith("JumpKing.MiscSystems.LocationText.", StringComparison.Ordinal);
        }
        internal static void EntityDraw(Entity entity)
        {
            if (RenderContext.Active && IsViewportEntity(entity)) return;
            if (RenderContext.Active && RenderContext.Column != 0 && ReferenceEquals(entity, GameLoop.m_player))
            {
                int column = RenderContext.Column;
                using (new RenderContext(RenderContext.BaseScreen))
                { RenderContext.HorizontalCorrection = -column * 480; entity.Draw(); }
            }
            else entity.Draw();
        }

        internal static bool IsWorldForeground(IForeground item)
        {
            string name = item.GetType().FullName;
            return name == "JumpKing.MiscEntities.OldManEntity"
                || name == "JumpKing.MiscEntities.Merchant.MerchantEntity"
                || name == "JumpKing.Props.RattmanText.RattmanEntity"
                || name == "JumpKing.MiscSystems.ScreenEvents.FlyingGargoyle";
        }

        internal static void Overlay(IForeground item)
        {
            // Pause menus, location labels, fades and lightning retain their native
            // viewport coordinates and ordering outside the screen passes.
            if (frameActive && IsWorldForeground(item)) return;
            item.ForegroundDraw();
        }
    }

}
