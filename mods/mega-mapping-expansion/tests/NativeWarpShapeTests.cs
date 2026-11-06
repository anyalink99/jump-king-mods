using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using JKRuntime;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static int warpSounds;
        private static bool CountWarpSound(){warpSounds++;return false;}
        private static void NativeWarpShapeChecks(string provider)
        {
            var assembly=Assembly.LoadFrom(Path.GetFullPath(provider));
            var factoryType=assembly.GetType("JumpKingPlus.JumpKingPlusBlockFactory",true);
            var factory=(IBlockFactory)Activator.CreateInstance(factoryType);
            var flags=factoryType.GetField("Flags",OwnedPatches.Members);
            var sound=assembly.GetType("JumpKingPlus.JumpKingPlusEntry",true).GetProperty("WarpTransition");
            var camera=typeof(Camera).GetField("_current_screen",OwnedPatches.Members);
            var screenCount=typeof(LevelManager).GetField("_total_screens",OwnedPatches.Members);object oldCount=screenCount.GetValue(null);
            object oldFlags=flags.GetValue(null),oldSound=sound.GetValue(null,null),oldCamera=camera.GetValue(null);
            using(var audio=new OwnedPatches("mapping.tests.native-warp-audio"))
            try
            {
                screenCount.SetValue(null,2);
                audio.Add(sound.PropertyType.GetMethod("PlayOneShot",Type.EmptyTypes),prefix:typeof(Tests).GetMethod("CountWarpSound",OwnedPatches.Members));
                sound.GetSetMethod(true).Invoke(null,new[]{FormatterServices.GetUninitializedObject(sound.PropertyType)});
                foreach(int screen in new[]{0,1})foreach(var shape in new[]{SlopeType.TopLeft,SlopeType.TopRight,SlopeType.BottomLeft,SlopeType.BottomRight})
                {
                    camera.SetValue(null,screen);flags.SetValue(null,new string[0]);
                    var bounds=new Rectangle(80,80-screen*360,8,8);
                    var block=factory.GetBlock(new Color(40,38,75),bounds,null,null,screen,10,10);
                    var clipped=SlopeClipping.Apply(block,shape);
                    Require(ReferenceEquals(block,clipped[0])&&block.GetType().FullName=="JumpKingPlus.Blocks.WarpBlock","JK+ retains its actual block instance and type");
                    var world=new LevelScreen(screen,clipped,new LevelScreen.Graphics(),false,new TeleportLink[0],0,null);
                    IBlock native=new SlopeBlock(bounds,shape);
                    var behavior=(IBlockBehaviour)Activator.CreateInstance(assembly.GetType("JumpKingPlus.BlockBehaviours.WarpBlockBehaviour",true));
                    var body=new BodyComp(Vector2.Zero,1,1);
                    var context=new BehaviourContext(body);
                    for(int y=bounds.Y-1;y<=bounds.Bottom;y++)for(int x=79;x<=88;x++)
                    {
                        var origin=new Vector2(x,y);var velocity=new Vector2(-3.25f,7.5f);body.Position=origin;body.Velocity=velocity;
                        var probe=new Rectangle(x,y,1,1);Rectangle intersection;
                        bool hit=native.Intersects(probe,out intersection)!=BlockCollisionType.NoCollision;
                        typeof(BehaviourContextCollisionInfo).GetProperty("StartOfFrameCollisionInfo").GetSetMethod(true).Invoke(context.CollisionInfo,new object[]{world.GetCollisionInfo(probe)});
                        int before=warpSounds;behavior.ExecuteBlockBehaviour(context);
                        Require(body.Position==(hit?new Vector2(320,304-screen*360):origin),"Native JK+ triangle contact: screen="+screen+" shape="+shape+" probe="+probe+" hit="+hit+" position="+body.Position);
                        Require(body.Velocity==velocity,"Changing shape preserves JK+ velocity semantics");
                        Require(warpSounds-before==(hit?1:0),"Native warp sound follows actual triangle contact");
                        Require(context.CollisionInfo.StartOfFrameCollisionInfo.SlopeType==SlopeType.None,"A nonblocking warp doesn't become supporting terrain");
                    }
                    flags.SetValue(null,new[]{"MuteWarpTransitionSound"});
                    typeof(BehaviourContextCollisionInfo).GetProperty("StartOfFrameCollisionInfo").GetSetMethod(true).Invoke(context.CollisionInfo,new object[]{world.GetCollisionInfo(bounds)});int muted=warpSounds;
                    behavior.ExecuteBlockBehaviour(context);Require(warpSounds==muted,"Native mute tag remains effective");
                }
                Console.WriteLine("[OK] Installed JK+ warp: four slopes, empty halves, two screens, destination, velocity and native sound/mute behavior");
            }
            finally{flags.SetValue(null,oldFlags);sound.GetSetMethod(true).Invoke(null,new[]{oldSound});camera.SetValue(null,oldCamera);screenCount.SetValue(null,oldCount);SlopeClipping.BeginWorld();}
        }
    }
}
