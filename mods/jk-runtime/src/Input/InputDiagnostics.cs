using System;
using System.Collections.Generic;
using System.Diagnostics;
using JumpKing.Controller;

namespace JKRuntime.Input
{
    // Reads the game's cached Jump state, never polls/consumes physical buttons.
    // Optional metadata failures must not disable input or crash gameplay.
    public sealed class InputDiagnostics
    {
        private sealed class Device
        {
            public string Id;
            public string Description;
            public bool Down;
            public bool Seen;
        }

        private readonly Dictionary<PadInstance, Device> devices = new Dictionary<PadInstance, Device>();
        private readonly Func<PadInstance, bool> isKeyboard;
        private readonly Func<PadInstance, int> xinputSlot;
        private readonly Action<string> write;
        private readonly Func<Guid, bool> directInputReady;
        private readonly string context = Guid.NewGuid().ToString("N").Substring(0, 8);
        private int nextId;
        private long nextMetadata;
        private string lastError;
        private readonly List<string> active = new List<string>();
        private readonly List<PadInstance> removed = new List<PadInstance>();
        public string ActiveDevices { get; private set; }

        public InputDiagnostics(Func<PadInstance, bool> keyboard,
            Func<PadInstance, int> slot, Action<string> sink, Func<Guid, bool> legacyReady = null)
        {
            isKeyboard = keyboard;
            xinputSlot = slot;
            write = sink;
            directInputReady = legacyReady;
            ActiveDevices = "none";
        }

        public void Observe()
        {
            try
            {
                ControllerManager manager = ControllerManager.instance;
                if (manager != null) ObservePads(BindingSnapshot.Registered(), Stopwatch.GetTimestamp());
            }
            catch (Exception error) { ReportError(error); }
        }

        public void ObservePads(List<PadInstance> connected, long now)
        {
            bool metadata = now >= nextMetadata;
            if (metadata) nextMetadata = now + Stopwatch.Frequency;
            foreach (Device device in devices.Values) device.Seen = false;
            active.Clear();
            bool changed = false;
            foreach (PadInstance instance in connected)
            {
                if (instance == null) continue;
                try
                {
                    Device device;
                    bool added = !devices.TryGetValue(instance, out device);
                    if (added)
                    {
                        changed = true;
                        device = new Device { Id = context + "/" + (++nextId) };
                        devices.Add(instance, device);
                    }
                    device.Seen = true;
                    bool down = instance.GetState().jump;
                    if (added || metadata || down != device.Down)
                    {
                        string description = Describe(instance);
                        if (description != device.Description)
                        {
                            write("input device id=" + device.Id + " registered=True " + description);
                            device.Description = description;
                        }
                    }
                    if (added || down != device.Down)
                    {
                        changed = true;
                        write("input device jump id=" + device.Id + " edge="
                            + (down ? "down" : "up") + " initial=" + added);
                    }
                    device.Down = down;
                    if (down) active.Add(device.Id);
                }
                catch (Exception error) { ReportError(error); }
            }
            removed.Clear();
            foreach (KeyValuePair<PadInstance, Device> item in devices)
            {
                if (!item.Value.Seen)
                {
                    write("input device id=" + item.Value.Id + " connected=False wasJumpDown=" + item.Value.Down);
                    removed.Add(item.Key);
                    changed = true;
                }
            }
            foreach (PadInstance instance in removed) devices.Remove(instance);
            if (changed) ActiveDevices = active.Count == 0 ? "none" : string.Join(",", active.ToArray());
        }

        private string Describe(PadInstance instance)
        {
            IPad pad = instance.GetPad();
            if (pad == null) return "reason=missing-pad";
            string backend = "unknown";
            Guid directId;
            bool directCandidate = DirectInputDevice.TryGetDeviceId(instance, out directId);
            bool directReady = directCandidate && directInputReady != null && directInputReady(directId);
            int slot = -1;
            string classificationError = "none";
            try
            {
                slot = xinputSlot(instance);
                backend = isKeyboard(instance) ? "keyboard" : slot >= 0 ? "xinput"
                    : directCandidate || pad.GetType().FullName == "JumpKing.Controller.LegacyPad" ? "legacy-directinput" : "unknown";
            }
            catch (Exception error) { classificationError = error.GetType().Name; }
            PadBinding binding = instance.GetBind();
            int[] raw = binding == null ? null : binding.jump;
            int[][] physical = PhysicalBindings.ResolvePhysicalBinding(instance,
                binding != null && binding.Enabled ? raw : null);
            string reason = classificationError != "none" ? "classification-error"
                : binding == null ? "missing-binding" : !binding.Enabled ? "binding-disabled"
                : backend == "unknown" ? "unrecognized-backend"
                : physical.Length == 0 ? "no-resolved-jump-binding"
                : backend == "legacy-directinput" ? (!directCandidate ? "directinput-id-unavailable"
                    : directReady ? "directinput-sampling" : "directinput-not-ready") : "eligible-check-sampler-config";
            List<string> names = new List<string>();
            if (raw != null) foreach (int button in raw)
            {
                int code = button;
                names.Add(code + ":" + Safe(delegate { return pad.ButtonToString(code); }));
            }
            List<string> chords = new List<string>();
            foreach (int[] chord in physical)
                chords.Add(string.Join("+", Array.ConvertAll(chord, value => value.ToString())));
            string product = "unavailable";
            if (pad.GetType().FullName == "JumpKing.Controller.LegacyPad")
                product = Safe(delegate
                {
                    // Installed LegacyPad.Pad -> SlimPad.ProductGuid contract.
                    object slim = pad.GetType().GetProperty("Pad").GetValue(pad, null);
                    return slim.GetType().GetProperty("ProductGuid").GetValue(slim, null).ToString();
                });
            return "type=" + Quote(pad.GetType().FullName)
                + " assembly=" + Quote(pad.GetType().Assembly.GetName().FullName)
                + " name=" + Quote(Safe(pad.GetPrintName))
                + " identifier=" + Quote(Safe(pad.GetSaveIdentifier))
                + " productGuid=" + Quote(product)
                + " backend=" + backend + " xinputSlot=" + slot
                + " directinputReady=" + directReady
                + " bindingEnabled=" + (binding != null && binding.Enabled)
                + " jumpRuntime=" + Quote(string.Join(",", names.ToArray()))
                + " jumpPhysical=" + Quote(string.Join(",", chords.ToArray()))
                + " reason=" + reason + " classificationError=" + classificationError;
        }

        private void ReportError(Exception error)
        {
            string name = error.GetType().FullName;
            if (name != lastError) write("input diagnostics unavailable error=" + name);
            lastError = name;
        }

        private static string Safe(Func<string> read)
        {
            try { return read() ?? "null"; }
            catch (Exception error) { return "unavailable:" + error.GetType().Name; }
        }

        private static string Quote(string value)
        {
            value = value ?? "null";
            if (value.Length > 512) value = value.Substring(0, 512);
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
        }
    }
}
