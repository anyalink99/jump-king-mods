using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using JumpKing.Controller;

namespace JKRuntime.Input
{
    public sealed class BindingSnapshot
    {
        private static readonly FieldInfo Pads = typeof(ControllerManager).GetField("m_pads",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly List<PadInstance> Empty = new List<PadInstance>();
        private sealed class Entry
        {
            public PadInstance Instance;
            public IPad Pad;
            public bool Enabled;
            public int[] Keys;
        }
        private readonly List<Entry> entries = new List<Entry>();
        private long nextRefresh;
        private int apiEpoch = -1;

        // Reading IsConnected would poll the driver's API AGAIN on the game
        // thread. Native registered pads already carry cached per-frame input;
        // availability is established by our independent device workers.
        public static List<PadInstance> Registered()
        {
            ControllerManager manager = ControllerManager.instance;
            if (manager == null) return Empty;
            if (Pads == null) throw new MissingFieldException("ControllerManager.m_pads");
            return Pads.GetValue(manager) as List<PadInstance> ?? Empty;
        }

        public bool Unchanged(long now)
        {
            List<PadInstance> pads = Registered();
            bool same = entries.Count == pads.Count && apiEpoch == PhysicalBindings.Epoch;
            if (same)
                for (int i = 0; i < pads.Count; i++)
                {
                    PadInstance pad = pads[i];
                    PadBinding bind = pad.GetBind();
                    Entry entry = entries[i];
                    if (!ReferenceEquals(entry.Instance, pad) || !ReferenceEquals(entry.Pad, pad.GetPad())
                        || entry.Enabled != (pad.IsValid && bind != null && bind.Enabled)
                        || !Same(entry.Keys, bind == null ? null : bind.jump)) { same = false; break; }
                }
            if (same && now < nextRefresh) return true;
            entries.Clear();
            foreach (PadInstance pad in pads)
            {
                PadBinding bind = pad.GetBind();
                entries.Add(new Entry { Instance = pad, Pad = pad.GetPad(),
                    Enabled = pad.IsValid && bind != null && bind.Enabled,
                    Keys = bind == null || bind.jump == null ? null : (int[])bind.jump.Clone() });
            }
            apiEpoch = PhysicalBindings.Epoch;
            // Opaque third-party physical mappings have no change event. Keep
            // rediscovering those at 4 Hz; ordinary rebinds invalidate immediately.
            nextRefresh = now + Stopwatch.Frequency / 4;
            return false;
        }
        private static bool Same(int[] a, int[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
