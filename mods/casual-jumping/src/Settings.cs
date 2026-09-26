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
        public ControlMode Mode { get; set; }

        public CasualJumpingSettings()
        {
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
            EnsureLoaded();
            var next = new CasualJumpingSettings { Mode = mode };
            file.Save(next);
            Current = next;
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
