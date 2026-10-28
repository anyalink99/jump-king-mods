using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JKRuntime.UI;

namespace WardrobePlus
{
    internal sealed partial class WardrobePage
    {
        private static string ChannelName(string channel)
        { switch(channel){case "surfaceSound":return "Surface sounds";case "equipmentSound":return "Equipment sounds";case "shake":return "Camera shake";case "particles":return "Particles";default:return "Animations";} }
        private void PresentationOptions()
        {
            var rows=new List<Row>();
            foreach(var name in new[]{"animation","particles","surfaceSound","equipmentSound","shake"})
            {
                string channel=name;rows.Add(new Row(ChannelName(channel),()=>PresentationSources(channel))
                    {Text=()=>ChannelName(channel)+": "+PresentationLabel(draft.Presentation.For(channel))});
            }
            rows.Add(new Row("Particle amount",()=>{draft.Presentation.ParticleScale=Cycle(draft.Presentation.ParticleScale,new[]{0f,.25f,.5f,1f,1.5f,2f});Changed();}){Text=()=>"Particle amount: "+draft.Presentation.ParticleScale.ToString("0.##")+"x"});
            rows.Add(new Row("Custom sound volume",()=>{draft.Presentation.SoundVolume=Cycle(draft.Presentation.SoundVolume,new[]{0f,.25f,.5f,.75f,1f});Changed();}){Text=()=>"Custom sound volume: "+draft.Presentation.SoundVolume.ToString("0%")});
            rows.Add(new Row("Character shake",()=>{draft.Presentation.ShakeScale=Cycle(draft.Presentation.ShakeScale,new[]{0f,.25f,.5f,.75f,1f});Changed();}){Text=()=>"Character shake: "+draft.Presentation.ShakeScale.ToString("0%")});
            rows.Add(new Row("Author preview",AuthorPreview));
            rows.Add(new Row("Reload package resources",ReloadPresentation,"Reload JSON, PNG, WAV and shaders. A failed asset load retains the active appearance."));
            rows.Add(new Row("Presentation diagnostics",PresentationDiagnostics));
            Show("ANIMATIONS & EFFECTS",rows,"Choose channels independently. Automatic follows the equipped outfit.");
        }
        private static float Cycle(float current,float[] values) {int index=Array.IndexOf(values,current);return values[(index+1)%values.Length];}
        private string PresentationLabel(string id)
        {
            if(id=="")return "Automatic";if(id=="native")return "Native";
            var parts=id.Split('/');Advanced.SkinPackage p;
            return Controller.Catalog.Advanced.Packages.TryGetValue(parts[0],out p)?p.Manifest.name+" / "+parts[1]:"Missing: "+id;
        }
        private void PresentationSources(string channel)
        {
            var rows=new List<Row>{new Row("Automatic",()=>{draft.Presentation.Set(channel,"");Changed();}),new Row("Native",()=>{draft.Presentation.Set(channel,"native");Changed();})};
            foreach(var package in Controller.Catalog.Advanced.Packages.Values.OrderBy(p=>p.Manifest.name))
            {
                var ids=channel=="animation"?package.Manifest.animations.Select(a=>a.id):package.Manifest.profiles.Select(p=>p.id);
                foreach(var id in ids)
                {
                    string reference=package.Manifest.id+"/"+id;string label=package.Manifest.name+" / "+id;
                    rows.Add(new Row(label,()=>{draft.Presentation.Set(channel,reference);Changed();}){SourceId=reference,Text=()=> (draft.Presentation.For(channel)==reference?"> ":"")+label});
                }
            }
            Show(ChannelName(channel).ToUpperInvariant(),rows,"Only this channel changes. References remain saved if a package is missing.");
        }
        private void ReloadPresentation()
        {
            try { if(Controller.Load(true)){preview.FollowActive();Controller.Status="Resources reloaded";} }
            catch(Exception error){Controller.LastError=Controller.Status=error.GetBaseException().Message;}
        }
        private void AuthorPreview()
        {
            preview.AdvancedPreview=true;
            var rows=new List<Row>{new Row("Surface",()=>{
                var values=new[]{"normal","snow","ice","water","sand"};preview.TestSurface=values[(Array.IndexOf(values,preview.TestSurface)+1)%values.Length];}){Text=()=>"Surface: "+preview.TestSurface},
                new Row("Sound preview",()=>{preview.SoundPreview=!preview.SoundPreview;if(preview.Actor!=null)preview.Actor.Reset();}){Text=()=>"Sound preview: "+(preview.SoundPreview?"On":"Off")},
                new Row("Heavy boots",()=>{if(preview.Actor==null)return;int boots=(int)JumpKing.MiscEntities.WorldItems.Items.GiantBoots;
                    preview.Actor.Equipment=preview.Actor.Equipment.Contains(boots)?preview.Actor.Equipment.Where(i=>i!=boots).ToArray():preview.Actor.Equipment.Concat(new[]{boots}).ToArray();})
                    {Text=()=>"Heavy boots: "+(preview.Actor!=null&&preview.Actor.Equipment.Contains((int)JumpKing.MiscEntities.WorldItems.Items.GiantBoots)?"On":"Off")},
                new Row("Charge",()=>{if(preview.Actor!=null)preview.Actor.Charge=Cycle(preview.Actor.Charge,new[]{0f,.25f,.5f,.75f,1f});}){Text=()=>"Charge: "+(preview.Actor==null?0:preview.Actor.Charge).ToString("0%")},
                new Row("Speed",()=>{if(preview.Actor!=null)preview.Actor.Velocity=new Microsoft.Xna.Framework.Vector2(0,Cycle(preview.Actor.Velocity.Y,new[]{0f,3f,6f,12f,20f}));}){Text=()=>"Impact speed: "+(preview.Actor==null?0:preview.Actor.Velocity.Y).ToString("0")},
                new Row("Next frame (1/60 s)",()=>{if(preview.Actor!=null)preview.Actor.Update(1f/60);}),
                SettingRow("Animate",()=>Controller.Data.Animate,data=>data.Animate=!data.Animate,false),
                new Row("Reset preview",()=>{if(preview.Actor!=null)preview.Actor.Reset();}),new Row("Reload resources",ReloadPresentation)};
            foreach(var value in Advanced.ManifestIO.States){string state=value;rows.Add(new Row("Pose: "+state,()=>{preview.TestState=state;}));}
            foreach(var value in Advanced.ManifestIO.Triggers){string trigger=value;rows.Add(new Row("Play: "+trigger,()=>preview.Trigger(trigger)));}
            Show("AUTHOR PREVIEW",rows,"Simulated context. Sound is opt-in; preview never shakes the game camera.");
        }
        private void PresentationDiagnostics()
        {
            var lines=new List<string>();lines.AddRange(Controller.Catalog.Advanced.Problems);
            var actor=preview.Actor;
            if(actor!=null){lines.Add("Packages: "+actor.Assets.Packages.Count+"; textures: "+actor.Assets.TextureBytes+" bytes; particles: "+actor.Particles.Count);lines.AddRange(actor.Trace);}
            var live=Advanced.PresentationRuntime.Live;if(live!=null&&live.Actor!=null){lines.Add("Live presentation:");lines.AddRange(live.Actor.Trace);}
            if(lines.Count==0)lines.Add("No extended packages or diagnostics.");
            var rows=lines.Select(line=>new Row(line,delegate{},line)).ToList();
            rows.Insert(0,new Row("Write report",()=>{string path=Path.Combine(Controller.Store.DirectoryPath,"presentation-diagnostics.txt");Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllLines(path,lines);Controller.Status="Saved presentation-diagnostics.txt";}));
            Show("PRESENTATION DIAGNOSTICS",rows,"Rules list the event, channel, package and decision.");
        }
    }
}
