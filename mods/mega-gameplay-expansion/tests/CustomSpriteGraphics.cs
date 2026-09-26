using System;
using System.IO;
using System.Linq;
using System.Reflection;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaGameplayExpansion
{
    internal static partial class Tests
    {
        private sealed class ProceduralSprite : Sprite
        {
            internal bool Fail;
            internal ProceduralSprite(Texture2D value) {texture=value;source=new Rectangle(0,0,1,1);}
            public override void Draw(Vector2 at,SpriteEffects flip=SpriteEffects.None)
            {
                Game1.spriteBatch.Draw(texture,new Rectangle((int)at.X-7,(int)at.Y-19,14,19),Color.Cyan);
                Game1.spriteBatch.Draw(texture,new Rectangle((int)at.X+((flip&SpriteEffects.FlipHorizontally)!=0?3:-5),(int)at.Y-17,2,4),Color.Red);
                Game1.spriteBatch.Draw(texture,new Rectangle((int)at.X+8,(int)at.Y-10,2,4),Color.Blue*.5f);
                if(Fail)throw new InvalidOperationException("Deliberate procedural Draw failure");
            }
        }
        private static void CustomSpriteGraphics(string gameDir)
        {
            var previousBatch=Game1.spriteBatch;
            var oldOffset=Camera.Offset;
            try
            {
                using(var fixture=new SpriteGameFixture())
                using(var window=new System.Windows.Forms.Form {ShowInTaskbar=false})
                using(var device=new GraphicsDevice(GraphicsAdapter.DefaultAdapter,GraphicsProfile.HiDef,
                    new PresentationParameters {DeviceWindowHandle=window.Handle,BackBufferWidth=480,BackBufferHeight=360}))
                using(var batch=new SpriteBatch(device))
                using(var target=new RenderTarget2D(device,480,360,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents))
                using(var white=new Texture2D(device,1,1))
                {
                    white.SetData(new[]{Color.White});Game1.spriteBatch=batch;Camera.Offset=Vector2.Zero;
                    var sprite=new ProceduralSprite(white);var anchor=new Vector2(180,200);
                    Func<Sprite,SpriteEffects,Color[]> direct=(value,flip)=>{
                        device.SetRenderTarget(target);device.Clear(Color.Transparent);
                        batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                        value.Draw(anchor,flip);batch.End();device.SetRenderTarget(null);
                        var pixels=new Color[480*360];target.GetData(pixels);return pixels;
                    };
                    Action<Sprite,string> verify=(value,label)=>{
                        foreach(float x in new[]{0f,.25f,.5f,.75f,.999f})
                        foreach(float y in new[]{0f,.25f,.5f,.75f})
                        foreach(var flip in new[]{SpriteEffects.None,SpriteEffects.FlipHorizontally})
                        {
                            anchor=new Vector2(180+x,200+y);
                            var expected=direct(value,flip);
                            device.SetRenderTarget(target);device.Clear(Color.Magenta);
                            var viewport=device.Viewport;var clip=device.ScissorRectangle;
                            var blend=device.BlendState;var raster=device.RasterizerState;var depth=device.DepthStencilState;
                            device.Textures[2]=white;device.SamplerStates[2]=SamplerState.PointWrap;
                            var image=new WarpImage(value,value,flip,anchor);
                            Require(image.Layers.Length==1 && image.Layers[0].Rasterized==(value.GetType()!=typeof(Sprite)),label+" used the wrong capture path");
                            Require(ReferenceEquals(Game1.spriteBatch,batch) && device.GetRenderTargets().Length==1
                                && ReferenceEquals(device.GetRenderTargets()[0].RenderTarget,target) && device.Viewport.Equals(viewport)
                                && device.ScissorRectangle==clip && device.BlendState==blend && device.RasterizerState==raster
                                && device.DepthStencilState==depth && device.Textures[2]==white && device.SamplerStates[2]==SamplerState.PointWrap,
                                label+" leaked GPU state");
                            device.SetRenderTarget(null);var sentinel=new Color[480*360];target.GetData(sentinel);
                            Require(sentinel.All(c=>c==Color.Magenta),label+" damaged the caller target");
                            foreach(bool arriving in new[]{false,true})
                            {
                                if(arriving)
                                {
                                    anchor+=new Vector2(31.25f,-6.5f);
                                    image=image.WithArrival(value,anchor);
                                    expected=direct(value,flip);
                                }
                                var layer=arriving ? image.ArrivalLayers[0] : image.Layers[0];
                                var paths=WarpImage.PrepareLayer(layer,flip,Scene(new JumpKing.Level.IBlock[0]),anchor);
                                device.SetRenderTarget(target);device.Clear(Color.Transparent);
                                batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                                WarpVisual.DrawLayer(layer,flip,anchor,arriving?1:0,arriving,paths);
                                batch.End();device.SetRenderTarget(null);
                                var actual=new Color[480*360];target.GetData(actual);
                                Require(actual.SequenceEqual(expected),label+" changed captured colours, position, alpha or facing at "+anchor+" "+flip+" arriving="+arriving);
                            }
                        }
                        // Native actor/charge/landing timing remains independent
                        // of the image type, including retained snapshots.
                        InstalledControllerTick(value);
                    };
                    verify(Sprite.CreateSpriteWithCenter(white,new Rectangle(0,0,1,1),new Vector2(.5f,1)),"Native sprite with fractional pivot");
                    verify(sprite,"Procedural sprite");
                    sprite.Fail=true;
                    var fallback=new WarpImage(sprite,sprite,SpriteEffects.None,anchor);
                    Require(fallback.Layers.Length==1 && !fallback.Layers[0].Rasterized,"Throwing custom Draw did not retain the texture fallback");
                    Require(ReferenceEquals(Game1.spriteBatch,batch) && device.GetRenderTargets().Length==0,"Throwing Draw leaked the capture batch or target");
                    InstalledControllerTick(sprite);sprite.Fail=false;
                    var noTexture=new ProceduralSprite(null) {Fail=true};
                    var silhouette=new WarpImage(noTexture,noTexture,SpriteEffects.None,anchor);
                    Require(silhouette.Layers.Length==1 && silhouette.Layers[0].Rasterized && silhouette.Layers[0].Pixels.Length==18*26,
                        "Missing texture/custom Draw failure did not produce the bounded silhouette");
                    InstalledControllerTick(noTexture);
                    batch.Begin();
                    var busy=new WarpImage(sprite,sprite,SpriteEffects.None,anchor);
                    batch.Draw(white,new Rectangle(0,0,1,1),Color.White);batch.End();
                    Require(!busy.Layers[0].Rasterized,"Capture interrupted an already active batch");

                    string workshop=Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(gameDir)),"workshop","content","1061090");
                    string wardrobe=Path.Combine(workshop,"3799882710","WardrobePlus.dll");
                    if(File.Exists(wardrobe))
                    {
                        var package=Assembly.LoadFrom(wardrobe);Assembly module;
                        using(var stream=package.GetManifestResourceStream("JKRuntime.Module"))
                        using(var memory=new MemoryStream()){stream.CopyTo(memory);module=Assembly.Load(memory.ToArray());}
                        var type=module.GetType("WardrobePlus.CosmicSprite",true);
                        var renderer=module.GetType("WardrobePlus.CosmicRenderer",true);
                        renderer.GetField("TestTime",Flags).SetValue(null,(float?)0);
                        using(var mask=new Texture2D(device,24,32))
                        using(var baseTexture=new Texture2D(device,24,32))
                        {
                            var pixels=Enumerable.Range(0,24*32).Select(i=>new Color(i%24==0 || i%24==23 || i<24 || i>=24*31?255:0,128,0,255)).ToArray();
                            mask.SetData(pixels);baseTexture.SetData(Enumerable.Repeat(Color.Black,pixels.Length).ToArray());
                            var original=Sprite.CreateSpriteWithCenter(baseTexture,new Rectangle(0,0,24,32),new Vector2(.5f,1));
                            var cosmic=(Sprite)Activator.CreateInstance(type,Flags,null,new object[]{original,mask,new Rectangle(0,0,24,32),Point.Zero},null);
                            verify(cosmic,"Installed Wardrobe+ CosmicSprite");
                        }
                        renderer.GetMethod("Release",Flags).Invoke(null,null);
                        Console.WriteLine("[OK] Native/procedural/Cosmic GPU reconstruction: fractional anchors, both facings, departure/arrival endpoints and native Warp lifecycle");
                    }
                    else Console.WriteLine("[SKIP] Installed Wardrobe+ Cosmic fixture unavailable");
                }
            }
            finally {Game1.spriteBatch=previousBatch;Camera.Offset=oldOffset;}
        }
    }
}
