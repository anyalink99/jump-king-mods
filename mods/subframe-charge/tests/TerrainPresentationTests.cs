using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using HarmonyLib;
using JumpKing.Level;
using JumpKing.Level.Sampler;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using SubframeCharge;

internal static partial class PerformanceTests
{
    private sealed class TerrainGraphics : IGraphicsDeviceService
    {
        public GraphicsDevice GraphicsDevice { get; private set; }
        internal TerrainGraphics(GraphicsDevice value) { GraphicsDevice=value; }
        public event EventHandler<EventArgs> DeviceCreated { add { } remove { } }
        public event EventHandler<EventArgs> DeviceDisposing { add { } remove { } }
        public event EventHandler<EventArgs> DeviceReset { add { } remove { } }
        public event EventHandler<EventArgs> DeviceResetting { add { } remove { } }
    }

    private static void TerrainTests(string gameDir,string switchPath,string upsidePath)
    {
        var fixtures=new List<Harmony>();
        var screensField=AccessTools.Field(typeof(LevelManager),"m_screens");
        object oldScreens=screensField.GetValue(null);
        try
        {
            // These actual installed patches exist even in the main campaign.
            var switches=Assembly.LoadFrom(switchPath);
            var switchHook=new Harmony("Zebra.SwitchBlocks.Harmony"); fixtures.Add(switchHook);
            switchHook.Patch(PredictionCollision.Slope,postfix:new HarmonyMethod(AccessTools.Method(
                switches.GetType("SwitchBlocks.Patches.PatchSlopeBlock"),"Postfix")));
            var upside=Assembly.LoadFrom(upsidePath);
            var upsideHook=new Harmony("JeFi.UpsideDownCore.Harmony"); fixtures.Add(upsideHook);
            upsideHook.Patch(PredictionCollision.Slope,transpiler:new HarmonyMethod(AccessTools.Method(
                upside.GetType("UpsideDownCore.Patching.SlopeBlock"),"transpileIntersects")));
            PredictionCollision.Prepare();
            using(var window=new Form { ShowInTaskbar=false })
            using(var device=new GraphicsDevice(GraphicsAdapter.DefaultAdapter,GraphicsProfile.HiDef,
                new PresentationParameters { DeviceWindowHandle=window.Handle,BackBufferWidth=480,BackBufferHeight=360 }))
            {
                var services=new GameServiceContainer(); services.AddService(typeof(IGraphicsDeviceService),new TerrainGraphics(device));
                using(var content=new ContentManager(services,Path.Combine(gameDir,"Content")))
                {
                    var texture=LevelTexture.FromTexture(content.Load<Texture2D>("level"));
                    var screens=new LevelScreen[texture.GetTotalScreensPotential()];
                    var blocks=new IBlock[screens.Length][];
                    var load=AccessTools.Method(typeof(LevelManager),"LoadBlocksInterval");
                    for(int i=0;i<screens.Length;i++)
                    {
                        object[] args={texture,null,i,false,null,0f,null};
                        blocks[i]=(IBlock[])load.Invoke(null,args);
                        screens[i]=new LevelScreen(i,blocks[i],new LevelScreen.Graphics(),false,new TeleportLink[0],0,null);
                    }
                    screensField.SetValue(null,screens);
                    int iceCases=0,slopeCases=0;
                    var path=new PredictionPath();
                    // Actual campaign ice tiles, with their complete surrounding
                    // screens. Only clear travel segments are expected to be full.
                    for(int screen=0;screen<Math.Min(43,screens.Length);screen++)
                    {
                        var nearby=blocks.Skip(Math.Max(0,screen-1)).Take(screen==0 ? 2 : 3).SelectMany(b=>b).ToArray();
                        foreach(var block in blocks[screen])
                        {
                            if(!(block is IceBlock) && !(block is SlopeBlock)) continue;
                            Rectangle r=block.GetRect();
                            Vector2 motion=block is IceBlock ? new Vector2(4,0) : new Vector2(4,4);
                            var slope=block as SlopeBlock;
                            if(slope!=null)
                            {
                                if(slope.GetSlopeType()==SlopeType.TopLeft) motion=new Vector2(-4,4);
                                else if(slope.GetSlopeType()!=SlopeType.TopRight) continue;
                            }
                            for(int offset=-18;offset<=0;offset+=3)
                            {
                                Vector2 start=new Vector2(r.X+offset,r.Y-27);
                                if(!TerrainClear(nearby,start) || !TerrainClear(nearby,start+motion)) continue;
                                path.Prepare(start,motion,new Rectangle((int)start.X,(int)start.Y,18,26),screen);
                                Check(Vector2.Distance(path.At(.5f),start+motion*.5f)<.001f,
                                    "Campaign material retains intermediate drawing with installed collision patches; screen="+(screen+1));
                                for(int fraction=0;fraction<=4;fraction++)
                                    Check(TerrainClear(nearby,path.At(fraction/4f)),"Campaign prediction remains outside known solid geometry");
                                if(slope==null) iceCases++; else slopeCases++;
                            }
                        }
                    }
                    Check(iceCases>50 && slopeCases>50,"Campaign fixture must exercise substantial real ice and slope geometry");
                    Console.WriteLine("[OK] Installed main-campaign terrain: "+iceCases+" ice and "+slopeCases+" slope paths, with SwitchBlocks/UpSideDownCore hooks");
                }
            }
        }
        finally { screensField.SetValue(null,oldScreens); foreach(var fixture in fixtures)fixture.UnpatchAll(fixture.Id); }
    }
    private static bool TerrainClear(IBlock[] blocks,Vector2 position)
    {
        var box=new Rectangle((int)position.X,(int)position.Y,18,26); Rectangle overlap;
        foreach(var block in blocks)
            if(block.Intersects(box,out overlap)==BlockCollisionType.Collision_Blocking) return false;
        return true;
    }
}
