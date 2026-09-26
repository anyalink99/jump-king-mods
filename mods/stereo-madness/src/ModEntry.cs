using System;
using System.IO;
using System.Reflection;
using JKRuntime;
using JKRuntime.Modules;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.Level;
using MegaMappingExpansion.Api;

namespace StereoMadness
{
    [RuntimeModule("stereo-madness", "Stereo Madness", Requires = new[] { "mega.mapping.scene:1:0" }, Before = new[] { "morph-ball" }, After = new[] { "casual-jumping", "subframe-charge", "smooth-camera" })]
    public static class ModEntry
    {
        internal static Resources World;
        internal static Controller Active;
        [OnWorldReady]
        public static void Prepare(RuntimeScope scope)
        {
            try { PrepareAssets(scope); }
            catch (Exception error) { Console.WriteLine("[Stereo Madness] World preparation failed: " + error); throw; }
        }
        private static void PrepareAssets(RuntimeScope scope)
        {
            GC.KeepAlive(typeof(HarmonyLib.Harmony));
            string path = Path.Combine(Game1.instance.contentManager.root, "props", "stereo-madness");
            if (!File.Exists(Path.Combine(path, "course.xml"))) return;
            using (RuntimeApi.MeasureStartup("stereo-madness.assets")) {
                var value = scope.Own(Resources.Load(path)); World = value;
                Console.WriteLine("[Stereo Madness] Prepared " + value.Course.Objects.Length + " course draw records");
                scope.Defer(delegate { if (ReferenceEquals(World, value)) World = null; });
            }
        }
        [OnLevelStart]
        public static void Start(ModuleContext context)
        {
            try { Activate(context); }
            catch (Exception error) { Console.WriteLine("[Stereo Madness] Activation failed: " + error); throw; }
        }
        private static void Activate(ModuleContext context)
        {
            if (World == null) return;
            var patches = context.Track(new OwnedPatches("stereo-madness.native"));
            patches.Add(typeof(LevelScreen).GetMethod("Draw"), prefix: typeof(ModEntry).GetMethod("DrawScene", OwnedPatches.Members));
            patches.Add(typeof(LevelScreen).GetMethod("DrawForeground"), prefix: typeof(ModEntry).GetMethod("DrawForeground", OwnedPatches.Members));
            PatchEnding(patches);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                var renderer = assembly.GetType("SmoothCamera.Renderer", false);
                if (renderer != null) patches.Add(renderer.GetMethod("BeginFrame", OwnedPatches.Members),
                    prefix: typeof(ModEntry).GetMethod("NativeViewport", OwnedPatches.Members));
            }
            // Reserve before Ball King can restore a saved folded form. Its
            // cooperative TryAcquire then leaves the king unfolded on this map.
            var scene = context.Require<IMappingScene>("mega.mapping.scene");
            if (!scene.Available) throw new InvalidOperationException("Stereo Madness requires its Mega Mapping scene");
            var controller = context.Track(new Controller(GameLoop.m_player, World, scene));
            Active = controller;
            Console.WriteLine("[Stereo Madness] Native controller active");
        }
        private static bool DrawScene()
        { if (Active == null || Active.Released) return true; Active.DrawScene(); return false; }
        private static bool DrawForeground()
        { if (Active == null || Active.Released) return true; Active.DrawHud(); return false; }
        private static bool CheckFinish(ref bool __result)
        { if (Active == null || Active.Released) return true; __result=false; return false; }
        internal static void PatchEnding(OwnedPatches patches)
        {
            patches.Add(typeof(JumpKing.GameManager.MultiEnding.NormalEnding.NormalEnding).GetMethod("CheckWin"),
                prefix: typeof(ModEntry).GetMethod("CheckFinish", OwnedPatches.Members));
            // The native intro spawns Babe before gameplay. Course altitude can
            // cross her screen long before the horizontal finish is reached.
            var babe = typeof(Game1).Assembly.GetType("JumpKing.GameManager.MultiEnding.NormalEnding.EndingBabe", true);
            patches.Add(babe.GetMethod("Draw", OwnedPatches.Members),
                prefix: typeof(ModEntry).GetMethod("DrawEndingBabe", OwnedPatches.Members));
        }
        private static bool DrawEndingBabe()
        { return Active == null || Active.Released; }
        // Scope the horizontal course to the native viewport without changing the
        // user's Smooth Camera settings. Its ordinary path resumes at the finish.
        private static bool NativeViewport(ref bool ___frameActive, ref bool ___worldPending, ref bool ___canPresent)
        {
            if (Active == null || Active.Released) return true;
            ___frameActive = ___worldPending = ___canPresent = false; return false;
        }
        [OnLevelEnd] public static void End() { Active = null; }
        [OnLevelUnload] public static void Unload() { Active = null; }
        internal static void StopNativeMusic()
        { typeof(Game1).Assembly.GetType("JumpKing.MusicManager", true).GetMethod("Stop", OwnedPatches.Members).Invoke(null, null); }
    }
}
