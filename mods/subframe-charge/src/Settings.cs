using JKRuntime.Input;
using System;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;

namespace SubframeCharge
{
    [Serializable]
    public sealed class SubframeChargeSettings
    {
        public bool Enabled { get; set; }
        public bool ShowMeasurement { get; set; }
        public bool QuarterStepCharge { get; set; }
        public bool Optimizations { get; set; }
        public bool SubframeInputs { get; set; }
        public bool HighRefresh { get; set; }

        public void Normalize() { if (HighRefresh) SubframeInputs = true; }
        public void SetInputs(bool value) { SubframeInputs = value; if (!value) HighRefresh = false; }
        public void SetRefresh(bool value) { HighRefresh = value; if (value) SubframeInputs = true; }

        public SubframeChargeSettings()
        {
            Enabled = true;
            ShowMeasurement = true;
        }
    }

    internal static class SettingsStore
    {
        private const string FileName = "SubframeCharge.Settings.xml";
        private static bool loaded;
        private static JKRuntime.Settings.SettingsFile<SubframeChargeSettings> file;

        internal static SubframeChargeSettings Current { get; private set; }

        internal static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }
            loaded = true;
            file = new JKRuntime.Settings.SettingsFile<SubframeChargeSettings>(GetPath(FileName), delegate { return new SubframeChargeSettings(); });
            Current = file.Value;
            Current.Normalize();
            if (file.CanSave)
                try { JKRuntime.NativePerformance.ImportLegacy(Current.Optimizations); }
                catch (Exception error) { Console.WriteLine("[Subframe Charge] Native optimization preference was not migrated: " + error.Message); }
        }

        private static SubframeChargeSettings Copy()
        {
            EnsureLoaded();
            return new SubframeChargeSettings { Enabled=Current.Enabled, ShowMeasurement=Current.ShowMeasurement, QuarterStepCharge=Current.QuarterStepCharge,
                Optimizations=Current.Optimizations, SubframeInputs=Current.SubframeInputs, HighRefresh=Current.HighRefresh };
        }
        private static void Commit(SubframeChargeSettings next)
        { file.Save(next); Current=next; }
        internal static void SetOptimizations(bool value) { var next=Copy(); next.Optimizations=value; Commit(next); }
        internal static void SetQuarterSteps(bool value) { var next=Copy(); next.QuarterStepCharge=value; Commit(next); }
        internal static void SetInputs(bool value) { var next=Copy(); next.SetInputs(value); Commit(next); }
        internal static void SetRefresh(bool value) { var next=Copy(); next.SetRefresh(value); Commit(next); }

        internal static void SetEnabled(bool enabled)
        {
            var next = Copy(); next.Enabled = enabled; Commit(next);
        }

        internal static void SetShowMeasurement(bool visible)
        {
            var next = Copy(); next.ShowMeasurement = visible; Commit(next);
        }

        private static string GetPath(string fileName)
        {
            string assemblyPath = Path.Combine(JKRuntime.PackageHost.GetDataDirectory(Assembly.GetExecutingAssembly()), "module.dll");
            string directory = Path.GetDirectoryName(assemblyPath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException(
                    "Subframe Charge assembly path is unavailable");
            }
            return Path.Combine(directory, fileName);
        }
    }
}
