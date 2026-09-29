using System;
using System.IO;

namespace WorldsmithExtension
{
    internal static class WorkshopPatches
    {
        static void WorkshopReady(object __instance) { WorkshopUI.Attach((System.Windows.Controls.Page)__instance); }
        static void WorkshopSummaryReady(object __instance) { WorkshopUI.AttachSummary((System.Windows.Controls.Page)__instance); }
        static bool LinkWorkshopProject(object possible_steamugcdetails)
        {
            try { WorkshopCatalog.Link(Engine.ProjectRoot, NativeWorkshop.FromNative(possible_steamugcdetails)); return true; }
            catch (Exception error) { Panel.Error(error); return false; }
        }

        static bool UnlinkWorkshopProject()
        {
            try
            {
                if (Operations.Current.Busy || LoadState.Busy) throw new InvalidOperationException("Finish the current operation before unlinking the project.");
                WorkshopCatalog.Library.Link(Engine.ProjectRoot, 0);
                return true;
            }
            catch (Exception error) { Panel.Error(error); return false; }
        }

        static bool NoRestart(ref bool __result)
        {
            __result = false;
            return false;
        }

        static bool Allow(ref bool __result)
        {
            __result = true;
            return false;
        }

        static bool OpenPublish()
        {
            Panel.Open();
            return false;
        }

        static bool CheckSubmit(Steamworks.SubmitItemUpdateResult_t param, bool bIOFailure)
        {
            if (!bIOFailure && param.m_eResult == Steamworks.EResult.k_EResultOK)
                return true;
            Panel.Error(new IOException("Steam: " + param.m_eResult + "; transport failure=" + bIOFailure));
            return false;
        }
    }
}
