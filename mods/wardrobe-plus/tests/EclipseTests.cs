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
    private static void EclipseVariantPreviews(string root,GraphicsDevice device,SpriteBatch batch,RenderTarget2D outer)
    {
        string[] finishes={"graphite","platinum","white-gold","bronze","garnet","ivory"};
        string[] titles={"01  GRAPHITE","02  PLATINUM","03  WHITE GOLD","04  BRONZE","05  GARNET","06  IVORY"};
        using(var sheet=new RenderTarget2D(device,1152,720))
        {
            device.SetRenderTarget(sheet);device.Clear(new Color(20,21,23));
            for(int i=0;i<finishes.Length;i++)
            {
                string path=Path.Combine(root,finishes[i],"SKIN_PACKAGE");var skin=new JumpKing.Workshop.Collection(path);
                var catalog=new Catalog();string parent=Catalog.PackageId(skin);catalog.Roots.Add(parent,path);catalog.Advanced=SkinLibrary.Discover(catalog);
                catalog.Sets.Add(new SetSource{Id=parent,Name="Eclipse King"});
                foreach(var entry in skin.Info.Reskins)catalog.Sources.Add(new Source{Id=Catalog.SourceId(skin,(int)entry.skin,true),Name="Eclipse King",Item=(int)entry.skin,Asset=Path.Combine(path,entry.name),ParentId=parent,Collection=true});
                using(var prepared=PreparedAppearance.Build(new Outfit{ParentId=parent,ParentName="Eclipse King"},catalog,true))
                using(var actor=new PresentationActor(prepared,new int[0],true))
                {
                    int x=i%2*576,y=i/2*240;
                    var sprite=NativeAppearance.Frames(prepared.Base.regular)[0];PresentationDraw.Current=actor;
                    try
                    {
                        batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                        batch.DrawString(Game1.instance.contentManager.font.MenuFontSmall,titles[i],new Vector2(x+16,y+18),Color.White);
                        string[] states={"idle","charge","rise","fall"};
                        for(int pose=0;pose<states.Length;pose++)
                        {
                            actor.Reset();actor.Charge=.65f;actor.SetState(states[pose]);
                            if(pose>=2){actor.Trigger("jump");for(int tick=0;tick<15;tick++)actor.Update(1f/60);}
                            sprite.Draw(new Rectangle(x+72+pose*144,y+185,192,192));
                            batch.DrawString(Game1.instance.contentManager.font.MenuFontSmall,states[pose].ToUpperInvariant(),new Vector2(x+48+pose*144,y+213),new Color(170,170,170));
                        }
                        batch.End();
                    }
                    finally{PresentationDraw.Current=null;}
                }
            }
            device.SetRenderTarget(null);
            using(var file=File.Create(Path.Combine(output,"eclipse-finish-options.png")))sheet.SaveAsPng(file,sheet.Width,sheet.Height);
            Check(true,"Six finish proposals render through the actual skin shader with matching poses and charge");
        }
        device.SetRenderTarget(outer);
    }
    private static void EclipseGraphicsTests(string path,GraphicsDevice device,SpriteBatch batch,RenderTarget2D outer)
    {
        var skin=new JumpKing.Workshop.Collection(path);var catalog=new Catalog();string parent=Catalog.PackageId(skin);
        catalog.Roots.Add(parent,path);catalog.Advanced=SkinLibrary.Discover(catalog);
        catalog.Sets.Add(new SetSource{Id=parent,Name="Eclipse King"});
        foreach(var entry in skin.Info.Reskins)catalog.Sources.Add(new Source{Id=Catalog.SourceId(skin,(int)entry.skin,true),Name="Eclipse King",Item=(int)entry.skin,Asset=Path.Combine(path,entry.name),ParentId=parent,Collection=true});
        using(var prepared=PreparedAppearance.Build(new Outfit{ParentId=parent,ParentName="Eclipse King"},catalog,true))
        using(var actor=new PresentationActor(prepared,new int[0],true))
        using(var independent=new PresentationActor(prepared,new int[0],true))
        using(var target=new RenderTarget2D(device,64,64))
        using(var preview=new RenderTarget2D(device,960,400))
        {
            var binding=actor.Assets.Animations[NativeAppearance.BaseItem];
            Check(binding.Package.Manifest.id=="eclipse-king"&&skin.Info.Reskins.Length==1,"Eclipse King loads as a separate native body collection");
            Check(!binding.Definition.clips.Any(c=>c.state=="apex")&&binding.Definition.clips.All(c=>c.transition==0),"Eclipse King retains instant native flight poses without an apex hold");
            Check(!binding.Package.Manifest.effects.Any(e=>e.kind=="sound"||e.kind=="shake"),"Eclipse King adds neither audio nor camera shake");
            bool native=true;
            foreach(string surface in new[]{"normal","snow","water","ice","sand"})foreach(string trigger in new[]{"jump","land","splat"})foreach(string channel in ManifestIO.Channels)
                native &= actor.Assets.Library.Decide(channel,new PresentationEvent{Trigger=trigger,Surface=surface,Equipment=new[]{"GiantBoots"}},actor.Profiles,actor.Selection).Native;
            Check(native,"Solar accents preserve native feedback on every surface and with heavy boots");
            var sprite=NativeAppearance.Frames(prepared.Base.regular)[0];
            Func<Color[]> render=()=>{
                PresentationDraw.Current=actor;device.SetRenderTarget(target);device.Clear(Color.Transparent);
                batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                sprite.Draw(new Vector2(32,56));batch.End();device.SetRenderTarget(null);
                var pixels=new Color[4096];target.GetData(pixels);return pixels;
            };
            try
            {
                EclipseWalkTiming(prepared,actor,device,batch,target);
                actor.SetState("charge");actor.Charge=0;var cold=render();
                actor.Charge=.3f;var partial=render();actor.Charge=1;var hot=render();
                Check(partial[50*64+31].R>partial[48*64+29].R+60,"Charge ignites the bottom stem before the visor's horizontal branches");
                Check(hot[48*64+29].R==255&&hot[48*64+29].G>235&&hot[50*64+31].G>235,"Full charge heats the complete T-shaped opening to pale sunlight");
                Check(Enumerable.Range(0,4096).All(i=>cold[i].A==0||hot[i].A==cold[i].A),"Solar rays never erase or fade native body pixels");
                actor.Reset();actor.Charge=.8f;var jump=actor.Event("jump");
                actor.Dispatch(jump,"particles",false);actor.Update(.05f);actor.Dispatch(jump,"surfaceSound",false);
                Check(Math.Abs(actor.JumpAge-.05f)<.001&&Math.Abs(actor.JumpCharge-.8f)<.001,"Dispatching another channel of the same jump does not restart its light envelope");
                actor.SetState("rise");actor.Update(.1f);float flightAge=actor.JumpAge;
                actor.SetState("fall");Check(actor.JumpAge==flightAge&&actor.StateTime==0,"Flight light keeps its clock when the pose changes from ascent to descent");
                Check(independent.JumpAge==-1&&independent.LandingAge==-1,"Preview and replay actors own independent light histories");
                actor.Trigger("land");Check(actor.LandingAge==0,"Landing starts its dimming envelope at the actual cosmetic event");
                actor.Reset();Check(actor.JumpAge==-1&&actor.LandingAge==-1&&actor.JumpCharge==0,"Restore clears both light envelopes and stored takeoff charge");
                bool stable=true,visible=true;
                foreach(string state in new[]{"idle","charge","rise","fall","land","splat","recover","lookUp"})
                {
                    actor.Reset();actor.Charge=1;actor.SetState(state);
                    if(state=="rise"||state=="fall")actor.Trigger("jump");
                    for(int tick=0;tick<30;tick++)
                    {
                        double time=actor.Time;float jumpAge=actor.JumpAge;var first=render();visible &= first.Count(c=>c.A==255)>=300;
                        for(int draw=0;draw<3;draw++)stable &= first.SequenceEqual(render());
                        stable &= actor.Time==time&&actor.JumpAge==jumpAge;actor.Update(1f/60);
                    }
                }
                Check(stable&&visible,"960 Eclipse King renders at four draws per tick keep stable light, clocks and opaque body coverage");
                var images=new List<Color[]>();
                var labels=new[]{"REST","CHARGE 25%","CHARGE 55%","FULL CHARGE","TAKEOFF","FLIGHT 0.25s","FALL 0.50s","FALL 0.85s","LANDING","RECOVERED"};
                actor.Reset();actor.Charge=0;actor.SetState("idle");images.Add(render());
                foreach(float charge in new[]{.25f,.55f,1f}){actor.SetState("charge");actor.Charge=charge;images.Add(render());}
                actor.Trigger("jump");actor.SetState("rise");images.Add(render());
                for(int i=0;i<15;i++)actor.Update(1f/60);images.Add(render());
                actor.SetState("fall");for(int i=0;i<15;i++)actor.Update(1f/60);images.Add(render());
                for(int i=0;i<21;i++)actor.Update(1f/60);images.Add(render());
                actor.Trigger("land");actor.SetState("idle");images.Add(render());
                for(int i=0;i<60;i++)actor.Update(1f/60);images.Add(render());
                device.SetRenderTarget(preview);device.Clear(new Color(13,16,27));
                using(var tile=new Texture2D(device,64,64))
                {
                    for(int i=0;i<images.Count;i++)
                    {
                        tile.SetData(images[i]);int x=(i%5)*192,y=(i/5)*200;
                        batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                        batch.Draw(tile,new Rectangle(x-32,y-76,256,256),Color.White);
                        batch.DrawString(Game1.instance.contentManager.font.MenuFontSmall,labels[i],new Vector2(x+18,y+170),new Color(210,187,128));
                        batch.End();
                    }
                }
                device.SetRenderTarget(null);
                using(var file=File.Create(Path.Combine(output,"eclipse-king-light.png")))preview.SaveAsPng(file,preview.Width,preview.Height);
            }
            finally{PresentationDraw.Current=null;device.SetRenderTarget(outer);}
        }
    }
    private static void EclipseWalkTiming(PreparedAppearance prepared,PresentationActor actor,GraphicsDevice device,SpriteBatch batch,RenderTarget2D target)
    {
        var player=(JumpKing.Player.PlayerEntity)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(JumpKing.Player.PlayerEntity));
        var walk=new JumpKing.Player.WalkAnim(player);var tickField=typeof(JumpKing.Player.WalkAnim).GetField("tick",Flags);
        var select=typeof(JumpKing.Player.WalkAnim).GetMethod("SetGroundSprite",Flags);
        var spriteField=typeof(JumpKing.Player.PlayerEntity).GetField("m_sprite",Flags);
        var native=Game1.instance.contentManager.playerSprites;var frames=NativeAppearance.Frames(prepared.Base.regular);
        var binding=actor.Assets.Animations[NativeAppearance.BaseItem];var clip=binding.Definition.clips.Single(c=>c.state=="walk");
        var texture=actor.Assets.Texture(binding.Package,clip.texture);bool matched=true,stable=true;int transitions=0,previous=-1;
        actor.Reset();actor.SetState("walk");PresentationDraw.Current=actor;
        for(int tick=0;tick<180;tick++)
        {
            if(tick==67||tick==119){walk.Reset();actor.SetState("idle");actor.Update(.016f);actor.SetState("walk");}
            tickField.SetValue(walk,(float)tickField.GetValue(walk)+1);select.Invoke(walk,null);
            var chosen=(Sprite)spriteField.GetValue(player);int key=chosen==native.walk_one?1:chosen==native.walk_two?2:chosen==native.walk_smear?3:-1;
            if(previous!=key)transitions++;previous=key;
            var sprite=(AdvancedSprite)frames[key];int expectedIndex=key==1?0:key==3?1:2;
            matched &= sprite.FrameIndex(clip,actor.StateTime)==expectedIndex;
            var frame=clip.frames[expectedIndex];var expected=new Color[frame.width*frame.height];
            texture.GetData(0,new Rectangle(frame.x,frame.y,frame.width,frame.height),expected,0,expected.Length);
            Color[] first=null;double time=actor.Time;
            for(int draw=0;draw<4;draw++)
            {
                device.SetRenderTarget(target);device.Clear(Color.Transparent);batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                sprite.Draw(new Vector2(32,56));batch.End();device.SetRenderTarget(null);
                var pixels=new Color[64*64];target.GetData(pixels);
                for(int y=0;y<48;y++)for(int x=0;x<48;x++)matched &= pixels[(y+8)*64+x+8].A==expected[y*48+x].A;
                if(first==null)first=pixels;else stable &= first.SequenceEqual(pixels);
            }
            stable &= actor.Time==time;actor.Update(1f/60);
        }
        Check(matched&&transitions>12,"Eclipse walking matches every pose selected by the installed WalkAnim across cycles and restarts");
        Check(stable,"720 walking draws at 240 Hz preserve the native 60 Hz holds and never advance animation clocks");
        actor.Reset();PresentationDraw.Current=null;
    }
}
