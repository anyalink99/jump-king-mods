using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using JKRuntime.Input;
using JKRuntime.UI;
using JumpKing.Controller;

namespace JKRuntime
{
    internal static class MouseInputTests
    {
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Mouse input: " + message); }

        private sealed class Pad : IPad
        {
            internal int[] Pressed = new int[0];
            internal string Identifier = "pc_keyboard_jump_king";
            public int[] GetPressedButtons() { return Pressed; }
            public string ButtonToString(int button) { return button.ToString(); }
            public string GetSaveIdentifier() { return Identifier; }
            public string GetPrintName() { return "Keyboard"; }
            public bool IsConnected() { return true; }
            public PadBinding GetDefaultBind() { return new PadBinding { jump = new[] { 32 } }; }
        }

        private static void Main()
        {
            ButtonsAndCapture();
            ChordsAndWorker();
            PersistenceAndLayers();
            Console.WriteLine("[OK] Mouse input: 5 buttons, frame/worker mapping, mixed chords, OR holds, capture, focus/re-arm, persistence, layer removal");
        }

        private static void ButtonsAndCapture()
        {
            int[] virtualKeys = { 1, 2, 4, 5, 6 };
            for (int i = 0; i < virtualKeys.Length; i++)
            {
                int button = MouseButtons.Left + i, vk = virtualKeys[i];
                Check(MouseButtons.IsMouseButton(button) && MouseButtons.ToVirtualKey(button) == vk, "stable physical ID mapping");
                bool down = false, focus = true;
                Pad native = new Pad { Pressed = new[] { 65 } };
                Func<int, short> read = key => key == vk && down ? unchecked((short)0x8000) : (short)0;
                KeyboardMousePad frame = new KeyboardMousePad(native, read, () => focus);
                using (HighRateInputSampler sampler = new HighRateInputSampler(true, read, null))
                {
                    sampler.Configure(new[] { button }, true);
                    Check(frame.GetPressedButtons().Length == 1, "native keyboard kept");
                    sampler.PollSource(0);
                    down = true;
                    int[] pressed = frame.GetPressedButtons();
                    Check(Array.IndexOf(pressed, button) >= 0 && Array.IndexOf(pressed, 65) >= 0, "frame merges mouse and keys");
                    Check(frame.ButtonToString(button) == MouseButtons.GetLabel(button), "mouse labels");
                    sampler.PollSource(0);
                    JumpInputTransition edge;
                    Check(sampler.TryDequeue(out edge) && edge.Reliable && edge.IsDown, "worker reads mapped Win32 press");
                    ChordCapture capture = new ChordCapture(2);
                    int[] result;
                    Check(!capture.Update(new[] { button }, out result), "mouse capture begins");
                    down = false;
                    frame.GetPressedButtons(); sampler.PollSource(0);
                    Check(sampler.TryDequeue(out edge) && edge.Reliable && !edge.IsDown, "worker reads release");
                    capture.Update(new int[0], out result); capture.Update(new int[0], out result);
                    Check(capture.Update(new int[0], out result) && result[0] == button, "mouse capture completes");
                    focus = false; down = true;
                    Check(Array.IndexOf(frame.GetPressedButtons(), button) < 0, "unfocused mouse ignored");
                    focus = true;
                    Check(Array.IndexOf(frame.GetPressedButtons(), button) < 0, "held focus-return click ignored");
                    down = false; frame.GetPressedButtons(); down = true;
                    Check(Array.IndexOf(frame.GetPressedButtons(), button) >= 0, "neutral re-arms mouse");
                }
            }
        }

        private static void ChordsAndWorker()
        {
            HashSet<int> held = new HashSet<int>();
            Func<int, short> read = key => held.Contains(key) ? unchecked((short)0x8000) : (short)0;
            Pad native = new Pad();
            KeyboardMousePad mouse = new KeyboardMousePad(native, read, () => true);
            PadInstance instance = new PadInstance(mouse);
            int[] runtime = ChordVirtualizer.Apply(instance, "mouse-test", new[] { new UiChord(16, MouseButtons.X1), new UiChord(32) });
            int[][] physical = PhysicalBindings.ResolvePhysicalBinding(instance, runtime);
            Check(physical[0][0] == 16 && physical[0][1] == MouseButtons.X1, "public API expands mixed chord");
            using (HighRateInputSampler sampler = new HighRateInputSampler(true, read, null))
            {
                sampler.Configure(physical, new XInputJumpBinding[0], true);
                instance.GetPad().GetPressedButtons(); sampler.PollSource(0);
                held.Add(5); sampler.PollSource(0);
                JumpInputTransition edge;
                Check(!sampler.TryDequeue(out edge) && Array.IndexOf(instance.GetPad().GetPressedButtons(), runtime[0]) < 0, "partial chord inactive in both paths");
                held.Add(16); native.Pressed = new[] { 16 }; sampler.PollSource(0);
                Check(sampler.TryDequeue(out edge) && edge.IsDown, "mixed chord worker down");
                Check(Array.IndexOf(instance.GetPad().GetPressedButtons(), runtime[0]) >= 0, "mixed chord frame down");
                held.Add(32); sampler.PollSource(0); held.Remove(5); sampler.PollSource(0);
                Check(!sampler.TryDequeue(out edge), "alternate key keeps same hold alive");
                held.Remove(32); sampler.PollSource(0);
                Check(sampler.TryDequeue(out edge) && !edge.IsDown, "last alternative releases");
                // A rebind while held cannot manufacture a complete measurement.
                sampler.Configure(new[] { MouseButtons.X1 }, true);
                sampler.ObservePhysical(sampler.ConfigurationGeneration, 0, false, false, System.Diagnostics.Stopwatch.GetTimestamp());
                held.Add(5); sampler.PollSource(0);
                Check(!sampler.TryDequeue(out edge), "held unavailable source does not create press");
                held.Remove(5); sampler.PollSource(0); held.Add(5); sampler.PollSource(0);
                Check(sampler.TryDequeue(out edge) && edge.IsDown && edge.Reliable, "worker neutral re-arm");
            }
            ChordVirtualizer.Remove(instance, "mouse-test");
            Check(ReferenceEquals(instance.GetPad(), mouse), "removing chords retains mouse extension");
            Check(ReferenceEquals(KeyboardMousePad.Wrap(mouse), mouse), "mouse wrapping is idempotent");
        }

        private static void PersistenceAndLayers()
        {
            UiChord[] values = { new UiChord(MouseButtons.Left), new UiChord(16, MouseButtons.Right), new UiChord(32) };
            int[] safe = ChordVirtualizer.NativeAlternatives(new Pad(), values);
            Check(safe.Length == 1 && safe[0] == 32, "native profile excludes entire mouse chords");
            Pad controller = new Pad { Identifier = "xbox_pad_jump_kingOne" };
            Check(ReferenceEquals(KeyboardMousePad.Wrap(controller), controller), "controller untouched");
            safe = ChordVirtualizer.NativeAlternatives(controller, new[] { new UiChord(65536) });
            Check(safe.Length == 1 && safe[0] == 65536, "controller button namespace never filtered as mouse");
            UIApiSettings settings = new UIApiSettings { BindingChords = new[] {
                new UiChordSettingsEntry { Id = "jump-king.pc_keyboard_jump_king.jump", Chords = new[] { values[0].Buttons, values[1].Buttons } }
            } };
            XmlSerializer serializer = new XmlSerializer(typeof(UIApiSettings));
            string xml;
            using (StringWriter writer = new StringWriter()) { serializer.Serialize(writer, settings); xml = writer.ToString(); }
            using (StringReader reader = new StringReader(xml)) settings = (UIApiSettings)serializer.Deserialize(reader);
            Check(settings.BindingChords[0].Chords[0][0] == MouseButtons.Left
                && settings.BindingChords[0].Chords[1][1] == MouseButtons.Right, "extended mouse IDs persist without loss");
        }
    }
}
