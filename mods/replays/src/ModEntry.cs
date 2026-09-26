using JKRuntime.Modules;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;

namespace Replays
{
    [RuntimeModule("replays", "Replays", Provides = new[] { "replays.verification:1:0" })]
    public static class ModEntry
    {
        [BeforeLevelLoad]
        public static void BeforeLevelLoad()
        {
            ReplaySettingsStore.EnsureLoaded();
            ReplayAppearanceTrack.ValidateContract();
            ReplayAppearanceSprites.ValidateContract();
            ReplayFrameCapture.ValidateContract();
            ReplayEntityDrawOrder.ValidateContract();
            ReplayVictoryIsolation.ValidateContract();
            JKRuntime.State.GameClock.ValidateContract();
            ReplayUI.SyncMenus();
        }

        [BeforeAttempt]
        public static void PrepareAttempt(JKRuntime.RuntimeScope scope) { ReplayRuntime.Prepare(scope); }

        [OnLevelStart]
        public static void OnLevelStart(JKRuntime.ModuleContext context)
        {
            ReplayRuntime.Install();
            context.Publish("replays.verification",typeof(VerificationBridge));
            context.Track(JKRuntime.RuntimeApi.Mechanics.Register("replays", "replays.presentation", new System.Version(1, 0),
                JKRuntime.Gameplay.MechanicEffects.Presentation, delegate {
                    return new JKRuntime.Gameplay.MechanicState(true, true, ReplayRuntime.ViewerActive || ReplayRuntime.RecordingActive,
                        JKRuntime.Gameplay.MechanicSource.Setting, ReplayRuntime.ViewerActive ? "Pose playback; not gameplay simulation" : "Recording includes suspended presentation ticks");
                }));
        }

        [OnLevelUnload]
        public static void OnLevelUnload()
        {
            ReplayRuntime.Uninstall();
        }

        [OnLevelEnd]
        public static void OnLevelEnd()
        {
            ReplayRuntime.EndLevel();
        }

        [PauseMenuItemSetting]
        public static TextButton PauseMenuReplays(
            object factory,
            GuiFormat format)
        {
            return ReplayUI.CreatePauseMenuReplays(factory, format);
        }

        [PauseMenuItemSetting]
        public static TextButton PauseMenuSaveReplay(
            object factory,
            GuiFormat format)
        {
            return ReplayUI.CreatePauseMenuSaveReplay(factory, format);
        }

        [MainMenuItemSetting]
        public static TextButton MainMenuReplays(
            object factory,
            GuiFormat format)
        {
            return ReplayUI.CreateMainMenuReplays(factory, format);
        }

        [MainMenuItemSetting]
        public static RecordingEnabledOption MainMenuRecording(
            object factory,
            GuiFormat format)
        {
            return new RecordingEnabledOption();
        }

        [PauseMenuItemSetting]
        public static RecordingEnabledOption PauseMenuRecording(
            object factory,
            GuiFormat format)
        {
            return new RecordingEnabledOption();
        }

        [MainMenuItemSetting]
        public static ReplaysInMainMenuOption MainMenuPlacement(
            object factory,
            GuiFormat format)
        {
            return new ReplaysInMainMenuOption();
        }

        [PauseMenuItemSetting]
        public static ReplaysInMainMenuOption PauseMenuPlacement(
            object factory,
            GuiFormat format)
        {
            return new ReplaysInMainMenuOption();
        }

        [MainMenuItemSetting]
        public static SaveReplayInPauseMenuOption MainMenuSavePlacement(
            object factory,
            GuiFormat format)
        {
            return new SaveReplayInPauseMenuOption();
        }

        [PauseMenuItemSetting]
        public static SaveReplayInPauseMenuOption PauseMenuSavePlacement(
            object factory,
            GuiFormat format)
        {
            return new SaveReplayInPauseMenuOption();
        }
    }
}
