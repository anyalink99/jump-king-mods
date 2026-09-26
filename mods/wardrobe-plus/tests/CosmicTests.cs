using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using JumpKing;
using JumpKing.MiscEntities.WorldItems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WardrobePlus;

internal static partial class WardrobeTests
{
    private static void CosmicTests(string gameDir, GraphicsDevice device, SpriteBatch batch, RenderTarget2D target, string compatibilityRepo)
    {
        var cameraField = typeof(Camera).GetField("_current_screen", Flags);
        int savedScreen = (int)cameraField.GetValue(null); var savedOffset = Camera.Offset;
        try
        {
            cameraField.SetValue(null,0); Camera.Offset = Vector2.Zero; CosmicRenderer.TestTime = 0;
            Check(CosmicRenderer.Supported, "Installed SpriteBatch supports Cosmic's saved batch contract");
            var outfit = new Outfit { Material = MaterialKind.Cosmic }; outfit.SetMaterial(4,MaterialKind.Cosmic);
            var store = new Store(Path.Combine(output,"cosmic-store"));
            store.Save(new WardrobeData { Current = outfit, Presets = new List<Outfit> { outfit.Copy() }, Undo = outfit.Copy() });
            var saved = store.Load();
            Check(saved.Current.Material == MaterialKind.Cosmic && saved.Presets[0].MaterialFor(4) == MaterialKind.Cosmic
                && saved.Undo.Material == MaterialKind.Cosmic && store.Import(store.Export(outfit)).Material == MaterialKind.Cosmic,
                "Cosmic survives settings, presets, undo and recipe round trips");
            using (var mask = new Texture2D(device,72,64))
            using (var fallback = new Texture2D(device,72,64))
            {
                var data = new Color[72*64];
                for (int y = 0; y < 64; y++) for (int x = 0; x < 72; x++) data[y*72+x] = new Color(x == 0 || y == 0 || x == 71 || y == 63 ? 255 : 0, 128, 0,255);
                mask.SetData(data); fallback.SetData(Enumerable.Repeat(Color.Black,data.Length).ToArray());
                var sprite = new CosmicSprite(Sprite.CreateSprite(fallback),mask,new Rectangle(0,0,72,64),Point.Zero);
                Func<Vector2,SpriteEffects,Color[]> render = (at,flip) => {
                    device.SetRenderTarget(target); device.Clear(Color.Transparent);
                    batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                    sprite.Draw(at,flip);
                    batch.Draw(Game1.instance.contentManager.Pixel.texture,new Rectangle(400,300,2,2),Color.Magenta);
                    batch.End(); device.SetRenderTarget(null);
                    var result = new Color[480*360]; target.GetData(result); return result;
                };
                int draws = CosmicRenderer.Draws, captures = CrystalRenderer.Captures;
                var first = render(new Vector2(100,100),SpriteEffects.None);
                var moved = render(new Vector2(110,100),SpriteEffects.None);
                bool locked = true;
                for (int y = 101; y < 163; y++) for (int x = 111; x < 171; x++) locked &= first[y*480+x] == moved[y*480+x];
                Check(locked, "Moving the Cosmic mask reveals the same stars at the same map coordinates");
                Check(first.SequenceEqual(render(new Vector2(100,100),SpriteEffects.FlipHorizontally)), "Mirroring the mask does not mirror the Cosmic field");
                Check(first[100*480+100] == Color.White && first[120*480+100] == Color.White
                    && first[163*480+140] == Color.White && first[99*480+100] == Color.Transparent,
                    "Cosmic has a crisp one-pixel white contour with no exterior halo");
                Check(first[300*480+400] == Color.Magenta && CosmicRenderer.Draws == draws+3 && CrystalRenderer.Captures == captures,
                    "Cosmic draws one quad, performs no refraction capture and resumes the caller batch");
                CosmicRenderer.TestTime = 6;
                var animated = render(new Vector2(100,100),SpriteEffects.None);
                Check(!first.SequenceEqual(animated), "Cosmic animates while the character stands still");
                CosmicRenderer.TestTime = 0; Camera.Offset = new Vector2(0,19);
                var shifted = render(new Vector2(100,119),SpriteEffects.None);
                bool follows = true;
                for (int y = 0; y < 64; y++) for (int x = 0; x < 72; x++) follows &= first[(100+y)*480+100+x] == shifted[(119+y)*480+100+x];
                Check(follows, "Cosmic follows camera/background translation without swimming across the map");
                Camera.Offset = Vector2.Zero;
                if (compatibilityRepo != null) CosmicSmoothCameraTest(compatibilityRepo,render,first);
                using (var raster = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true })
                {
                    device.SetRenderTarget(target); device.Clear(Color.Transparent);
                    var viewport = new Viewport(20,30,400,300); var clip = new Rectangle(60,70,20,20);
                    device.Viewport = viewport; device.ScissorRectangle = clip;
                    device.Textures[1] = mask; device.Textures[2] = fallback;
                    device.SamplerStates[1] = SamplerState.PointClamp; device.SamplerStates[2] = SamplerState.LinearClamp;
                    batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,raster,null,Matrix.CreateScale(2));
                    sprite.Draw(new Vector2(20,20));
                    Check(device.Viewport.Equals(viewport) && device.ScissorRectangle == clip && device.Textures[1] == mask && device.Textures[2] == fallback
                        && device.SamplerStates[1] == SamplerState.PointClamp && device.SamplerStates[2] == SamplerState.LinearClamp,
                        "Cosmic preserves viewport, scissor and extra texture/sampler slots under scaling");
                    draws = CosmicRenderer.Draws;
                    sprite.Draw(new Vector2(-500,-500)); sprite.SetColor(Color.Transparent); sprite.Draw(new Vector2(20,20)); sprite.SetColor(Color.White);
                    Check(CosmicRenderer.Draws == draws, "Offscreen and invisible Cosmic sprites issue no shader draw");
                    batch.End(); device.SetRenderTarget(null);
                    var pixels = new Color[480*360]; target.GetData(pixels);
                    Check(pixels[0].A == 0 && pixels[90*480+90].A == 0 && pixels[70*480+60].A == 255, "Cosmic honors scaled viewport clipping");
                }
                using (var tall = new RenderTarget2D(device,480,720))
                {
                    var images = new[] { new Color[480*720],new Color[480*720] };
                    for (int pass = 0; pass < 2; pass++)
                    {
                        device.SetRenderTarget(tall); device.Clear(Color.Transparent);
                        if (pass == 0)
                        {
                            cameraField.SetValue(null,2);
                            batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                            sprite.Draw(Camera.TransformVector2(new Vector2(100,-380))); batch.End();
                        }
                        else for (int screen = 2; screen >= 1; screen--)
                        {
                            cameraField.SetValue(null,screen); device.Viewport = new Viewport(0,(2-screen)*360,480,360);
                            batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                            sprite.Draw(Camera.TransformVector2(new Vector2(100,-380))); batch.End();
                        }
                        device.SetRenderTarget(null); tall.GetData(images[pass]);
                    }
                    Check(images[0].SequenceEqual(images[1]), "Two adjacent screen viewports stitch pixel-identically to one continuous Cosmic view");
                }
            }
            cameraField.SetValue(null,0);
            using (var appearance = PreparedAppearance.Build(outfit,Controller.Catalog,false))
            {
                var body = (CosmicSprite)NativeAppearance.Frames(appearance.Base.regular)[0];
                Check(appearance.HasCosmic && !appearance.HasRefraction, "Cosmic activation is independent of the parked refraction renderer");
                bool coverage = true;
                foreach (var group in appearance.Base.m_groups) foreach (var frame in NativeAppearance.Frames(group).Values)
                foreach (var flip in new[] { SpriteEffects.None,SpriteEffects.FlipHorizontally })
                {
                    var images = new[] { new Color[480*360],new Color[480*360] };
                    for (int pass = 0; pass < 2; pass++)
                    {
                        CosmicRenderer.Suspended = pass == 0;
                        device.SetRenderTarget(target); device.Clear(Color.Transparent); batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                        frame.Draw(new Vector2(240,220),flip); batch.End(); device.SetRenderTarget(null); target.GetData(images[pass]);
                    }
                    coverage &= images[0].Select(c => c.A).SequenceEqual(images[1].Select(c => c.A));
                }
                CosmicRenderer.Suspended = false;
                Check(coverage, "Cosmic preserves every native pose silhouette and alpha in both directions");
                var fittedOutfit = outfit.Copy();
                fittedOutfit.SetFit(new FitAdjustment { BaseId = appearance.Resolved[NativeAppearance.BaseItem].Id,
                    SourceId = appearance.Resolved[(int)Items.Cap].Id, Item = (int)Items.Cap, X = 7, Y = -5 });
                using (var normal = PreparedAppearance.Build(fittedOutfit,Controller.Catalog,false,false))
                using (var fitted = PreparedAppearance.Build(fittedOutfit,Controller.Catalog,false,true))
                {
                    bool fit = true;
                    foreach (var flip in new[] { SpriteEffects.None,SpriteEffects.FlipHorizontally })
                    {
                        var images = new[] { new Color[480*360],new Color[480*360] };
                        for (int pass = 0; pass < 2; pass++)
                        {
                            device.SetRenderTarget(target); device.Clear(Color.Transparent); batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                            var sprite = NativeAppearance.Frames((pass == 0 ? normal : fitted).Items[Items.Cap].regular)[0];
                            sprite.Draw(pass == 0 ? new Vector2(flip == SpriteEffects.None ? 247 : 233,215) : new Vector2(240,220),flip);
                            batch.End(); device.SetRenderTarget(null); target.GetData(images[pass]);
                        }
                        fit &= images[0].SequenceEqual(images[1]);
                    }
                    Check(fit, "Cosmic item fitting and mirroring preserve both mask position and map-anchored field");
                }
                using (var content = new Microsoft.Xna.Framework.Content.ContentManager(Game1.instance.Services))
                {
                    var bg = content.Load<Texture2D>(Path.Combine(gameDir,"Content/screens/background/bg100"));
                    var mid = content.Load<Texture2D>(Path.Combine(gameDir,"Content/screens/midground/100"));
                    for (int frame = 0; frame < 24; frame++)
                    {
                        CosmicRenderer.TestTime = frame * .4f;
                        device.SetRenderTarget(target); device.Clear(new Color(7,8,15));
                        batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                        batch.Draw(bg,Vector2.Zero,Color.White); batch.Draw(mid,Vector2.Zero,Color.White);
                        body.DrawScaled(new Vector2(170+frame*2,250),4,SpriteEffects.None);
                        body.Draw(new Vector2(320+frame,245));
                        JKRuntime.UI.UiTheme.TextLine("COSMIC / WINDOW INTO SPACE",new Vector2(18,16),Color.White,true);
                        batch.End(); device.SetRenderTarget(null);
                        using (var stream = File.Create(Path.Combine(output,"cosmic-"+frame.ToString("D2")+".png"))) target.SaveAsPng(stream,480,360);
                    }
                }
                CosmicBenchmark(device,batch,target,body);
            }
        }
        finally { CosmicRenderer.TestTime = null; CosmicRenderer.Suspended = false; Camera.Offset = savedOffset; cameraField.SetValue(null,savedScreen); }
    }

    private static void CosmicBenchmark(GraphicsDevice device,SpriteBatch batch,RenderTarget2D target,Sprite body)
    {
        if (Environment.GetEnvironmentVariable("WARDROBE_COSMIC_BENCHMARK") != "1") return;
        var lines = new List<string> { "GPU-completed fixture frames, identical one-pixel fence in both modes; not full-game FPS.","copies,mode,median_ms,p95_ms,p99_ms" };
        var pixel = new Color[1]; var watch = new Stopwatch();
        foreach (int copies in new[] { 1,4 })
        {
            var samples = new[] { new List<double>(), new List<double>() };
            for (int round = 0; round < 4; round++) for (int index = 0; index < 2; index++)
            {
                int mode = (index+round)%2; CosmicRenderer.Suspended = mode == 0;
                for (int frame = 0; frame < 200; frame++)
                {
                    watch.Restart(); device.SetRenderTarget(target); device.Clear(Color.Black);
                    batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                    for (int copy = 0; copy < copies; copy++) body.Draw(new Vector2(100+copy*65+frame%17,220));
                    batch.End(); device.SetRenderTarget(null); target.GetData(0,new Rectangle(0,0,1,1),pixel,0,1); watch.Stop();
                    if (frame >= 50) samples[mode].Add(watch.Elapsed.TotalMilliseconds);
                }
            }
            for (int mode = 0; mode < 2; mode++)
            {
                samples[mode].Sort(); lines.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2:F4},{3:F4},{4:F4}",copies,
                    mode == 0 ? "static" : "cosmic",samples[mode][300],samples[mode][569],samples[mode][593]));
            }
        }
        CosmicRenderer.Suspended = false;
        File.WriteAllLines(Path.Combine(output,"cosmic-performance.csv"),lines);
        foreach (string line in lines) Console.WriteLine("BENCH: "+line);
    }

    private static void CosmicSmoothCameraTest(string repo,Func<Vector2,SpriteEffects,Color[]> render,Color[] reference)
    {
        // Install the real camera postfix after Cosmic has already drawn/JITted.
        // This catches accidental inlining that bypasses late camera hooks.
        var smooth = Assembly.LoadFrom(Path.Combine(repo,"build","smooth-camera","_INTERNAL","SmoothCamera.Module.dll"));
        var context = smooth.GetType("SmoothCamera.RenderContext",true);
        var handler = context.GetMethod("Vector",Flags);
        var harmony = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetType("HarmonyLib.Harmony") != null);
        var engineType = harmony.GetType("HarmonyLib.Harmony"); var metadata = harmony.GetType("HarmonyLib.HarmonyMethod");
        var engine = Activator.CreateInstance(engineType,new object[] { "WardrobeTests.CosmicCamera" });
        var native = typeof(Camera).GetMethod("TransformVector2");
        engineType.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5).Invoke(engine,
            new object[] { native,null,Activator.CreateInstance(metadata,new object[] { handler }),null,null });
        try
        {
            using ((IDisposable)Activator.CreateInstance(context,Flags,null,new object[] { 0,17,0,-1 },null))
            {
                var actual = render(new Vector2(100,117),SpriteEffects.None); bool matches = true;
                for (int y = 0; y < 64; y++) for (int x = 0; x < 72; x++) matches &= reference[(100+y)*480+100+x] == actual[(117+y)*480+100+x];
                Check(matches,"Cosmic follows the actual Smooth Camera render hook installed after its first draw");
            }
        }
        finally { engineType.GetMethod("Unpatch",new[] { typeof(MethodBase),typeof(MethodInfo) }).Invoke(engine,new object[] { native,handler }); }
    }
}
