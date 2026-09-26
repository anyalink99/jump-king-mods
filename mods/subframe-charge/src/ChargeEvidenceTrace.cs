using System;
using System.Diagnostics;

namespace SubframeCharge
{
    internal sealed partial class SubframeChargeState
    {
        private ChargeEvidenceTrace evidence;
        private ChargeEvidenceTrace Evidence { get { return evidence ?? (evidence = new ChargeEvidenceTrace()); } }
        private void TraceEvidence(string stage, string detail)
        {
            try
            {
                long begin = Stopwatch.GetTimestamp();
                Evidence.Capture(stage, detail, Timeline.FrameNumber, chargeId, input, body, this,
                    sampler, configuredKeyboardBindings, sampledPressTimestamp, sampledReleaseTimestamp,
                    nativeChargeTimestamp, nativeCharging, sampledPress, sampledRelease, unsupportedCharge,
                    Timeline.EligibleSince, deferredNativeTimer);
                Evidence.RecordCost(Stopwatch.GetTimestamp() - begin);
            }
            catch (Exception error) { Evidence.Fault(error); }
        }
        private void TraceIncident(string reason)
        {
            try
            {
                Evidence.Incident(reason, chargeId, Timeline.FrameNumber);
                Evidence.IncidentEnvironment(input, body, observationOnly, quarterSteps);
            }
            catch (Exception error) { Evidence.Fault(error); }
        }
        private void TraceEnvironment()
        {
            try { Evidence.Environment(input, body, observationOnly, quarterSteps); }
            catch (Exception error) { Evidence.Fault(error); }
        }
    }
}

namespace SubframeCharge
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using HarmonyLib;
    using JKRuntime.Input;
    using JumpKing;
    using JumpKing.Controller;
    using JumpKing.Player;

    // Independent read-only recorder. Never dequeues gameplay input, polls a
    // device, subscribes to the worker, or patches/executes a native getter.
    internal sealed class ChargeEvidenceTrace
    {
        private readonly EvidenceWindow window = new EvidenceWindow(768, DiagnosticLog.Write);
        private readonly Dictionary<Type, Dictionary<string, FieldInfo>> fields = new Dictionary<Type, Dictionary<string, FieldInfo>>();
        private readonly List<KeyboardSample> workerSamples = new List<KeyboardSample>(256);
        private bool incidentEnvironment;
        private long workerCursor = -1, previousSample, maximumCost, captureCount;
        private string previousKeys;
        private bool previousReliable;
        private IntPtr previousForeground;
        private int faultCount;
        private static int nextObserver;
        private readonly int observer = Interlocked.Increment(ref nextObserver);
        private readonly object physicalSource;
        internal ChargeEvidenceTrace(object source = null) { physicalSource = source; }

        private object Field(object target, string name)
        {
            if (target == null) return null;
            Type type = target as Type ?? target.GetType();
            Dictionary<string, FieldInfo> typeFields;
            if (!fields.TryGetValue(type, out typeFields)) { typeFields = new Dictionary<string, FieldInfo>(); fields.Add(type, typeFields); }
            FieldInfo field;
            if (!typeFields.TryGetValue(name, out field))
            {
                field = AccessTools.Field(type, name);
                typeFields.Add(name, field);
            }
            return field == null ? null : field.GetValue(target is Type ? null : target);
        }
        private static string Value(object value)
        { return value == null ? "unavailable" : Convert.ToString(value, CultureInfo.InvariantCulture); }
        private static long Number(object value) { return value == null ? -1 : Convert.ToInt64(value); }
        private void Add(string message, long frame)
        { window.Add("observer=" + observer + " qpc=" + Stopwatch.GetTimestamp() + " frame=" + frame + " " + message, frame); }

        internal void RecordCost(long ticks) { maximumCost = Math.Max(maximumCost, ticks); captureCount++; }
        internal void Fault(Exception error)
        {
            if (faultCount++ < 3) DiagnosticLog.Write("evidence unavailable observer=" + observer + " type=" + error.GetType().Name + " message=" + error.Message);
        }

        internal void Capture(string stage, string detail, long frame, long charge,
            InputComponent input, BodyComp body, object jump, IHighRateInput sampler, int[][] bindings,
            long press, long release, long origin, bool charging, bool hasPress, bool hasRelease,
            bool unsupported, long eligible, float? deferred)
        {
            string physical = Worker(bindings, frame);
            object stream = Field(sampler, "stream");
            object actualSampler = Field(stream, "Sampler") ?? sampler;
            var clientQueue = Field(sampler, "queue") as ICollection;
            string samplerState = Sampler(actualSampler) + " clientQueued=" + (clientQueue == null ? "unavailable" : clientQueue.Count.ToString());
            object manager = ControllerManager.instance;
            object main = Field(manager, "_current_main");
            var pads = Field(manager, "m_pads") as IEnumerable;
            var padText = new StringBuilder();
            if (pads != null) foreach (object pad in pads)
            {
                object current = Field(pad, "current_state"), last = Field(pad, "last_state");
                padText.Append(" [").Append(ReferenceEquals(main, pad) ? "main:" : "pad:")
                    .Append(TypeName(Field(pad, "m_pad"))).Append(" held=")
                    .Append(current is PadState ? ((PadState)current).jump.ToString() : "unavailable")
                    .Append(" last=").Append(last is PadState ? ((PadState)last).jump.ToString() : "unavailable").Append(']');
            }
            Add("stage=" + stage + " charge=" + charge + " " + detail
                + " nativeTimer=" + Value(Field(jump, "m_timer")) + " canJump=" + Value(Field(input, "_can_jump"))
                + " charging=" + charging + " press=" + hasPress + " release=" + hasRelease + " unsupported=" + unsupported
                + " pressQpc=" + press + " releaseQpc=" + release + " originQpc=" + origin + " eligibleQpc=" + eligible
                + " deferred=" + Value(deferred) + " ground=" + Value(Field(body, "_is_on_ground"))
                + " position=" + (body == null ? "unavailable" : body.Position.ToString())
                + " screen=" + (body == null ? "unavailable" : body.LastScreen.ToString())
                + " velocity=" + (body == null ? "unavailable" : body.Velocity.ToString())
                + " overlay=" + Value(Field(typeof(PadInstance), "_steam_overlay_active"))
                + " active=" + (Game1.instance != null && Game1.instance.IsActive)
                + " restoreEpoch=" + JKRuntime.State.GameState.Snapshots.RestoreEpoch
                + " " + physical + " " + samplerState + padText + ResponsiveInput.JumpDiagnostic(), frame);
        }

        private string Worker(int[][] bindings, long frame)
        {
            object source = physicalSource ?? Field(typeof(SharedKeyboard), "source");
            object sync = Field(source, "sync");
            if (sync == null) return "worker=unavailable";
            if (!Monitor.TryEnter(sync)) return "worker=busy";
            var samples = workerSamples;
            samples.Clear();
            long sequence, lost = 0;
            try
            {
                var history = Field(source, "history") as KeyboardSample[];
                sequence = Number(Field(source, "sequence"));
                if (history == null || sequence < 0) return "worker=unsupported-layout";
                long start = workerCursor < 0 ? Math.Max(0, sequence - 1) : workerCursor;
                if (start < sequence - history.Length) { lost = sequence - history.Length - start; start = sequence - history.Length; }
                for (long i = start; i < sequence; i++) samples.Add(history[(int)(i % history.Length)]);
                workerCursor = sequence;
            }
            finally { Monitor.Exit(sync); }
            if (lost > 0) Add("physical-history-overflow samplesLost=" + lost, frame);
            foreach (KeyboardSample sample in samples)
            {
                string keys = DownKeys(sample, bindings);
                double gap = previousSample == 0 ? 0 : (sample.Timestamp - previousSample) * 1000.0 / Stopwatch.Frequency;
                if (keys != previousKeys || sample.Reliable != previousReliable || sample.Foreground != previousForeground || gap > 10)
                    Add("physical sampleQpc=" + sample.Timestamp + " boundKeys=" + keys + " reliable=" + sample.Reliable
                        + " foreground=" + sample.Foreground + " gapMs=" + gap.ToString("F3", CultureInfo.InvariantCulture), frame);
                previousKeys = keys; previousReliable = sample.Reliable; previousForeground = sample.Foreground; previousSample = sample.Timestamp;
            }
            return "workerSeq=" + sequence + " workerQpc=" + previousSample + " boundKeys=" + previousKeys
                + " workerReliable=" + previousReliable + " foreground=" + previousForeground;
        }
        private static string DownKeys(KeyboardSample sample, int[][] bindings)
        {
            var keys = new StringBuilder();
            if (bindings != null) foreach (int[] chord in bindings) if (chord != null) foreach (int code in chord)
            {
                int vk = MouseButtons.ToVirtualKey(code);
                if (sample.IsDown(vk)) keys.Append(vk).Append(',');
            }
            return keys.Length == 0 ? "none" : keys.ToString();
        }
        private string Sampler(object sampler)
        {
            object sync = Field(sampler, "sync");
            if (sync == null) return "samplerLayout=unavailable";
            if (!Monitor.TryEnter(sync)) return "samplerLayout=busy";
            try
            {
                object keyboard = Field(sampler, "keyboard");
                var states = Field(sampler, "physical") as IEnumerable;
                var text = new StringBuilder("samplerCursor=").Append(Value(Field(keyboard, "Cursor")))
                    .Append(" samplerGeneration=").Append(Value(Field(sampler, "configurationGeneration")))
                    .Append(" samplerWasDown=").Append(Value(Field(sampler, "wasDown")));
                var queued = Field(sampler, "transitions") as ICollection;
                text.Append(" sourceQueued=").Append(queued == null ? "unavailable" : queued.Count.ToString());
                if (states != null) foreach (object state in states)
                    text.Append(" [source=").Append(Value(Field(state, "Name"))).Append(" ready=").Append(Value(Field(state, "Ready")))
                        .Append(" down=").Append(Value(Field(state, "Down"))).Append(" qpc=").Append(Value(Field(state, "Timestamp"))).Append(']');
                return text.ToString();
            }
            finally { Monitor.Exit(sync); }
        }
        internal void Incident(string reason, long charge, long frame)
        {
            window.Trigger("observer=" + observer + " charge=" + charge + " reason=" + reason
                + " frame=" + frame + " qpc=" + Stopwatch.GetTimestamp()
                + " maxCaptureUs=" + (maximumCost * 1000000.0 / Stopwatch.Frequency).ToString("F2", CultureInfo.InvariantCulture)
                + " captures=" + captureCount, frame);
        }
        internal void IncidentEnvironment(InputComponent input, BodyComp body, bool observation, bool quarters)
        {
            if (incidentEnvironment) return;
            incidentEnvironment = true;
            Environment(input, body, observation, quarters);
        }
        private static string TypeName(object value) { return value == null ? "none" : value.GetType().FullName; }

        internal void Environment(InputComponent input, BodyComp body, bool observation, bool quarters)
        {
            var settings = SettingsStore.Current;
            DiagnosticLog.Write("evidence environment observer=" + observer + " evidenceTrace=sfc-evidence-v1"
                + " qpcFrequency=" + Stopwatch.Frequency + " inputType=" + TypeName(input)
                + " observation=" + observation + " quarters=" + quarters + " inputs=" + settings.SubframeInputs
                + " refresh=" + settings.HighRefresh + " legacyOptimizationsPreference=" + settings.Optimizations);
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic) continue;
                DiagnosticLog.Write("evidence assembly name=" + assembly.GetName().Name + " version=" + assembly.GetName().Version
                    + " mvid=" + assembly.ManifestModule.ModuleVersionId);
            }
            foreach (MethodBase method in Harmony.GetAllPatchedMethods())
            {
                string name = method.DeclaringType == null ? "" : method.DeclaringType.FullName;
                if (!(name.Contains("Input") || name.Contains("Controller") || name.Contains("JumpState") || name.Contains("BodyComp"))) continue;
                var patches = Harmony.GetPatchInfo(method);
                if (patches == null) continue;
                foreach (var patch in patches.Prefixes) Patch(method, "prefix", patch);
                foreach (var patch in patches.Postfixes) Patch(method, "postfix", patch);
                foreach (var patch in patches.Transpilers) Patch(method, "transpiler", patch);
                foreach (var patch in patches.Finalizers) Patch(method, "finalizer", patch);
            }
            var behaviours = Field(body, "m_behaviours") as IEnumerable;
            if (behaviours != null) foreach (object behaviour in behaviours)
                DiagnosticLog.Write("evidence behaviour type=" + TypeName(behaviour));
        }
        private static void Patch(MethodBase method, string kind, Patch patch)
        {
            DiagnosticLog.Write("evidence patch target=" + method.DeclaringType.FullName + "." + method.Name + " kind=" + kind
                + " owner=" + patch.owner + " priority=" + patch.priority + " method=" + patch.PatchMethod.DeclaringType.FullName + "." + patch.PatchMethod.Name);
        }
    }

    internal sealed class EvidenceWindow
    {
        private readonly string[] history;
        private readonly Action<string> write;
        private int next, count;
        private long until = -1, incident;
        internal EvidenceWindow(int capacity, Action<string> sink) { history = new string[capacity]; write = sink; }
        internal void Add(string value, long frame)
        {
            history[next] = value; next = (next + 1) % history.Length; count = Math.Min(count + 1, history.Length);
            if (until >= 0 && frame > until) { write("evidence end incident=" + incident); until = -1; }
            if (until >= 0) write("evidence after incident=" + incident + " " + value);
        }
        internal void Trigger(string detail, long frame)
        {
            // Overlapping failures share the post-window but retain each reason.
            if (until >= 0 && frame <= until) { write("evidence related incident=" + incident + " " + detail); until = frame + 30; return; }
            incident++;
            write("evidence begin incident=" + incident + " historyRows=" + count + " " + detail);
            for (int i = 0; i < count; i++) write("evidence before incident=" + incident + " " + history[(next - count + i + history.Length) % history.Length]);
            until = frame + 30;
        }
    }
}
