using System;
using System.Collections.Generic;
using System.Linq;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal sealed partial class NativeRenderer
    {
        private struct Particle
        {
            internal bool Active;
            internal int Screen;
            internal double Born,Life;
            internal SceneValue Position,Velocity,Acceleration;
        }
        private sealed class EmitterPool
        {
            internal Particle[] Particles;
            internal int Next;
            internal uint Random;
            internal uint Seed;
        }
        private sealed class Stamp
        {
            internal int Screen;
            internal double Born;
            internal SceneValue Position;
        }
        private readonly Dictionary<string,EmitterPool> emitters=new Dictionary<string,EmitterPool>(StringComparer.Ordinal);
        private readonly Dictionary<string,List<Stamp>> stamps=new Dictionary<string,List<Stamp>>(StringComparer.Ordinal);
        private void PrepareDynamic()
        {
            PrepareAtlases();
            if(scene.Components.Values.Any(c=>c.Descriptor.Kind=="material"&&Text(c.Id,"channel")=="alpha"))foreach(string id in assets.Bitmaps.Keys)PrepareMask(id);
            int totalParticles=0;
            foreach(var component in scene.Components.Values)
            {
                if(component.Descriptor.Kind=="material")PrepareMask(Text(component.Id,"mask"));
                if(component.Descriptor.Kind=="stamp"){stamps.Add(component.Id,new List<Stamp>((int)Number(component.Id,"capacity")));PrepareMask(Text(component.Id,"mask"));}
                if(component.Descriptor.Kind=="emitter")
                {
                    int capacity=(int)Number(component.Id,"capacity");totalParticles+=capacity;if(totalParticles>131072)throw new System.IO.InvalidDataException("Scene particle pools exceed 131072 slots");
                    uint seed=(uint)Number(component.Id,"seed");if(seed==0)foreach(char c in component.Id)seed=unchecked(seed*16777619u^c);
                    emitters.Add(component.Id,new EmitterPool{Particles=new Particle[capacity],Random=seed==0?1:seed,Seed=seed==0?1:seed});
                }
            }
        }
        private static double Random(EmitterPool pool){uint x=pool.Random;x^=x<<13;x^=x>>17;x^=x<<5;pool.Random=x;return x/4294967296.0;}
        private void TickDynamic()
        {
            TickAtlases();
            foreach(var entry in stamps)entry.Value.RemoveAll(s=>StampTime(entry.Key)-s.Born>=Number(entry.Key,"duration"));
        }
        private double StampTime(string id){return Text(id,"clock")=="presentation"?scene.PresentationTime:scene.GameplayTime;}
        private double EmitterTime(string id){return Text(id,"clock")=="presentation"?scene.PresentationTime:scene.GameplayTime;}
        private void Spawn(string id,EmitterPool pool,ComponentPose pose,SceneValue? explicitPosition,int screen)
        {
            var area=Value(id,"area");var spread=Value(id,"spread");var velocity=Value(id,"velocity");double x=(Random(pool)-.5)*area.X,y=(Random(pool)-.5)*area.Y;
            string shape=Text(id,"shape");if(shape=="point")x=y=0;else if(shape=="line")y=0;else if(shape=="disc"){double a=Random(pool)*Math.PI*2,r=Math.Sqrt(Random(pool));x=Math.Cos(a)*r*area.X/2;y=Math.Sin(a)*r*area.Y/2;}
            var position=explicitPosition??pose.Point(SceneValue.Vector(x,y));
            pool.Particles[pool.Next]=new Particle{Active=true,Screen=screen,Born=EmitterTime(id),Life=Number(id,"lifetime"),Position=position,
                Velocity=SceneValue.Vector(velocity.X+(Random(pool)-.5)*spread.X,velocity.Y+(Random(pool)-.5)*spread.Y),Acceleration=Value(id,"acceleration")};
            pool.Next=(pool.Next+1)%pool.Particles.Length;
        }
        internal bool HandleAction(NativeSceneAction action)
        {
            if(action.Operation!="stamp"&&action.Operation!="burst")return false;
            var payload=action.Event.Payload.FirstOrDefault(p=>p.Id=="position");SceneValue? position=action.Value.HasValue&&action.Value.Value.Kind==SceneValueKind.Vector2?action.Value.Value:payload==null?(SceneValue?)null:payload.Value;
            if(action.Operation=="stamp")
            {
                if(!position.HasValue)throw new InvalidOperationException("Stamp needs a position value or event payload");
                var list=stamps[action.Target];int capacity=(int)Number(action.Target,"capacity");if(list.Count==capacity)list.RemoveAt(0);
                list.Add(new Stamp{Screen=action.Event.Screen,Born=StampTime(action.Target),Position=SceneValue.Vector(Math.Round(position.Value.X),Math.Round(position.Value.Y))});
            }
            else
            {
                var pool=emitters[action.Target];var pose=transforms.Resolve(Text(action.Target,"transform"));int count=action.Value.HasValue&&action.Value.Value.Kind==SceneValueKind.Number?(int)action.Value.Value.X:16;
                for(int i=0;i<Math.Max(0,Math.Min(pool.Particles.Length,count));i++)Spawn(action.Target,pool,pose,position,position.HasValue?action.Event.Screen:pose.Screen);
            }
            return true;
        }
        private void DrawStamps(CompiledComponent component,int screen)
        {
            string id=component.Id,asset=Text(id,"asset");if(asset=="")return;var bitmap=assets.Bitmaps[asset];string material=Text(id,"material"),mask=Text(id,"mask");
            foreach(var stamp in stamps[id])
            {
                if(stamp.Screen!=screen)continue;double age=StampTime(id)-stamp.Born;if(age<0||age>=Number(id,"duration"))continue;
                int frame=Math.Min(bitmap.Frames-1,(int)(age/Number(id,"duration")*bitmap.Frames));var pose=ComponentPose.Identity;pose.X=stamp.Position.X;pose.Y=stamp.Position.Y;
                DrawQuad(bitmap.Texture,bitmap.Frame(frame),pose,SceneValue.Vector(-bitmap.FrameWidth/2,-bitmap.FrameHeight/2),SceneValue.Vector(bitmap.FrameWidth,bitmap.FrameHeight),Tint(material),material,mask==""?null:masks[mask]);
            }
        }
        private void DrawEmitter(CompiledComponent component,ComponentPose pose,string material,int screen)
        {
            string id=component.Id;
            {
                string asset=Text(id,"asset");var bitmap=asset==""?null:assets.Bitmaps[asset];var texture=bitmap==null?Game1.instance.contentManager.Pixel.texture:bitmap.Texture;
                double time=EmitterTime(id),rate=Number(id,"rate"),life=Number(id,"lifetime");var pool=emitters[id];
                double size=Number(id,"size");var tint=Tint(material);
                // steady particles come from the clock, so restores can't erase the stream
                if(rate>0)
                {
                    long last=(long)Math.Floor(time*rate);int count=Math.Min(pool.Particles.Length,(int)Math.Ceiling(rate*life));
                    var area=Value(id,"area");var spread=Value(id,"spread");var velocity=Value(id,"velocity");var acceleration=Value(id,"acceleration");string shape=Text(id,"shape");
                    for(int i=0;i<count;i++)
                    {
                        long index=last-i;if(index<0)break;double age=time-index/rate;if(age<0||age>=life)continue;
                        uint state=unchecked(pool.Seed^(uint)index*747796405u^(uint)(index>>32)*2891336453u);if(state==0)state=1;
                        double x=(ParticleSequence.Next(ref state)-.5)*area.X,y=(ParticleSequence.Next(ref state)-.5)*area.Y;
                        if(shape=="point")x=y=0;else if(shape=="line")y=0;else if(shape=="disc"){double a=ParticleSequence.Next(ref state)*Math.PI*2,r=Math.Sqrt(ParticleSequence.Next(ref state));x=Math.Cos(a)*r*area.X/2;y=Math.Sin(a)*r*area.Y/2;}
                        x+=(velocity.X+(ParticleSequence.Next(ref state)-.5)*spread.X)*age+acceleration.X*age*age/2;
                        y+=(velocity.Y+(ParticleSequence.Next(ref state)-.5)*spread.Y)*age+acceleration.Y*age*age/2;
                        DrawParticle(bitmap,texture,pose.Point(SceneValue.Vector(x,y)),age,life,size,tint,material,pose.PixelSnap);
                    }
                }
                foreach(var particle in pool.Particles)
                {
                    double age=time-particle.Born;if(!particle.Active||(particle.Screen!=0&&particle.Screen!=screen)||age<0||age>=particle.Life)continue;
                    var point=SceneValue.Vector(particle.Position.X+particle.Velocity.X*age+particle.Acceleration.X*age*age/2,particle.Position.Y+particle.Velocity.Y*age+particle.Acceleration.Y*age*age/2);
                    DrawParticle(bitmap,texture,point,age,particle.Life,size,tint,material,pose.PixelSnap);
                }
            }
        }
        private void DrawParticle(PreparedBitmap bitmap,Texture2D texture,SceneValue point,double age,double life,double size,Color tint,string material,bool pixelSnap)
        {
            var at=ComponentPose.Identity;at.PixelSnap=pixelSnap;at.X=point.X;at.Y=point.Y;var source=bitmap==null?new Rectangle(0,0,1,1):bitmap.Frame((int)(age*bitmap.Definition.Fps)%bitmap.Frames);
            double width=size*(bitmap==null?1:bitmap.FrameWidth),height=size*(bitmap==null?1:bitmap.FrameHeight);
            DrawQuad(texture,source,at,SceneValue.Vector(-Math.Floor(width/2),-Math.Floor(height/2)),SceneValue.Vector(width,height),tint*(float)(1-age/life),material,null);
        }
        internal void ClearTransient()
        {
            foreach(var pool in emitters.Values){Array.Clear(pool.Particles,0,pool.Particles.Length);pool.Next=0;pool.Random=pool.Seed;}
            foreach(var list in stamps.Values)list.Clear();
            foreach(var state in atlasStates.Values)state.Initialized=false;TickAtlases();
        }
    }
    internal static class ParticleSequence
    {
        internal static double Next(ref uint state){uint x=state;x^=x<<13;x^=x>>17;x^=x<<5;state=x;return x/4294967296.0;}
    }
}
