using System;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;

namespace CasualJumping
{
    public enum ControlMode
    {
        Vanilla = 0,
        Casual = 1,
        CasualPlus = 2
    }

    [Serializable]
    public sealed class CasualJumpingSettings
    {
        public bool Enabled { get; set; }
        public ControlMode Mode { get; set; }

        public CasualJumpingSettings()
        {
            Enabled = true;
            Mode = ControlMode.CasualPlus;
        }
    }

    internal static class SettingsStore
    {
        private const string FileName = "CasualJumping.Settings.xml";
        private static bool loaded;
        private static JKRuntime.Settings.SettingsFile<CasualJumpingSettings> file;

        internal static CasualJumpingSettings Current { get; private set; }

        internal static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }
            loaded = true;
            file = new JKRuntime.Settings.SettingsFile<CasualJumpingSettings>(GetPath(), delegate { return new CasualJumpingSettings(); },
                delegate(CasualJumpingSettings value) { if (!Enum.IsDefined(typeof(ControlMode), value.Mode)) throw new InvalidDataException("Unsupported control mode"); });
            Current = file.Value;
        }

        internal static void SetMode(ControlMode mode)
        {
            JKRuntime.Gameplay.MapMechanics.RequireUserEnable(MapActivation.Id, mode != ControlMode.Vanilla);
            EnsureLoaded();
            var next = new CasualJumpingSettings { Mode = mode, Enabled = mode != ControlMode.Vanilla };
            file.Save(next);
            Current = next;
        }
        internal static void DisableForMap()
        {
            EnsureLoaded(); if (!Current.Enabled) return;
            var next = new CasualJumpingSettings { Mode = Current.Mode, Enabled = false }; file.Save(next); Current = next;
        }

        private static string GetPath()
        {
            string assemblyPath = Path.Combine(JKRuntime.PackageHost.GetDataDirectory(Assembly.GetExecutingAssembly()), "module.dll");
            string directory = Path.GetDirectoryName(assemblyPath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException("Casual Jumping assembly path is unavailable");
            }
            return Path.Combine(directory, FileName);
        }
    }
}
