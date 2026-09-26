using System;
using System.Runtime.InteropServices;

namespace JKRuntime.Input
{
    // Extended keyboard-pad IDs, NOT Win32 virtual keys or controller button IDs.
    // Stable values are persisted in JKRuntime.Settings.xml and exposed by the
    // physical-binding API. Keyboard and mouse share one OR/chord input source.
    public static class MouseButtons
    {
        public const int Left = 0x10000;
        public const int Right = 0x10001;
        public const int Middle = 0x10002;
        public const int X1 = 0x10003;
        public const int X2 = 0x10004;

        public static bool IsMouseButton(int button) { return button >= Left && button <= X2; }

        public static int ToVirtualKey(int button)
        {
            switch (button)
            {
                case Left: return 1;
                case Right: return 2;
                case Middle: return 4;
                case X1: return 5;
                case X2: return 6;
                default: return button;
            }
        }

        public static string GetLabel(int button)
        {
            switch (button)
            {
                case Left: return "Mouse L";
                case Right: return "Mouse R";
                case Middle: return "Mouse M";
                case X1: return "Mouse 4";
                case X2: return "Mouse 5";
                default: throw new ArgumentOutOfRangeException("button");
            }
        }

        // Physical left/right, even with swapped Windows primary buttons.
        // Both frame input and the high-rate worker use this same convention.
        [DllImport("user32.dll")]
        internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();
    }
}
