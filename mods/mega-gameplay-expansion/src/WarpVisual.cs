using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using EntityComponent;
using JumpKing;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaGameplayExpansion
{
    internal sealed class WarpImage
    {
        internal sealed class Layer
        {
            internal Texture2D Texture;
            internal Rectangle Source;
            internal Vector2 Center;
            internal Color Tint;
            internal Color[] Pixels;
            internal bool Rasterized;
            internal Vector2? CapturedOffset;
            internal Vector2 ScatterCenter;
            internal PixelPath[] Departure, Arrival;
            internal Vector2 TopLeft(Vector2 anchor)
            {
                // Captured texels already occupy the screen pixel grid. Retain
                // their exact integer offset; normalized pivots can round twice.
                if(CapturedOffset.HasValue)
                    return new Vector2((float)Math.Floor(anchor.X),(float)Math.Floor(anchor.Y))+CapturedOffset.Value;
                return (anchor-new Vector2(Source.Width,Source.Height)*Center).ToPoint().ToVector2();
            }
        }
        private static readonly ConditionalWeakTable<Texture2D,Dictionary<Rectangle,Color[]>> Texels = new ConditionalWeakTable<Texture2D,Dictionary<Rectangle,Color[]>>();
        private static readonly HashSet<Type> CaptureFailures=new HashSet<Type>();
        private readonly Vector2 captureAnchor;
        internal readonly Sprite Original, ArrivalSprite;
        internal readonly SpriteEffects Flip;
        internal readonly Layer[] Layers, ArrivalLayers;
        internal WarpImage(Sprite sprite, Sprite arrival, SpriteEffects flip,Vector2? anchor=null)
        {
            captureAnchor=anchor??Vector2.Zero;
            Original = sprite; ArrivalSprite = arrival; Flip = flip;
            var layers = new List<Layer>(); Collect(sprite, layers, 0); Layers = layers.ToArray();
            layers.Clear(); Collect(arrival, layers, 0); ArrivalLayers = layers.ToArray();
        }
        private WarpImage(WarpImage departure,Sprite arrival,Vector2? anchor)
        {
            captureAnchor=anchor??departure.captureAnchor;
            Original=departure.Original; Flip=departure.Flip; Layers=departure.Layers;
            ArrivalSprite=arrival;
            var layers=new List<Layer>(); Collect(arrival,layers,0); ArrivalLayers=layers.ToArray();
        }
        // A pending forecast may already be in a snapshot. Select the landing
        // pose on a new image, without replacing that snapshot's arrival data.
        internal WarpImage WithArrival(Sprite sprite,Vector2? anchor=null) { return new WarpImage(this,sprite,anchor); }
        private void Collect(Sprite sprite, List<Layer> layers, int depth)
        {
            if (sprite == null || depth > 8 || layers.Count >= 32) return;
            if (sprite.GetType().FullName == "JumpKing.XnaWrappers.LayeredSprite")
            {
                foreach (Sprite layer in (IEnumerable)sprite.GetType().GetProperty("Sprites").GetValue(sprite, null)) Collect(layer,layers,depth+1);
                return;
            }
            if(sprite.GetType()!=typeof(Sprite))
            {
                try { var rendered=SpriteCapture.Capture(sprite,Flip,captureAnchor);FindCenter(rendered);layers.Add(rendered);return; }
                catch(Exception error)
                {
                    if(CaptureFailures.Add(sprite.GetType()))WarpDiagnostics.Write("Custom sprite capture fallback: "+sprite.GetType().FullName+"; "+error.Message);
                }
                // Texture-backed subclasses still have a usable static appearance.
                // A visual fallback must not veto an otherwise supported flight.
                if(sprite.texture==null) { AddSilhouette(layers);return; }
            }
            if (sprite.source.Width < 1 || sprite.source.Height < 1 || (long)sprite.source.Width * sprite.source.Height > 16384)
            { AddSilhouette(layers);return; }
            var captured=new Layer { Texture=sprite.texture, Source=sprite.source, Center=sprite.center, Tint=sprite.GetColor() };
            try { ReadPixels(captured); layers.Add(captured); }
            catch(Exception error)
            { if(CaptureFailures.Add(sprite.GetType()))WarpDiagnostics.Write("Sprite texel fallback: "+sprite.GetType().FullName+"; "+error.Message);AddSilhouette(layers); }
        }
        private static void AddSilhouette(List<Layer> layers)
        {
            var pixels=new Color[18*26];for(int i=0;i<pixels.Length;i++)pixels[i]=Color.White;
            var batch=Game1.spriteBatch;
            var layer=new Layer {Texture=batch==null ? null : WarpVisual.WhitePixel(batch.GraphicsDevice),
                Source=new Rectangle(0,0,18,26),Center=new Vector2(.5f,1),Tint=Color.White,Pixels=pixels,Rasterized=true};
            FindCenter(layer);layers.Add(layer);
        }
        internal static void ReadPixels(Layer layer)
        {
            if(layer.Texture==null || layer.Pixels!=null) return;
            var cache=Texels.GetOrCreateValue(layer.Texture);
            Color[] pixels;
            if(!cache.TryGetValue(layer.Source,out pixels))
            {
                pixels=new Color[layer.Source.Width*layer.Source.Height];
                layer.Texture.GetData<Color>(0,layer.Source,pixels,0,pixels.Length);
                cache.Add(layer.Source,pixels);
            }
            layer.Pixels=pixels;
            FindCenter(layer);
        }
        private static void FindCenter(Layer layer)
        {
            if(layer.Pixels==null)return;
            var pixels=layer.Pixels;
            Vector2 sum=Vector2.Zero; int visible=0;
            for(int y=0;y<layer.Source.Height;y++) for(int x=0;x<layer.Source.Width;x++)
                if(pixels[y*layer.Source.Width+x].A!=0) { sum+=new Vector2(x,y); visible++; }
            layer.ScatterCenter=visible==0?Vector2.Zero:sum/visible;
        }
        internal static Vector2 ParticleCenter(Layer layer,SpriteEffects flip)
        {
            if(layer.Rasterized)flip=SpriteEffects.None;
            var center=layer.ScatterCenter;
            if((flip&SpriteEffects.FlipHorizontally)!=0) center.X=layer.Source.Width-1-center.X;
            if((flip&SpriteEffects.FlipVertically)!=0) center.Y=layer.Source.Height-1-center.Y;
            return center;
        }
        internal void Prepare(LevelScreen[] screens,Vector2 origin,Vector2 destination)
        {
            PrepareDeparture(screens,origin);
            PrepareArrival(screens,destination);
        }
        internal void PrepareDeparture(LevelScreen[] screens,Vector2 origin)
        {
            foreach(var layer in Layers)
                layer.Departure=PrepareLayer(layer,Flip,screens,origin+new Vector2(9,26));
        }
        internal void PrepareArrival(LevelScreen[] screens,Vector2 destination)
        {
            foreach(var layer in ArrivalLayers)
                layer.Arrival=PrepareLayer(layer,Flip,screens,destination+new Vector2(9,26));
        }
        internal static PixelPath[] PrepareLayer(Layer layer,SpriteEffects flip,LevelScreen[] screens,Vector2 anchor)
        {
            if(layer.Rasterized)flip=SpriteEffects.None;
            if(layer.Texture==null) return null;
            ReadPixels(layer);
            int width=layer.Source.Width,height=layer.Source.Height;
            Vector2 topLeft=layer.CapturedOffset.HasValue ? layer.TopLeft(anchor) : anchor-new Vector2(width,height)*layer.Center;
            topLeft=new Vector2((float)Math.Floor(topLeft.X),(float)Math.Floor(topLeft.Y));
            var region=new Rectangle((int)topLeft.X-24,(int)topLeft.Y-24,width+48,height+48);
            var space=new PixelCollision(screens,region);
            var paths=new PixelPath[width*height];
            Vector2 center=ParticleCenter(layer,flip);
            for(int y=0;y<height;y++) for(int x=0;x<width;x++)
            {
                if(layer.Pixels[y*width+x].A==0) continue;
                int px=(flip&SpriteEffects.FlipHorizontally)!=0?width-1-x:x;
                int py=(flip&SpriteEffects.FlipVertically)!=0?height-1-y:y;
                paths[y*width+x]=new PixelPath(space,topLeft+new Vector2(px,py),MatrixPixels.At(px,py,width,height,1,false,center).Offset);
            }
            return paths;
        }
    }

    // Compact RGB disassembly; no upward plume or vertical exhaust.
    internal static class MatrixPixels
    {
        internal const float Dissolve=.10f, Transfer=.40f/3, Assemble=.32f/3, Duration=Transfer+Assemble;
        private static readonly Color[] Palette={new Color(255,45,65),new Color(40,255,90),new Color(55,105,255)};
        internal static Color Colour(int x,int y) { return Palette[(x*13+y*7)%3]; }
        internal struct Sample { internal Vector2 Offset; internal float Alpha, Scatter; }
        private static float Clamp(float value) { return Math.Max(0,Math.Min(1,value)); }
        internal static Sample At(int x,int y,int width,int height,float progress,bool arriving,Vector2? center=null)
        {
            float noise=((x*37+y*61+11)%97)/96f;
            float phase=arriving?1-Clamp(progress):Clamp(progress);
            float delay=noise*.20f;
            float t=phase>=1?1:Clamp((phase-delay)/.80f), ease=t*t*(3-2*t);
            // Radial motion with small deterministic jitter, no shared Y bias.
            Vector2 direction=new Vector2(x,y)-(center??new Vector2((width-1)*.5f,(height-1)*.5f));
            if(direction.LengthSquared()<.1f) direction=Vector2.UnitX;
            direction.Normalize();
            float angle=(noise-.5f)*1.8f, cosine=(float)Math.Cos(angle), sine=(float)Math.Sin(angle);
            direction=new Vector2(direction.X*cosine-direction.Y*sine,direction.X*sine+direction.Y*cosine);
            return new Sample {
                Offset=direction*(4+noise*8)*ease,
                Alpha=t>=1?0:1-Clamp((t-.60f)/.40f), Scatter=ease
            };
        }
    }

    internal sealed class WarpVisual : Component
    {
        private static readonly FieldInfo SpriteField=typeof(PlayerEntity).GetField("m_sprite",BindingFlags.Instance|BindingFlags.NonPublic);
        private static readonly FieldInfo FlipField=typeof(PlayerEntity).GetField("m_flip",BindingFlags.Instance|BindingFlags.NonPublic);
        private readonly PlayerEntity player;
        private WarpSprite wrapper;
        private static readonly ConditionalWeakTable<GraphicsDevice,Texture2D> WhitePixels=new ConditionalWeakTable<GraphicsDevice,Texture2D>();
        internal static Texture2D WhitePixel(GraphicsDevice device)
        {
            return WhitePixels.GetValue(device,delegate(GraphicsDevice value) {
                var pixel=new Texture2D(value,1,1); pixel.SetData(new[]{Color.White});
                value.Disposing+=delegate { pixel.Dispose(); };
                return pixel;
            });
        }
        internal Action<float> Advance;
        internal WarpVisual(PlayerEntity target)
        {
            player=target;
            if(SpriteField==null || FlipField==null) throw new InvalidOperationException("Native player sprite contract unavailable");
        }
        internal WarpImage CaptureImage()
        {
            var sprite=AirDashVisual.Unwrap(SpriteField.GetValue(player) as Sprite);
            var prior=sprite as WarpSprite;
            var game=Game1.instance;
            if(game==null || game.contentManager==null || game.contentManager.playerSprites==null || game.contentManager.playerSprites._CurrentSprites==null)
                throw new InvalidOperationException("Native standing sprite unavailable");
            // The native idle accessor already includes the active reskin/outfit
            // layers. Never reinterpret the charge atlas coordinates as idle.
            return new WarpImage(prior==null?sprite:prior.Image.Original,game.contentManager.playerSprites.idle,(SpriteEffects)FlipField.GetValue(player),
                Camera.TransformVector2(player.m_body.Position+new Vector2(9,26)));
        }
        internal void Present(WarpImage image,Vector2 origin,Vector2 destination,float age)
        {
            if(wrapper==null || wrapper.Image!=image) wrapper=new WarpSprite(image);
            wrapper.Origin=origin; wrapper.Destination=destination; wrapper.Age=age;
            player.SetSprite(wrapper);
        }
        internal void Reset(bool landed=false)
        {
            if(wrapper!=null && ReferenceEquals(SpriteField.GetValue(player),wrapper)) player.SetSprite(landed?wrapper.Image.ArrivalSprite:wrapper.Image.Original);
            wrapper=null;
        }
        protected override void Update(float delta) { if(Advance!=null) Advance(delta); }
        protected override void LateUpdate(float delta) { if(wrapper!=null) player.SetSprite(wrapper); }
        private sealed class WarpSprite : Sprite
        {
            internal readonly WarpImage Image;
            internal Vector2 Origin,Destination;
            internal float Age;
            internal WarpSprite(WarpImage image) { Image=image; }
            public override void Draw(Vector2 position,SpriteEffects effects=SpriteEffects.None)
            {
                if(Age>=MatrixPixels.Dissolve && Age<MatrixPixels.Transfer) return;
                bool arriving=Age>=MatrixPixels.Transfer;
                float progress=arriving?(Age-MatrixPixels.Transfer)/MatrixPixels.Assemble:Age/MatrixPixels.Dissolve;
                Vector2 anchor=Camera.TransformVector2((arriving?Destination:Origin)+new Vector2(9,26));
                foreach(var layer in (arriving?Image.ArrivalLayers:Image.Layers)) DrawLayer(layer,Image.Flip,anchor,progress,arriving,arriving?layer.Arrival:layer.Departure);
            }
            public override void Draw(float x,float y,SpriteEffects effects=SpriteEffects.None) { Draw(new Vector2(x,y),effects); }
            public override void Draw(Point point,SpriteEffects effects=SpriteEffects.None) { Draw(point.ToVector2(),effects); }
            public override void Draw(Rectangle rectangle,SpriteEffects effects=SpriteEffects.None) { Draw(rectangle.Location.ToVector2(),effects); }
        }
        internal static void DrawLayer(WarpImage.Layer layer,SpriteEffects flip,Vector2 anchor,float progress,bool arriving,PixelPath[] paths=null)
        {
            if(layer.Rasterized)flip=SpriteEffects.None;
            if(layer.Texture==null) return;
            WarpImage.ReadPixels(layer);
            Texture2D white=WhitePixel(layer.Texture.GraphicsDevice);
            int width=layer.Source.Width,height=layer.Source.Height;
            Vector2 center=WarpImage.ParticleCenter(layer,flip);
            Vector2 topLeft=layer.TopLeft(anchor);
            for(int y=0;y<height;y++) for(int x=0;x<width;x++)
            {
                int index=y*width+x;
                if(layer.Pixels[index].A==0) continue;
                int px=(flip&SpriteEffects.FlipHorizontally)!=0?width-1-x:x;
                int py=(flip&SpriteEffects.FlipVertically)!=0?height-1-y:y;
                var motion=MatrixPixels.At(px,py,width,height,progress,arriving,center);
                if(motion.Alpha<=0) continue;
                if(paths!=null && motion.Scatter>0)
                {
                    if(paths[index]==null || !paths[index].Visible) continue;
                    motion.Offset=paths[index].At(motion.Scatter);
                }
                var sample=new Rectangle(layer.Source.X+x,layer.Source.Y+y,1,1);
                var point=(topLeft+new Vector2(px,py)+motion.Offset).ToPoint();
                var destination=new Rectangle(point.X,point.Y,1,1);
                float rgb=Math.Min(1,motion.Scatter*5);
                // White texel + captured alpha yields actual RGB, even for black
                // armour. Multiplying the original orange texels cannot do that.
                if(rgb<1)
                {
                    if(layer.Rasterized)Game1.spriteBatch.Draw(white,destination,layer.Pixels[index]*(motion.Alpha*(1-rgb)));
                    else Game1.spriteBatch.Draw(layer.Texture,destination,sample,layer.Tint*(motion.Alpha*(1-rgb)));
                }
                if(rgb>0) Game1.spriteBatch.Draw(white,destination,MatrixPixels.Colour(px,py)*(motion.Alpha*rgb*layer.Pixels[index].A/255f*layer.Tint.A/255f));
            }
        }
    }
}
