using System;
using System.Reflection;
using EntityComponent;
using HarmonyLib;
using JumpKing;
using JumpKing.Controller;
using JumpKing.GameManager.TitleScreen;
using JumpKing.MiscEntities;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace SubframeCharge
{
    internal static class PerformanceFeatures
    {
        internal const string Id = "subframe-charge.presentation";
        private static bool installed;
        private static JKRuntime.OwnedPatches patches;
        internal static bool InputsRequested { get { return SettingsStore.Current != null && SettingsStore.Current.SubframeInputs; } }
        internal static bool RefreshRequested { get { return InputsRequested && SettingsStore.Current.HighRefresh; } }
        internal static void Apply()
        {
            SettingsStore.EnsureLoaded();
            if (InputsRequested) Install();
            if (InputsRequested) ResponsiveInput.PrepareKeyboard();
            ConnectCamera();
            DiagnosticLog.Write("features: subframeInputs="+InputsRequested+" presentation240="+RefreshRequested);
        }
        internal static void ConnectCamera() { }
        internal static void FindCameraInterval() { }
        private static MethodInfo Method(Type type, string name)
        { return AccessTools.Method(type, name); }
        internal static void Install()
        {
            FeatureClock.Ensure();
            if (installed) return;
            FeatureClock.AssertContracts();
            if (patches != null) { patches.Dispose(); patches = null; }
            if (JKRuntime.OwnedPatches.SharedEngine() != typeof(Harmony).Assembly) throw new InvalidOperationException("SFC Harmony ABI does not match the shared engine");
            var harmony = patches = new JKRuntime.OwnedPatches(Id);
            try
            {
                harmony.Add(AccessTools.Method(typeof(ControllerManager), "Update"), prefix: Method(typeof(ResponsiveInput), "BeforeNativeController"), transpiler: Method(typeof(ResponsiveInput), "RewriteControllerUpdate"));
                harmony.Add(AccessTools.Method(MenuCadence.Pause, "PauseUpdate"), prefix: Method(typeof(MenuCadence), "NativePauseUpdate"));
                harmony.Add(AccessTools.Method(typeof(JKRuntime.UI.UiPointer), "Update"), prefix: Method(typeof(MenuCadence), "NativePointerUpdate"));
                harmony.Add(AccessTools.Method(typeof(GameTitleScreen), "OnNewRun"), prefix: Method(typeof(MenuCadence), "TitleStart"));
                harmony.Add(AccessTools.Method(typeof(GameTitleScreen), "MyRun"), transpiler: Method(typeof(MenuCadence), "RewriteTitle"));
                harmony.Add(AccessTools.Method(typeof(JumpGame), "Update"), postfix: Method(typeof(PlayerPresentation), "AfterUpdate"));
                harmony.Add(AccessTools.Method(typeof(PlayerEntity), "Draw"), transpiler: Method(typeof(PlayerPresentation), "RewritePlayer"));
                harmony.Add(AccessTools.Method(typeof(Game1), "OnExiting"), prefix: Method(typeof(ResponsiveInput), "Reset"));
                installed = true;
            }
            catch (Exception failure) { try { Uninstall(); } catch (Exception cleanup) { throw new AggregateException("SFC hook rollback failed", failure, cleanup); } throw; }
        }
        internal static void Uninstall()
        {
            var release = new JKRuntime.RuntimeScope();
            release.Defer(delegate { if (patches != null) { patches.Dispose(); patches = null; } });
            release.Defer(PlayerPresentation.Reset); release.Defer(MenuCadence.Reset);
            release.Defer(ResponsiveInput.Reset); release.Defer(FeatureClock.Release);
            release.Dispose(); installed = false;
        }
    }
}
