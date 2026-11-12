using System;
using System.IO;
using JKRuntime;
using JKRuntime.Settings;

namespace FreeScaling
{
    public sealed class Preferences
    {
        public int Schema = 1;
        public bool Enabled;
        public PixelSampling Sampling = PixelSampling.Sharp;
        public int Width, Height;
        public bool HasPosition;
        public int X, Y;
        public Preferences Copy() { return (Preferences)MemberwiseClone(); }
        public static void Validate(Preferences value)
        {
            if (value.Schema != 1 || !Enum.IsDefined(typeof(PixelSampling), value.Sampling)) throw new InvalidDataException("Unsupported Graphics+ settings");
            if ((value.Width == 0) != (value.Height == 0) || value.Width < 0 || value.Height < 0 || value.Width > 32768 || value.Height > 32768)
                throw new InvalidDataException("Invalid saved window dimensions");
            if (InvalidCoordinate(value.X) || InvalidCoordinate(value.Y)) throw new InvalidDataException("Invalid saved window position");
        }
        private static bool InvalidCoordinate(int value) { return value < -1000000 || value > 1000000; }
    }
    internal static class Settings
    {
        internal static SettingsFile<Preferences> File;
        internal static Preferences Current { get { return File.Value; } }
        internal static void Load()
        {
            if (File != null) return;
            File = new SettingsFile<Preferences>(Path.Combine(PackageHost.GetDataDirectory(typeof(ModEntry).Assembly), "FreeScaling.Settings.xml"),
                () => new Preferences(), Preferences.Validate);
        }
    }
}
