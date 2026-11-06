using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using EntityComponent;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.GameManager.TitleScreen;
using JumpKing.Level;
using JumpKing.SaveThread;
using Microsoft.Xna.Framework;

namespace JKRuntime
{
    internal static class IntroPreviewTests
    {
        private const BindingFlags Flags = OwnedPatches.Members;
        private static readonly Type Folly = typeof(Game1).Assembly.GetType("JumpKing.GameManager.TitleScreen.FollyPlayer", true);
        private static readonly PropertyInfo Save = typeof(Game1).Assembly.GetType("JumpKing.SaveThread.SaveLube", true).GetProperty("PlayerPosition", Flags);
        private static readonly FieldInfo Preview = typeof(GameTitleScreen).GetField("m_folly", Flags);
        private static readonly Sprite Splat = Empty<Sprite>(), Falling = Empty<Sprite>(), Rising = Empty<Sprite>();
        private static int refreshes;
        private static object Empty(Type type) { var value = FormatterServices.GetUninitializedObject(type); GC.SuppressFinalize(value); return value; }
        private static T Empty<T>() { return (T)Empty(typeof(T)); }
        private static MethodInfo Method(string name) { return typeof(IntroPreviewTests).GetMethod(name, Flags); }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private static bool Skip() { return false; }
        private static bool NoDefault() { throw new Exception("intro refresh must not read the default save/map XML"); }
        private static bool SplatSprite(ref Sprite __result) { __result = Splat; return false; }
        private static bool FallingSprite(ref Sprite __result) { __result = Falling; return false; }
        private static bool RisingSprite(ref Sprite __result) { __result = Rising; return false; }
        private static void Refreshed() { refreshes++; }
        private static SaveState Position(float x, float y)
        { return new SaveState { position = new Vector2(x, y), velocity = Vector2.Zero, direction = 1, is_on_ground = true }; }
        private static void Write(SaveState value) { Save.SetValue(null, value, null); }
        private static void CheckSave(SaveState expected)
        { Check(((SaveState)Save.GetValue(null, null)).IsSame(expected), "preview refresh changed saved player state"); }
        private static Entity NewPreview(GameTitleScreen title, SaveState value)
        {
            var player = (Entity)Activator.CreateInstance(Folly, Flags, null, new object[] { value }, null);
            Preview.SetValue(title, player); return player;
        }
        private static void CheckPreview(Entity player, SaveState save, int screen, Sprite sprite)
        {
            Check(Camera.CurrentScreenIndex1 == screen, "preview camera chose the wrong screen");
            Check(((SaveState)Folly.GetField("m_save", Flags).GetValue(player)).IsSame(save), "preview kept a stale save");
            var center = save.position.ToPoint(); center.X += 9; center.Y += 26;
            Check((Point)Folly.GetField("m_center", Flags).GetValue(player) == Camera.TransformVector2(center.ToVector2()).ToPoint(), "preview kept stale screen coordinates");
            Check(ReferenceEquals(Folly.GetField("m_sprite", Flags).GetValue(player), sprite), "preview pose no longer follows the native save");
            CheckSave(save);
        }
        private static void Main(string[] args)
        {
            Assembly.LoadFrom(args[0]);
            var game = Empty<Game1>(); typeof(Game1).GetField("_instance", Flags).SetValue(null, game);
            game.contentManager = Empty<JKContentManager>();
            game.contentManager.root = "missing-map-no-file-reads";
            game.contentManager.playerSprites = new JKContentManager.PlayerSprites();
            var world = Empty<JumpGame>(); typeof(JumpGame).GetField("_instance", Flags).SetValue(null, world);
            var title = new GameTitleScreen(); typeof(JumpGame).GetField("m_title_screen", Flags).SetValue(world, title);
            var loop = new GameLoop(); var entities = new EntityManager(); SaveManager.instance = new SaveManager();
            typeof(LevelManager).GetField("_total_screens", Flags).SetValue(null, 324);
            var ending = typeof(Game1).Assembly.GetType("JumpKing.GameManager.MultiEnding.EndingManager", true);
            using (var fixture = new OwnedPatches("jk-runtime.tests.intro-preview")) {
                fixture.Add(typeof(SaveState).GetMethod("GetDefault"), prefix: Method("NoDefault"));
                fixture.Add(typeof(JKContentManager.PlayerSprites).GetProperty("splat").GetGetMethod(), prefix: Method("SplatSprite"));
                fixture.Add(typeof(JKContentManager.PlayerSprites).GetProperty("jump_fall").GetGetMethod(), prefix: Method("FallingSprite"));
                fixture.Add(typeof(JKContentManager.PlayerSprites).GetProperty("jump_up").GetGetMethod(), prefix: Method("RisingSprite"));
                fixture.Add(Folly.GetMethod("ApplySave", Flags), postfix: Method("Refreshed"));
                fixture.Add(ending.GetMethod("SetUpEndings", Flags), prefix: Method("Skip"));
                fixture.Add(typeof(RuntimeHost).GetMethod("PrepareAttempt", Flags), prefix: Method("Skip"));
                fixture.Add(typeof(IntroState).GetMethod("MakeBT", Flags), prefix: Method("Skip"));
                fixture.Add(typeof(IntroState).GetMethod("InitializeEntities", Flags), prefix: Method("Skip"));
                PreparationHooks.Install(); Check(PreparationHooks.TickAvailable, PreparationHooks.Status);
                var intro = typeof(IntroState).GetMethod("OnNewRun", Flags);
                var source = Position(235, -40226); var target = Position(231, -21690);
                var preview = NewPreview(title, source); Write(target);
                Check(Camera.CurrentScreenIndex1 == 113, "regression setup must retain the source screen");
                // the previous map's endings would clamp this destination to screen 61
                var oldEnding = Empty(ending);
                ending.GetField("WIN_SCREENS0", Flags).SetValue(oldEnding, new[] { 60 });
                ending.GetField("_instance", Flags).SetValue(null, oldEnding);
                int before = refreshes;
                var cold = Stopwatch.StartNew(); intro.Invoke(new IntroState(), null); cold.Stop();
                Check(refreshes == before + 1, "one native refresh per intro");
                CheckPreview(preview, target, 62, Splat);
                Console.WriteLine("[PERF] First headless native intro with preview refresh: {0:F3} ms", cold.Elapsed.TotalMilliseconds);

                // restart and cold entry already have a correct preview; keep their state
                target = Position(231, 302); Write(target);
                for (int i = 0; i < 3; i++) { intro.Invoke(new IntroState(), null); CheckPreview(preview, target, 1, Splat); }
                // cancelled intro removes its entity; don't revive or hold on to it
                preview.Destroy(); before = refreshes; intro.Invoke(new IntroState(), null);
                Check(refreshes == before && !preview.IsAlive, "cancelled intro revived its preview");
                Preview.SetValue(title, null); intro.Invoke(new IntroState(), null);
                Check(refreshes == before, "missing preview should be a no-op");
                preview = NewPreview(title, source); Write(target); intro.Invoke(new IntroState(), null);
                CheckPreview(preview, target, 1, Splat);

                // resumed airborne saves must keep their pose, direction, velocity and timestamp
                target.position = new Vector2(80, -800); target.velocity = new Vector2(-2, 4);
                target.is_on_ground = false; target.direction = -1; target.time_stamp = 987;
                Write(target); intro.Invoke(new IntroState(), null); CheckPreview(preview, target, 4, Falling);
                target.velocity = new Vector2(-2, -4); Write(target); intro.Invoke(new IntroState(), null); CheckPreview(preview, target, 4, Rising);
                var debugField = typeof(LevelDebugState).GetField("_instance", Flags);
                debugField.SetValue(null, Empty<LevelDebugState>()); before = refreshes; intro.Invoke(new IntroState(), null);
                Check(refreshes == before, "debug entry should leave its camera alone"); debugField.SetValue(null, null);

                var warm = Stopwatch.StartNew();
                for (int i = 0; i < 10000; i++) IntroPreview.Refresh();
                warm.Stop(); CheckSave(target);
                Console.WriteLine("[PERF] Warm preview refresh: {0:F6} ms/call (10000 calls, graphics lookup stubbed)", warm.Elapsed.TotalMilliseconds / 10000);
                before = refreshes;
                for (int i = 0; i < 120; i++) { preview.UpdateComponents(1f / 60); CheckSave(target); }
                Check(refreshes == before, "preview fix leaked into per-frame updates");
                entities.DestroyAll();
                Console.WriteLine("[OK] Intro preview: native hook ordering, 113 -> 62, restart, cancellation, missing preview, airborne save, debug, no default-save reads or frame polling");
            }
        }
    }
}
