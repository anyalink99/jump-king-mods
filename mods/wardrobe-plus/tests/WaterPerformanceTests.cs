using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using JumpKing;
using JumpKing.Level;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WardrobePlus.Advanced;

internal static partial class WardrobeTests
{
    private static void WaterPerformanceTests()
    {
        if(Environment.GetEnvironmentVariable("WARDROBE_BENCHMARK")!="1")return;
        var screensField=typeof(LevelManager).GetField("m_screens",BindingFlags.Static|BindingFlags.NonPublic);
        var blocksField=typeof(LevelScreen).GetField("m_hitboxes",BindingFlags.Instance|BindingFlags.NonPublic);
        var old=screensField.GetValue(null);
        try
        {
            var screens=new LevelScreen[6];
            for(int screen=0;screen<screens.Length;screen++)
            {
                var blocks=new List<IBlock>();
                for(int y=0;y<20;y++)for(int x=0;x<48;x++)
                    blocks.Add(new BoxBlock(new Rectangle(x*10,y*18-screen*360,8,3)));
                screens[screen]=(LevelScreen)FormatterServices.GetUninitializedObject(typeof(LevelScreen));
                blocksField.SetValue(screens[screen],blocks.ToArray());
            }
            screensField.SetValue(null,screens);
            using(var preparation=new JKRuntime.RuntimeScope())
            {
            JKRuntime.Particles.ParticleWorlds.PrepareNative(preparation);
            var effect=new EffectBinding{Definition=new EffectDefinition{collision="water",gravity=310,drag=0}};
            var drops=new List<PresentationActor.Particle>();
            var collisionFrame=new SpilledWater.CollisionFrame();
            foreach(int count in new[]{64,256})
            {
                drops.Clear();for(int i=0;i<count;i++)drops.Add(new PresentationActor.Particle{Effect=effect,FlowDirection=i%2==0?-1:1});
                var samples=new List<double>();int gc=GC.CollectionCount(0);
                for(int run=0;run<4;run++)
                {
                    for(int i=0;i<count;i++){drops[i].Position=new Vector2(240+(i%8),-900);drops[i].Velocity=new Vector2((i%16-8)*12,-80-i%40);}
                    for(int tick=0;tick<240;tick++)
                    {
                        var watch=Stopwatch.StartNew();var collision=collisionFrame.Prepare(drops);
                        foreach(var drop in drops)SpilledWater.Step(drop,1f/60,collision);
                        collisionFrame.Clear();
                        watch.Stop();if(run>0)samples.Add(watch.Elapsed.TotalMilliseconds);
                    }
                }
                samples.Sort();Console.WriteLine("[PERF] water collision {0} drops / 960 blocks per screen: median={1:F4}ms p95={2:F4}ms max={3:F4}ms gc0={4}",count,samples[samples.Count/2],samples[(int)(samples.Count*.95)],samples.Last(),GC.CollectionCount(0)-gc);
            }
            }
        }
        finally{screensField.SetValue(null,old);}
    }

    private static void VesselPerformanceTests(PresentationActor actor,Sprite sprite,GraphicsDevice device,SpriteBatch batch,RenderTarget2D target)
    {
        if(Environment.GetEnvironmentVariable("WARDROBE_BENCHMARK")!="1")return;
        var sample=new Color[1];
        foreach(string mode in new[]{"fallback","idle","splat"})
        {
            actor.Reset();actor.SetState(mode=="splat"?"splat":"idle");if(mode=="splat")actor.Trigger("splat");
            var samples=new List<double>();int gc=GC.CollectionCount(0);
            for(int i=0;i<180;i++)
            {
                var watch=Stopwatch.StartNew();actor.Update(1f/240);
                device.SetRenderTarget(target);device.Clear(Color.Transparent);
                PresentationDraw.Current=mode=="fallback"?null:actor;
                batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                sprite.Draw(new Vector2(240,200));PresentationDraw.Particles(actor,"front",new Vector2(240,200),1,false,Color.White);batch.End();
                device.SetRenderTarget(null);target.GetData(0,new Rectangle(0,0,1,1),sample,0,1);
                watch.Stop();if(i>=30)samples.Add(watch.Elapsed.TotalMilliseconds);
            }
            samples.Sort();Console.WriteLine("[PERF] Vessel GPU-completed {0}: median={1:F4}ms p95={2:F4}ms max={3:F4}ms gc0={4} (includes identical readback fence)",mode,samples[samples.Count/2],samples[(int)(samples.Count*.95)],samples.Last(),GC.CollectionCount(0)-gc);
        }
        PresentationDraw.Current=null;actor.Reset();device.SetRenderTarget(target);
    }
}
