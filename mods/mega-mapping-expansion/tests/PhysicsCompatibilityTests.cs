using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using JKRuntime;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using JumpKing.SaveThread;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static string PhysicsNativeTrace()
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                for(int screen=0;screen<5;screen++)for(int charge=0;charge<36;charge++)
                {
                    var body=FrameBody(screen*360);body.Velocity=new Vector2(-3.5f,PlayerValues.JUMP*(charge+1)/36f);
                    for(int tick=0;tick<120;tick++)
                    {frameTicks[body]();var box=body.GetHitbox();writer.Write(body.Position.X);writer.Write(body.Position.Y);writer.Write(body.Velocity.X);writer.Write(body.Velocity.Y);writer.Write(box.X);writer.Write(box.Y);writer.Write(body.IsOnGround);}
                    frameTicks.Remove(body);
                }
                writer.Flush();using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream.ToArray()));
            }
        }
        private static void PhysicsCompatibilityChecks(string directory)
        {
            var hashes=new List<string>();string exe=Assembly.GetExecutingAssembly().Location;
            foreach(string mode in new[]{"physics-native-original","physics-native-patched"})
            {
                var start=new ProcessStartInfo(exe,"\""+directory+"\" unused "+mode){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
                using(var process=Process.Start(start))
                {
                    string output=process.StandardOutput.ReadToEnd(),error=process.StandardError.ReadToEnd();process.WaitForExit();
                    Require(process.ExitCode==0,"Native comparison subprocess: "+error);hashes.Add(output.Trim());
                }
            }
            Require(hashes[0]==hashes[1]&&hashes[0]==PhysicsNativeTrace(),"Unconfigured native trajectories match untouched process before and after symmetric worlds");
            var probes=new List<Tuple<IBlock,Rectangle,BlockCollisionType,Rectangle>>();
            foreach(var type in new[]{SlopeType.TopLeft,SlopeType.TopRight,SlopeType.BottomLeft,SlopeType.BottomRight})
            {
                IBlock slope=new SlopeBlock(new Rectangle(100,100,48,48),type);
                for(int y=90;y<158;y++)for(int x=80;x<168;x++)
                {var box=new Rectangle(x,y,18,26);Rectangle overlap;var hit=slope.Intersects(box,out overlap);probes.Add(Tuple.Create(slope,box,hit,overlap));}
            }
            using(var physics=new PhysicsCoordinates())foreach(var probe in probes)
            {Rectangle overlap;Require(probe.Item1.Intersects(probe.Item2,out overlap)==probe.Item3&&overlap==probe.Item4,"Symmetric profile leaves slope intersection and overlap unchanged");}
            PhysicsSaveChecks(directory);PhysicsEdgeChecks();
            Console.WriteLine("[OK] Native byte-identical trajectories, 23936 unchanged slope queries, save matching and native screen edges");
        }
        private static void PhysicsSaveChecks(string directory)
        {
            string folder=Path.Combine(directory,"physics-save");Directory.CreateDirectory(folder);
            var body=new BodyComp(new Vector2(100,-1400000),18,26);
            using(var physics=new PhysicsCoordinates())using(physics.Attach(body,false))
            {
                body.Velocity=new Vector2(0.00001f,0.00001f);
                var context=new BehaviourContext(body);var handlers=new LinkedList<IBlockBehaviour>();
                new UpdateXPositionFromVelocityBehaviour(handlers).ExecuteBehaviour(context);new UpdateYPositionFromVelocityBehaviour(handlers).ExecuteBehaviour(context);
                var state=(PhysicsCoordinateState)physics.Capture();var native=new SaveState{position=body.Position,velocity=body.Velocity,time_stamp=27};
                using(var save=new PhysicsPersistence(folder,()=>folder))
                {
                    save.Publish(native,state);var packet=save.Find(native);Require(packet!=null,"Exact state matches the native save being written");
                    var changed=native;changed.time_stamp++;Require(save.Find(changed)==null,"A later native save cannot receive an earlier fraction");
                    save.Write(packet);
                }
                using(var restored=new PhysicsPersistence(folder,()=>folder))
                {
                    body.Position=new Vector2(0,0);Require(restored.Restore(physics,native),"Disk save restores the matching native frame");
                    var actual=(PhysicsCoordinateState)physics.Capture();Require(actual.X==state.X&&actual.Y==state.Y,"Disk save retains fractions not representable in world float");
                }
                using(var stale=new PhysicsPersistence(folder,()=>folder))
                {
                    var changed=native;changed.time_stamp++;Require(!stale.Restore(physics,changed),"Stale disk record cannot move a different native save");
                    stale.Write(new PhysicsSavePacket{Native=native,Exact=new PhysicsCoordinateState{X=long.MaxValue,Y=state.Y}});
                }
                using(var invalid=new PhysicsPersistence(folder,()=>folder))
                    Require(!invalid.Restore(physics,native)&&((PhysicsCoordinateState)physics.Capture()).X==state.X,"Invalid auxiliary record leaves the native player usable");
                var carried=(PhysicsCoordinateState)physics.Capture();PhysicsCoordinates.Carry(body,480,-360);
                Require(((PhysicsCoordinateState)physics.Capture()).Y==carried.Y-360*PhysicsCoordinates.Unit,"Carry/relocation retains the invisible Y fraction");
                PhysicsCoordinates.RestoreCarry(body,carried);Require(((PhysicsCoordinateState)physics.Capture()).Y==carried.Y,"Obstructed platform restores exact carry state");
                using(var reset=new PhysicsPersistence(folder,()=>folder))
                {
                    reset.Publish(native,state);SceneSaveCoordinator.Reset(folder);reset.Write(reset.Find(native));
                    using(var empty=new PhysicsPersistence(folder,()=>folder))Require(!empty.Restore(physics,native),"Reset invalidates old exact save packets");
                }
            }
        }
        private static void PhysicsEdgeChecks()
        {
            var screensField=typeof(LevelManager).GetField("m_screens",OwnedPatches.Members);var count=typeof(LevelManager).GetField("_total_screens",OwnedPatches.Members);var camera=typeof(Camera).GetField("_current_screen",OwnedPatches.Members);
            object oldScreens=screensField.GetValue(null),oldCount=count.GetValue(null),oldCamera=camera.GetValue(null);
            var screens=Enumerable.Range(0,8).Select(i=>new LevelScreen(i,new IBlock[0],new LevelScreen.Graphics(),false,new[]{new TeleportLink(),new TeleportLink()},0,null)).ToArray();
            try
            {
                screensField.SetValue(null,screens);count.SetValue(null,8);
                foreach(int width in new[]{17,18})foreach(bool left in new[]{false,true})
                {
                    camera.SetValue(null,1);var body=new BodyComp(new Vector2(left?-40:500,-42.125f),width,26);
                    using(var physics=new PhysicsCoordinates())using(physics.Attach(body,false))
                    {
                        Require(new CapPositionBehaviour().ExecuteBehaviour(new BehaviourContext(body)),"Cap keeps the native behavior chain running");
                        Require(body.Position.X==(left?-width/2f:480-width/2f),"Map edge cap mirrors odd and even body widths");
                    }
                }
                screens[1].teleport[0]=new TeleportLink(5);screens[1].teleport[1]=new TeleportLink(5);
                foreach(bool left in new[]{false,true})
                {
                    camera.SetValue(null,1);var body=new BodyComp(new Vector2(left?-30.25f:480.25f,-42.125f),18,26);
                    using(var physics=new PhysicsCoordinates())using(physics.Attach(body,false))
                    {
                        var before=(PhysicsCoordinateState)physics.Capture();var context=new BehaviourContext(body);
                        Require(new HandlePlayerTeleportBehaviour().ExecuteBehaviour(context),"Native teleport keeps the behavior chain running");
                        var after=(PhysicsCoordinateState)physics.Capture();
                        Require(after.X==before.X+(left?480:-480)*PhysicsCoordinates.Unit&&after.Y==before.Y-1080*PhysicsCoordinates.Unit,"Native side link retains both fractions");
                        Require(Camera.CurrentScreen==4&&context.ContainsKey("TeleportedPlayer"),"Native link updates camera and event flag");
                    }
                }
            }
            finally{screensField.SetValue(null,oldScreens);count.SetValue(null,oldCount);camera.SetValue(null,oldCamera);}
        }
    }
}
