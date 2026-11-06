using JKRuntime.UI;

namespace Replays
{
    internal static class ReplayMenuRegistration
    {
        private const string MainMenuRegistrationId = "replays.library";
        private const string PauseSaveRegistrationId = "replays.save-current";

        internal static void Sync()
        {
            ReplaySettingsStore.EnsureLoaded();
            SyncMainMenu();
            SyncPauseMenu();
        }

        private static void SyncMainMenu()
        {
            if (!ReplaySettingsStore.Current.ReplaysInMainMenu)
            {
                UIApi.UnregisterMainMenuItem(MainMenuRegistrationId);
                return;
            }
            UIApi.RegisterMainMenuItem(
                UiMainMenuItemDefinition.Page(
                    MainMenuRegistrationId,
                    "Replays",
                    UiMainMenuPlacement.BeforeExtras,
                    100,
                    delegate(UiMenuContext context)
                    {
                        return new ReplayLibraryPage(
                            delegate { context.ContinueGame(); });
                    }));
        }

        private static void SyncPauseMenu()
        {
            if (!ReplaySettingsStore.Current.SaveReplayInPauseMenu)
            {
                UIApi.UnregisterPauseMenuItem(PauseSaveRegistrationId);
                return;
            }
            UIApi.RegisterPauseMenuItem(
                UiPauseMenuItemDefinition.FeedbackAction(
                    PauseSaveRegistrationId,
                    "Save replay",
                    UiPauseMenuPlacement.BeforeSaveAndExit,
                    100,
                    SaveReplay));
        }

        internal static UiMenuActionResult SaveReplay()
        {
            if (!ReplayRuntime.SaveCurrentReplay()) return UiMenuActionResult.Rejected("Not saved - check library", 3f);
            var work = ReplayRuntime.Repository.LastSave;
            return UiMenuActionResult.Pending("Saving...", delegate
            {
                if (!work.IsCompleted) return null;
                return work.State == JKRuntime.BackgroundWorkState.Succeeded
                    ? UiMenuActionResult.Completed("Saved!", 2f)
                    : UiMenuActionResult.Rejected("Save failed - retry in library", 4f);
            });
        }
    }
}
