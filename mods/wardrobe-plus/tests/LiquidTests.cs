using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WardrobePlus;
using WardrobePlus.Advanced;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

internal static partial class WardrobeTests
{
    private static void LiquidTests()
    {
        var liquid=new LiquidMotion();liquid.Update(1f/60,Vector2.Zero,"idle");liquid.Update(1f/60,new Vector2(2,0),"walk");
        Check(liquid.Tilt<0,"Water lags horizontal acceleration");
        float tilt=liquid.Tilt;liquid.Update(1f/60,new Vector2(2,0),"walk");
        Check(liquid.Tilt<tilt,"Water retains momentum after acceleration ends");
        for(int i=0;i<480;i++)liquid.Update(1f/60,new Vector2(2,0),"walk");
        Check(Math.Abs(liquid.Tilt)<.001f&&Math.Abs(liquid.Wave)<.001f,"Water settles under constant velocity instead of oscillating forever");
        liquid.Event(new PresentationEvent{Trigger="land",Speed=20});liquid.Update(1f/60,Vector2.Zero,"land");
        Check(liquid.Wave>0,"Landing excites a vertical surface wave");
        liquid.Event(new PresentationEvent{Trigger="splat"});for(int i=0;i<120;i++)liquid.Update(1f/60,Vector2.Zero,"splat");
        Check(liquid.Level<.001f,"Splat drains the vessel and it stays empty while flattened");
        for(int i=0;i<30;i++)liquid.Update(1f/60,Vector2.Zero,"recover");
        Check(liquid.Level>.1f&&liquid.Level<.2f,"Recovery refills gradually instead of snapping to full");
        liquid.Reset();Check(liquid.Level==LiquidMotion.Capacity&&liquid.Tilt==0&&liquid.Wave==0,"Restore resets the full vessel and clears motion history");
        bool bounded=true;
        for(int i=0;i<400;i++){liquid.Update(.1f,new Vector2(i%2==0?10000:-10000,i%3==0?10000:-10000),"rise");bounded &= Math.Abs(liquid.Tilt)<=.3f&&Math.Abs(liquid.Wave)<=.14f;}
        Check(bounded,"Extreme velocity changes cannot destabilize the cosmetic fluid integrator");
    }
    private static void VesselGraphicsTests(string path,GraphicsDevice device,SpriteBatch batch,RenderTarget2D outer)
    {
        var skin=new JumpKing.Workshop.Collection(path);var catalog=new Catalog();string parent=Catalog.PackageId(skin);
        catalog.Roots.Add(parent,path);catalog.Advanced=SkinLibrary.Discover(catalog);catalog.Sets.Add(new SetSource{Id=parent,Name="Vessel King"});
        foreach(var entry in skin.Info.Reskins)catalog.Sources.Add(new Source{Id=Catalog.SourceId(skin,(int)entry.skin,true),Name="Vessel King",Item=(int)entry.skin,Asset=Path.Combine(path,entry.name),ParentId=parent,Collection=true});
        var crownOutfit=new Outfit{ParentId=parent,ParentName="Vessel King"};
        using(var original=PreparedAppearance.Build(crownOutfit,catalog,false))
        {
            crownOutfit.SetMaterial((int)JumpKing.MiscEntities.WorldItems.Items.Crown,MaterialKind.Diamond);
            using(var changed=PreparedAppearance.Build(crownOutfit,catalog,false))
            {
                var source=NativeAppearance.Frames(original.Base.m_groups[2])[10];
                var result=NativeAppearance.Frames(changed.Base.m_groups[2])[10];
                var before=FramePixels(source);var after=FramePixels(result);
                var mask=OwnershipMasks.Embedded((int)JumpKing.MiscEntities.WorldItems.Items.Crown,2,10,64,64);
                Check(before.Where((c,i)=>!mask.Visible[i]).SequenceEqual(after.Where((c,i)=>!mask.Visible[i]))
                    && after.Where((c,i)=>mask.Visible[i]&&c.A>0).All(c=>c.A<255)
                    && changed.Advanced.Animations[NativeAppearance.BaseItem].MaterialFrames.Count==0,
                    "Vessel King keeps its water shader and ending body while the embedded crown becomes Glass");
                SaveEmbeddedFrame(device,source,Path.Combine(output,"vessel-crown-original.png"));
                SaveEmbeddedFrame(device,result,Path.Combine(output,"vessel-crown-glass.png"));
            }
        }
        foreach(var kind in new[]{MaterialKind.Gold,MaterialKind.Diamond,MaterialKind.RedVelvet,MaterialKind.Cosmic})
        {
            var materialOutfit=new Outfit{ParentId=parent,ParentName="Vessel King",Material=kind};
            using(var materialAppearance=PreparedAppearance.Build(materialOutfit,catalog,false))
            using(var materialActor=new PresentationActor(materialAppearance,new int[0],true))
            {
                var animation=materialActor.Assets.Animations[NativeAppearance.BaseItem];
                Check(animation.Material==kind&&animation.MaterialFrames.Count==animation.Definition.clips.Sum(c=>c.frames.Count),kind+" overrides the collection surface for every animated pose");
                var clip=animation.Definition.clips.First(c=>c.state=="idle");var frame=clip.frames[0];
                var textured=animation.MaterialFrames[frame];var data=FramePixels(textured);var source=materialActor.Assets.Texture(animation.Package,clip.texture);
                var pixels=new Color[source.Width*source.Height];source.GetData(pixels);
                Check(data.Any(c=>c.A>0)&&data.Where((c,i)=>c!=pixels[(frame.y+i/frame.width)*source.Width+frame.x+i%frame.width]).Count()>100,"Animated "+kind+" produces material pixels instead of hiding behind the collection shader");
                device.SetRenderTarget(outer);device.Clear(Color.Transparent);PresentationDraw.Current=materialActor;
                try{batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);NativeAppearance.Frames(materialAppearance.Base.regular)[0].Draw(new Vector2(240,200));batch.End();}
                finally{PresentationDraw.Current=null;device.SetRenderTarget(null);}
            }
            materialOutfit.SetMaterial(NativeAppearance.BaseItem,MaterialKind.Original);
            using(var restored=PreparedAppearance.Build(materialOutfit,catalog,false))
                Check(restored.Advanced.Animations[NativeAppearance.BaseItem].MaterialFrames.Count==0,"Original texture restores the collection shader over an inherited "+kind+" outfit material");
        }
        using(var prepared=PreparedAppearance.Build(new Outfit{ParentId=parent,ParentName="Vessel King"},catalog,true))
        using(var actor=new PresentationActor(prepared,new int[0],true))
        using(var other=new PresentationActor(prepared,new int[0],true))
        using(var target=new RenderTarget2D(device,96,80))
        using(var sheet=new RenderTarget2D(device,960,420))
        {
            Check(actor.Liquid!=null&&other.Liquid!=actor.Liquid,"Vessel shaders opt into independent actor-owned water simulation");
            var binding=actor.Assets.Animations[NativeAppearance.BaseItem];var frames=NativeAppearance.Frames(prepared.Base.regular);
            VesselPerformanceTests(actor,frames[0],device,batch,outer);
            var sprite=frames[0];var pictures=new List<Color[]>();
            Func<bool,Color[]> render=particles=>{
                device.SetRenderTarget(target);device.Clear(Color.Transparent);PresentationDraw.Current=actor;
                batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                if(particles)PresentationDraw.Particles(actor,"back",new Vector2(48,54),1,false,Color.White);
                sprite.Draw(new Vector2(48,54),actor.Flipped?SpriteEffects.FlipHorizontally:SpriteEffects.None);
                if(particles)PresentationDraw.Particles(actor,"front",new Vector2(48,54),1,false,Color.White);
                batch.End();device.SetRenderTarget(null);var result=new Color[96*80];target.GetData(result);return result;
            };
            try
            {
                actor.Reset();actor.Update(1f/60);var full=render(false);pictures.Add(render(true));
                Check(full.Count(c=>c.A>0)>=300&&full.Any(c=>c.A>0&&c.A<100),"Vessel renders a readable contour around a transparent cavity");
                actor.SetState("walk");sprite=frames[1];actor.Velocity=new Vector2(3,0);for(int i=0;i<6;i++)actor.Update(1f/60);pictures.Add(render(true));
                sprite=frames[0];actor.SetState("idle");actor.Velocity=Vector2.Zero;for(int i=0;i<8;i++)actor.Update(1f/60);pictures.Add(render(true));
                actor.SetState("rise");sprite=frames[5];actor.Charge=1;actor.Velocity=new Vector2(2,-12);actor.Trigger("jump");for(int i=0;i<8;i++)actor.Update(1f/60);pictures.Add(render(true));
                actor.SetState("fall");sprite=frames[6];actor.Velocity=new Vector2(2,10);for(int i=0;i<8;i++)actor.Update(1f/60);pictures.Add(render(true));
                actor.SetState("land");sprite=frames[0];actor.Velocity=Vector2.Zero;var land=actor.Event("land");land.Speed=20;foreach(var channel in ManifestIO.Channels)actor.Dispatch(land,channel,true);
                for(int i=0;i<5;i++)actor.Update(1f/60);pictures.Add(render(true));
                actor.SetState("splat");sprite=frames[8];actor.Trigger("splat");for(int i=0;i<7;i++)actor.Update(1f/60);pictures.Add(render(true));
                Check(actor.Particles.Count>=20&&actor.Liquid.Level<LiquidMotion.Capacity,"Splat emits colliding droplets while the vessel drains");
                var drop=actor.Particles.First(p=>p.Effect.Definition.id=="spill");float beforeVelocity=drop.Velocity.Y;actor.Update(1f/60);
                Check(drop.Velocity.Y>beforeVelocity,"Spilled drops follow downward gravitational acceleration");
                for(int i=0;i<40;i++)actor.Update(1f/60);pictures.Add(render(true));
                int count=actor.Particles.Count;actor.Trigger("splat");Check(actor.Particles.Count==count,"An empty vessel cannot emit another full spill");
                actor.SetState("idle");sprite=frames[0];var empty=render(false);
                Check(full.Count(c=>c.B>c.R*1.3&&c.A>200)>empty.Count(c=>c.B>c.R*1.3&&c.A>200)+50,"Draining visibly removes the water rather than only spawning particles");
                for(int i=0;i<90;i++)actor.Update(1f/60);pictures.Add(render(true));
                for(int i=0;i<120;i++)actor.Update(1f/60);pictures.Add(render(true));
                Check(other.Liquid.Level==LiquidMotion.Capacity&&other.Liquid.Tilt==0,"Spilling does not affect a separate preview or ghost actor");
                bool stable=true,visible=true;
                foreach(string state in new[]{"idle","walk","charge","rise","fall","splat","recover","lookUp"})
                {
                    actor.Reset();actor.SetState(state);actor.Velocity=Vector2.Zero;actor.Update(1f/60);actor.Velocity=new Vector2(3,-8);
                    for(int tick=0;tick<12;tick++)
                    {
                        var first=render(false);float level=actor.Liquid.Level,tilt=actor.Liquid.Tilt;double time=actor.Time;
                        for(int draw=0;draw<3;draw++)stable &= first.SequenceEqual(render(false));
                        stable &= level==actor.Liquid.Level&&tilt==actor.Liquid.Tilt&&time==actor.Time;
                        visible &= first.Count(c=>c.A>0)>=300;actor.Update(1f/60);
                    }
                }
                Check(stable&&visible,"384 vessel draws at four renders per tick preserve water state and body coverage");
                actor.Reset();Check(actor.Liquid.Level==LiquidMotion.Capacity&&actor.Particles.Count==0,"Actor restore clears spilled water and resets the vessel");
                string[] labels={"REST","ACCELERATE","BRAKE","JUMP","FALL","LAND","SPLAT","DRAINED","REFILL","RESTORED"};
                device.SetRenderTarget(sheet);device.Clear(new Color(24,31,35));
                using(var tile=new Texture2D(device,96,80))for(int i=0;i<pictures.Count;i++)
                {
                    tile.SetData(pictures[i]);int x=i%5*192,y=i/5*210;
                    batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                    batch.Draw(tile,new Rectangle(x-48,y-26,288,240),Color.White);
                    batch.DrawString(Game1.instance.contentManager.font.MenuFontSmall,labels[i],new Vector2(x+18,y+185),new Color(165,226,233));batch.End();
                }
                device.SetRenderTarget(null);using(var file=File.Create(Path.Combine(output,"vessel-king.png")))sheet.SaveAsPng(file,sheet.Width,sheet.Height);
            }
            finally{PresentationDraw.Current=null;device.SetRenderTarget(outer);}
        }
    }
}
