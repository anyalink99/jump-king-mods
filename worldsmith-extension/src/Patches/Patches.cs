using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace WorldsmithExtension
{
    internal static class Patches
    {
        static readonly Harmony Harmony = new Harmony("worldsmith.extension");
        internal static void Install()
        {
            EditorTypography.Install(Harmony);
            ProjectHistory.Install(Harmony);
            var scrolling = Engine.Type("JKWorldsmith.Models.PageModels.Screen.ScrollingImagesModel");
            foreach (string method in new[] { "Load", "Save" })
                Harmony.Patch(AccessTools.Method(scrolling, method), new HarmonyMethod(typeof(ScrollingSettings), method));
            var audio = Engine.Type("JKWorldsmith.Models.PageModels.Screen.AudioModel");
            Harmony.Patch(AccessTools.Method(audio, "Load"), new HarmonyMethod(typeof(AudioSettings), "Loading"));
            Harmony.Patch(AccessTools.Method(audio, "GetCurrentAudio"), new HarmonyMethod(typeof(AudioSettings), "Current"));
            Harmony.Patch(AccessTools.Method(audio, "SetCurrentAudio"), new HarmonyMethod(typeof(AudioSettings), "Set"));
            Patch("JKWorldsmith.ViewModels.MainWindowViewModel", "CmdArgsDealer", "NativeArguments");
            Harmony.Patch(AccessTools.Method(Engine.Type("JKWorldsmith.ViewModels.MainWindowViewModel"), "InitializePage"), Method("PageStarting"), Method("PageReady"));
            Patch("JKWorldsmith.ViewModels.DashboardViewModel", "LoadRSS", "LoadNews");
            Harmony.Patch(AccessTools.Method(Engine.Type("JKWorldsmith.ViewModels.DashboardViewModel"), "OnNavigatedTo"), null, Method("RefreshNews"));
            Patch("JKWorldsmith.Converters.AppIconToImageConverter", "Convert", "EditorIcon");
            Harmony.Patch(AccessTools.Method(Engine.Type("JKWorldsmith.Models.PageModels.Screen.PropsModel"), "GetProps"), new HarmonyMethod(typeof(ProjectSafety).GetMethod("LoadProps", BindingFlags.Static | BindingFlags.NonPublic)));
            Harmony.Patch(AccessTools.Method(Engine.Type("JKWorldsmith.Models.PageModels.Screen.PropsModel"), "GetPropSettings"), new HarmonyMethod(typeof(ProjectSafety).GetMethod("ReadSettings", BindingFlags.Static | BindingFlags.NonPublic)));
            foreach (string method in new[]{"GetPropSettings", "GetProps"})
                Harmony.Patch(AccessTools.Method(Engine.Type("JKWorldsmith.Models.PageModels.Screen.PropsModel"), method), null, null, null, new HarmonyMethod(typeof(ProjectSafety).GetMethod("PropLoadFinished", BindingFlags.Static | BindingFlags.NonPublic)));
            Harmony.Patch(AccessTools.Method(Engine.Type("JKWorldsmith.Extensions.DisposableImage"), "Get"), new HarmonyMethod(typeof(ProjectSafety).GetMethod("LocalImage", BindingFlags.Static | BindingFlags.NonPublic)));
            Patch("XmlSerializerHelper", "SafeSerialize", "SaveXml");
            Patch("JKWorldsmith.ViewModels.Level.HitboxViewModel", "GenerateInGameLevelHitbox", "Generate");
            Patch("JKWorldsmith.ViewModels.Level.HitboxViewModel", "InitializeHitbox", "InitializeCollision");
            Patch("JKWorldsmith.ViewModels.Level.HitboxViewModel", "ReplaceHitbox", "Import");
            Patch("JKWorldsmith.ViewModels.Level.HitboxViewModel", "get_CanFrameNumberUp", "CanUp");
            Patch("JKWorldsmith.ViewModels.Level.HitboxViewModel", "GetPreview", "Preview");
            Harmony.Patch(AccessTools.Method(Engine.Type("JKWorldsmith.Models.Attributes.InexistentScreenAttribute"), "IsValid", new[]{typeof(object)}), Method("ScreenValid"));
            Patch("JKWorldsmith.Models.XNBConverters", "ToTexture2D", "Texture");
            Patch("JKWorldsmith.Models.XNBConverters", "ToSound", "Sound");
            Patch("JKWorldsmith.Models.FileWatcher.ProperFileWatcher", "WaitForFile", "WaitFile");
            foreach (var method in Engine.Type("JKWorldsmith.Models.FileWatcher.ProperFileWatcher").GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(m => m.Name == "WaitForChanged"))
                Harmony.Patch(method, new HarmonyMethod(typeof(LoadState).GetMethod("Watch", BindingFlags.Static | BindingFlags.NonPublic)));
            Harmony.Patch(AccessTools.Method(Engine.Type("JKWorldsmith.Models.Projects.CurrentProjectManager"), "SetCurrentProject"),
                new HarmonyMethod(typeof(LoadState).GetMethod("Starting", BindingFlags.Static | BindingFlags.NonPublic)), null, null,
                new HarmonyMethod(typeof(LoadState).GetMethod("StartFinished", BindingFlags.Static | BindingFlags.NonPublic)));
            LoadState.Register();
            Harmony.Patch(AccessTools.Method(Engine.Type("JKWorldsmith.Shared.Structs.LayerAnimationObject"), "FromStruct"),
                new HarmonyMethod(typeof(ProjectSafety).GetMethod("LoadLayerAnimation", BindingFlags.Static | BindingFlags.NonPublic)));
            FileSafety.Install(Harmony);
            Harmony.Patch(AccessTools.Method(Engine.Type("JKWorldsmith.Views.Windows.MainWindow"), "TestInGameClick"), new HarmonyMethod(typeof(GameTest).GetMethod("Start", BindingFlags.Static | BindingFlags.NonPublic)));
            Patch("JKWorldsmith.Views.Pages.Shared.DetailsPage", "OpenFlyout", "ProjectAction");
            foreach (var target in new[]{Tuple.Create("JKWorldsmith.Views.Pages.Shared.DetailsPage", "DeleteEverythingDialog"), Tuple.Create("JKWorldsmith.ViewModels.Shared.DetailsViewModel", "DeleteEverything")})
                Harmony.Patch(AccessTools.Method(Engine.Type(target.Item1), target.Item2), new HarmonyMethod(typeof(ProjectArchive).GetMethod("Prompt", BindingFlags.Static | BindingFlags.NonPublic)));
            Patch("JKWorldsmith.Shared.Workshop.Workshop", "IsAMod", "IsModProject");
            Patch("JKWorldsmith.ViewModels.Shared.DetailsViewModel", "LinkProject", "LinkWorkshopProject");
            Patch("JKWorldsmith.ViewModels.Shared.DetailsViewModel", "UnlinkProject", "UnlinkWorkshopProject");
            Patch("JKWorldsmith.ViewModels.DashboardViewModel", "OpenProject", "OpenRecentProject");
            Patch("JKWorldsmith.Views.Pages.DashboardPage", "LoadProjectButton_Click", "BrowseFromHome");
            Harmony.Patch(AccessTools.Method(Engine.Type("JKWorldsmith.Views.Pages.DashboardPage"), "InitializeComponent"), null, Method("DashboardReady"));
            Harmony.Patch(AccessTools.Method(Engine.Type("JKWorldsmith.Views.Pages.Shared.DetailsPage"), "InitializeComponent"), null, Method("DetailsReady"));
            foreach (var pair in new[]{Tuple.Create("StartAnimation", "Start"), Tuple.Create("ChangeSprite", "Tick")})
                Harmony.Patch(AccessTools.Method(Engine.Type("JKWorldsmith.Controls.SpriteImage"), pair.Item1), new HarmonyMethod(typeof(SpritePlayback).GetMethod(pair.Item2, BindingFlags.Static | BindingFlags.NonPublic)));
            Harmony.Patch(AccessTools.Method(Engine.Type("JKWorldsmith.ViewModels.Level.WardrobeViewModel"), "LoadSettings"),
                new HarmonyMethod(typeof(WardrobeSettings).GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic)));
            foreach (string method in new[]{"SaveSkinSettings", "SaveCosmeticSettings", "SaveSetSettings"})
                Patch("JKWorldsmith.ViewModels.Level.WardrobeViewModel", method, "SaveWardrobe");
            Patch("JKWorldsmith.Models.Projects.CurrentProjectManager", "CheckForNewFiles", "Scan");
            Patch("JKWorldsmith.Models.Projects.CurrentProjectManager", "CreateAllFolders", "CreateOutputFolders");
            Patch("JKWorldsmith.Models.Steamworks.SteamManager", "TryDeleteSteamAppID", "Allow");
            var restart = typeof(Steamworks.SteamAPI).GetMethod("RestartAppIfNecessary");
            Harmony.Patch(restart, Method("NoRestart"));
            foreach (string category in new[] { "Level", "Skin", "Set", "Mod" })
            {
                string page = "JKWorldsmith.Views.Pages.Workshop.Workshop" + category + "Page";
                Harmony.Patch(AccessTools.Method(Engine.Type(page), "InitializeComponent"), null, Method("WorkshopReady"));
            }
            Harmony.Patch(AccessTools.Method(Engine.Type("JKWorldsmith.Views.Pages.Workshop.WorkshopSummaryPage"), "InitializeComponent"), null, Method("WorkshopSummaryReady"));
            foreach (string name in new[]{"Upload", "Update"})
                Patch("JKWorldsmith.Views.Pages.Shared.DetailsPage", name, "OpenPublish");
            Patch("JKWorldsmith.Models.Steamworks.SteamManager", "OnUpdatedItem", "CheckSubmit");
            var ctor = Engine.Type("JKWorldsmith.Views.Windows.MainWindow").GetConstructors().Single();
            Harmony.Patch(ctor, Method("WindowStarting"), Method("WindowCreated"));
            Harmony.Patch(AccessTools.Method(Engine.Type("JKWorldsmith.Views.Windows.MainWindow"), "InitializeComponent"), null, null, null, Method("WindowFailure"));
            foreach (string type in new[]{"JKWorldsmith.Models.PageModels.Screen.PropsModel", "JKWorldsmith.Models.PageModels.Screen.ScreensModel", "JKWorldsmith.ViewModels.Level.HitboxViewModel", "JKWorldsmith.ViewModels.Level.LocationViewModel", "JKWorldsmith.ViewModels.Level.WardrobeViewModel"})
            {
                var method = AccessTools.Method(Engine.Type(type), "OnNewProject");
                if (method != null)
                    Harmony.Patch(method, Method("LoadStarting"), null, null, Method("LoadFinished"));
            }
        }

        static HarmonyMethod Method(string name)
        {
            var handlers = new[] { typeof(CollisionPatches), typeof(CompilerPatches), typeof(SavePatches), typeof(WorkshopPatches), typeof(EditorPatches) };
            var method = handlers.Select(type => type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)).Single(value => value != null);
            return new HarmonyMethod(method);
        }

        static void Patch(string type, string method, string prefix)
        {
            var m = AccessTools.Method(Engine.Type(type), method);
            if (m == null)
                throw new MissingMethodException(type, method);
            Harmony.Patch(m, Method(prefix));
        }

    }
}
