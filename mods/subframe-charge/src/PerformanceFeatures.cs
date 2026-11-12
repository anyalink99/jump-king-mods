using System;
using System.Reflection;
using System.Collections.Generic;
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
        private static IDisposable presentation, controls;
        internal static bool InputsRequested { get { return SubframePhysics.Active || !PerformanceSuspension.Active && SettingsStore.Current != null && SettingsStore.Current.SubframeInputs; } }
        internal static bool RefreshAvailable { get { return !PerformanceSuspension.Active && SettingsStore.Current != null; } }
        internal static bool RefreshRequested { get { return RefreshAvailable && JKRuntime.Presentation.SmoothRendering.Enabled; } }
        internal static void Apply()
        {
            SettingsStore.EnsureLoaded();
            EnsurePresentation();
            if (InputsRequested || RefreshRequested) Install();
            if (InputsRequested) JKRuntime.Input.ResponsiveControls.PrepareKeyboard();
            ConnectCamera();
            DiagnosticLog.Write("features: subframeInputs="+InputsRequested+" presentation240="+RefreshRequested);
        }
        internal static void ConnectCamera() { }
        internal static void FindCameraInterval() { }
        private static MethodInfo Method(Type type, string name)
        { return AccessTools.Method(type, name); }
        internal static void Install()
        {
            EnsurePresentation();
            FeatureClock.Ensure();
            if (installed) { PrepareDrawAdapters(); return; }
            FeatureClock.AssertContracts();
            if (patches != null) { patches.Dispose(); patches = null; }
            if (JKRuntime.OwnedPatches.SharedEngine() != typeof(Harmony).Assembly) throw new InvalidOperationException("SFC Harmony ABI does not match the shared engine");
            var harmony = patches = new JKRuntime.OwnedPatches(Id);
            try
            {
                harmony.Add(AccessTools.Method(typeof(JumpGame), "Update"), postfix: Method(typeof(PlayerPresentation), "AfterUpdate"));
                PrepareDrawAdapters();
                installed = true;
            }
            catch (Exception failure) { try { Uninstall(); } catch (Exception cleanup) { throw new AggregateException("SFC hook rollback failed", failure, cleanup); } throw; }
        }
        internal static void PrepareDrawAdapters()
        {
            if(JKRuntime.Presentation.SmoothRendering.Enabled)JKRuntime.Presentation.SmoothRendering.Prepare();
        }
        internal static void EnsurePresentation()
        {
            if(presentation==null)presentation=JKRuntime.Presentation.SmoothRendering.Register("subframe-charge",()=>!PerformanceSuspension.Active,()=>SubframePhysics.Active);
            if(controls==null)controls=JKRuntime.Input.ResponsiveControls.Register("subframe-charge",()=>InputsRequested,true,SubframeChargeInstaller.OwnsCompletedTap);
        }
        internal static void Uninstall()
        {
            var release = new JKRuntime.RuntimeScope();
            release.Defer(delegate { if (patches != null) { patches.Dispose(); patches = null; } });
            release.Defer(PlayerPresentation.Reset); release.Defer(FeatureClock.Release);
            release.Defer(delegate { if(controls!=null){controls.Dispose();controls=null;} });
            release.Defer(delegate { if(presentation!=null){presentation.Dispose();presentation=null;} });
            release.Dispose(); installed = false;
        }
    }
}
