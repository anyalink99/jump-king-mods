using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WardrobePlus.Advanced
{
    internal sealed class AdvancedSprite : Sprite
    {
        internal readonly Sprite Fallback;
        internal readonly AnimationBinding Binding;
        internal readonly Vector2 Fit;
        internal readonly int NativeKey;
        internal AdvancedSprite(Sprite fallback,AnimationBinding binding,Vector2 fit,int nativeKey=-1)
        { Fallback=fallback; Binding=binding; Fit=fit; NativeKey=nativeKey; texture=fallback.texture; source=fallback.source; center=fallback.center; SetColor(fallback.GetColor()); }
        internal static void Wrap(PreparedAppearance prepared)
        {
            foreach(var binding in prepared.Advanced.Animations.Values)
            {
                JumpKing.JKMemory.KingSprites sprites;
                if(binding.Item==NativeAppearance.BaseItem) sprites=prepared.Base;
                else if(!prepared.Items.TryGetValue((JumpKing.MiscEntities.WorldItems.Items)binding.Item,out sprites)) continue;
                var frames=NativeAppearance.Frames(sprites.regular);
                foreach(var key in frames.Keys.ToArray())
                { var fit=prepared.SourceOutfit.Fit(prepared.Resolved[NativeAppearance.BaseItem].Id,prepared.Resolved[binding.Item].Id,binding.Item,0,key);
                    frames[key]=new AdvancedSprite(frames[key],binding,new Vector2(fit.X,fit.Y),key); }
            }
            AnimationBinding body;
            if(prepared.Advanced.Animations.TryGetValue(NativeAppearance.BaseItem,out body))foreach(var anchor in body.Definition.equipmentAnchors)
            {
                int item=(int)Enum.Parse(typeof(JumpKing.MiscEntities.WorldItems.Items),anchor.item);JumpKing.JKMemory.KingSprites sprites;
                if(prepared.Advanced.Animations.ContainsKey(item)||!prepared.Items.TryGetValue((JumpKing.MiscEntities.WorldItems.Items)item,out sprites))continue;
                var frames=NativeAppearance.Frames(sprites.regular);foreach(var key in frames.Keys.ToArray())frames[key]=new AnchoredSprite(frames[key],anchor);
            }
        }
        public override void Draw(float x,float y,SpriteEffects effect=SpriteEffects.None) { Draw(new Vector2(x,y),effect); }
        public override void Draw(Point p,SpriteEffects effect=SpriteEffects.None) { Draw(p.ToVector2(),effect); }
        public override void Draw(Vector2 p,SpriteEffects effect=SpriteEffects.None)
        { if(!DrawAdvanced(p,Vector2.One,effect)) { var old=Fallback.GetColor(); try {Fallback.SetColor(GetColor());Fallback.Draw(p,effect);}finally{Fallback.SetColor(old);} } }
        public override void Draw(Rectangle p,SpriteEffects effect=SpriteEffects.None)
        { if(!DrawAdvanced(p.Location.ToVector2(),new Vector2(p.Width/(float)source.Width,p.Height/(float)source.Height),effect)) base.Draw(p,effect); }
        internal bool DrawAdvanced(Vector2 position,Vector2 scale,SpriteEffects flip)
        {
            var actor=PresentationDraw.Current; if(actor==null || actor.Disposed || !actor.Assets.Packages.Contains(Binding.Package)) return false;
            var clip=actor.Clip(Binding,actor.State); if(clip==null)return false;
            bool flipped=(flip&SpriteEffects.FlipHorizontally)!=0;
            Vector2 fit=new Vector2(flipped?-Fit.X:Fit.X,Fit.Y); position+=fit*scale;
            PresentationDraw.Attachments(actor,Binding,position,scale,flip,"back",GetColor());
            var material=Binding.Package.Manifest.materials.Find(m=>m.id==Binding.Definition.material);
            float blend=actor.Blend(clip);var old=actor.Clip(Binding,actor.PreviousState);
            if(blend<1 && old!=null) DrawFrame(actor,old,actor.PreviousStateTime,material,position,scale,flip,GetColor()*(1-blend));
            DrawFrame(actor,clip,actor.StateTime,material,position,scale,flip,GetColor()*blend);
            PresentationDraw.Attachments(actor,Binding,position,scale,flip,"front",GetColor()); return true;
        }
        private void DrawFrame(PresentationActor actor,AnimationClip clip,double time,MaterialDefinition material,Vector2 position,Vector2 scale,SpriteEffects flip,Color tint)
        {
            var frame=clip.frames[FrameIndex(clip,time)];
            var origin=new Vector2((flip&SpriteEffects.FlipHorizontally)!=0?frame.width-frame.originX:frame.originX,frame.originY);
            position=(position-origin*scale).ToPoint().ToVector2()+origin*scale;
            Sprite surface;
            if(Binding.MaterialFrames.TryGetValue(frame,out surface))
            {
                var top=position-origin*scale;var cosmic=surface as CosmicSprite;
                if(cosmic!=null){cosmic.SetColor(tint);if(CosmicRenderer.Draw(cosmic,top,scale,flip,actor.WorldParticles))return;}
                Game1.spriteBatch.Draw(surface.texture,position,surface.source,tint,0,origin,scale,flip,0);return;
            }
            PresentationDraw.Material(actor,Binding,material,actor.Assets.Texture(Binding.Package,clip.texture),new Rectangle(frame.x,frame.y,frame.width,frame.height),position,origin,scale,flip,tint);
        }
        internal int FrameIndex(AnimationClip clip,double time)
        {int index=clip.nativeFrames.IndexOf(NativeKey);return index>=0?index:PresentationActor.FrameIndex(clip,time);}
    }
    internal sealed class AnchoredSprite:Sprite
    {
        private readonly Sprite fallback;private readonly EquipmentAnchor anchor;
        internal AnchoredSprite(Sprite sprite,EquipmentAnchor value){fallback=sprite;anchor=value;texture=sprite.texture;source=sprite.source;center=sprite.center;SetColor(sprite.GetColor());}
        internal Vector2 Offset(SpriteEffects flip){var actor=PresentationDraw.Current;if(actor==null||actor.Disposed)return Vector2.Zero;return actor.Anchor(anchor.anchor)-new Vector2((flip&SpriteEffects.FlipHorizontally)!=0?-anchor.nativeX:anchor.nativeX,anchor.nativeY);}
        public override void Draw(Vector2 position,SpriteEffects flip=SpriteEffects.None){var color=fallback.GetColor();try{fallback.SetColor(GetColor());fallback.Draw(position+Offset(flip),flip);}finally{fallback.SetColor(color);}}
        public override void Draw(float x,float y,SpriteEffects flip=SpriteEffects.None){Draw(new Vector2(x,y),flip);}
        public override void Draw(Point position,SpriteEffects flip=SpriteEffects.None){Draw(position.ToVector2(),flip);}
        public override void Draw(Rectangle rectangle,SpriteEffects flip=SpriteEffects.None){rectangle.Offset((Offset(flip)*new Vector2(rectangle.Width/(float)source.Width,rectangle.Height/(float)source.Height)).ToPoint());var color=fallback.GetColor();try{fallback.SetColor(GetColor());fallback.Draw(rectangle,flip);}finally{fallback.SetColor(color);}}
    }
    internal static class PresentationDraw
    {
        [ThreadStatic] internal static PresentationActor Current;
        private static readonly BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private static readonly FieldInfo sort=typeof(SpriteBatch).GetField("_sortMode",flags),blend=typeof(SpriteBatch).GetField("_blendState",flags),sampler=typeof(SpriteBatch).GetField("_samplerState",flags),
            depth=typeof(SpriteBatch).GetField("_depthStencilState",flags),raster=typeof(SpriteBatch).GetField("_rasterizerState",flags),effect=typeof(SpriteBatch).GetField("_effect",flags),matrix=typeof(SpriteBatch).GetField("_matrix",flags);
        internal static bool Pass(Effect shader,BlendState blending,Action draw)
        {
            if(shader==null && blending==null) {draw();return true;}
            if(sort==null||blend==null||sampler==null||depth==null||raster==null||effect==null||matrix==null)return false;
            var b=Game1.spriteBatch; var s=(SpriteSortMode)sort.GetValue(b); var e=(Effect)effect.GetValue(b);
            var d=(DepthStencilState)depth.GetValue(b);
            if(e!=null || (s!=SpriteSortMode.Deferred && s!=SpriteSortMode.Immediate) || (d!=null && (d.StencilEnable||d.DepthBufferEnable)))return false;
            var bl=(BlendState)blend.GetValue(b);var sa=(SamplerState)sampler.GetValue(b);var r=(RasterizerState)raster.GetValue(b);var m=(Matrix?)matrix.GetValue(b);
            var device=b.GraphicsDevice; var textures=new Texture[4]; var samplers=new SamplerState[4];
            for(int i=0;i<4;i++){textures[i]=device.Textures[i];samplers[i]=device.SamplerStates[i];}
            var db=device.BlendState;var dd=device.DepthStencilState;var dr=device.RasterizerState;
            b.End();bool begun=false;
            try { if(shader!=null && shader.Parameters["MatrixTransform"]!=null) shader.Parameters["MatrixTransform"].SetValue((m??Matrix.Identity)*Matrix.CreateOrthographicOffCenter(0,device.Viewport.Width,device.Viewport.Height,0,0,-1));
                b.Begin(SpriteSortMode.Deferred,blending??bl,sa,d,r,shader,m);begun=true;draw();b.End();begun=false; }
            finally { if(begun)b.End(); for(int i=0;i<4;i++){device.Textures[i]=textures[i];device.SamplerStates[i]=samplers[i];}
                device.BlendState=db;device.DepthStencilState=dd;device.RasterizerState=dr;b.Begin(s,bl,sa,d,r,e,m); }
            return true;
        }
        internal static void Material(PresentationActor actor,AnimationBinding binding,MaterialDefinition material,Texture2D texture,Rectangle rect,Vector2 position,Vector2 origin,Vector2 scale,SpriteEffects flip,Color tint)
        {
            Effect shader=null;
            if(material!=null)
            {
                var color=PresentationActor.Color(material.color);
                if(material.pulse>0) tint*=1-material.pulse*.5f+(float)Math.Sin(actor.Time*material.pulseSpeed*Math.PI*2)*material.pulse*.5f;
                if(material.kind!="original" && material.kind!="shader")
                {
                    shader=actor.Assets.SurfaceShader;
                    string[] modes={"tint","palette","glow","scroll","gold","glass","magenta","cosmic"};
                    var pattern=material.texture.Length>0?actor.Assets.Texture(binding.Package,material.texture):material.kind=="cosmic"?actor.Assets.Cosmic:texture;
                    shader.Parameters["Mode"].SetValue((float)(Array.IndexOf(modes,material.kind)+1));
                    shader.Parameters["Strength"].SetValue(material.strength);shader.Parameters["Time"].SetValue((float)actor.Time);shader.Parameters["Charge"].SetValue(actor.Charge);
                    shader.Parameters["Tint"].SetValue(color.ToVector4());shader.Parameters["Scroll"].SetValue(new Vector2(material.scrollX,material.scrollY));
                    shader.Parameters["Pattern"].SetValue(pattern);shader.Parameters["PatternSize"].SetValue(new Vector2(pattern.Width,pattern.Height));shader.Parameters["AtlasSize"].SetValue(new Vector2(texture.Width,texture.Height));
                    shader.Parameters["HasMask"].SetValue(material.mask.Length>0?1f:0f);
                    shader.Parameters["Mask"].SetValue(material.mask.Length>0?actor.Assets.Texture(binding.Package,material.mask):actor.Assets.White);
                    Texture2D palette;shader.Parameters["Palette"].SetValue(actor.Assets.Textures.TryGetValue(binding.Package.Manifest.id+"/@palette/"+material.id,out palette)?palette:actor.Assets.White);
                }
                if(material.kind=="shader" && actor.Assets.Shaders.TryGetValue(binding.Package.Manifest.id+"/"+material.id,out shader))
                {
                    if(shader.Parameters["Time"]!=null)shader.Parameters["Time"].SetValue((float)actor.Time);
                    if(shader.Parameters["Charge"]!=null)shader.Parameters["Charge"].SetValue(actor.Charge);
                    if(shader.Parameters["IsCharging"]!=null)shader.Parameters["IsCharging"].SetValue(actor.State=="charge"?1f:0f);
                    if(shader.Parameters["JumpAge"]!=null)shader.Parameters["JumpAge"].SetValue(actor.JumpAge);
                    if(shader.Parameters["LandingAge"]!=null)shader.Parameters["LandingAge"].SetValue(actor.LandingAge);
                    if(shader.Parameters["JumpCharge"]!=null)shader.Parameters["JumpCharge"].SetValue(actor.JumpCharge);
                    if(shader.Parameters["StateTime"]!=null)shader.Parameters["StateTime"].SetValue((float)actor.StateTime);
                    if(actor.Liquid!=null)
                    {
                        if(shader.Parameters["LiquidLevel"]!=null)shader.Parameters["LiquidLevel"].SetValue(actor.Liquid.Level);
                        if(shader.Parameters["LiquidTilt"]!=null)shader.Parameters["LiquidTilt"].SetValue(actor.Liquid.Tilt);
                        if(shader.Parameters["LiquidWave"]!=null)shader.Parameters["LiquidWave"].SetValue(actor.Liquid.Wave);
                        if(shader.Parameters["Facing"]!=null)shader.Parameters["Facing"].SetValue((flip&SpriteEffects.FlipHorizontally)!=0?-1f:1f);
                    }
                    if(shader.Parameters["Tint"]!=null)shader.Parameters["Tint"].SetValue(color.ToVector4());
                    if(shader.Parameters["Mask"]!=null && material.mask.Length>0)shader.Parameters["Mask"].SetValue(actor.Assets.Texture(binding.Package,material.mask));
                    if(shader.Parameters["Pattern"]!=null && material.texture.Length>0)shader.Parameters["Pattern"].SetValue(actor.Assets.Texture(binding.Package,material.texture));
                }
            }
            if(shader!=null && shader.Parameters["SpriteTexture"]!=null)shader.Parameters["SpriteTexture"].SetValue(texture);
            Action draw=()=>Game1.spriteBatch.Draw(texture,position,rect,tint,0,origin,scale,flip,0);
            if(!Pass(shader,null,draw)) draw();
        }
        internal static void Attachments(PresentationActor actor,AnimationBinding binding,Vector2 position,Vector2 scale,SpriteEffects flip,string layer,Color color)
        {
            if(binding.Definition.attachments.Count==0)return;
            var computed=new Dictionary<string,Tuple<Vector2,float>>();
            foreach(var node in binding.Definition.attachments.Where(n=>n.layer==layer))
            {
                var pose=AttachmentPose(actor,binding,node,computed);
                bool flipped=(flip&SpriteEffects.FlipHorizontally)!=0; var origin=new Vector2(flipped?node.width-node.originX:node.originX,node.originY);
                Game1.spriteBatch.Draw(actor.Assets.Texture(binding.Package,node.texture),position+pose.Item1*scale,new Rectangle(node.x,node.y,node.width,node.height),
                    color,pose.Item2,origin,scale,flip,0);
            }
        }
        private static Tuple<Vector2,float> AttachmentPose(PresentationActor actor,AnimationBinding binding,Attachment node,Dictionary<string,Tuple<Vector2,float>> cache)
        {
            Tuple<Vector2,float> pose;if(cache.TryGetValue(node.id,out pose))return pose;
            var parent=node.parent.Length==0?Tuple.Create(actor.Anchor(binding,node.anchor),actor.AnchorRotation(binding,node.anchor)):AttachmentPose(actor,binding,binding.Definition.attachments.Find(n=>n.id==node.parent),cache);
            float sign=actor.Flipped?-1:1; float angle=parent.Item2+MathHelper.ToRadians((node.rotation+actor.Joint(binding,node))*sign);
            var offset=Vector2.Transform(new Vector2(node.offsetX*sign,node.offsetY),Matrix.CreateRotationZ(parent.Item2));
            pose=Tuple.Create(parent.Item1+offset,angle);cache[node.id]=pose;return pose;
        }
        internal static void Particles(PresentationActor actor,string layer,Vector2 origin,float scale,bool world,Color tint)
        {
            if(actor.Particles.Count==0)return;
            for(int pass=0;pass<2;pass++)
            {
                bool additive=pass==1,found=false;
                for(int i=0;i<actor.Particles.Count;i++){var d=actor.Particles[i].Effect.Definition;if(d.layer==layer&&(d.blend=="additive")==additive){found=true;break;}}
                if(!found)continue;
                Action draw=delegate {
                    actor.ParticleBatch.Clear();
                    for(int particle=0;particle<actor.Particles.Count;particle++)
                    {
                        var p=actor.Particles[particle];
                        var d=p.Effect.Definition;if(d.layer!=layer || (d.blend=="additive")!=additive)continue;
                        float progress=p.Age/p.Life; var raw=Color.Lerp(p.StartColor,p.EndColor,progress);
                        var color=new Color(raw.ToVector4()*new Vector4(raw.A/255f,raw.A/255f,raw.A/255f,1)*tint.ToVector4());
                        Vector2 pos=p.Position+(d.space=="actor"?actor.Position:Vector2.Zero);
                        pos=world?Camera.TransformVector2(pos):origin+(pos-actor.Position)*scale;
                        Texture2D texture;Rectangle rect;
                        if(d.native.Length>0) {var sprites=p.NativeSprites;if(sprites==null||sprites.Length==0)continue;var sprite=sprites[Math.Min(sprites.Length-1,(int)(p.Age*d.fps))];texture=sprite.texture;rect=sprite.source;}
                        else if(d.texture.Length>0){texture=p.Texture;int frame=(int)(p.Age*d.fps)%(d.columns*d.rows);rect=new Rectangle(frame%d.columns*(texture.Width/d.columns),frame/d.columns*(texture.Height/d.rows),texture.Width/d.columns,texture.Height/d.rows);}
                        else {texture=Game1.instance.contentManager.Pixel.texture;rect=new Rectangle(0,0,1,1);}
                        float size=MathHelper.Lerp(d.size,d.endSize,progress)*scale;
                        actor.ParticleBatch.Add(texture,pos,rect,color,p.Rotation,new Vector2(rect.Width*.5f,rect.Height*.5f),new Vector2(size));
                    }
                    actor.ParticleBatch.Flush(Game1.spriteBatch);
                };
                if(!Pass(null,additive?BlendState.Additive:null,draw)) draw();
            }
        }
    }
}
