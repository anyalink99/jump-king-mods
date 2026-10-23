using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace WardrobePlus.Advanced
{
    public static class ManifestIO
    {
        public const int MaximumBytes = 1024 * 1024;
        public static readonly string[] Channels = { "particles", "surfaceSound", "equipmentSound", "shake" };
        public static readonly string[] Triggers = { "chargeStart", "charge", "chargeEnd", "jump", "rise", "apex", "fall", "land", "splat", "walk", "step", "waterEnter", "waterExit", "idle", "recover" };
        public static readonly string[] States = { "idle", "walk", "charge", "rise", "apex", "fall", "land", "splat", "recover", "lookUp" };
        internal static bool ValidTrigger(string value) {return value!=null && (Triggers.Contains(value)||Regex.IsMatch(value,"^[a-z][a-z0-9_.-]{0,79}/[a-z][a-z0-9_.-]{0,79}$"));}
        public static SkinManifest Read(string path)
        {
            if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException("Manifest exceeds 1 MiB: " + path);
            return Parse(File.ReadAllText(path));
        }
        public static SkinManifest Parse(string json)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = MaximumBytes, RecursionLimit = 32 };
            CheckFields(serializer.DeserializeObject(json), typeof(SkinManifest), "skin");
            var manifest = serializer.Deserialize<SkinManifest>(json);
            Validate(manifest); return manifest;
        }
        public static string Write(SkinManifest manifest) { Validate(manifest); return new JavaScriptSerializer().Serialize(manifest); }
        private static void CheckFields(object value, Type type, string path)
        {
            if (value == null) throw new InvalidDataException(path + ": null is not supported");
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var array = value as object[]; if (array == null) throw new InvalidDataException(path + ": expected array");
                if (array.Length > 4096) throw new InvalidDataException(path + ": too many entries");
                for (int i = 0; i < array.Length; i++) CheckFields(array[i], type.GetGenericArguments()[0], path + "[" + i + "]");
                return;
            }
            if (type == typeof(string) || type.IsPrimitive) return;
            var fields = value as IDictionary<string, object>;
            if (fields == null) throw new InvalidDataException(path + ": expected object");
            foreach (var entry in fields)
            {
                var field = type.GetField(entry.Key, BindingFlags.Instance | BindingFlags.Public);
                if (field == null) throw new InvalidDataException(path + "." + entry.Key + ": unknown field");
                CheckFields(entry.Value, field.FieldType, path + "." + entry.Key);
            }
        }
        internal static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
        internal static void Id(string value, string path) { Require(value != null && Regex.IsMatch(value, "^[a-z][a-z0-9_.-]{0,79}$"), path + ": invalid ID"); }
        internal static void Range(float value, float min, float max, string path) { Require(!float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max, path + ": outside " + min + ".." + max); }
        private static void One(string value, string choices, string path) { Require(choices.Split('|').Contains(value), path + ": unknown value " + value); }
        private static void Unique<T>(IEnumerable<T> values, Func<T,string> key, int limit, string path)
        { Require(values != null && values.Count() <= limit && values.All(v => v != null), path + ": invalid list"); var keys = values.Select(key).ToArray(); Require(keys.Distinct().Count() == keys.Length, path + ": duplicate ID"); }
        internal static void Reference(string value) { Require(value != null && value.Length <= 180 && Regex.IsMatch(value, "^[a-z][a-z0-9_.-]*(/[a-z][a-z0-9_.-]*)?$"), "Invalid reference: " + value); }
        public static string Asset(string root, string relative)
        {
            Require(!string.IsNullOrWhiteSpace(relative) && relative.Length <= 240 && !Path.IsPathRooted(relative) && relative.IndexOf(':') < 0, "Invalid asset path: " + relative);
            string prefix = Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(Path.Combine(prefix, relative));
            Require(full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase), "Asset escapes package: " + relative);
            for (string cursor = full; cursor != null && cursor.Length >= prefix.Length - 1; cursor = Path.GetDirectoryName(cursor))
                if (File.Exists(cursor) || Directory.Exists(cursor)) Require((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) == 0, "Linked package assets are not supported: " + relative);
            return full;
        }
        private static void Color(string value) { Require(value != null && Regex.IsMatch(value, "^#[0-9a-fA-F]{8}$"), "Expected #RRGGBBAA: " + value); }
        public static void Validate(SkinManifest m)
        {
            Require(m != null && m.schema == 1, "Unsupported skin schema"); Id(m.id, "skin.id");
            Require(!string.IsNullOrWhiteSpace(m.name) && m.name.Length <= 128, "skin.name is required (128 characters maximum)");
            Version version, minimum; Require(Version.TryParse(m.version, out version) && Version.TryParse(m.minimumWardrobe, out minimum), "Invalid package version");
            Require(new Version(m.minimumWardrobe) <= typeof(ManifestIO).Assembly.GetName().Version, "A newer Wardrobe+ is required");
            Unique(m.dependencies, d => d.id, 32, "dependencies");
            foreach (var d in m.dependencies) { Id(d.id,"dependency.id"); Require(d.id != m.id && Version.TryParse(d.minimumVersion, out version), "Invalid dependency"); }
            Unique(m.effects, e => e.id, 256, "effects");
            foreach (var e in m.effects)
            {
                Id(e.id, "effect.id"); One(e.collision,"none|water",e.id); Require(e.collision=="none" || (e.kind=="particles" && e.space=="world"),e.id+": water collision requires world particles"); One(e.kind,"particles|sound|shake",e.id); One(e.space,"world|actor",e.id); One(e.layer,"back|front",e.id); One(e.blend,"alpha|additive",e.id);
                Require(e.count >= 0 && e.count <= 512 && e.maxAlive > 0 && e.maxAlive <= 1024, e.id + ": invalid particle budget");
                Require(e.columns > 0 && e.rows > 0 && (long)e.columns * e.rows <= 4096, e.id + ": invalid atlas grid");
                Range(e.rate,0,512,e.id); Range(e.duration,0,30,e.id); Range(e.life,.01f,30,e.id); Range(e.lifeSpread,0,30,e.id);
                Range(e.speed,0,1000,e.id); Range(e.speedSpread,0,1000,e.id); Range(e.angle,-360,360,e.id); Range(e.spread,0,360,e.id);
                Range(e.gravity,-2000,2000,e.id); Range(e.drag,0,100,e.id); Range(e.rotation,-360,360,e.id); Range(e.spin,-2000,2000,e.id);
                Range(e.size,0,16,e.id); Range(e.endSize,0,16,e.id); Range(e.offsetX,-512,512,e.id); Range(e.offsetY,-512,512,e.id); Range(e.fps,0,240,e.id);
                Color(e.color); Color(e.endColor); Id(e.anchor,e.id + ".anchor");
                Range(e.volume,0,1,e.id); Range(e.pitch,-1,1,e.id); Range(e.pitchSpread,0,1,e.id); Range(e.cooldown,0,60,e.id);
                Require(e.voices > 0 && e.voices <= 16 && e.sounds != null && e.sounds.Count <= 16, e.id + ": invalid voice budget");
                Range(e.shakeX,0,16,e.id); Range(e.shakeY,0,16,e.id); Range(e.frequency,1,120,e.id); One(e.waveform,"noise|sine|alternating",e.id);
                if (e.loop) Require(ValidTrigger(e.stopOn),e.id + ": invalid loop stop event");
                if (e.native.Length > 0) Require(NativeEffects.Supports(e.kind, e.native), e.id + ": unknown native effect " + e.native);
                Require(e.kind != "sound" || e.native.Length > 0 || e.sounds.Count > 0,e.id + ": sound has no source");
            }
            Unique(m.profiles,p => p.id,64,"profiles");
            foreach (var p in m.profiles)
            {
                Id(p.id,"profile.id"); Unique(p.rules,r=>r.id,256,p.id + ".rules");
                foreach (var r in p.rules)
                {
                    Id(r.id,"rule.id"); Require(ValidTrigger(r.trigger),r.id + ": invalid trigger");
                    Require(r.surface != null && r.surface.Length <= 128 && r.equipment != null && r.equipment.Length <= 128,r.id + ": invalid condition");
                    Range(r.minCharge,0,1,r.id); Range(r.maxCharge,r.minCharge,1,r.id); Range(r.minSpeed,0,10000,r.id); Range(r.maxSpeed,r.minSpeed,10000,r.id);
                    Require(r.priority >= -1000 && r.priority <= 1000,r.id + ": invalid priority"); Unique(r.channels,c=>c.channel,4,r.id + ".channels");
                    foreach (var c in r.channels) { Require(Channels.Contains(c.channel),r.id + ": invalid channel"); One(c.mode,"keep|add|replace|off",r.id); Range(c.scale,0,4,r.id);
                        Require(c.effects != null && c.effects.Count <= 16,r.id + ": too many effects"); foreach (var reference in c.effects) Reference(reference);
                        Require((c.mode != "off" && c.mode != "keep") || c.effects.Count == 0,r.id + ": keep/off cannot emit effects"); }
                }
            }
            Require(m.defaultProfile == "" || m.profiles.Any(p=>p.id == m.defaultProfile),"Missing default profile");
            Unique(m.animations,a=>a.id,64,"animations");
            foreach (var a in m.animations)
            {
                Id(a.id,"animation.id"); JumpKing.MiscEntities.WorldItems.Items item;
                Require(Enum.TryParse(a.item,out item) && Enum.IsDefined(typeof(JumpKing.MiscEntities.WorldItems.Items),item),a.id + ": invalid equipment item");
                Unique(a.clips,c=>c.state,32,a.id + ".clips");
                foreach (var c in a.clips)
                {
                    Require(States.Contains(c.state),a.id + ": invalid animation state"); Range(c.transition,0,1,a.id);
                    Require(c.frames != null && c.frames.Count > 0 && c.frames.Count <= 512,a.id + ": invalid frames");
                    Require(c.nativeFrames != null && (c.nativeFrames.Count==0 ||
                        (c.nativeFrames.Count==c.frames.Count && c.nativeFrames.All(k=>k>=0&&k<=12) && c.transition==0 && c.frames.All(f=>f!=null&&f.effects!=null&&f.effects.Count==0))),
                        a.id+": nativeFrames requires one native key per frame, no crossfade and no timed frame effects");
                    foreach (var f in c.frames) { Rect(f.x,f.y,f.width,f.height); Range(f.duration,1f/240,30,a.id); Range(f.originX,-512,512,a.id); Range(f.originY,-512,512,a.id);
                        Unique(f.anchors,n=>n.id,64,"anchors"); foreach (var n in f.anchors) { Id(n.id,"anchor.id"); Range(n.x,-512,512,a.id); Range(n.y,-512,512,a.id); Range(n.rotation,-360,360,a.id); }
                        Require(f.effects != null && f.effects.Count <= 16,"Invalid frame effects"); foreach (var reference in f.effects) Reference(reference); }
                }
                Unique(a.attachments,n=>n.id,32,"attachments");
                Unique(a.equipmentAnchors,n=>n.item,64,"equipmentAnchors");
                foreach(var n in a.equipmentAnchors){Require(Enum.TryParse(n.item,out item)&&Enum.IsDefined(typeof(JumpKing.MiscEntities.WorldItems.Items),item)&&item!=JumpKing.MiscEntities.WorldItems.Items.NULL,"Invalid equipment anchor item");Id(n.anchor,"equipment anchor");Range(n.nativeX,-512,512,n.item);Range(n.nativeY,-512,512,n.item);}
                foreach (var n in a.attachments) { Id(n.id,"attachment.id"); Rect(n.x,n.y,n.width,n.height); One(n.layer,"back|front",n.id); Id(n.anchor,"attachment.anchor");
                    Range(n.inertia,0,1,n.id); Range(n.stiffness,1,200,n.id); Range(n.damping,0,40,n.id); Range(n.originX,-512,512,n.id); Range(n.originY,-512,512,n.id);
                    Range(n.offsetX,-512,512,n.id); Range(n.offsetY,-512,512,n.id); Range(n.rotation,-360,360,n.id);
                    var seen = new HashSet<string>{n.id}; var parent = n.parent;
                    while (parent != "") { Require(seen.Add(parent),"Attachment cycle: " + n.id); var next=a.attachments.Find(x=>x.id==parent); Require(next != null,"Missing attachment parent: " + parent); parent=next.parent; }
                }
            }
            Unique(m.materials,x=>x.id,64,"materials");
            foreach (var mtl in m.materials) { Id(mtl.id,"material.id"); One(mtl.kind,"original|gold|glass|magenta|cosmic|tint|palette|glow|scroll|shader",mtl.id);
                Color(mtl.color); Range(mtl.strength,0,1,mtl.id); Range(mtl.scrollX,-512,512,mtl.id); Range(mtl.scrollY,-512,512,mtl.id); Range(mtl.pulse,0,1,mtl.id); Range(mtl.pulseSpeed,0,30,mtl.id);
                Require(mtl.palette != null && mtl.palette.Count <= 256,"Invalid palette"); foreach(var color in mtl.palette) Color(color);
                Require(mtl.kind != "palette" || mtl.palette.Count >= 2,"Palette requires two colors"); Require(mtl.kind != "shader" || mtl.shader.Length > 0,"Shader source missing"); }
        }
        private static void Rect(int x,int y,int width,int height) { Require(x>=0 && y>=0 && width>0 && height>0 && (long)x+width<=8192 && (long)y+height<=8192,"Invalid frame rectangle"); }
    }
}
