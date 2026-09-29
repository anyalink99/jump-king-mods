using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using HarmonyLib;
using JumpKing;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SubframeCharge;

internal static partial class PerformanceTests
{
    private static void InstalledBlockTests(string inventory)
    {
        int assemblies=0,types=0;
        foreach(string file in File.ReadAllLines(inventory).Where(p=>!string.IsNullOrWhiteSpace(p)))
        {
            var assembly=Assembly.LoadFrom(file);assemblies++;
            foreach(var type in assembly.GetTypes().Where(t=>!t.IsAbstract && !t.IsInterface && typeof(IBlock).IsAssignableFrom(t)))
            {
                // No constructors or collision callbacks: this checks the safe
                // fallback for real foreign types, not their gameplay semantics.
                var block=(IBlock)FormatterServices.GetUninitializedObject(type);
                GC.SuppressFinalize(block);
                foreach(var motion in new[]{new Vector2(8,0),new Vector2(0,-8),new Vector2(4,8)})
                {
                    var path=new PredictionPath();path.Begin(new Vector2(100,100),motion,18,26,false);
                    path.Add(block);path.Build();
                    for(int frame=1;frame<4;frame++)
                        Check(Vector2.Distance(path.At(frame/4f),new Vector2(100,100)+motion*(frame/4f))<.001f,
                            "Unknown installed geometry must not freeze presentation: "+type.FullName);
                }
                types++;
            }
        }
        Check(types>0,"Installed block fixture found no block types");
        Console.WriteLine("[OK] Installed geometry fallback: "+types+" block types from "+assemblies+" assemblies; no foreign callbacks");
    }

    private static void UpsideDownDrawTests(string file,bool late)
    {
        var assembly=Assembly.LoadFrom(file);
        var patch=AccessTools.Method(assembly.GetType("UpsideDownCore.Patching.PlayerEntity",true),"transpileDraw");
        var flipped=AccessTools.Field(assembly.GetType("UpsideDownCore.Models.Manager",true),"isUpsideDown");
        var foreign=new Harmony("sfc.upside.graphics.tests");
        using(var window=new Form {ShowInTaskbar=false})
        using(var device=new GraphicsDevice(GraphicsAdapter.DefaultAdapter,GraphicsProfile.HiDef,
            new PresentationParameters {DeviceWindowHandle=window.Handle,BackBufferWidth=480,BackBufferHeight=360}))
        using(var batch=new SpriteBatch(device))
        using(var target=new RenderTarget2D(device,480,360))
        using(var white=new Texture2D(device,1,1))
        {
            var game=MakeGame();Game1.spriteBatch=batch;white.SetData(new[]{Color.White});
            try
            {
                if(!late) foreign.Patch(AccessTools.Method(typeof(PlayerEntity),"Draw"),transpiler:new HarmonyMethod(patch));
                SettingsStore.Current.SetRefresh(true);PerformanceFeatures.Install();FeatureClock.BeforeTick(game);
                if(late) foreign.Patch(AccessTools.Method(typeof(PlayerEntity),"Draw"),transpiler:new HarmonyMethod(patch));
                Camera.Offset=Vector2.Zero;AccessTools.Field(typeof(Camera),"_current_screen").SetValue(null,0);
                var player=EmptyPlayer();player.m_body=new BodyComp(new Vector2(91,74),18,26);
                AccessTools.Field(typeof(PlayerEntity),"m_sprite").SetValue(player,Sprite.CreateSprite(white));
                AccessTools.Field(typeof(PlayerPresentation),"body").SetValue(null,player.m_body);
                var history=(PositionHistory)AccessTools.Field(typeof(PlayerPresentation),"history").GetValue(null);
                history.Observe(new Vector2(71,74),new Vector2(20,0),1,false);
                history.Observe(player.m_body.Position,new Vector2(20,0),1,false);
                var path=(PredictionPath)AccessTools.Field(typeof(PlayerPresentation),"path").GetValue(null);
                path.Begin(player.m_body.Position,new Vector2(20,0),18,26,false);path.Build();
                foreach(bool upside in new[]{false,true,false})
                for(int frame=0;frame<4;frame++)
                {
                    flipped.SetValue(null,upside);
                    AccessTools.Field(typeof(JKRuntime.PresentationScheduling),"remainder").SetValue(null,42500L*frame);
                    device.SetRenderTarget(target);device.Clear(Color.Black);game.StartBatch();player.Draw();game.EndBatch();device.SetRenderTarget(null);
                    var pixels=new Color[480*360];target.GetData(pixels);
                    Check(pixels[(upside ? 122 : 100)*480+100+5*frame]==Color.White,"Installed UpSideDownCore must preserve intermediate player drawing and its flipped anchor");
                    Check(player.m_body.Position==new Vector2(91,74),"Upside-down drawing must leave physics position untouched");
                }
                Console.WriteLine("[OK] Installed UpSideDownCore GPU drawing: "+(late ? "late" : "early")+" patch order, normal/flipped/normal");
            }
            finally {flipped.SetValue(null,false);PerformanceFeatures.Uninstall();foreign.UnpatchAll(foreign.Id);Game1.spriteBatch=null;AccessTools.Field(typeof(Game1),"_instance").SetValue(null,null);}
        }
    }
}
