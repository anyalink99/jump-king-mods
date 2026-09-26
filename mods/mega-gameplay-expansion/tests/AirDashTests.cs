using System;
using System.Collections.Generic;
using System.Linq;
using EntityComponent;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Controller;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal static partial class Tests
    {
        private static void DashPad(WalkInputFixture pad,bool current,bool previous)
        {
            typeof(PadInstance).GetField("current_state",Flags).SetValue(pad.Pad,new PadState { jump=current });
            typeof(PadInstance).GetField("last_state",Flags).SetValue(pad.Pad,new PadState { jump=previous });
        }
        private static PlayerEntity DashPlayer()
        {
            var player=ResumePlayer();
            var pipeline=NativeFlight.Get<LinkedList<IBodyCompBehaviour>>(player.m_body,"m_behaviours");
            foreach(var b in pipeline.ToArray()) if(b.GetType().Name=="WaterParticleSpawningBehaviour") pipeline.Remove(b);
            player.m_body.Position=new Vector2(180,180); player.m_body.Velocity=new Vector2(3.5f,-4);
            NativeFlight.Set(player.m_body,"_is_on_ground",false);
            return player;
        }
        private static void DashWorld(params IBlock[] blocks)
        {
            typeof(LevelManager).GetField("m_screens",Flags).SetValue(null,Scene(blocks));
            typeof(LevelManager).GetField("_total_screens",Flags).SetValue(null,1);
            typeof(Camera).GetField("_current_screen",Flags).SetValue(null,0);
        }
        private static void DashPress(AirDashController controller,PlayerEntity player,WalkInputFixture pad)
        {
            DashPad(pad,true,false);
            typeof(Component).GetMethod("LowUpdate",Flags).Invoke(player.GetComponent<InputComponent>(),new object[]{1f/60f});
            controller.AfterInput(1f/60f);
        }
        private static void AirDashRegression()
        {
            bool setting=Settings.Current.AirDash;
            var screens=MapPixels.AirDash.Screens.ToArray();
            try
            {
                using(var pad=new WalkInputFixture())
                {
                    Settings.Current.AirDash=false; MapPixels.AirDash.Screens.Clear();
                    // Disabled controller cannot alter an ordinary flight or input.
                    DashWorld(new BoxBlock(new Rectangle(0,320,480,40)));
                    var idle=DashPlayer();
                    using(var controller=new AirDashController(idle))
                    {
                        DashPress(controller,idle,pad);
                        Require(!controller.Active && idle.GetComponent<InputComponent>().TryConsumeJump(),"Disabled dash stole native input");
                    }
                    MapPixels.AirDash.Screens.Add(0);
                    foreach(int direction in new[]{-1,1}) foreach(bool water in new[]{false,true}) foreach(float vy in new[]{-7f,5f})
                    {
                        var blocks=new List<IBlock>{new BoxBlock(new Rectangle(0,320,480,40))};
                        if(water) blocks.Add(new WaterBlock(new Rectangle(0,0,480,320)));
                        DashWorld(blocks.ToArray());
                        var player=DashPlayer(); player.m_body.Velocity=new Vector2(direction*3.5f,vy);
                        int audioPlays=0, audioStops=0;
                        using(var controller=new AirDashController(player,delegate { audioPlays++; },delegate { audioStops++; }))
                        {
                            DashPress(controller,player,pad);
                            var description=controller.Describe();
                            Require(description.Active && description.Available,"An active dash remains describable after consuming its charge");
                            Require(audioPlays==0,"Whoosh played before actual dash movement");
                            Require(controller.Active && !player.GetComponent<InputComponent>().TryConsumeJump(),"Dash press was also buffered");
                            var jumpNode=new JumpState(player);
                            Require(jumpNode.Run(new BehaviorTree.TickData { delta_time=1f/60f })==BehaviorTree.BTresult.Failure,"Dash press started native charge");
                            var start=player.m_body.Position; var velocity=player.m_body.Velocity;
                            object snapshot=controller.Capture(), midway=null; controller.Validate(snapshot);
                            for(int tick=0;tick<14;tick++)
                            {
                                DashPad(pad,tick>=1,tick>=2);
                                player.UpdateComponents(1f/60f);
                                Require(audioPlays==1,"Dash whoosh was missing or repeated on a movement tick");
                                if(tick==1) midway=controller.Capture();
                                Require(player.m_body.Position.Y==start.Y && player.m_body.Position.X==start.X+direction*Math.Min(80,6*(tick+1)),"Dash was curved, material-scaled or wrong length");
                                if(tick<13) Require(player.m_body.Velocity==new Vector2(direction*6,0),"Exit scaling slowed the active dash");
                                if(tick==1) Require(NativeFlight.Get<bool>(player.GetComponent<InputComponent>(),"_can_jump"),"Second press during dash lost its native buffer token");
                            }
                            Require(!controller.Active && controller.Used && player.m_body.Velocity==new Vector2(direction*3,0),"Dash did not hand half its horizontal impulse to free physics");
                            DashFreePhysicsRegression(player,pad);
                            Require(NativeFlight.Get<bool>(player.GetComponent<InputComponent>(),"_can_jump"),"Second press was consumed before landing");
                            NativeFlight.Set(player.m_body,"_is_on_ground",true);
                            Require(jumpNode.Run(new BehaviorTree.TickData { delta_time=1f/60f })==BehaviorTree.BTresult.Running,"Second press did not start native buffered charge at landing");
                            NativeFlight.Set(player.m_body,"_is_on_ground",false); player.m_body.Velocity=velocity;
                            DashPress(controller,player,pad); Require(!controller.Active,"A second dash was allowed in one flight");
                            player.m_body.Position=start+new Vector2(direction*12,0);
                            controller.Restore(midway);
                            Require(audioStops==1,"Snapshot restore left stale audio playing");
                            DashPad(pad,false,true);
                            for(int tick=0;tick<12;tick++) player.UpdateComponents(1f/60f);
                            Require(!controller.Active && player.m_body.Position==start+new Vector2(direction*80,0)
                                && player.m_body.Velocity==new Vector2(direction*3,0),"Restored dash lost distance or half-speed handoff momentum");
                            Require(audioPlays==1,"Restoring a partial dash replayed its attack sound");
                            player.m_body.Position=start;
                            controller.Restore(snapshot); Require(controller.Active,"Mid-flight state restoration lost dash");
                        }
                        Require(player.m_body.Velocity==new Vector2(direction*3.5f,vy),"Teardown left dash velocity locked");
                    }
                    Console.WriteLine("[OK] Air Dash: 80px / 14 ticks with a 2px final step; half-speed exit momentum, 8 native continuation ticks, both directions, ascending/falling, water, native buffer, once per flight and snapshots");
                    DashCollisionRegression(pad);
                    DashRechargeRegression(pad);
                    DashScopeRegression(pad);
                }
            }
            finally { Settings.Current.AirDash=setting; MapPixels.AirDash.Screens.Clear(); foreach(int screen in screens) MapPixels.AirDash.Screens.Add(screen); }
        }
        private static void DashFreePhysicsRegression(PlayerEntity player,WalkInputFixture pad)
        {
            var reference=DashPlayer();
            NativeFlight.Commit(player.m_body,reference.m_body,
                NativeFlight.Get<BehaviourContext>(reference.m_body,"m_behaviourContext"));
            var end=player.m_body.Position;
            int direction=Math.Sign(player.m_body.Velocity.X);
            DashPad(pad,true,true);
            for(int tick=0;tick<8;tick++)
            {
                player.UpdateComponents(1f/60f); reference.UpdateComponents(1f/60f);
                Require(player.m_body.Position==reference.m_body.Position && player.m_body.Velocity==reference.m_body.Velocity,
                    "Post-dash handoff differs from native free physics at tick "+tick);
            }
            Require(direction*(player.m_body.Position.X-end.X)>0 && player.m_body.Position.Y>end.Y
                && player.m_body.Velocity.Y>0,"Dash kept flight locked or lost its outgoing impulse");
        }
        private static void DashCollisionRegression(WalkInputFixture pad)
        {
            foreach(int direction in new[]{-1,1})
            foreach(int offset in new[]{0,70})
            {
                int wall=direction>0?205+offset:171-offset;
                DashWorld(new BoxBlock(new Rectangle(wall,120,1,160)));
                var player=DashPlayer(); player.m_body.Velocity=new Vector2(direction*3.5f,-4);
                using(var controller=new AirDashController(player))
                {
                    DashPress(controller,player,pad); DashPad(pad,false,true);
                    for(int i=0;i<14 && controller.Active;i++) player.UpdateComponents(1f/60f);
                    Require(!controller.Active && player.m_body.Velocity.X==-direction*AirDashController.Speed*PlayerValues.BOUNCE,"Dash wall bounce differs from native half impulse");
                    Require(player.m_body.Velocity.Y==0 && player.m_body.IsKnocked,"Dash wall collision lost native knock state");
                    Require(direction>0?player.m_body.GetHitbox().Right<=wall:player.m_body.GetHitbox().Left>=wall+1,"Dash tunneled through a 1px wall");
                }
            }
            Console.WriteLine("[OK] Air Dash: swept 1px walls, exact native half-impulse bounce and knocked state, both sides");
        }
        private static void DashScopeRegression(WalkInputFixture pad)
        {
            MapPixels.AirDash.Screens.Clear(); Settings.Current.AirDash=false;
            foreach(int jump in new[]{0,1,2})
            {
                DashWorld(new AirDashSurfaceBlock(new Rectangle(150,320,80,40)));
                var player=DashPlayer(); player.m_body.Position=new Vector2(180,294); player.m_body.Velocity=new Vector2(0,.25f);
                NativeFlight.Set(player.m_body,"_is_on_ground",true);
                using(var controller=new AirDashController(player))
                {
                    DashPad(pad,false,false); controller.AfterInput(1f/60f);
                    player.m_body.Position=new Vector2(250,250); player.m_body.Velocity=new Vector2(3.5f,jump==1?-4:4);
                    NativeFlight.Set(player.m_body,"_last_velocity",new Vector2(3.5f,jump==2?-.1f:4));
                    NativeFlight.Set(player.m_body,"_is_on_ground",false);
                    DashPress(controller,player,pad);
                    Require(controller.Active==(jump!=0),"Solid must grant even a tiny jump, not a walk-off");
                }
            }
            DashWorld(new AirDashZoneBlock(new Rectangle(170,170,40,60)));
            var zonePlayer=DashPlayer();
            using(var controller=new AirDashController(zonePlayer))
            {
                zonePlayer.m_body.Position.X=240; DashPress(controller,zonePlayer,pad); Require(!controller.Active,"Zone leaked outside volume");
                zonePlayer.m_body.Position.X=180; DashPress(controller,zonePlayer,pad); Require(controller.Active,"Zone entry in flight not recognized");
                zonePlayer.m_body.Position.X=240; DashPad(pad,false,true);
                for(int i=0;i<14;i++) zonePlayer.UpdateComponents(1f/60f);
                Require(zonePlayer.m_body.Position.X==320,"Dash did not complete after leaving activation Zone");
            }
            Settings.Current.AirDash=true;
            DashWorld();
            var globalPlayer=DashPlayer();
            using(var controller=new AirDashController(globalPlayer))
            {
                DashPress(controller,globalPlayer,pad); Require(controller.Active,"Global setting missing");
                Settings.Current.AirDash=false;
                controller.ExecuteBehaviour(NativeFlight.Get<BehaviourContext>(globalPlayer.m_body,"m_behaviourContext"));
                Require(!controller.Active && globalPlayer.m_body.Velocity==new Vector2(3.5f,-4),"Disabling global dash did not restore velocity");
            }
            Console.WriteLine("[OK] Air Dash scopes: Solid jump-only grant, local Zone activation and exit completion, Screen/global activation, disable cleanup");
        }
        private static void DashRechargeRegression(WalkInputFixture pad)
        {
            foreach(int direction in new[]{-1,1})
            {
                DashWorld();
                var player=DashPlayer(); player.m_body.Velocity=new Vector2(0,4);
                typeof(PlayerEntity).GetField("m_flip",Flags).SetValue(player,direction<0
                    ? Microsoft.Xna.Framework.Graphics.SpriteEffects.FlipHorizontally
                    : Microsoft.Xna.Framework.Graphics.SpriteEffects.None);
                using(var controller=new AirDashController(player))
                {
                    DashPress(controller,player,pad); DashPad(pad,false,true);
                    for(int i=0;i<14;i++) player.UpdateComponents(1f/60f);
                    Require(player.m_body.Position.X==180+direction*80,"Vertical flight dash ignored native facing");
                    NativeFlight.Set(player.m_body,"_is_on_ground",true);
                    controller.AfterInput(1f/60f);
                    Require(!controller.Used,"Landing did not refresh dash");
                    NativeFlight.Set(player.m_body,"_is_on_ground",false);
                    DashPress(controller,player,pad);
                    Require(controller.Active,"Refreshed dash could not activate on the next flight");
                }
            }
            Console.WriteLine("[OK] Air Dash: vertical-flight facing fallback and landing refresh");
        }
    }
}
