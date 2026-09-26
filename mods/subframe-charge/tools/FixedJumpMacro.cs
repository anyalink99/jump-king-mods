using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;

namespace SubframeCharge.Tools
{
    internal static class FixedJumpMacro
    {
        private const int VkSpace = 0x20;
        private const int VkF8 = 0x77;
        private const int VkF9 = 0x78;
        private const uint InputKeyboard = 1;
        private const uint KeyEventKeyUp = 0x0002;

        private static volatile bool exitRequested;
        private static bool spaceInjectedDown;

        private static int Main(string[] args)
        {
            double holdMilliseconds;
            if (!TryGetHoldMilliseconds(args, out holdMilliseconds))
            {
                Console.Error.WriteLine(
                    "Usage: FixedJumpMacro.exe [hold-ms]  (example: 28)");
                return 2;
            }

            Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs eventArgs)
            {
                eventArgs.Cancel = true;
                exitRequested = true;
            };

            int exitCode = 0;
            bool timerPeriodSet = timeBeginPeriod(1) == 0;
            try
            {
                int expectedInputSize = IntPtr.Size == 8 ? 40 : 28;
                int actualInputSize = Marshal.SizeOf(typeof(Input));
                if (actualInputSize != expectedInputSize)
                {
                    throw new InvalidOperationException(
                        "Unexpected Win32 INPUT size: " + actualInputSize);
                }

                Thread.CurrentThread.Priority = ThreadPriority.Highest;
                Console.WriteLine("Fixed Jump Macro");
                Console.WriteLine(
                    "F8: one Space press held for {0:0.###} ms",
                    holdMilliseconds);
                Console.WriteLine("F9 or Ctrl+C: exit");
                Console.WriteLine(
                    "Trigger only while the king is grounded and Space is released.");

                bool f8WasDown = IsKeyDown(VkF8);
                bool f9WasDown = IsKeyDown(VkF9);
                int jumpNumber = 0;

                while (!exitRequested)
                {
                    bool f9IsDown = IsKeyDown(VkF9);
                    if (f9IsDown && !f9WasDown)
                    {
                        break;
                    }
                    f9WasDown = f9IsDown;

                    bool f8IsDown = IsKeyDown(VkF8);
                    if (f8IsDown && !f8WasDown)
                    {
                        if (IsKeyDown(VkSpace))
                        {
                            Console.WriteLine(
                                "Skipped: Space was already held when F8 was pressed.");
                        }
                        else
                        {
                            jumpNumber++;
                            RunJump(jumpNumber, holdMilliseconds);
                        }
                    }
                    f8WasDown = f8IsDown;
                    Thread.Sleep(1);
                }
            }
            catch (Exception error)
            {
                exitCode = 1;
                Console.Error.WriteLine();
                Console.Error.WriteLine("Macro error: " + error);
                Console.Error.WriteLine("Press Enter to close.");
                Console.ReadLine();
            }
            finally
            {
                ReleaseInjectedSpace(false);
                if (timerPeriodSet)
                {
                    timeEndPeriod(1);
                }
            }

            return exitCode;
        }

        private static void RunJump(int jumpNumber, double holdMilliseconds)
        {
            long requestedTicks = (long)Math.Round(
                holdMilliseconds * Stopwatch.Frequency / 1000.0,
                MidpointRounding.AwayFromZero);

            SendSpace(true, true);
            spaceInjectedDown = true;
            try
            {
                long started = Stopwatch.GetTimestamp();
                long deadline = started + requestedTicks;

                while (true)
                {
                    long remaining = deadline - Stopwatch.GetTimestamp();
                    if (remaining <= 0)
                    {
                        break;
                    }

                    double remainingMilliseconds =
                        remaining * 1000.0 / Stopwatch.Frequency;
                    if (remainingMilliseconds > 2.0)
                    {
                        Thread.Sleep(1);
                    }
                    else
                    {
                        Thread.SpinWait(32);
                    }
                }

                ReleaseInjectedSpace(true);
                long finished = Stopwatch.GetTimestamp();
                double measuredMilliseconds =
                    (finished - started) * 1000.0 / Stopwatch.Frequency;
                Console.WriteLine(
                    "Jump {0}: requested {1:0.###} ms, macro measured {2:0.###} ms",
                    jumpNumber,
                    holdMilliseconds,
                    measuredMilliseconds);
            }
            finally
            {
                ReleaseInjectedSpace(false);
            }
        }

        private static void ReleaseInjectedSpace(bool throwOnFailure)
        {
            if (!spaceInjectedDown)
            {
                return;
            }
            SendSpace(false, throwOnFailure);
            spaceInjectedDown = false;
        }

        private static void SendSpace(bool down, bool throwOnFailure)
        {
            Input input = new Input();
            input.Type = InputKeyboard;
            input.Data.Keyboard.VirtualKey = VkSpace;
            input.Data.Keyboard.Flags = down ? 0u : KeyEventKeyUp;

            Input[] inputs = new Input[] { input };
            uint sent = SendInput(
                1,
                inputs,
                Marshal.SizeOf(typeof(Input)));
            if (sent != 1 && throwOnFailure)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        private static bool IsKeyDown(int virtualKey)
        {
            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        private static bool TryGetHoldMilliseconds(
            string[] args,
            out double holdMilliseconds)
        {
            holdMilliseconds = 28.0;
            if (args.Length == 0)
            {
                return true;
            }
            if (args.Length != 1
                || !double.TryParse(
                    args[0],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out holdMilliseconds))
            {
                return false;
            }
            return holdMilliseconds >= 1.0 && holdMilliseconds <= 590.0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Input
        {
            internal uint Type;
            internal InputUnion Data;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            internal KeyboardInput Keyboard;

            // INPUT is sized for its largest native union member. Without the
            // mouse member a 64-bit CLR produces 32 bytes instead of Win32's
            // required 40-byte INPUT structure and SendInput rejects it.
            [FieldOffset(0)]
            internal MouseInput Mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput
        {
            internal ushort VirtualKey;
            internal ushort ScanCode;
            internal uint Flags;
            internal uint Time;
            internal UIntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput
        {
            internal int X;
            internal int Y;
            internal uint MouseData;
            internal uint Flags;
            internal uint Time;
            internal UIntPtr ExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(
            uint inputCount,
            Input[] inputs,
            int inputSize);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint period);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint period);
    }
}
