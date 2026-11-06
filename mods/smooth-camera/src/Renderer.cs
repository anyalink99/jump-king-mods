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
using JKRuntime.Presentation;

namespace SmoothCamera
{
    internal static class Renderer
    {
        internal static readonly FieldInfo Screens = typeof(LevelManager).GetField("m_screens", BindingFlags.Static | BindingFlags.NonPublic);
        internal static readonly CameraSession Session = new CameraSession();
        internal static readonly CameraMotion Motion = Session.Rig.Vertical;
        internal static readonly HorizontalMotion Horizontal = Session.Rig.Horizontal;
        internal static readonly CameraView View = Session.Rig.View;
        private static readonly ScriptedView scripted = Session.Rig.Shots;
        private static readonly SubjectTimeline timeline = new SubjectTimeline();
        internal static Vector2 SubjectDrawOffset;
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
        private static readonly Action<Game1> NativeBlit=(Action<Game1>)Delegate.CreateDelegate(typeof(Action<Game1>),
            typeof(Game1).GetMethod("DrawRenderTarget",BindingFlags.Instance|BindingFlags.NonPublic));
        private static readonly Action<Game1> CalculateRect = (Action<Game1>)Delegate.CreateDelegate(typeof(Action<Game1>),
            typeof(Game1).GetMethod("CalculateGameRect", BindingFlags.Instance | BindingFlags.NonPublic));
        internal static bool Blocked { get; private set; }
        private static bool hasDecision;
        internal static bool TargetOverridden { get; private set; }
        private static Vector2 targetCenter, targetVelocity;
        private static int targetScreen;
        private static long targetRevision, targetDiscontinuity;
        internal static CameraDecision Decision;
        internal static bool HighRefreshRequested { get { return running && Settings.Current.HighRefresh && !Blocked &&
            (hasDecision ? Decision.Active : Settings.Current.Smooth && !MapPolicy.Current.ExplicitOnly && !MapPolicy.Current.DisableOnEnter); } }
        internal static string Status { get { return Blocked ? "Incompatible renderer" : hasDecision ? Decision.Reason : "No active attempt"; } }
        internal static string DebugStatus {get{return "enabled="+Settings.Current.Smooth+" hooks="+Hooks.Installed+" running="+running
            +" active="+(hasDecision && Decision.Active)+" present="+canPresent+" reason="+Status+" screen="+targetScreen+" view="+View.X+","+View.Y
            +" mode="+CameraControls.Gestures.Current+" paused="+paused+" target="+targetCenter;}}

        internal static void Start() { Session.Reset(); timeline.Reset(); scripted.Reset(); PresentationClock.Ensure(); running = true; hasDecision = false; Blocked = false; InvalidateCompatibility(); previousDrawStamp = 0; revision++; Motion.Reset(); Horizontal.Reset(); View.Reset(); DebugDrag.Reset(); CameraControls.Gestures.Reset(); }
        internal static void InvalidateCompatibility() { nextMappingScan = 0; JKRuntime.Geometry.MapTopology.Invalidate(); }
        internal static void HorizontalChanged() { View.Rebase(-Horizontal.Translation, 0); Horizontal.Reset(); canPresent = false; revision++; }
        internal static void SettingsChanged()
        { hasDecision = false; Session.InvalidatePolicy(); DebugDrag.Reset(); CameraControls.Gestures.Reset(); InvalidateCompatibility(); canPresent = false; revision++; }
        internal static void RefreshCompatibility(long now)
        {
            if (now < nextMappingScan) return;
            CacheMapping(Harmony.GetPatchInfo(ScreenForeground)); CompatibilityScans++;
            nextMappingScan = now + System.Diagnostics.Stopwatch.Frequency;
        }
        internal static void Stop() { Session.Reset(); timeline.Reset(); scripted.Reset(); hasDecision = running = frameActive = worldPending = Blocked = canPresent = TargetOverridden = false; Motion.Reset(); Horizontal.Reset(); View.Reset(); DebugDrag.Reset(); CameraControls.Gestures.Reset(); JKRuntime.Geometry.MapTopology.Invalidate(); compositor.Dispose(); }
        internal static void AfterUpdate(JumpGame __instance = null)
        {
            revision++;
            JKRuntime.Geometry.MapTopology.Invalidate();
            if (!running || GameLoop.m_player == null) return;
            ObservePlayer(true);
            DebugDrag.AfterUpdate();
            CameraControls.Update(Decision.Active && !TargetOverridden && !paused && !Blocked && (__instance == null || __instance.IsPlaying()));
        }

        private static void ObservePlayer(bool resolvePolicy = false)
        {
            var screens = (LevelScreen[])Screens.GetValue(null);
            var body = GameLoop.m_player.m_body;
            var bounds = body.GetHitbox();
            CameraTarget target;
            bool overridden = CameraTargets.TryRead(out target) && target.Screen < LevelManager.TotalScreens;
            bool cut = overridden != TargetOverridden || overridden && target.Discontinuity != targetDiscontinuity;
            bool changed = overridden && target.Revision != targetRevision;
            TargetOverridden = overridden;
            Vector2 native = body.Position + new Vector2(bounds.Width * .5f, bounds.Height * .5f);
            targetCenter = overridden ? target.Center : native;
            targetVelocity = overridden ? target.Velocity : body.Velocity * 60;
            targetScreen = overridden ? target.Screen : (int)RenderContext.NativeScreen.GetValue(null);
            paused = overridden ? target.Paused : JKRuntime.Gameplay.NativePause.IsPaused;
            targetRevision = target.Revision; targetDiscontinuity = target.Discontinuity;
            if (changed || cut) revision++;
            if (overridden) DebugDrag.Reset();
            timeline.Observe(new SubjectSample { Center = targetCenter, Velocity = targetVelocity, Screen = targetScreen, Paused = paused, Cut = cut }, screens, resolvePolicy);
            var sample = timeline.Sample(resolvePolicy ? 0 : drawDelta, Settings.Current.HighRefresh && !ModEntry.ExternalInterpolation && !DebugDrag.State.Active,
                PresentationClock.Active ? JKRuntime.PresentationScheduling.Alpha : float.NaN);
            Vector2 drawn = SubjectSource.Read(body) + new Vector2(bounds.Width * .5f, bounds.Height * .5f);
            if (!overridden && ModEntry.ExternalInterpolation) sample.Center = drawn;
            SubjectDrawOffset = overridden ? Vector2.Zero : sample.Center - drawn;
            targetCenter = sample.Center;
            Session.Observe(sample, screens, Settings.Current, MapPolicy.Current, resolvePolicy || !hasDecision);
            Decision = Session.Decision; hasDecision = true;
            CameraControls.Gestures.SetPermissions(Decision.AllowFocus, Decision.AllowLook);
            if (Decision.Profile != null) CameraControls.Gestures.HoldThreshold = Decision.Profile.HoldMilliseconds / 1000.0;
        }

        internal static void FindPortalNeighborhood(LevelScreen[] screens, int current, float translation, out int anchor, out float influence, float y = float.NaN)
        {
            var scene = new CameraScene { Screens = screens, Settings = Settings.Current, Rules = MapPolicy.Current };
            scene.Neighborhood(current, translation, y, out anchor, out influence);
        }

        private static float PlayerCenter()
        { return targetCenter.Y; }

        internal static void BeforeGameDraw()
        {
            DebugDrag.ObserveWindow();
            // SFC owns its draw position. refresh the screen captures for that pose
            if (ModEntry.ExternalInterpolation) revision++;
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            drawDelta = previousDrawStamp == 0 ? 0 : (float)((now - previousDrawStamp) / (double)System.Diagnostics.Stopwatch.Frequency);
            previousDrawStamp = now;
            deferredFrame = true; canPresent = false;
        }

        internal static void BeginFrame()
        {
            frameActive = worldPending = false;
            if (!running || GameLoop.m_player == null) return;
            // this renderer redirects the game's native render target from its own
            // hooks. Combining that compositor with ours needs an explicit adapter
            // Cache reflection/patch deserialization across simulation ticks
            // read the bound property every frame so known toggles stay immediate;
            // rescan once per second for independently installed/removed hooks
            RefreshCompatibility(System.Diagnostics.Stopwatch.GetTimestamp());
            bool conflict = legacyMapping;
            try { if (mappingEnabled != null) conflict = mappingEnabled(); } catch { conflict = true; }
            if (conflict && !Blocked) Console.WriteLine("[Smooth Camera] Using native screens: Mega Mapping Expansion compositor is active.");
            Blocked = conflict;
            if (Blocked) return;
            float previousY = View.Y;
            var previousView=new Vector2(View.X,View.Y);
            ObservePlayer();
            CameraShot shot=default(CameraShot);bool hasShot=Settings.Current.Smooth&&CameraShots.TryRead(out shot);
            if(!Settings.Current.Smooth||TargetOverridden)scripted.Reset();
            if (!Decision.Active&&!hasShot&&!scripted.Active) { DebugDrag.Reset(); return; }
            Session.Advance(drawDelta, CameraControls.Gestures.Current);
            if (!TargetOverridden && DebugDrag.Frame(previousY)) Session.BuildPlan();
            // script coordinates are explicit framing, independent of the player's follow window
            if(scripted.Advance(hasShot?(CameraShot?)shot:null,new Vector2(View.X,View.Y),previousView,drawDelta,
                paused||JKRuntime.UI.UIApi.IsOpen,ConnectedScreens,targetScreen))
                View.Place(scripted.X,scripted.Y,PlayerCenter());
            Session.BuildPlan();
            frameActive = LevelManager.TotalScreens > 0 && Session.Images.Length > 0;
        }
        private static bool ConnectedScreens(int from,int to)
        {
            int first,last;var screens=(LevelScreen[])Screens.GetValue(null);
            JKRuntime.Geometry.MapTopology.VerticalRange(screens,from,out first,out last);return to>=first&&to<=last;
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
                // optional discovery contract, older Mapping has no switch
                // it still needs to block the competing compositor
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

        internal static Vector2 ProjectWorld(Vector2 point)
        {
            var screens = (LevelScreen[])Screens.GetValue(null);
            if (screens != null && screens.Length > 0 && (Session.Images.Length == 0 || !ReferenceEquals(Session.Scene.Screens, screens)))
            {
                Session.Scene.Screens = screens; Session.Scene.Settings = Settings.Current; Session.Scene.Rules = MapPolicy.Current;
                Session.Scene.Plan(JKRuntime.Geometry.MapTopology.ScreenAt(targetCenter.Y, screens.Length), new Vector2(View.X, View.Y), Horizontal.Anchor, Horizontal.Left, Horizontal.Right);
            }
            return Session.Scene.Project(point, new Vector2(View.X, View.Y)) + Camera.Offset;
        }
        // late activation mustn't reuse a final blit inlined before the camera hooks existed
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static void DrawTarget(Game1 game) {NativeBlit(game);}
        internal static bool Present(Game1 __instance)
        {
            if (!canPresent) return true;
            CalculateRect(__instance);
            Rectangle destination = __instance.GetGameRect();
            if (JumpGame.screenShakeManager != null) destination.Location += JumpGame.screenShakeManager.GetOffset();
            var overlay = (RenderTarget2D)NativeTarget.GetValue(__instance);
            if (JKRuntime.FrameComposition.CaptureRequested) compositor.Capture(overlay, View.Y, View.X);
            compositor.Present(destination, overlay, View.Y, View.X, JKRuntime.FrameComposition.Style);
            DebugDrag.Present(destination);
            return false;
        }

        internal static void Background(LevelScreen screen)
        { if (frameActive) worldPending = true; else screen.Draw(); }
        internal static void Foreground(LevelScreen screen)
        { if (!frameActive) screen.DrawForeground(); }
        internal static void Entities(EntityManager manager)
        {
            if (!frameActive || !worldPending)
            {
                manager.Draw();
                if (!frameActive && running && hasDecision && Settings.Current.Diagnostics && GameLoop.m_player != null)
                    CameraDiagram.DrawDiagnostics(Decision, 0, targetScreen * 360, PlayerCenter(), targetCenter.X);
                return;
            }
            worldPending = false;
            var screens = (LevelScreen[])Screens.GetValue(null);
            compositor.DrawPlan(Session.Images, new Vector2(View.X, View.Y), delegate(int index)
            {
                screens[index].Draw();
                manager.Draw();
                JKRuntime.FrameComposition.DrawWorld();
                screens[index].DrawForeground();
                if (JKRuntime.FrameComposition.InScreenPass) JKRuntime.FrameComposition.ComposeScreen();
                foreach (var entity in manager.Entities)
                {
                    var foreground = entity as IForeground;
                    if (foreground != null && IsWorldForeground(foreground)) foreground.ForegroundDraw();
                }
            }, deferredFrame, revision, true, scripted.Active ? scripted.Screen : targetScreen);
            canPresent = deferredFrame;
            foreach (var entity in manager.Entities)
                if (IsViewportEntity(entity)) entity.Draw();
            if (Settings.Current.Diagnostics) CameraDiagram.DrawDiagnostics(Decision, View.X, View.Y, PlayerCenter(), targetCenter.X);
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
            if (RenderContext.Active && RenderContext.Planned && ReferenceEquals(entity, GameLoop.m_player))
            {
                Vector2 shift = SubjectDrawOffset - RenderContext.ImageShift;
                using (new RenderContext(RenderContext.Screen))
                { RenderContext.SetTranslation(shift); entity.Draw(); }
                return;
            }
            if (RenderContext.Active && RenderContext.Column != 0 && ReferenceEquals(entity, GameLoop.m_player))
            {
                int column = RenderContext.Column;
                using (new RenderContext(RenderContext.BaseScreen))
                { RenderContext.SetHorizontal(-column * 480); entity.Draw(); }
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
            // Pause menus, location labels, fades and lightning keep their native
            // viewport coordinates and ordering outside the screen passes
            if (frameActive && IsWorldForeground(item)) return;
            item.ForegroundDraw();
        }
    }

}
