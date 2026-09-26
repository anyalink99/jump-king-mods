using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Steamworks;

namespace RunVerifier
{
    internal sealed class Connection { public string token, profileId; public bool online, pending; }
    internal sealed class Client
    {
        internal const string Site="https://autocubes.site";
        private readonly Repository repository;
        private readonly object gate=new object();
        private readonly Queue<Action> queue=new Queue<Action>();
        private bool running;
        private Connection connection;
        private string connectionError;
        private sealed class Observation
        { internal string RunId,Id,Hash;internal int Sequence;internal double Time,Y;internal bool Failed,Ended;internal double SentTime;internal Timer Timer; }
        private Observation current;
        internal string Status="Offline history ready";
        internal bool Online { get { return connection.online; } }
        internal bool Paired { get { return !string.IsNullOrEmpty(connection.token)&&!connection.pending&&!string.IsNullOrEmpty(connection.profileId); } }
        internal Client(Repository r)
        {
            repository=r; connection=new Connection();
            try { var text=File.ReadAllBytes(Path.Combine(r.Root,"profile.json")); var saved=Json.Read<Connection>(Encoding.UTF8.GetString(ProtectedData.Unprotect(text,null,DataProtectionScope.CurrentUser)));if(saved==null)throw new InvalidDataException("Invalid profile");connection=saved; } catch(FileNotFoundException) { } catch(Exception) { connectionError="Cannot read profile.json with this Windows account; keep the file";Status=connectionError; }
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }
        internal void ToggleOnline() { if(connectionError!=null){Status=connectionError;return;}connection.online=!connection.online; SaveConnection(); Status=Online?"Online observation enabled for next attempt":"Online observation disabled"; }
        private void SaveConnection()
        { lock(gate) Repository.Atomic(Path.Combine(repository.Root,"profile.json"),ProtectedData.Protect(Encoding.UTF8.GetBytes(Json.Write(connection)),null,DataProtectionScope.CurrentUser)); }
        internal void Pair()
        {
            if(connectionError!=null) { Status=connectionError;return; }
            // Read Steam on the game thread. It is metadata, not an authentication claim.
            string steam, nickname;
            try { steam=SteamUser.GetSteamID().m_SteamID.ToString();nickname=SteamFriends.GetPersonaName(); }
            catch { Status="Start Steam before creating a profile";return; }
            if(steam=="0") { Status="Start Steam before creating a profile";return; }
            Status="Opening website sign-in...";
            Enqueue(delegate
            {
                if(string.IsNullOrEmpty(connection.token))
                {
                    byte[] secret=new byte[32];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(secret);
                    connection.token=BitConverter.ToString(secret).Replace("-","").ToLowerInvariant();
                    connection.pending=true;SaveConnection();
                }
                // Save the device secret before registration so retries recover the same profile.
                var account=Request("profile/register",new {steamId=steam,displayName=nickname});
                if(!account.ContainsKey("steamId")||(string)account["steamId"]!=steam)
                    throw new InvalidOperationException("Profile unavailable or belongs to another Steam user; keep your connection file");
                connection.profileId=(string)account["profileId"];
                connection.pending=false;SaveConnection();
                var reply=Request("auth/code/start",new { });string code=(string)reply["code"];
                Status="Sign-in code: "+code+" (5 min)";
                // Fragments are not sent to server logs or as HTTP referrers.
                Process.Start(Site+"/jumpking/connect#"+Uri.EscapeDataString(code));
            });
        }
        internal void Begin(RunRecord r)
        {
            if(!Online||!Paired) return;
            var copy=r.Copy();var observation=new Observation {RunId=r.id,Time=r.time};current=observation;
            Enqueue(delegate { var p=Request("observe/start",new { runId=copy.id,steamId=copy.steamId,revision=copy.revision,time=copy.time,sessionId=copy.sessions.Last().id }); observation.Id=(string)p["id"]; observation.Hash=(string)p["challenge"]; Status="Online observation active"; });
            observation.Timer=new Timer(delegate { SendObservation(observation); },null,5000,5000);
        }
        internal void Observe(RunRecord r)
        {
            var observation=current;if(observation==null||observation.RunId!=r.id)return;
            var point=r.heights.LastOrDefault();lock(observation){observation.Time=r.time;observation.Y=point==null?0:point.y;}
        }
        private void SendObservation(Observation observation)
        {
            double time,y;lock(observation){if(observation.Ended)return;time=observation.Time;y=observation.Y;}
            Enqueue(delegate
            {
                if(observation.Id==null||observation.Failed||time<observation.SentTime) return;
                string payload=Json.Write(new { runId=observation.RunId,time=time,y=y });
                string hash=Json.Hash(observation.Hash+"\n"+payload);
                var batch=new {id=observation.Id,sequence=observation.Sequence,previous=observation.Hash,payload=payload,hash=hash};
                try { Request("observe/batch",batch); }
                catch { try{Request("observe/batch",batch);}catch{observation.Failed=true;throw;} }
                observation.Sequence++; observation.Hash=hash;observation.SentTime=time;
            });
        }
        internal void End(RunRecord r)
        {
            Observe(r);var observation=current;current=null;
            if(observation!=null){observation.Timer.Dispose();SendObservation(observation);lock(observation){observation.Ended=true;}
                Enqueue(delegate { if(observation.Id!=null&&!observation.Failed) Request("observe/end",new { id=observation.Id,runId=r.id }); });}
            if(r.complete && Online && Paired) Publish(r);
        }
        internal void Publish(RunRecord r)
        {
            if(!Paired) { Status="Sign in to website from Settings first"; return; }
            string id=r.id;
            Repository.Atomic(Path.Combine(repository.Root,id+".upload.json"),Json.Write(new { id=id }));
            Enqueue(delegate
            {
                RunRecord record=repository.Get(id); if(record==null||!record.complete) throw new InvalidOperationException("Completed record required");
                var wire=record.Copy(); foreach(var a in wire.attachments) a.path=null;
                wire.certificate=wire.signature=wire.keyId=null; wire.favorite=false; wire.status="local";
                var receipt=Request("runs",new { report=wire });
                VerifyReceipt(receipt,record);
                record.status=(string)receipt["level"]; record.certificate=(string)receipt["payload"]; record.signature=(string)receipt["signature"]; record.keyId=(string)receipt["keyId"];
                repository.Save(record); repository.Flush();
                foreach(var a in record.attachments) if(File.Exists(a.path)) UploadReplay(record.id,a);
                File.Delete(Path.Combine(repository.Root,id+".upload.json"));
                Status="Published: "+record.status;
            });
        }
        internal void PublishMarker(CompletionMarker marker)
        {
            if(!Paired){Status="Sign in to website from Settings first";return;}
            string id=Json.Hash("marker|"+connection.profileId+"|"+marker.MapId+"|"+marker.Complete).Substring(0,32);
            SubmitUnverified(new {id=id,source="marker",mapId=marker.MapId,mapName=marker.Name,complete=marker.Complete,time=(double?)null,finished=(string)null,note="Imported from the game's completion marker.",@public=false});
        }
        internal void PublishAttempt(RunRecord record)
        {
            if(!Paired){Status="Sign in to website from Settings first";return;}
            if(record==null||record.complete||record.time<=0){Status="Load an unfinished attempt first";return;}
            var copy=record.Copy();
            string id=Json.Hash("attempt|"+connection.profileId+"|"+copy.id+"|"+copy.time.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Substring(0,32);
            SubmitUnverified(new {id=id,source="attempt",mapId=copy.mapId,mapName=copy.mapName,complete=false,time=(double?)copy.time,finished=(string)null,note="Unfinished attempt exported from the game.",@public=false});
        }
        private void SubmitUnverified(object submission)
        {
            Status="Submitting unverified record...";
            Enqueue(delegate{var result=Request("submissions",submission);Status="Submitted; awaiting review";Process.Start(Site+"/jumpking/run/"+(string)result["id"]);});
        }
        internal void Retry()
        { foreach(string p in Directory.GetFiles(repository.Root,"*.upload.json")) { var r=repository.Get(Path.GetFileName(p).Split('.')[0]); if(r!=null) Publish(r); } }
        private void UploadReplay(string id,Attachment a)
        {
            if(a.size>64*1024*1024) throw new InvalidOperationException("Replay exceeds 64 MB upload limit");
            if(Json.FileHash(a.path)!=a.hash) throw new InvalidOperationException("Replay changed after recording");
            byte[] bytes=File.ReadAllBytes(a.path);
            Request("runs/"+id+"/attachments",new {id=a.id,hash=a.hash,size=a.size,start=a.start,end=a.end,sessionId=a.sessionId});
            Raw("runs/"+id+"/replay/"+a.id,bytes,"application/octet-stream");
        }
        internal void VerifyReceipt(Dictionary<string,object> receipt,RunRecord record)
        {
            var key=Request("keys/"+(string)receipt["keyId"],null);
            using(var rsa=new RSACryptoServiceProvider())
            {
                rsa.FromXmlString((string)key["xml"]);
                if(!rsa.VerifyData(Convert.FromBase64String((string)receipt["payload"]),CryptoConfig.MapNameToOID("SHA256"),Convert.FromBase64String((string)receipt["signature"])))
                    throw new InvalidDataException("Server certificate signature rejected");
                var signed=Json.Read<Dictionary<string,object>>(Encoding.UTF8.GetString(Convert.FromBase64String((string)receipt["payload"])));
                if(!signed.ContainsKey("profileId")||(string)signed["profileId"]!=connection.profileId)
                    throw new InvalidDataException("Certificate belongs to another profile");
                if((string)signed["id"]!=record.id||(string)signed["steamId"]!=record.steamId||(string)signed["revision"]!=record.revision
                    || Math.Abs(Convert.ToDouble(signed["time"])-record.time)>.001||(string)signed["level"]!=(string)receipt["level"])
                    throw new InvalidDataException("Certificate does not match this completion");
            }
        }
        private Dictionary<string,object> Request(string path,object data)
        { return Json.Read<Dictionary<string,object>>(Raw(path,data==null?null:Encoding.UTF8.GetBytes(Json.Write(data)),"application/json")); }
        private string Raw(string path,byte[] data,string contentType)
        {
            var req=(HttpWebRequest)WebRequest.Create(Site+"/api/jumpking/v1/"+path); req.Method=data==null?"GET":"POST"; req.Timeout=15000; req.ReadWriteTimeout=15000; req.AllowAutoRedirect=false;
            if(!string.IsNullOrEmpty(connection.token)) req.Headers[HttpRequestHeader.Authorization]="Bearer "+connection.token;
            if(data!=null) { req.ContentType=contentType; req.ContentLength=data.Length; using(var stream=req.GetRequestStream()) stream.Write(data,0,data.Length); }
            try { using(var res=req.GetResponse()) using(var reader=new StreamReader(res.GetResponseStream())) return reader.ReadToEnd(); }
            catch(WebException e) { if(e.Response!=null) using(var reader=new StreamReader(e.Response.GetResponseStream())) throw new InvalidOperationException("Server: "+reader.ReadToEnd()); throw; }
        }
        private void Enqueue(Action action)
        {
            lock(gate)
            {
                if(queue.Count>=24) { Status="Network queue full; observation has a gap"; return; }
                queue.Enqueue(action); if(running) return; running=true;
                ThreadPool.QueueUserWorkItem(delegate { while(true) { Action a; lock(gate) { if(queue.Count==0) { running=false; return; } a=queue.Dequeue(); } try { a(); } catch(Exception e) { Status=e.Message; } } });
            }
        }
    }
}
