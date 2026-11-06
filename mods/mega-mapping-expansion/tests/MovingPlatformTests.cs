using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JKRuntime;
using JumpKing;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static MovingPlatformPlan PlatformPlan(string motion="axis='horizontal' distance='96'",string platform="rect='100,200,48,8'",string profile="")
        {return MovingPlatformPlan.Parse("<MovingPlatforms version='1'><Motion id='route' "+motion+"/><Profile id='deck' motion='route' "+profile+"/><Platform id='test' profile='deck' "+platform+"/></MovingPlatforms>",3);}
        private static void MovingPlatformChecks(string directory)
        {
            var plan=PlatformPlan();var motion=new PlatformMotionPlayer(plan.Motions["route"]);
            motion.Advance(0.5);Require(Math.Abs(motion.Position.X-30)<1e-9,"platform speed uses seconds rather than update count");
            var fine=new PlatformMotionPlayer(plan.Motions["route"]);for(int n=0;n<120;n++)fine.Advance(1.0/240);
            Require(Math.Abs(fine.Position.X-motion.Position.X)<1e-8,"fractional and ordinary updates cover the same route distance");
            var stopped=motion.State.Copy();motion.Command("stop");motion.Advance(1);Require(motion.Position.X==30,"stopped platforms retain their phase");motion.Command("start");motion.Command("reverse");motion.Advance(.25);Require(Math.Abs(motion.Position.X-15)<1e-9,"reversal retains position and changes travel direction");motion.Restore(stopped);Require(motion.Position.X==30,"motion snapshots restore exact progress");
            var waits=new PlatformMotionPlayer(PlatformPlan("speed='60' wait='0.5' axis='vertical' distance='-60'").Motions["route"]);
            waits.Advance(.5);Require(waits.Position.Y==0,"initial waypoint wait is respected");waits.Advance(1);Require(waits.Position.Y==-60,"arrival reaches the exact endpoint");waits.Advance(.5);Require(waits.Position.Y==-60,"endpoint wait has no extra turnaround frame");waits.Advance(.5);Require(Math.Abs(waits.Position.Y+30)<1e-9,"ping-pong resumes after the configured wait");
            var diagonal=new PlatformMotionPlayer(PlatformPlan("speed='100' mode='once'><Point x='0' y='0'/><Point x='30' y='40'/></Motion><Motion id='unused' axis='horizontal' distance='10'").Motions["route"]);
            diagonal.Advance(.25);Require(diagonal.Position.X==15&&diagonal.Position.Y==20,"diagonal speed is measured along the path");diagonal.Advance(1);Require(!diagonal.State.Playing&&diagonal.Position.X==30,"one-shot routes stop at the final point");
            var looping=new PlatformMotionPlayer(PlatformPlan("mode='loop' axis='horizontal' distance='60'").Motions["route"]);looping.Advance(1.5);Require(Math.Abs(looping.Position.X-30)<1e-8,"closed routes traverse the final-to-first segment");
            var phased=new PlatformMotionPlayer(PlatformPlan("phase='86400' axis='horizontal' distance='60'").Motions["route"]);Require(Math.Abs(phased.Position.X)<1e-7,"large phase offsets skip whole periods without unbounded stepping");
            var reversing=new PlatformMotionPlayer(PlatformPlan("axis='horizontal' distance='60' onBlocked='reverse' blockedWait='0.2'").Motions["route"]);reversing.Advance(.5);reversing.Obstruct();reversing.Advance(.2);reversing.Advance(.25);Require(Math.Abs(reversing.Position.X-15)<1e-8,"obstruction reverses from the safe position after its wait");
            var heldMotion=new PlatformMotionPlayer(PlatformPlan().Motions["route"]);heldMotion.Obstruct();Require(heldMotion.Velocity.X==0,"a blocked deck supplies no jump momentum");
            var stopping=new PlatformMotionPlayer(PlatformPlan("axis='horizontal' onBlocked='stop'").Motions["route"]);stopping.Obstruct();stopping.Advance(1);Require(!stopping.State.Playing&&stopping.Position.X==0,"stop-on-obstruction requires an explicit restart");stopping.Command("start");stopping.Advance(.5);Require(stopping.Position.X==30,"start releases stop-on-obstruction");
            foreach(string bad in new[]{"speed='NaN'","speed='0'","phase='-1'","mode='teleport'","axis='diagonal'"})BadRoute(()=>PlatformPlan(bad+" distance='20'"),"invalid platform motion is rejected: "+bad);
            BadRoute(()=>MovingPlatformPlan.Parse("<MovingPlatforms version='1'><Motion id='a'><Point/><Point/></Motion></MovingPlatforms>",1),"zero-length segments are rejected");
            BadRoute(()=>MovingPlatformPlan.Parse("<MovingPlatforms version='1'><Profile id='a' motion='missing'/></MovingPlatforms>",1),"unknown route references fail before activation");
            BadRoute(()=>PlatformPlan(platform:"screen='4' rect='0,0,16,8'"),"platforms cannot reference unauthored screens");
            BadRoute(()=>PlatformPlan(profile:"texture='../escape.png'"),"platform assets cannot escape the map");
            foreach(string bad in new[]{"momentum='-1'","momentum='NaN'","momentum='5'","momentumAxes='diagonal'"})BadRoute(()=>PlatformPlan(profile:bad),"invalid momentum settings are rejected: "+bad);
            BadRoute(()=>PlatformPlan("axis='horizontal' onBlocked='crush'"),"unknown obstruction behavior is rejected");
            BadRoute(()=>MovingPlatformPlan.Parse("<MovingPlatforms version='1'><Motion id='a' axis='horizontal'/><Profile id='a' motion='a'/><Paint color='#000000' profile='a'/></MovingPlatforms>",1),"native material colors cannot become platform aliases");
            var painted=MovingPlatformPlan.Defaults();painted.AddPainted(new[]{
                new PlatformCell{Color="#D060A0",Screen=1,Rect=new PlatformRect(100,200,16,8)},new PlatformCell{Color="#D060A0",Screen=1,Rect=new PlatformRect(116,200,16,8)},
                new PlatformCell{Color="#D060A0",Screen=1,Rect=new PlatformRect(200,200,16,8)},new PlatformCell{Color="#D061A0",Screen=1,Rect=new PlatformRect(132,200,24,8)},new PlatformCell{Color="#D061A0",Screen=1,Rect=new PlatformRect(216,200,16,8)}});
            Require(painted.Platforms.Count==2&&painted.Platforms[0].Parts.Count==2,"side-connected painted pieces form one platform; separated islands stay independent");
            var colorMotion=painted.Motions[painted.Profiles[painted.Platforms[0].Profile].Motion];Require(colorMotion.Points[1].Position.X==24,"range markers determine travel without adding static collision");
            var defaults=MovingPlatformPlan.Defaults();defaults.AddPainted(new[]{new PlatformCell{Color="#60A0D0",Screen=1,Rect=new PlatformRect(100,200,32,8)}});Require(defaults.Platforms.Count==1,"default colors also work without range markers");
            var twoSided=MovingPlatformPlan.Defaults();twoSided.AddPainted(new[]{new PlatformCell{Color="#D060A0",Screen=1,Rect=new PlatformRect(100,200,48,8)},new PlatformCell{Color="#D061A0",Screen=1,Rect=new PlatformRect(76,200,24,8)},new PlatformCell{Color="#D061A0",Screen=1,Rect=new PlatformRect(148,200,24,8)}});
            var twoWay=new PlatformMotionPlayer(twoSided.Motions[twoSided.Profiles[twoSided.Platforms[0].Profile].Motion]);Require(twoSided.Platforms[0].Parts[0].X+twoWay.Position.X==100,"two-sided ranges start at the painted position");twoWay.Advance(.4);twoWay.Advance(.4);Require(twoWay.State.Direction==-1&&Math.Abs(twoWay.Position.X-24)<1e-8,"two-sided ranges pass the origin without an extra turnaround");
            PlatformImageScan.ValidateBounds(plan,3);BadRoute(()=>PlatformImageScan.ValidateBounds(PlatformPlan("axis='horizontal' distance='1000'"),3),"routes outside horizontal map bounds fail validation");
            var layout=new MapLayout{Screens=1,Columns=1,Rows=1,CellWidth=60,CellHeight=45};
            var cells=PlatformImageScan.Collect(MovingPlatformPlan.Defaults(),layout,(x,y)=>y==20&&x>=10&&x<14?0xffD060A0U:0U);
            Require(cells.Count==1&&cells[0].Rect.Width==32&&cells[0].Rect.Y==160,"editor palette scanning uses the declared collision scale");
            NativePlatformChecks();
        }
        private static void NativePlatformChecks()
        {
            var screensField=typeof(LevelManager).GetField("m_screens",OwnedPatches.Members);var countField=typeof(LevelManager).GetField("_total_screens",OwnedPatches.Members);
            var oldScreens=screensField.GetValue(null);var oldCount=countField.GetValue(null);
            try
            {
                var screen=new LevelScreen(0,new IBlock[0],new LevelScreen.Graphics(),false,new TeleportLink[0],0,null);screensField.SetValue(null,new[]{screen});countField.SetValue(null,1);
                using(var owner=new NativeMovingPlatforms(PlatformPlan(),"",false))
                {
                    var body=new BodyComp(new Vector2(110,174),18,26);owner.Tick(body,1.0/60);Require(body.Position.X==111&&body.Position.Y==174,"a horizontal platform carries its rider without changing height");
                    var saved=owner.Capture();owner.Tick(body,1.0/60);owner.Restore(saved);var bytes=new JKRuntime.World.WorldStateCodec(typeof(MovingPlatformPacket),true);Require(bytes.Capture(saved).SequenceEqual(bytes.Capture(owner.Capture())),"native platform packets restore all route state");
                    var corrupt=(MovingPlatformPacket)owner.Capture();corrupt.States[0]="999|0|1|0|0|0|1";var before=bytes.Capture(owner.Capture());BadRoute(()=>owner.Restore(corrupt),"invalid native packets are rejected");Require(before.SequenceEqual(bytes.Capture(owner.Capture())),"a rejected packet cannot partially mutate clocks");
                    owner.Command("test","stop");float x=body.Position.X;owner.Tick(body,1.0/60);Require(body.Position.X==x,"scene stop commands hold physical collision");
                }
                using(var lift=new NativeMovingPlatforms(PlatformPlan("axis='vertical' distance='-96'"),"",false))
                {var body=new BodyComp(new Vector2(110,174),18,26);lift.Tick(body,1.0/60);Require(body.Position.Y==173,"a rising platform carries the King before native gravity");body.Velocity.Y=-5;lift.Tick(body,1.0/60);Require(body.Position.Y==173,"jumping detaches from vertical support");}
                using(var shared=new NativeMovingPlatforms(PlatformPlan(),"",false))
                {
                    var first=new BodyComp(new Vector2(105,174),18,26);var second=new BodyComp(new Vector2(127,174),18,26);shared.ObserveBody(second);shared.Tick(first,1.0/60);
                    Require(first.Position.X==106&&second.Position.X==128,"one world step carries multiple bodies without advancing the platform twice");
                }
                var ceiling=new LevelScreen(0,new IBlock[]{new BoxBlock(new Rectangle(0,150,480,24))},new LevelScreen.Graphics(),false,new TeleportLink[0],0,null);screensField.SetValue(null,new[]{ceiling});
                using(var lift=new NativeMovingPlatforms(PlatformPlan("axis='vertical' distance='-96'"),"",false))
                {var body=new BodyComp(new Vector2(110,174),18,26);var packet=(MovingPlatformPacket)lift.Capture();lift.Tick(body,1.0/60);Require(body.Position.Y==174,"a blocked lift does not push the King into the ceiling");var blocked=(MovingPlatformPacket)lift.Capture();Require(packet.States[0].Split('|').Take(7).SequenceEqual(blocked.States[0].Split('|').Take(7)),"blocked movement rolls back phase as well as geometry");lift.Validate(blocked);}
                using(var lift=new NativeMovingPlatforms(PlatformPlan("axis='vertical' distance='-96' onBlocked='reverse'","rect='100,240,48,8'"),"",false))
                {
                    var body=new BodyComp(new Vector2(110,214),18,26);for(int n=0;n<90;n++)lift.Tick(body,1.0/60);
                    Require(body.GetHitbox().Top>=174&&body.Position.Y>174,"a ceiling-blocked rider returns downward without being crushed");
                }
                screensField.SetValue(null,new[]{new LevelScreen(0,new IBlock[]{new BoxBlock(new Rectangle(132,150,18,24))},new LevelScreen.Graphics(),false,new TeleportLink[0],0,null)});
                using(var shared=new NativeMovingPlatforms(PlatformPlan("axis='vertical' distance='-96'"),"",false))
                {var first=new BodyComp(new Vector2(105,174),18,26);var second=new BodyComp(new Vector2(127,174),18,26);shared.ObserveBody(second);shared.Tick(first,1.0/60);Require(first.Position.Y==174&&second.Position.Y==174,"a blocked second rider rolls back the entire group and every displaced body");}
                var player=(PlayerEntity)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(PlayerEntity));player.m_body=new BodyComp(new Vector2(110,174),18,26);screensField.SetValue(null,new[]{screen});
                var hitboxes=typeof(LevelScreen).GetField("m_hitboxes",OwnedPatches.Members);var original=hitboxes.GetValue(screen);
                using(var owner=new NativeMovingPlatforms(PlatformPlan(),"",false))using(owner.Activate(player,false))
                {Require(((IBlock[])hitboxes.GetValue(screen)).OfType<NativePlatformPart>().Count()==1,"activation installs moving geometry into native queries");}
                Require(ReferenceEquals(original,hitboxes.GetValue(screen)),"teardown restores the original native collision array");
                foreach(string axis in new[]{"horizontal","vertical"})
                foreach(double factor in new[]{0.0,0.5,1.0,2.0})
                {
                    player.m_body=PlatformBody(new Vector2(110,174));player.m_body.Velocity.Y=-5;
                    using(var owner=new NativeMovingPlatforms(PlatformPlan("axis='"+axis+"' distance='-96'",profile:"momentum='"+factor.ToString(System.Globalization.CultureInfo.InvariantCulture)+"'"),"",false))using(owner.Activate(player,false))
                    {
                        PlatformBodyStep(player.m_body);float expected=axis=="horizontal"?-(float)factor:0;
                        Require(Math.Abs(player.m_body.Velocity.X-expected)<1e-5&&Math.Abs(player.m_body.Velocity.Y-(-5+PlayerValues.GRAVITY-(axis=="vertical"?(float)factor:0)))<1e-5,"jump momentum inherits the analytic "+axis+" speed at scale "+factor);
                        var velocity=player.m_body.Velocity;PlatformBodyStep(player.m_body);
                        Require(player.m_body.Velocity.X==velocity.X&&Math.Abs(player.m_body.Velocity.Y-velocity.Y-PlayerValues.GRAVITY)<1e-5,"jump momentum applies once rather than accelerating every airborne tick");
                    }
                }
                foreach(string axes in new[]{"horizontal","vertical"})
                {
                    player.m_body=PlatformBody(new Vector2(110,174));player.m_body.Velocity.Y=-5;
                    using(var owner=new NativeMovingPlatforms(PlatformPlan(profile:"momentum='1' momentumAxes='"+axes+"'"),"",false))using(owner.Activate(player,false))
                    {PlatformBodyStep(player.m_body);Require(player.m_body.Velocity.X==(axes=="horizontal"?1:0),"momentum axes select which velocity components are inherited");}
                }
                player.m_body=PlatformBody(new Vector2(110,174));player.m_body.Velocity.Y=-5;
                using(var owner=new NativeMovingPlatforms(PlatformPlan("axis='horizontal' distance='96' wait='1'",profile:"momentum='1'"),"",false))using(owner.Activate(player,false))
                {PlatformBodyStep(player.m_body);Require(player.m_body.Velocity.X==0,"waiting platforms add no launch impulse");}
                foreach(string collision in new[]{"solid","oneWay"})
                {
                    player.m_body=PlatformBody(new Vector2(110,174));
                    using(var owner=new NativeMovingPlatforms(PlatformPlan(profile:"collision='"+collision+"'"),"",false))using(owner.Activate(player,false))
                    {
                        for(int n=0;n<10;n++)PlatformBodyStep(player.m_body);
                        Require(player.m_body.IsOnGround&&player.m_body.Position.X==120&&Math.Abs(player.m_body.GetHitbox().Bottom-200)<=1,"native collision keeps a rider grounded on a moving "+collision+" deck");
                        player.m_body.Velocity.Y=-5;PlatformBodyStep(player.m_body);Require(player.m_body.GetHitbox().Bottom<200,"native jump leaves a moving "+collision+" deck");
                    }
                }
                foreach(string collision in new[]{"solid","oneWay"})
                foreach(string route in new[]{"axis='vertical' distance='-96' speed='40' wait='0.5'", "speed='55' wait='0.25'><Point x='0' y='0'/><Point x='64' y='-64'/></Motion><Motion id='unused' axis='horizontal' distance='1'"})
                foreach(int deckScreen in new[]{1,2})
                {
                    var second=new LevelScreen(1,new IBlock[0],new LevelScreen.Graphics(),false,new TeleportLink[0],0,null);screensField.SetValue(null,new[]{screen,second});countField.SetValue(null,2);
                    player.m_body=PlatformBody(new Vector2(110,174-(deckScreen-1)*360));
                    using(var owner=new NativeMovingPlatforms(PlatformPlan(route,"screen='"+deckScreen+"' rect='100,200,48,8'",profile:"collision='"+collision+"'"),"",false))using(owner.Activate(player,false))
                    {
                        var part=((IBlock[])hitboxes.GetValue(deckScreen==1?screen:second)).OfType<NativePlatformPart>().Single();
                        for(int n=0;n<480;n++)
                        {
                            PlatformBodyStep(player.m_body);
                            Require(player.m_body.IsOnGround&&player.m_body.GetHitbox().Bottom==part.Bounds.Top,"every lift/diagonal tick keeps native ground contact on "+collision+" at tick "+n+": "+player.m_body.Position+" / "+part.Bounds);
                        }
                        player.m_body.Velocity.Y=-5;PlatformBodyStep(player.m_body);
                        Require(!player.m_body.IsOnGround&&player.m_body.GetHitbox().Bottom<part.Bounds.Top,"jumping leaves a vertical/diagonal "+collision+" platform immediately: "+route+" / "+player.m_body.Position+" / "+player.m_body.Velocity+" / "+part.Bounds+" / ground="+player.m_body.IsOnGround);
                        player.m_body.Position=new Vector2(300,part.Bounds.Top-26);player.m_body.Velocity=Vector2.Zero;PlatformBodyStep(player.m_body);
                        Require(!player.m_body.IsOnGround,"leaving a moving deck horizontally releases ground contact");
                        player.m_body.Position=new Vector2(part.Bounds.X+8,part.Bounds.Top-56);player.m_body.Velocity=Vector2.Zero;
                        for(int n=0;n<120;n++)PlatformBodyStep(player.m_body);
                        Require(player.m_body.IsOnGround&&player.m_body.GetHitbox().Bottom==part.Bounds.Top,"falling onto a moving vertical/diagonal "+collision+" deck lands and stays supported");
                    }
                }
                screensField.SetValue(null,new[]{screen});countField.SetValue(null,1);
                player.m_body=PlatformBody(new Vector2(110,205));player.m_body.Velocity.Y=-8;
                using(var owner=new NativeMovingPlatforms(PlatformPlan("playing='false' axis='horizontal' distance='96'",profile:"collision='oneWay'"),"",false))using(owner.Activate(player,false))
                {
                    for(int n=0;n<12;n++)PlatformBodyStep(player.m_body);Require(player.m_body.GetHitbox().Bottom<200,"the King can jump upward through a one-way deck");
                    for(int n=0;n<100;n++)PlatformBodyStep(player.m_body);Require(player.m_body.IsOnGround&&Math.Abs(player.m_body.GetHitbox().Bottom-200)<=1,"native falling lands on the top of a one-way deck");
                }
            }
            finally{screensField.SetValue(null,oldScreens);countField.SetValue(null,oldCount);}
            Console.WriteLine("[OK] Moving platforms: routes, colors, native rides/jumps, one-way landings, multiple bodies, pressure rollback, packets and teardown");
        }
        private static BodyComp PlatformBody(Vector2 position)
        {
            var body=new BodyComp(position,18,26);var list=(LinkedList<JumpKing.API.IBodyCompBehaviour>)typeof(BodyComp).GetField("m_behaviours",OwnedPatches.Members).GetValue(body);
            foreach(var stage in list.ToArray())if(new[]{"WindVelocityUpdateBehaviour","CacheLastScreenBehaviour","WaterParticleSpawningBehaviour","CapPositionBehaviour","HandlePlayerTeleportBehaviour","PlayBumpSFXBehaviour"}.Contains(stage.GetType().Name))list.Remove(stage);
            return body;
        }
        private static void PlatformBodyStep(BodyComp body){typeof(BodyComp).GetMethod("Update",OwnedPatches.Members).Invoke(body,new object[]{1f/60});}
    }
}
