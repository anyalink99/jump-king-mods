using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using JKRuntime;
using JKRuntime.Modules;
using JumpKing;
using JumpKing.Level;

[assembly: AssemblyVersion("0.6.2.0")]
[assembly: AssemblyFileVersion("0.6.2.0")]

namespace MegaMappingExpansion
{
    internal static class MappingState
    {
        internal static SceneFile Pending { get; private set; }
        internal static SceneOptions Options { get { return Pending == null ? null : Pending.Options; } }
        internal static Microsoft.Xna.Framework.Color FrameTint { get; private set; }
        internal static void Load(string root)
        {
            SceneFile scene = SceneValidation.Read(root);
            if (scene != null && scene.Options == null) scene.Options = new SceneOptions();
            if (scene != null) SceneValidation.ValidateOptions(scene.Options);
            Commit(scene);
        }
        internal static void Reset() { Pending = null; }
        internal static void Commit(SceneFile scene)
        {
            Pending = scene;
            FrameTint = scene == null ? Microsoft.Xna.Framework.Color.White : SceneValidation.ParseColor(scene.Options.Tint, "Options.tint");
        }
    }

    [RuntimeModule("mega-mapping-expansion", "Mega Mapping Expansion", Provides = new[] { "mega.mapping.scene:1:0" })]
    public static class ModEntry
    {
        // Optional discovery contract for independent camera/rendering mods.
        // Persistent state, the scene API and map topology remain available while
        // presentation is suspended, so toggling does not reset the current map.
        public static bool IsPresentationEnabled { get { return MappingSettings.Enabled; } }
        public static bool SupportsCameraComposition { get { return true; } }
        public static bool IsCompositorActive { get { return MappingSettings.Enabled && SceneHost.Current != null; } }
        [MainMenuItemSetting]
        public static JKRuntime.UI.SettingToggle MainMenuEnabled(object factory, JumpKing.PauseMenu.GuiFormat format)
        { return new JKRuntime.UI.SettingToggle(MappingSettings.Toggle); }
        [PauseMenuItemSetting]
        public static JKRuntime.UI.SettingToggle PauseMenuEnabled(object factory, JumpKing.PauseMenu.GuiFormat format)
        { return MainMenuEnabled(factory, format); }
        [MainMenuItemSetting]
        public static JumpKing.PauseMenu.BT.TextButton MainMenu(object factory, JumpKing.PauseMenu.GuiFormat format)
        { return new JumpKing.PauseMenu.BT.TextButton("Mapping inspector", JKRuntime.UI.UIApi.CreateMenuPage(factory, new SceneInspector())); }
        [PauseMenuItemSetting]
        public static JumpKing.PauseMenu.BT.TextButton PauseMenu(object factory, JumpKing.PauseMenu.GuiFormat format)
        { return MainMenu(factory, format); }
        private static SceneHost host;
        private static int generation;
        private static SceneLifetime lifetime;
        private static bool prepared;
        private static string preparedRoot;
        private static LoadedScene preparedScene;
        private static SceneHost preparedHost;
        private static MapLayout preparedLayout;
        private static bool worldPrepared;
        internal static DateTime LastPreviewRequest = DateTime.UtcNow;

        [OnWorldReady]
        public static void PrepareWorld(RuntimeScope scope)
        {
            worldPrepared = true;
            scope.Defer(delegate { worldPrepared = false; Unload(); });
        }

        [BeforeAttempt]
        public static void PrepareAttempt(RuntimeScope scope)
        { PrepareAttempt(scope, SceneValidation.ResolveLevelRoot(Game1.instance.contentManager.root), LevelManager.TotalScreens, true); }
        internal static void PrepareAttempt(RuntimeScope scope, string root, int screens, bool prepareResources = false)
        {
            bool succeeded = false;
            scope.Defer(delegate { if (!worldPrepared || !succeeded) { NativeHooks.Uninstall(); Endings.NativeEndings.Release(); NativeHiddenWalls.Release(); NativeNarrative.Release(); } });
            scope.Defer(delegate {
                prepared = false; preparedRoot = null; preparedScene = null; preparedLayout = null; preparedHost = null;
            });
            preparedRoot = root;
            using (RuntimeApi.MeasureStartup("mega-mapping.endings-read")) Endings.NativeEndings.Prepare(root);
            using (RuntimeApi.MeasureStartup("mega-mapping.layout-read")) preparedLayout = MapLayout.Read(preparedRoot);
            using (RuntimeApi.MeasureStartup("mega-mapping.scene-read")) preparedScene = LoadedScene.Load(preparedRoot, screens);
            Endings.NativeEndings.ValidateScene(preparedScene == null ? null : preparedScene.Data);
            using (RuntimeApi.MeasureStartup("mega-mapping.scene-hooks")) NativeHooks.ConfigureForScene(preparedScene != null);
            using (RuntimeApi.MeasureStartup("mega-mapping.hidden-wall-hooks")) NativeHiddenWalls.Configure(preparedScene == null ? null : preparedScene.Data);
            if (prepareResources && preparedScene != null)
                using (RuntimeApi.MeasureStartup("mega-mapping.scene-resources"))
                    preparedHost = scope.Own(new SceneHost(preparedScene, root));
            NativeNarrative.Prepare(preparedHost);
            prepared = true; succeeded = true;
        }

        [BeforeLevelLoad]
        public static void BeforeLevelLoad()
        {
            MappingSettings.Load();
            MappingState.Reset();
            NativeMapLayout.Release();
            try
            {
                string contentRoot = Game1.instance.contentManager.root;
                string root = SceneValidation.ResolveLevelRoot(contentRoot);
                Endings.NativeEndings.Prepare(root);
                Log("BeforeLevelLoad contentRoot='" + contentRoot + "' resolvedRoot='" + root + "'");
                MappingState.Load(root);
                Endings.NativeEndings.ValidateScene(MappingState.Pending);
                NativeHooks.ConfigureForScene(MappingState.Pending != null);
                Log(MappingState.Pending == null ? "No scene.xml found" : "XML preflight loaded");
            }
            catch (Exception error)
            {
                NativeHooks.Uninstall(); Endings.NativeEndings.Release(); NativeHiddenWalls.Release();
                throw new InvalidDataException("Mega Mapping preflight failed; the authored map was not activated", error);
            }
        }

        [OnLevelStart]
        public static void Start(ModuleContext context)
        {
            MappingSettings.Load();
            Stop(false);
            context.Track(FrameComposition.RegisterScene(new CameraComposition()));
            var gateway = new SceneGateway();
            context.Track(gateway);
            context.Publish("mega.mapping.scene", gateway);
            context.Track(JKRuntime.State.GameState.Snapshots.Register(gateway));
            string currentRoot = SceneValidation.ResolveLevelRoot(Game1.instance.contentManager.root);
            bool usePrepared = prepared && string.Equals(preparedRoot, currentRoot, StringComparison.OrdinalIgnoreCase);
            if (!usePrepared) throw new InvalidOperationException("Mega Mapping activation requires successful BeforeAttempt preparation for this map");
            using (RuntimeApi.MeasureStartup("mega-mapping.layout-apply"))
                NativeMapLayout.BeginRun(preparedLayout);
            context.Track(RuntimeApi.Mechanics.Register("mega-mapping-expansion", "mega-mapping.side-links", new Version(1, 0),
                JKRuntime.Gameplay.MechanicEffects.Movement, delegate {
                    bool active = NativeMapLayout.Current != null && NativeMapLayout.Current.Links.Count > 0;
                    return new JKRuntime.Gameplay.MechanicState(active, active, active,
                        JKRuntime.Gameplay.MechanicSource.Screen, "Map-authored integer links; native teleport movement");
                }));
            context.Track(RuntimeApi.Mechanics.Register("mega-mapping-expansion", "mega-mapping.scene", new Version(1, 0),
                JKRuntime.Gameplay.MechanicEffects.Presentation, delegate {
                    return new JKRuntime.Gameplay.MechanicState(MappingSettings.Enabled, host != null, MappingSettings.Enabled && host != null,
                        JKRuntime.Gameplay.MechanicSource.Setting, !MappingSettings.Enabled ? "Disabled in settings" : host == null ? "No active map scene" : "Map-authored presentation; no collision override");
                }));
            try
            {
                string root = SceneValidation.ResolveLevelRoot(Game1.instance.contentManager.root);
                LoadedScene loaded = preparedScene;
                if (loaded == null) { Log("Start skipped: map has no scene.xml"); return; }
                SceneFile scene = loaded.Data;
                if (preparedHost == null) throw new InvalidOperationException("Scene resources were not prepared before activation");
                host = preparedHost;
                host.Activate();
                MappingState.Commit(host.SceneData);
                lifetime = new SceneLifetime(++generation);
                context.Track(lifetime);
                Log("Loaded " + SceneValidation.RelativeScenePath + " from '" + root
                    + "' for " + LevelManager.TotalScreens + " screen(s), textures=" + scene.Textures.Length
                    + ", compiled=" + host.UsedCompiledCache);
            }
            catch (Exception error)
            {
                Stop(false);
                throw new InvalidOperationException("Mega Mapping scene activation failed", error);
            }
        }

        [OnLevelUnload]
        public static void Unload() { Stop(true); NativeMapLayout.Release(); if (!worldPrepared) { NativeHooks.Uninstall(); Endings.NativeEndings.Release(); NativeHiddenWalls.Release(); NativeNarrative.Release(); } }
        [OnLevelEnd]
        public static void End() { Stop(true); }

        private sealed class CameraComposition : ISceneCompositor, ISceneFrameCapture
        {
            public bool Active { get { return IsCompositorActive; } }
            public FrameStyle Style
            {
                get
                {
                    var options = SceneHost.Current.Options;
                    string mirror = options.Mirror ?? "none";
                    var effects = Microsoft.Xna.Framework.Graphics.SpriteEffects.None;
                    if (string.Equals(mirror,"horizontal",StringComparison.OrdinalIgnoreCase) || string.Equals(mirror,"both",StringComparison.OrdinalIgnoreCase)) effects |= Microsoft.Xna.Framework.Graphics.SpriteEffects.FlipHorizontally;
                    if (string.Equals(mirror,"vertical",StringComparison.OrdinalIgnoreCase) || string.Equals(mirror,"both",StringComparison.OrdinalIgnoreCase)) effects |= Microsoft.Xna.Framework.Graphics.SpriteEffects.FlipVertically;
                    return new FrameStyle(Microsoft.Xna.Framework.Color.Lerp(Microsoft.Xna.Framework.Color.White, MappingState.FrameTint, options.TintOpacity), effects);
                }
            }
            public void Compose(Microsoft.Xna.Framework.Graphics.RenderTarget2D target) { SceneHost.Current.ComposeScenePass(target); }
            public bool CaptureRequested { get { return SceneHost.Current.WantsFinalCapture; } }
            public void Capture(Microsoft.Xna.Framework.Graphics.RenderTarget2D target) { SceneHost.Current.CapturePreview(target); }
        }
        internal static IDisposable RegisterCameraComposition() { return FrameComposition.RegisterScene(new CameraComposition()); }

        private sealed class SceneLifetime : EntityComponent.Entity, IDisposable
        {
            private readonly int owner;
            internal SceneLifetime(int value) { owner = value; }
            // The EntityManager postfix advances the scene after every actor's final update.
            internal void Detach() { if (IsAlive) Destroy(); }
            public void Dispose() { if (owner == generation) Stop(true); Detach(); }
        }

        internal static bool Reload(string root)
        {
            SceneHost candidate = null;
            SceneHost previous = host;
            bool activationStarted = false;
            try
            {
                LoadedScene loaded = LoadedScene.Load(root, LevelManager.TotalScreens);
                if (loaded == null) throw new FileNotFoundException("Hot reload requires scene.xml");
                Endings.NativeEndings.ValidateScene(loaded.Data);
                NativeHooks.ConfigureForScene(true);
                candidate = new SceneHost(loaded, root);
                if (previous != null) candidate.CopyPreviewState(previous);
                activationStarted = true;
                candidate.Activate();
                NativeNarrative.Prepare(candidate); NativeNarrative.Activate(candidate);
                NativeHiddenWalls.Configure(loaded.Data);
                MappingState.Commit(candidate.SceneData);
                host = candidate;
                if (previous != null)
                    try { previous.Dispose(); }
                    catch (Exception cleanup) { Log("Reload committed, but previous scene cleanup failed: " + cleanup); }
                Log("Hot reload committed; compiled=" + candidate.UsedCompiledCache);
                return true;
            }
            catch (Exception error)
            {
                if (candidate != null) candidate.Dispose();
                if (activationStarted)
                {
                    host = previous;
                    if (previous != null) previous.Activate();
                    NativeNarrative.Prepare(previous); if (previous != null) NativeNarrative.Activate(previous);
                    NativeHiddenWalls.Configure(previous == null ? null : previous.SceneData);
                    MappingState.Commit(previous == null ? null : previous.SceneData);
                }
                Log("Hot reload rejected; keeping last working scene: " + error);
                return false;
            }
        }

        private static void Stop(bool resetOptions)
        {
            if (lifetime != null) { lifetime.Detach(); lifetime = null; }
            if (host != null) host.Dispose();
            host = null;
            if (resetOptions) MappingState.Reset();
        }

        internal static void Log(string message)
        {
            string line = "[" + DateTime.UtcNow.ToString("o") + "] " + message;
            Console.WriteLine("[Mega Mapping Expansion] " + message);
            try
            {
                string directory = PackageHost.GetDataDirectory(typeof(ModEntry).Assembly);
                File.AppendAllText(Path.Combine(directory, "MegaMappingExpansion.log"), line + Environment.NewLine);
            }
            catch { }
        }
    }
}
