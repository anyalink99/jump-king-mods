using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace RunVerifier
{
    internal sealed class Repository
    {
        internal readonly string Root;
        private readonly object gate = new object();
        private readonly Dictionary<string, RunRecord> records = new Dictionary<string, RunRecord>();
        private readonly Dictionary<string, RunRecord> pending = new Dictionary<string, RunRecord>();
        private bool worker;
        internal string Error;
        internal Repository(string root)
        {
            Root = root; Directory.CreateDirectory(root);
            foreach (string path in Directory.GetFiles(root, "*.run.json")) try
            { var value = Json.Read<RunRecord>(File.ReadAllText(path)); if (ValidId(value.id)) records[value.id] = value; }
            catch (Exception e) {
                Error = "Unreadable history preserved: " + Path.GetFileName(path) + ": " + e.Message;
                try { File.Copy(path,path+".damaged-"+DateTime.UtcNow.Ticks,false); } catch { }
                try { var backup=Json.Read<RunRecord>(File.ReadAllText(path+".bak"));if(ValidId(backup.id)) { backup.Reason("Recovered from backup after damaged checkpoint");records[backup.id]=backup; } } catch { }
            }
        }
        internal static bool ValidId(string id) { return id != null && id.Length == 32 && id.All(c => "0123456789abcdef".Contains(c)); }
        internal RunRecord Resume(string continuity, string revision, double time)
        {
            lock (gate) return records.Values.Where(r => !r.complete && r.continuity == continuity && r.revision == revision && r.time <= time + 0.1)
                .OrderByDescending(r => r.time).Select(r=>r.Copy()).FirstOrDefault();
        }
        internal List<RunRecord> List()
        { lock (gate) return records.Values.Where(r => r.complete).OrderByDescending(r => r.finished).Select(r=>r.Copy()).ToList(); }
        internal RunRecord Get(string id) { lock (gate) { RunRecord r; return records.TryGetValue(id, out r) ? r.Copy() : null; } }
        internal string GetStatus(string id) { lock(gate) { RunRecord r;return records.TryGetValue(id,out r)?r.status:"local"; } }
        internal void Save(RunRecord value)
        {
            if (!ValidId(value.id)) throw new ArgumentException("Invalid run ID");
            var copy=value.Copy();
            lock (gate)
            {
                RunRecord existing;
                if(records.TryGetValue(value.id,out existing))
                {
                    foreach(var a in existing.attachments)if(!copy.attachments.Any(v=>v.id==a.id))copy.attachments.Add(a);
                    if(existing.certificate!=null&&copy.certificate==null){copy.certificate=existing.certificate;copy.signature=existing.signature;copy.keyId=existing.keyId;copy.status=existing.status;}
                }
                records[value.id] = copy; pending[value.id] = copy;
                if (worker) return; worker = true; ThreadPool.QueueUserWorkItem(delegate { Drain(); });
            }
        }
        internal void Flush()
        { lock (gate) { while (worker) Monitor.Wait(gate, 100); if(pending.Count!=0)throw new IOException(Error); } }
        private void Drain()
        {
            while (true)
            {
                string id;RunRecord value;
                lock (gate) { if (pending.Count == 0) { worker = false; Monitor.PulseAll(gate); return; } var p = pending.First(); id = p.Key; value = p.Value; pending.Remove(id); }
                try
                {
                    string json=Json.Write(value);
                    string path = Path.Combine(Root, id + ".run.json");
                    Atomic(path, json);
                    // Diagnostic digest journal; .run.json and .bak contain recoverable checkpoints.
                    using (var f = new FileStream(Path.Combine(Root,id+".journal"),FileMode.Append,FileAccess.Write,FileShare.Read))
                    {
                        byte[] data = Encoding.UTF8.GetBytes(Json.Write(new { utc=DateTime.UtcNow.ToString("o"), hash=Json.Hash(json) })+"\n");
                        f.Write(data,0,data.Length); f.Flush(true);
                    }
                    Error = null;
                }
                catch (Exception e) { lock(gate) {Error = "History save failed: " + e.Message;if(!pending.ContainsKey(id))pending[id]=value;worker=false;Monitor.PulseAll(gate);return;} }
            }
        }
        internal static void Atomic(string path, string text)
        { Atomic(path,Encoding.UTF8.GetBytes(text)); }
        internal static void Atomic(string path, byte[] bytes)
        {
            string temp = path + ".tmp";
            using (var f = new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) { f.Write(bytes,0,bytes.Length); f.Flush(true); }
            if (File.Exists(path)) File.Replace(temp,path,path+".bak",true); else File.Move(temp,path);
        }
    }
    internal sealed class Telemetry
    {
        internal readonly RunRecord Record;
        private string area;
        private double previousY, topY, fallStart, lastPoint = -10;
        private int topScreen;
        private bool initialized, descending;
        private readonly SortedList<double,List<Fall>> pendingRecovery=new SortedList<double,List<Fall>>();
        internal Telemetry(RunRecord record) { Record=record;foreach(var f in record.falls.Where(f=>f.kind=="fall"&&f.recovery<0))TrackRecovery(f); }
        private void TrackRecovery(Fall fall){List<Fall> list;if(!pendingRecovery.TryGetValue(fall.originY,out list)){list=new List<Fall>();pendingRecovery.Add(fall.originY,list);}list.Add(fall);}
        internal void Sample(double dt, double y, int screen, bool grounded, string areaId, string areaName, bool transport)
        {
            if (dt <= 0 || dt > 1 || double.IsNaN(y) || double.IsInfinity(y)) return;
            Record.observed += dt;
            var a=Record.areas.FirstOrDefault(v=>v.id==areaId);
            if(a==null) { a=new AreaTime { id=areaId,name=areaName,first=Record.time }; Record.areas.Add(a); }
            if(area!=areaId) { a.visits++; area=areaId; } a.seconds+=dt;
            if(Record.observed-lastPoint>=2) { Record.heights.Add(new HeightPoint { t=Record.time,y=y,screen=screen }); lastPoint=Record.observed; if(Record.heights.Count>10000) Record.heights=Record.heights.Where((p,i)=>i%2==0).ToList(); }
            if(!initialized) { previousY=topY=y; topScreen=screen; initialized=true; }
            if(transport || Math.Abs(y-previousY)>80)
            { Record.falls.Add(new Fall { at=Record.time,height=Math.Abs(y-previousY),kind="transport/unknown",from=topScreen,to=screen }); descending=false; topY=y; topScreen=screen; pendingRecovery.Clear(); }
            else
            {
                while(pendingRecovery.Count>0&&y<=pendingRecovery.Keys[pendingRecovery.Count-1]) { foreach(var fall in pendingRecovery.Values[pendingRecovery.Count-1])fall.recovery=Math.Max(0,Record.time-(fall.at+fall.duration));pendingRecovery.RemoveAt(pendingRecovery.Count-1); }
                if(!descending && y>previousY+0.1) { descending=true; fallStart=Record.time; }
                if(descending && grounded)
                {
                    if(y-topY>=30) { var fall=new Fall { at=fallStart,height=y-topY,originY=topY,duration=Record.time-fallStart,from=topScreen,to=screen }; Record.falls.Add(fall);TrackRecovery(fall); }
                    descending=false;topY=y;topScreen=screen;
                }
                if(!descending) { topY=y; topScreen=screen; }
            }
            previousY=y;
        }
    }
}
