using JKRuntime.Modules;
using JumpKing.Level;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;

namespace MorphBallMod
{
    [RuntimeModule("morph-ball", "Ball King", Provides = new[] { "player.form:1:0" })]
    public static class ModEntry
    {
        private static System.IDisposable form;
        internal static MorphSound PreparedSound;
        [OnWorldReady]
        public static void PrepareWorld(JKRuntime.RuntimeScope scope)
        {
            using (JKRuntime.RuntimeApi.MeasureStartup("morph-ball.audio"))
            {
                var sound = scope.Own(new MorphSound());
                PreparedSound = sound;
                scope.Defer(delegate { if (object.ReferenceEquals(PreparedSound, sound)) PreparedSound = null; });
            }
        }
        [BeforeAttempt]
        public static void PrepareAttempt(JKRuntime.RuntimeScope scope)
        { if (PreparedSound != null) scope.Defer(PreparedSound.Stop); }
        private sealed class Form : JKRuntime.Gameplay.IPlayerForm
        {
            public bool Morphed { get { return BallFormState.Morphed; } }
            public bool Attached { get { return BallFormState.Attached; } }
            public int JumpSequence { get { return BallFormState.JumpSequence; } }
            public bool OwnsSprite(object sprite) { return sprite is MorphSprite; }
        }
        [BeforeLevelLoad]
        public static void BeforeLevelLoad()
        {
            SettingsStore.EnsureLoaded();
            JumpKingContract.ValidateLevelLoading();
            BallKingMapRules.Clear();
            LevelManager.RegisterBlockFactory(new BallKingBlockFactory());
        }

        [OnLevelStart]
        public static void OnLevelStart(JKRuntime.ModuleContext context)
        {
            context.Track(JKRuntime.Geometry.BlockCatalog.Register("morph-ball", new[] {
                new JKRuntime.Geometry.BlockDeclaration("ball-king.restriction", BallKingMapPixels.RestrictedScreen, JKRuntime.Geometry.BlockVariant.Screen, true),
                new JKRuntime.Geometry.BlockDeclaration("ball-king.no-double-jump", BallKingMapPixels.RestrictedNoDoubleJump, JKRuntime.Geometry.BlockVariant.Screen, true),
                new JKRuntime.Geometry.BlockDeclaration("ball-king.force-double-jump", BallKingMapPixels.RestrictedForceDoubleJump, JKRuntime.Geometry.BlockVariant.Screen, true),
                new JKRuntime.Geometry.BlockDeclaration("ball-king.sticky", BallKingMapPixels.StickySurface, JKRuntime.Geometry.BlockVariant.Solid, true)
            }, "3792539795"));
            var provider = new Form();
            form = context.Track(JKRuntime.Gameplay.GameFeatures.RegisterForm(provider));
            context.Publish("player.form", provider);
            context.Track(JKRuntime.RuntimeApi.Geometry.Register("morph-ball", BallKingGeometryApi.RuntimeProfile,
                BallKingGeometryApi.RuntimeProfileVersion, MorphContourGeometry.ExportSolidPolygon));
            context.Track(JKRuntime.RuntimeApi.Mechanics.Register("morph-ball", new JKRuntime.Gameplay.MechanicDefinition("ball-king.form", new System.Version(1, 0),
                JKRuntime.Gameplay.MechanicEffects.Movement | JKRuntime.Gameplay.MechanicEffects.Collision | JKRuntime.Gameplay.MechanicEffects.Presentation,
                JKRuntime.Gameplay.MechanicComposition.Exclusive, "player.controller"),
                delegate {
                    var player = JumpKing.GameManager.GameLoop.m_player;
                    bool available = player != null && JKRuntime.Gameplay.PlayerControl.Available(player.m_body, "morph-ball");
                    return new JKRuntime.Gameplay.MechanicState(true, available, provider.Morphed,
                        JKRuntime.Gameplay.MechanicSource.Controller, !available ? "Another controller owns movement" : provider.Morphed ? "Ball controller active" : "Native player form"); }));
            MorphBallInstaller.Apply();
        }

        [OnLevelUnload]
        public static void OnLevelUnload()
        {
            MorphBallInstaller.Uninstall();
            if (form != null) { form.Dispose(); form = null; }
        }

        [OnLevelEnd]
        public static void OnLevelEnd()
        {
            OnLevelUnload();
        }

        [MainMenuItemSetting]
        public static MorphBallOption MainMenuEnabled(
            object factory,
            GuiFormat format)
        {
            return new MorphBallOption();
        }

        [PauseMenuItemSetting]
        public static MorphBallOption PauseMenuEnabled(
            object factory,
            GuiFormat format)
        {
            return new MorphBallOption();
        }

        [MainMenuItemSetting]
        public static MorphStickyOption MainMenuSticky(
            object factory,
            GuiFormat format)
        {
            return new MorphStickyOption(false, null);
        }

        [PauseMenuItemSetting]
        public static MorphStickyOption PauseMenuSticky(
            object factory,
            GuiFormat format)
        {
            return new MorphStickyOption(true, factory);
        }

        [MainMenuItemSetting]
        public static MorphDoubleJumpOption MainMenuDoubleJump(
            object factory,
            GuiFormat format)
        {
            return new MorphDoubleJumpOption(false, null);
        }

        [PauseMenuItemSetting]
        public static MorphDoubleJumpOption PauseMenuDoubleJump(
            object factory,
            GuiFormat format)
        {
            return new MorphDoubleJumpOption(true, factory);
        }

        [MainMenuItemSetting]
        public static TextButton MainMenuBinding(
            object factory,
            GuiFormat format)
        {
            return new TextButton(
                "Binds",
                JKRuntime.UI.UIApi.CreateMenuPage(factory, new JKRuntime.UI.UiBindingsPage("Binds", "Press Morph to curl up or unfold.", "auto.MorphBall.Morph")));
        }

        [PauseMenuItemSetting]
        public static TextButton PauseMenuBinding(
            object factory,
            GuiFormat format)
        {
            return MainMenuBinding(factory, format);
        }
    }
}
