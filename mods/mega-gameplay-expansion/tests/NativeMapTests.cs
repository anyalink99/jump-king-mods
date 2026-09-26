using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JumpKing;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Level.Sampler;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal static partial class Tests
    {
        private static void TeleportBoundaryRegression()
        {
            var floor = new IBlock[] { new BoxBlock(new Rectangle(0,320,480,40)) };
            var screens = new[] {
                new LevelScreen(0,floor,new LevelScreen.Graphics(),false,new[]{new TeleportLink(2)},0,null),
                new LevelScreen(1,new IBlock[0],new LevelScreen.Graphics(),true,new[]{new TeleportLink(1)},0,null)
            };
            var source = new BodyComp(new Vector2(180,294),18,26) { Velocity = new Vector2(0,-4) };
            BodyComp landing; int ticks; string reason;
            Require(NativeFlight.TryPredict(source,new FlightWorld(screens,0,0),out landing,out ticks,out reason),
                "Unactivated current/adjacent teleport or adjacent wind rejected: " + reason);
            Require(landing.Position == source.Position,"Interior trajectory on teleport screen lost native landing");
            source.Position = new Vector2(470,294); source.Velocity = new Vector2(3,-4);
            var sideWorld = new FlightWorld(screens,0,0); sideWorld.SetWindClock(0,.017,0);
            Require(NativeFlight.TryPredict(source,sideWorld,out landing,out ticks,out reason),
                "Native side-exit teleport failed: " + reason);
            Require(source.Position == new Vector2(470,294) && source.Velocity == new Vector2(3,-4),"Side-exit refusal mutated source");
            source.Position = new Vector2(180,20); source.Velocity = new Vector2(0,-10);
            var windWorld = new FlightWorld(screens,0,0); windWorld.SetWindClock(0,.017,0);
            Require(NativeFlight.TryPredict(source,windWorld,out landing,out ticks,out reason), "Native wind entry was refused: " + reason);
            Console.WriteLine("[OK] Teleport/wind boundaries: interior and adjacent metadata allowed; wind entry and actual side exits supported non-mutating");
        }
        // Read the installed uncompressed native atlas, not a hand-drawn mock.
        // No graphics device, game writes, map publish or Workshop loading.
        private static LevelScreen[] ReadNativeScreens(string gameDir, int count)
        {
            using (var reader = new BinaryReader(File.OpenRead(Path.Combine(gameDir,"Content","level.xnb"))))
            {
                Require(new string(reader.ReadChars(4)) == "XNBw" && reader.ReadByte() == 5 && (reader.ReadByte() & 0x80) == 0,"Native fixture requires uncompressed Windows XNB5");
                Require(reader.ReadInt32() == reader.BaseStream.Length,"Native atlas size");
                int readers = Read7Bit(reader);
                for (int i=0;i<readers;i++) { string type = reader.ReadString(); Require(type.StartsWith("Microsoft.Xna.Framework.Content.Texture2DReader"),"Native texture reader"); reader.ReadInt32(); }
                Require(Read7Bit(reader) == 0 && Read7Bit(reader) == 1,"Native atlas resources");
                Require(reader.ReadInt32() == 0,"Native atlas Color format");
                int width=reader.ReadInt32(), height=reader.ReadInt32();
                Require(reader.ReadInt32() >= 1 && reader.ReadInt32() == width*height*4,"Native atlas mip bytes");
                var colors = new Color[width*height];
                for(int i=0;i<colors.Length;i++) colors[i]=new Color(reader.ReadByte(),reader.ReadByte(),reader.ReadByte(),reader.ReadByte());
                var texture=(LevelTexture)typeof(LevelTexture).GetConstructor(Flags,null,new[]{typeof(Color[]),typeof(int),typeof(int)},null).Invoke(new object[]{colors,width,height});
                var load=typeof(LevelManager).GetMethod("LoadBlocksInterval",Flags);
                var screens=new LevelScreen[count];
                for(int i=0;i<count;i++) {
                    var args=new object[]{texture,null,i,false,null,0f,null};
                    var blocks=(IBlock[])load.Invoke(null,args);
                    screens[i]=new LevelScreen(i,blocks,new LevelScreen.Graphics(),(bool)args[3],(TeleportLink[])args[4],(float)args[5],(bool?)args[6]);
                }
                return screens;
            }
        }
        private static int Read7Bit(BinaryReader reader)
        { int value=0; for(int shift=0;shift<35;shift+=7) { byte b=reader.ReadByte(); value|=(b&127)<<shift; if((b&128)==0)return value; } throw new InvalidDataException("Invalid XNB integer"); }
        private static void InstalledVanillaMap(string gameDir)
        {
            var screens=ReadNativeScreens(gameDir,5);
            Require(!screens[0].CanTeleport && !screens[1].CanTeleport && screens[2].CanTeleport,
                "Reviewed vanilla first/second/third screen teleport fixture changed");
            typeof(LevelManager).GetField("m_screens",Flags).SetValue(null,screens);
            typeof(LevelManager).GetField("_total_screens",Flags).SetValue(null,screens.Length);
            int compared=0;
            for(int screen=0;screen<3;screen++) {
                int probes=0;
                for(int y=320-screen*360;y>=40-screen*360 && probes<3;y-=8)
                for(int x=32;x<440 && probes<3;x+=32) {
                    typeof(Camera).GetField("_current_screen",Flags).SetValue(null,screen);
                    var box=new Rectangle(x,y-26,18,26); Rectangle overlap; AdvCollisionInfo info;
                    if(LevelManager.CheckCollision(box,out overlap,out info)) continue;
                    box.Y++;
                    if(!LevelManager.CheckCollision(box,out overlap,out info)) continue;
                    var source=new BodyComp(new Vector2(x,y-26),18,26) { Velocity=new Vector2(0,-4) };
                    BodyComp landing; int ticks; string reason;
                    Require(NativeFlight.TryPredict(source,new FlightWorld(screens,screen),out landing,out ticks,out reason),
                        "Installed vanilla screen " + (screen+1) + " refused: " + reason);
                    var expected=new BodyComp(source.Position,18,26); NativeFlight.CopyState(source,expected);
                    var pipeline=NativeFlight.Get<LinkedList<IBodyCompBehaviour>>(expected,"m_behaviours");
                    foreach(var b in pipeline.ToArray()) if(b.GetType().Name=="PlayBumpSFXBehaviour" || b.GetType().Name=="WaterParticleSpawningBehaviour") pipeline.Remove(b);
                    int actualTicks=0;
                    do {
                        typeof(BodyComp).GetMethod("UpdateInternal",Flags).Invoke(expected,new object[]{1f/60f}); actualTicks++;
                        if(expected.IsOnGround) Camera.UpdateCamera(expected.GetHitbox().Center);
                        else Camera.UpdateCameraWithVelocity(expected.GetHitbox().Center,expected.Velocity);
                    } while(!expected.IsOnGround && actualTicks<1000);
                    Require(ticks==actualTicks && landing.Position==expected.Position && landing.Velocity==expected.Velocity,
                        "Installed vanilla native parity on screen " + (screen+1));
                    probes++; compared++;
                }
                Require(probes==3,"Missing three native takeoff fixtures on screen " + (screen+1));
            }
            Console.WriteLine("[OK] Installed vanilla atlas: " + compared + " native trajectories across first three screens, including the real third-screen teleport metadata");
        }
    }
}
