using System;
using System.Collections.Generic;
using System.Linq;
using BehaviorTree;
using EntityComponent;
using EntityComponent.BT;
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
        private static void WarpButtons(WalkInputFixture pad,bool held,int direction=0)
        {
            var current=(PadState)typeof(PadInstance).GetField("current_state",Flags).GetValue(pad.Pad);
            typeof(PadInstance).GetField("last_state",Flags).SetValue(pad.Pad,current);
            typeof(PadInstance).GetField("current_state",Flags).SetValue(pad.Pad,
                new PadState { jump=held,left=direction<0,right=direction>0 });
        }
        private static PlayerEntity WarpPlayablePlayer(bool splat)
        {
            var player=ResumePlayer();
            var components=(List<Component>)typeof(Entity).GetField("m_components",Flags).GetValue(player);
            components.Remove(player.GetComponent<BehaviorTreeComp>());
            // Use the installed full tree: FailState precedes JumpState, and
            // the native grounded evaluator owns when a buffered charge starts.
            var brain=(BehaviorTreeComp)typeof(PlayerEntity).GetMethod("MakeBT",Flags).Invoke(player,null);
            typeof(PlayerEntity).GetField("m_bt",Flags).SetValue(player,brain);
            player.AddComponents(brain);
            player.m_body.Position=new Vector2(180,splat?120:294);
            player.m_body.Velocity=new Vector2(0,splat?PlayerValues.MAX_FALL:-6);
            NativeFlight.Set(player.m_body,"_is_on_ground",false);
            // Sound-only contact supplies no-op particle callbacks for the
            // headless fixture, without replacing native jump/landing logic.
            var lookup=NativeFlight.Get<Dictionary<Type,IBlockBehaviour>>(player.m_body,"m_blockBehaviourLookup");
            var contact=(IBlockBehaviour)Activator.CreateInstance(lookup[typeof(WaterBlock)].GetType());
            contact.IsPlayerOnBlock=true; lookup.Add(typeof(WarpSurfaceBlock),contact);
            player.RegisterLandParticleSpawningAction<WarpSurfaceBlock>(delegate { });
            player.RegisterJumpParticleSpawningAction<WarpSurfaceBlock>(delegate { });
            return player;
        }
        private static void WarpBufferRegression()
        {
            bool added=MegaBlockFactory.WarpScreens.Add(0);
            var previousSplat=PlayerEntity.OnSplatCall;
            try
            {
                using(var pad=new WalkInputFixture())
                {
                    foreach(bool splat in new[]{false,true})
                    foreach(int pressAt in new[]{-2,-1,0,2,10})
                    foreach(bool releaseEarly in new[]{false,true})
                    {
                        if(pressAt<0 && releaseEarly) continue;
                        DashWorld(new BoxBlock(new Rectangle(0,320,480,40)));
                        var player=WarpPlayablePlayer(splat);
                        var jump=(JumpState)typeof(PlayerEntity).GetField("m_jump_state",Flags).GetValue(player);
                        var fail=(FailState)typeof(PlayerEntity).GetField("m_fail_state",Flags).GetValue(player);
                        int splats=0; PlayerEntity.OnSplatCall=delegate { splats++; };
                        var splatSound=new CountingSound(); player.RegisterFailSound<WarpSurfaceBlock>(splatSound);
                        WarpButtons(pad,false);
                        typeof(Component).GetMethod("LowUpdate",Flags).Invoke(player.GetComponent<InputComponent>(),new object[]{1f/60f});
                        if(pressAt==-2)
                        {
                            // A maximum auto-jump can depart with its already
                            // consumed button still held: that is not a buffer.
                            WarpButtons(pad,true); WarpButtons(pad,true);
                            player.GetComponent<InputComponent>().TryConsumeJump();
                        }
                        using(var controller=new WarpController(player))
                        {
                            bool held=false;
                            for(int tick=0;tick<40;tick++)
                            {
                                held=pressAt==-2 || (pressAt>=0 && tick>=pressAt && !(releaseEarly && tick>=pressAt+2));
                                WarpButtons(pad,held,-1);
                                player.UpdateComponents(1f/60f);
                                if(tick==0) Require(!player.m_body.Enabled,"Buffer fixture never started a warp");
                                if(player.m_body.Enabled) break;
                                Require(player.GetComponent<InputComponent>().Enabled,"Warp suspended native input polling");
                                Require(NativeFlight.Get<float>(jump,"m_timer")==0,"Warp charged before assembly ended");
                                var state=controller.Capture();
                                var plan=state.GetType().GetField("Plan",Flags).GetValue(state);
                                var landing=(BodyComp)plan.GetType().GetField("Landing",Flags).GetValue(plan);
                                if(landing!=null)
                                {
                                    Require((landing.LastVelocity.Y==PlayerValues.MAX_FALL)==splat,"Splat fixture has the wrong native impact speed");
                                    var image=(WarpImage)plan.GetType().GetField("Image",Flags).GetValue(plan);
                                    var expected=splat?Game1.instance.contentManager.playerSprites.splat:Game1.instance.contentManager.playerSprites.idle;
                                    Require(image.ArrivalSprite==expected && image.ArrivalLayers.Length==2,"Arrival pixels use the wrong native layered pose");
                                }
                            }
                            Require(player.m_body.Enabled,"Warp never finished assembly");
                            bool buffered=pressAt>=0 && !releaseEarly;
                            if(splat)
                            {
                                Require(fail.IsRunning() && NativeFlight.Get<float>(jump,"m_timer")==0,"Buffer bypassed native splat recovery");
                                Require(typeof(PlayerEntity).GetField("m_sprite",Flags).GetValue(player)==Game1.instance.contentManager.playerSprites.splat,"Finished splat warp showed idle/charge");
                                Require(splats==1 && splatSound.Plays==1,"Splat event/sound missing or duplicated");
                                // Keep the button held without a second edge. A
                                // native splat must recover before charge begins.
                                for(int tick=0;tick<100 && !jump.IsRunning();tick++)
                                {
                                    WarpButtons(pad,buffered,-1); player.UpdateComponents(1f/60f);
                                    if(!buffered && !fail.IsRunning()) break;
                                }
                            }
                            Require(jump.IsRunning()==buffered,"Warp lost a held buffer or retained a released one");
                            if(buffered)
                            {
                                Require(Math.Abs(NativeFlight.Get<float>(jump,"m_timer")-1f/60f)<.00001f,"Buffered charge inherited animation/recovery time");
                                WarpButtons(pad,false,-1); player.UpdateComponents(1f/60f);
                                Require(player.m_body.Velocity.Y<0 && player.m_body.Velocity.X<0,"Buffered jump failed to launch in the held direction");
                            }
                            Require(splats==(splat?1:0),"Warp changed native splat count");
                        }
                    }
                    WarpBufferCleanup(pad);
                    Console.WriteLine("[OK] Warp native buffer: departure/dissolve/assembly presses, release cancellation, full native tree, direction, no precharge; layered splat arrival, native recovery and one splat event/sound");
                }
            }
            finally { PlayerEntity.OnSplatCall=previousSplat; if(added) MegaBlockFactory.WarpScreens.Remove(0); }
        }
        private static void WarpBufferCleanup(WalkInputFixture pad)
        {
            foreach(bool transferred in new[]{false,true})
            foreach(bool enabled in new[]{false,true})
            {
                DashWorld(new BoxBlock(new Rectangle(0,320,480,40)));
                var player=WarpPlayablePlayer(true);
                var input=player.GetComponent<InputComponent>(); input.Enabled=enabled;
                using(var controller=new WarpController(player))
                {
                    typeof(BodyComp).GetMethod("UpdateInternal",Flags).Invoke(player.m_body,new object[]{1f/60f});
                    var snapshot=controller.Capture();
                    // A snapshot restore must not consume a token owned by
                    // native input, or re-enable an externally disabled input.
                    WarpButtons(pad,false); WarpButtons(pad,true);
                    typeof(Component).GetMethod("LowUpdate",Flags).Invoke(input,new object[]{1f/60f});
                    var buffered=NativeFlight.Get<bool>(input,"_can_jump");
                    controller.Restore(snapshot);
                    controller.Advance(0);
                    Require(NativeFlight.Get<bool>(input,"_can_jump")==buffered && input.Enabled==enabled,"Restore/pause mutated native input ownership");
                    if(transferred) AdvanceFor(controller,MatrixPixels.Transfer+.005f);
                }
                Require(input.Enabled==enabled,"Warp cleanup changed external input enabled state");
                if(enabled) Require(input.TryConsumeJump(),"Warp cancellation discarded held buffer");
                if(transferred) Require(typeof(PlayerEntity).GetField("m_sprite",Flags).GetValue(player)==Game1.instance.contentManager.playerSprites.splat,"Post-transfer cancellation lost splat pose");
            }
        }
    }
}
