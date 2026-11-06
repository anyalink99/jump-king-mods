using System;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using JKRuntime.UI;

namespace Replays
{
    internal interface IReplayViewer
    {
        bool IsOpen { get; }
        void Abort();
    }

    internal static class ReplayUI
    {
        public static bool IsOpen { get { return UIApi.IsOpen; } }


        public static void SyncMenus()
        {
            ReplayMenuRegistration.Sync();
        }

        public static TextButton CreatePauseMenuReplays(
            object factory,
            GuiFormat format)
        {
            return new TextButton(
                "Replays",
                UIApi.CreateMenuPage(factory, new ReplayLibraryPage()));
        }

        public static TextButton CreatePauseMenuSaveReplay(
            object factory,
            GuiFormat format)
        {
            ReplaySettingsStore.EnsureLoaded();
            if (ReplaySettingsStore.Current.SaveReplayInPauseMenu)
                return null;
            return UIApi.CreateFeedbackButton(
                "Save replay",
                ReplayMenuRegistration.SaveReplay);
        }

        public static TextButton CreateMainMenuReplays(
            object factory,
            GuiFormat format)
        {
            ReplaySettingsStore.EnsureLoaded();
            if (ReplaySettingsStore.Current.ReplaysInMainMenu)
                return null;
            return new TextButton(
                "Replays",
                UIApi.CreateMenuPage(
                    factory,
                    new ReplayLibraryPage(
                        delegate { UIApi.ContinueFromMainMenu(factory); })));
        }

        public static IReplayViewer CreateViewer(ReplayData replay)
        {
            return new ReplayViewerPage(replay);
        }

        public static bool OpenViewer(IReplayViewer viewer)
        {
            IUiPage page = viewer as IUiPage;
            return page != null
                && UIApi.Open(page, new UiModalOptions(false));
        }

        public static void ClosePauseMenu()
        {
            UIApi.ClosePauseMenu();
        }

        public static bool ReturnToMainMenu()
        {
            return UIApi.ReturnToMainMenu();
        }
    }
}
