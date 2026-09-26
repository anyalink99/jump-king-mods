using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace RunVerifier
{
    public sealed class AreaTime { public string id, name; public double seconds, first; public int visits; }
    public sealed class Fall { public double at, height, duration, originY, recovery = -1; public int from, to; public string kind = "fall"; }
    public sealed class HeightPoint { public double t, y; public int screen; }
    public sealed class Session { public string id, started, ended; public double start, end; }
    public sealed class Attachment { public string id, path, hash, sessionId; public double start, end; public long size; }
    public sealed class EnvironmentEntry { public string id, version, hash; }
    public sealed class RunRecord
    {
        public int schema = 1;
        public string id = Guid.NewGuid().ToString("N"), continuity, steamId, mapId, mapName, revision, category, ending;
        public string started = DateTime.UtcNow.ToString("o"), finished, status = "local", certificate, signature, keyId;
        public string gameVersion, runtimeVersion, verifierVersion = "1.3.0", replayMapId, replayRevision;
        public int ticks, jumps, nativeFalls, nativePeak;
        public double persistedSeconds, time, observed, paused, wall;
        public double tickSeconds=1d/60;
        public bool complete, favorite, unknown, partial;
        public List<string> sources = new List<string>(), reasons = new List<string>(), mechanics = new List<string>();
        public List<EnvironmentEntry> environment = new List<EnvironmentEntry>();
        public List<AreaTime> areas = new List<AreaTime>();
        public List<Fall> falls = new List<Fall>();
        public List<HeightPoint> heights = new List<HeightPoint>();
        public List<Session> sessions = new List<Session>();
        public List<Attachment> attachments = new List<Attachment>();
        public void Reason(string value) { partial = true; if (!reasons.Contains(value)) reasons.Add(value); }
        internal RunRecord Copy()
        {
            var r=(RunRecord)MemberwiseClone();
            r.sources=new List<string>(sources);r.reasons=new List<string>(reasons);r.mechanics=new List<string>(mechanics);
            r.environment=environment.ConvertAll(e=>new EnvironmentEntry {id=e.id,version=e.version,hash=e.hash});
            r.areas=areas.ConvertAll(a=>new AreaTime {id=a.id,name=a.name,seconds=a.seconds,first=a.first,visits=a.visits});
            r.falls=falls.ConvertAll(f=>new Fall {at=f.at,height=f.height,duration=f.duration,originY=f.originY,recovery=f.recovery,from=f.from,to=f.to,kind=f.kind});
            r.heights=heights.ConvertAll(p=>new HeightPoint {t=p.t,y=p.y,screen=p.screen});
            r.sessions=sessions.ConvertAll(s=>new Session {id=s.id,started=s.started,ended=s.ended,start=s.start,end=s.end});
            r.attachments=attachments.ConvertAll(a=>new Attachment {id=a.id,path=a.path,hash=a.hash,sessionId=a.sessionId,start=a.start,end=a.end,size=a.size});
            return r;
        }
    }
    internal static class Json
    {
        internal static string Write(object value) { return new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024, RecursionLimit = 64 }.Serialize(value); }
        internal static T Read<T>(string value) { return new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024, RecursionLimit = 64 }.Deserialize<T>(value); }
        internal static string Hash(string value) { using (var h = SHA256.Create()) return Hex(h.ComputeHash(Encoding.UTF8.GetBytes(value))); }
        internal static string FileHash(string path) { using (var f = File.OpenRead(path)) using (var h = SHA256.Create()) return Hex(h.ComputeHash(f)); }
        internal static string Hex(byte[] data) { return BitConverter.ToString(data).Replace("-", "").ToLowerInvariant(); }
        internal static T Clone<T>(T value) { return Read<T>(Write(value)); }
    }
    internal static class SealCodec
    {
        internal const uint Marker = 0xd3a5c69b;
        internal static uint Crc(byte[] bytes, int length)
        { uint c = 0xffffffff; for (int i = 0; i < length; i++) { c ^= bytes[i]; for (int k = 0; k < 8; k++) c = (c >> 1) ^ ((c & 1) != 0 ? 0xedb88320u : 0); } return ~c; }
        internal static byte[] Payload(string id, ulong steam, ulong ms, uint flags)
        {
            var bytes = new byte[40];
            for (int i = 0; i < 16; i++) bytes[i] = Convert.ToByte(id.Substring(i * 2, 2), 16);
            Array.Copy(BitConverter.GetBytes(steam), 0, bytes, 16, 8);
            Array.Copy(BitConverter.GetBytes(ms), 0, bytes, 24, 8);
            Array.Copy(BitConverter.GetBytes(flags), 0, bytes, 32, 4);
            Array.Copy(BitConverter.GetBytes(Crc(bytes, 36)), 0, bytes, 36, 4);
            return bytes;
        }
        internal static bool[] Encode(byte[] bytes)
        {
            if (bytes.Length != 40) throw new ArgumentException("Seal payload length");
            var bits = new bool[592]; for (int i = 0; i < 32; i++) bits[i] = ((Marker >> i) & 1) != 0;
            int n = 32;
            foreach (byte b in bytes) for (int half = 0; half < 2; half++)
            {
                int v = (b >> (half * 4)) & 15, a = v & 1, c = (v >> 1) & 1, d = (v >> 2) & 1, e = (v >> 3) & 1;
                int[] code = { a ^ c ^ e, a ^ d ^ e, a, c ^ d ^ e, c, d, e };
                foreach (int bit in code) bits[n++] = bit != 0;
            }
            return bits;
        }
        internal static byte[] Decode(bool[] bits)
        {
            if (bits.Length != 592) throw new InvalidDataException("Seal length");
            var bytes = new byte[40]; int n = 32;
            for (int i = 0; i < 80; i++)
            {
                int[] c = new int[8]; for (int k = 1; k <= 7; k++) c[k] = bits[n++] ? 1 : 0;
                int s = (c[1]^c[3]^c[5]^c[7]) | ((c[2]^c[3]^c[6]^c[7]) << 1) | ((c[4]^c[5]^c[6]^c[7]) << 2);
                if (s != 0) c[s] ^= 1;
                bytes[i / 2] |= (byte)((c[3] | c[5]<<1 | c[6]<<2 | c[7]<<3) << ((i % 2)*4));
            }
            if (BitConverter.ToUInt32(bytes,36) != Crc(bytes,36)) throw new InvalidDataException("Seal checksum");
            return bytes;
        }
    }
}
