using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JumpKing;
using JumpKing.Workshop;

namespace WardrobePlus.Advanced
{
    public sealed class SkinPackage
    {
        public SkinManifest Manifest { get; internal set; }
        public string Root { get; internal set; }
        public string Source { get; internal set; }
    }
    public sealed class EffectBinding
    {
        public SkinPackage Package;
        public EffectDefinition Definition;
        public float Scale = 1;
        public string Key { get { return Package.Manifest.id + "/" + Definition.id; } }
    }
    public sealed class ChannelDecision
    {
        public bool Native = true;
        public readonly List<EffectBinding> Effects = new List<EffectBinding>();
        public readonly List<string> Rules = new List<string>();
    }
    public sealed class ProfileBinding
    {
        public SkinPackage Package;
        public EffectProfile Profile;
        public int Rank;
        public string Key { get { return Package.Manifest.id + "/" + Profile.id; } }
    }
    public sealed class SkinLibrary
    {
        public readonly Dictionary<string,SkinPackage> Packages = new Dictionary<string,SkinPackage>(StringComparer.Ordinal);
        public readonly List<string> Problems = new List<string>();
        private readonly HashSet<string> ambiguous = new HashSet<string>();
        public void Add(string root, string source)
        {
            string file = Path.Combine(root,"wardrobe","skin.json"); if (!File.Exists(file)) return;
            try
            {
                var manifest = ManifestIO.Read(file);
                if (Packages.ContainsKey(manifest.id) || ambiguous.Contains(manifest.id))
                { Packages.Remove(manifest.id); ambiguous.Add(manifest.id); throw new InvalidDataException("Ambiguous package ID " + manifest.id); }
                Packages.Add(manifest.id,new SkinPackage { Manifest=manifest,Root=Path.GetDirectoryName(file),Source=source });
            }
            catch (Exception error) { Problems.Add(file + ": " + error.GetBaseException().Message); }
        }
        public void ValidateDependencies()
        {
            foreach (var id in Packages.Keys.ToArray())
                try { Visit(id,new HashSet<string>(),new HashSet<string>()); }
                catch (Exception error) { Problems.Add(id + ": " + error.Message); Packages.Remove(id); }
            bool removed;
            do { removed=false; foreach(var p in Packages.Values.ToArray()) if(p.Manifest.dependencies.Any(d=>!Packages.ContainsKey(d.id)))
                { Problems.Add(p.Manifest.id + ": dependency unavailable"); Packages.Remove(p.Manifest.id); removed=true; } } while(removed);
        }
        private void Visit(string id,HashSet<string> visiting,HashSet<string> done)
        {
            if(done.Contains(id)) return;
            ManifestIO.Require(visiting.Add(id),"Dependency cycle at " + id);
            SkinPackage p; ManifestIO.Require(Packages.TryGetValue(id,out p),"Missing dependency " + id);
            foreach(var d in p.Manifest.dependencies) { SkinPackage target; ManifestIO.Require(Packages.TryGetValue(d.id,out target),"Missing dependency " + d.id);
                ManifestIO.Require(new Version(target.Manifest.version)>=new Version(d.minimumVersion),"Dependency too old: " + d.id); Visit(d.id,visiting,done); }
            foreach(var profile in p.Manifest.profiles) foreach(var rule in profile.rules) foreach(var channel in rule.channels)
                foreach(var reference in channel.effects) { var effect = ResolveEffect(p,reference);
                    ManifestIO.Require(effect.Definition.kind == (channel.channel.EndsWith("Sound") ? "sound" : channel.channel),"Wrong effect kind for " + channel.channel + ": " + reference); }
            foreach(var animation in p.Manifest.animations) foreach(var clip in animation.clips) foreach(var frame in clip.frames)
                foreach(var reference in frame.effects) ResolveEffect(p,reference);
            visiting.Remove(id); done.Add(id);
        }
        public EffectBinding ResolveEffect(SkinPackage origin,string reference)
        {
            ManifestIO.Reference(reference); var parts=reference.Split('/'); var id=parts.Length==1?origin.Manifest.id:parts[0];
            ManifestIO.Require(id==origin.Manifest.id || origin.Manifest.dependencies.Any(d=>d.id==id),"Undeclared dependency: " + reference);
            SkinPackage package; ManifestIO.Require(Packages.TryGetValue(id,out package),"Missing effect package: " + id);
            var effect=package.Manifest.effects.Find(e=>e.id==parts[parts.Length-1]); ManifestIO.Require(effect!=null,"Missing effect: " + reference);
            return new EffectBinding { Package=package,Definition=effect };
        }
        public ProfileBinding Profile(string reference,int rank)
        {
            if(string.IsNullOrEmpty(reference) || reference=="native") return null;
            var parts=reference.Split('/'); SkinPackage package;
            if(parts.Length!=2 || !Packages.TryGetValue(parts[0],out package)) return null;
            var profile=package.Manifest.profiles.Find(p=>p.id==parts[1]);
            return profile==null?null:new ProfileBinding { Package=package,Profile=profile,Rank=rank };
        }
        internal List<ProfileBinding> OutfitProfiles(PreparedAppearance appearance,IEnumerable<int> worn)
        {
            var result=new List<ProfileBinding>();
            var ordered=new[]{NativeAppearance.BaseItem}.Concat(worn).ToArray();
            for(int i=0;i<ordered.Length;i++)
            {
                Resolution resolution; if(!appearance.Resolved.TryGetValue(ordered[i],out resolution)) continue;
                foreach(var package in Packages.Values.Where(p=>resolution.Id.StartsWith(p.Source+":",StringComparison.Ordinal)))
                {
                    if(package.Manifest.defaultProfile.Length==0) continue;
                    var binding=Profile(package.Manifest.id+"/"+package.Manifest.defaultProfile,i==0?0:100+i);
                    if(binding!=null) { result.RemoveAll(x=>x.Key==binding.Key); result.Add(binding); }
                }
            }
            return result;
        }
        public ChannelDecision Decide(string channel,PresentationEvent e,IEnumerable<ProfileBinding> inherited,PresentationSelection selection)
        {
            var result=new ChannelDecision(); var choice=selection.For(channel); if(choice=="native") return result;
            var profiles=inherited.ToList(); var explicitProfile=Profile(choice,10000);
            if(explicitProfile!=null) { profiles.RemoveAll(p=>p.Key==explicitProfile.Key); profiles.Add(explicitProfile); }
            var matches=profiles.SelectMany(p=>p.Profile.rules.Where(r=>Matches(r,e)).SelectMany(r=>r.channels.Where(c=>c.channel==channel)
                .Select(c=>new {Binding=p,Rule=r,Channel=c}))).OrderBy(x=>x.Binding.Rank).ThenBy(x=>x.Rule.priority).ThenBy(x=>x.Rule.id,StringComparer.Ordinal);
            foreach(var match in matches)
            {
                var c=match.Channel;
                if(c.mode=="keep" || c.mode=="replace" || c.mode=="off") { result.Native=c.mode=="keep"; result.Effects.Clear(); }
                result.Rules.Add(match.Binding.Key+":"+match.Rule.id+":"+c.mode);
                foreach(var reference in c.effects) { var effect=ResolveEffect(match.Binding.Package,reference); effect.Scale=c.scale; result.Effects.Add(effect); }
            }
            return result;
        }
        private static bool Matches(EffectRule rule,PresentationEvent e)
        { return rule.trigger==e.Trigger && (rule.surface=="*" || rule.surface==e.Surface) && (rule.equipment=="*" || e.Equipment.Contains(rule.equipment))
            && e.Charge>=rule.minCharge && e.Charge<=rule.maxCharge && e.Speed>=rule.minSpeed && e.Speed<=rule.maxSpeed; }
        internal static SkinLibrary Discover(Catalog catalog)
        {
            var result=new SkinLibrary();
            foreach(var root in catalog.Roots) result.Add(root.Value,root.Key);
            result.ValidateDependencies(); return result;
        }
    }
}
