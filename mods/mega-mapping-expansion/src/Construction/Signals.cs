using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal sealed class SignalDescriptor
    {
        internal readonly string Id, Units;
        internal readonly SceneValueKind Kind;
        internal readonly bool Observation;
        internal SignalDescriptor(string id, SceneValueKind kind, string units = "",bool observation=false) { Id = id; Kind = kind; Units = units; Observation=observation; }
    }
    internal sealed class SignalHub : IMappingSignals, IDisposable
    {
        private sealed class Lease : IDisposable
        {
            private SignalHub hub;
            private readonly Action release;
            internal Lease(SignalHub owner, Action action) { hub = owner; release = action; }
            public void Dispose() { if (hub == null) return; hub.CheckThread(); release(); hub = null; }
        }
        private readonly int thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private readonly Dictionary<string, SignalDescriptor> descriptors = new Dictionary<string, SignalDescriptor>(StringComparer.Ordinal);
        private readonly Dictionary<string, SignalSample> samples = new Dictionary<string, SignalSample>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Action<SignalSample>>> observers = new Dictionary<string, List<Action<SignalSample>>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Action<SceneEventSample>>> listeners = new Dictionary<string, List<Action<SceneEventSample>>>(StringComparer.Ordinal);
        private readonly Queue<SceneEventSample> events = new Queue<SceneEventSample>();
        internal int PendingEvents{get{return events.Count;}}
        private readonly List<string> errors = new List<string>();
        private bool disposed;
        private long serial;
        private int batchDepth;
        private readonly HashSet<string> pendingSignals=new HashSet<string>(StringComparer.Ordinal);
        private static long generations;
        public long Generation { get; private set; }
        public bool Available { get { return !disposed; } }
        internal IEnumerable<SignalDescriptor> Descriptors { get { return descriptors.Values; } }
        internal string[] Errors { get { return errors.ToArray(); } }
        internal SignalHub() { Generation = System.Threading.Interlocked.Increment(ref generations); }
        internal void AdvanceGeneration(){Check();Generation=System.Threading.Interlocked.Increment(ref generations);}
        private void CheckThread() { if (thread != System.Threading.Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("Scene observations require the game thread"); }
        internal void Check() { CheckThread(); if (disposed) throw new ObjectDisposedException("Scene observations"); }
        internal void Declare(SignalDescriptor descriptor, SceneValue initial)
        {
            Check(); if (descriptor.Kind != initial.Kind) throw new InvalidDataException(descriptor.Id + ": signal initial value has the wrong type");
            descriptors.Add(descriptor.Id, descriptor); samples.Add(descriptor.Id, new SignalSample(descriptor.Id, initial, ObservationQuality.Unavailable, 0));
            observationLayout=null;
        }
        internal SignalDescriptor Describe(string id)
        { Check(); SignalDescriptor value; if (!descriptors.TryGetValue(id, out value)) throw new InvalidDataException("Unknown signal: " + id); return value; }
        public SignalSample Read(string id)
        { Check(); SignalSample value; if (!samples.TryGetValue(id, out value)) throw new InvalidDataException("Unknown signal: " + id); return value; }
        internal void Publish(string id, SceneValue value, ObservationQuality quality)
        {
            Check(); var definition = Describe(id);
            if (definition.Kind != value.Kind) throw new InvalidDataException(id + ": signal type cannot change");
            if (!Enum.IsDefined(typeof(ObservationQuality), quality)) throw new ArgumentException("Unknown observation quality");
            var previous=samples[id];if(previous.Quality==quality&&previous.Value.Equals(value))return;
            var sample = new SignalSample(id, value, quality, ++serial); samples[id] = sample;
            if(batchDepth!=0){pendingSignals.Add(id);return;}
            Notify(sample);
        }
        private void Notify(SignalSample sample)
        {
            List<Action<SignalSample>> callbacks;
            if (observers.TryGetValue(sample.Id, out callbacks)) foreach (var callback in callbacks.ToArray())
                try { callback(sample); } catch (Exception error) { callbacks.Remove(callback); Error(sample.Id, error); }
        }
        internal IDisposable Batch()
        {
            Check();batchDepth++;
            return new Lease(this,delegate{
                if(--batchDepth!=0)return;
                var changed=pendingSignals.OrderBy(id=>id,StringComparer.Ordinal).ToArray();pendingSignals.Clear();
                foreach(string id in changed)Notify(samples[id]);
            });
        }
        public IDisposable Subscribe(string id, Action<SignalSample> observer)
        {
            Check(); Describe(id); if (observer == null) throw new ArgumentNullException("observer");
            List<Action<SignalSample>> callbacks; if (!observers.TryGetValue(id, out callbacks)) observers.Add(id, callbacks = new List<Action<SignalSample>>());
            if (callbacks.Count >= 128) throw new InvalidOperationException("Signal subscription limit exceeded: " + id);
            callbacks.Add(observer); return new Lease(this, delegate { callbacks.Remove(observer); });
        }
        public IDisposable SubscribeEvent(string id, Action<SceneEventSample> observer)
        {
            Check(); if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || observer == null) throw new ArgumentException("Event ID and observer required");
            List<Action<SceneEventSample>> callbacks;
            if (!listeners.TryGetValue(id, out callbacks))
            {
                if(listeners.Count>=1024)throw new InvalidOperationException("Event subscription limit exceeded");
                listeners.Add(id, callbacks = new List<Action<SceneEventSample>>());
            }
            if (callbacks.Count >= 128) throw new InvalidOperationException("Event subscription limit exceeded");
            callbacks.Add(observer); return new Lease(this, delegate { callbacks.Remove(observer); });
        }
        internal void Emit(string id, string source, string actor, int screen, double time, params SignalSample[] payload)
        {
            Check(); if (events.Count >= 256) throw new InvalidOperationException("Scene event queue limit exceeded");
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || screen < 1 || screen>4096 || source!=null&&source.Length>512 || actor!=null&&actor.Length>128 || !SceneValue.Finite(time) || time < 0 || payload == null || payload.Length > 32
                || payload.Any(p => p == null||string.IsNullOrEmpty(p.Id)||p.Id.Length>128||p.Sequence<0||!Enum.IsDefined(typeof(ObservationQuality),p.Quality))||payload.Select(p=>p.Id).Distinct(StringComparer.Ordinal).Count()!=payload.Length)
                throw new ArgumentException("Invalid scene event envelope");
            events.Enqueue(new SceneEventSample(id, source, actor, screen, ++serial, time, payload));
        }
        internal bool ObservesEvent(string id){List<Action<SceneEventSample>> values;return listeners.TryGetValue(id,out values)&&values.Count>0;}
        internal void Dispatch()
        {
            Check(); int count = events.Count;
            // callbacks can queue new work, but never recurse into the current delivery batch
            for (int i = 0; i < count; i++)
            {
                var sample = events.Dequeue(); List<Action<SceneEventSample>> callbacks;
                if (!listeners.TryGetValue(sample.Id, out callbacks)) continue;
                foreach (var callback in callbacks.ToArray()) try { callback(sample); } catch (Exception error) { callbacks.Remove(callback); Error(sample.Id, error); }
            }
        }
        internal SceneEventSample[] CaptureEvents() { Check(); return events.ToArray(); }
        private SignalDescriptor[] observationLayout;
        private SignalDescriptor[] ObservationLayout{get{return observationLayout??(observationLayout=descriptors.Values.Where(d=>d.Observation).OrderBy(d=>d.Id,StringComparer.Ordinal).ToArray());}}
        internal SignalSample[] CaptureObservations(){Check();return Array.ConvertAll(ObservationLayout,d=>Read(d.Id));}
        internal void ValidateObservations(SignalSample[] values)
        {
            if(values==null||values.Length!=ObservationLayout.Length)throw new InvalidDataException("Observation state layout differs");
            for(int i=0;i<values.Length;i++)if(values[i]==null||values[i].Id!=ObservationLayout[i].Id||values[i].Value.Kind!=ObservationLayout[i].Kind||!Enum.IsDefined(typeof(ObservationQuality),values[i].Quality))throw new InvalidDataException("Invalid observation state");
        }
        internal void RestoreObservations(SignalSample[] values){Check();ValidateObservations(values);foreach(var value in values)Publish(value.Id,value.Value,value.Quality);}
        internal void WriteObservations(System.IO.BinaryWriter writer)
        {var values=CaptureObservations();writer.Write((ushort)values.Length);foreach(var value in values){WorldValues.Write(writer,value.Value);writer.Write((byte)value.Quality);}}
        internal SignalSample[] ReadObservations(System.IO.BinaryReader reader)
        {
            if(reader.ReadUInt16()!=ObservationLayout.Length)throw new InvalidDataException("World observation state layout differs");
            var values=Array.ConvertAll(ObservationLayout,d=>new SignalSample(d.Id,WorldValues.Read(reader,d.Kind),(ObservationQuality)reader.ReadByte(),0));ValidateObservations(values);return values;
        }
        internal void RestoreEvents(SceneEventSample[] pending)
        {
            Check(); if (pending == null || pending.Length > 256 || pending.Any(p => p == null)) throw new InvalidDataException("Invalid pending scene events");
            events.Clear(); foreach (var sample in pending) events.Enqueue(sample);
        }
        private void Error(string id, Exception error) { if (errors.Count == 64) errors.RemoveAt(0); errors.Add(id + ": " + error.GetBaseException().Message); }
        public void Dispose() { CheckThread(); if (disposed) return; disposed = true; observers.Clear(); listeners.Clear(); events.Clear(); }
    }
}
