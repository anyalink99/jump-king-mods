using System;
using System.Collections.Generic;
using BehaviorTree;
using EntityComponent;
using EntityComponent.BT;
using JumpKing;
using JumpKing.Controller;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal static partial class Tests
    {
        private sealed class ResumeTestPlayer : PlayerEntity
        {
            // Only omit disk-save bookkeeping. Entity.UpdateComponents itself
            // and all component dispatch remain the installed game's methods.
            protected override void Update(float delta) { }
        }
        private static PlayerEntity ResumePlayer()
        {
            var player=(PlayerEntity)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ResumeTestPlayer));
            typeof(Entity).GetField("m_components",Flags).SetValue(player,new List<Component>());
            player.m_body=new BodyComp(new Vector2(180,294),18,26);
            var ground=new IsOnGround(player);
            typeof(PlayerEntity).GetField("m_is_on_ground_state",Flags).SetValue(player,ground);
            var root=new BTsequencor(); root.AddChild(ground); root.AddChild(new Walk(player));
            var brain=new BehaviorTreeComp(root);
            typeof(PlayerEntity).GetField("m_bt",Flags).SetValue(player,brain);
            // The installed PlayerEntity.SetComponents order, not manual Advance calls.
            player.AddComponents(player.m_body,new InputComponent(),brain);
            player.SetSprite(Game1.instance.contentManager.playerSprites.jump_charge);
            return player;
        }
        private static void ResumeOrderRegression()
        {
            var previous=ControllerManager.instance;
            var pads=(ControllerManager)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ControllerManager));
            var pad=(PadInstance)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(PadInstance));
            typeof(ControllerManager).GetField("m_pads",Flags).SetValue(pads,new List<PadInstance>{pad});
            ControllerManager.instance=pads;
            bool added=MegaBlockFactory.WarpScreens.Add(0);
            try
            {
                foreach(var floor in new IBlock[]{new BoxBlock(new Rectangle(0,320,480,40)),new IceBlock(new Rectangle(0,320,480,40)),new SnowBlock(new Rectangle(0,320,480,40))})
                foreach(int direction in new[]{-1,1}) foreach(int held in new[]{0,-1,1})
                foreach(float fraction in new[]{0f,.25f,.5f,.75f})
                {
                    var screens=Scene(new[]{floor});
                    typeof(LevelManager).GetField("m_screens",Flags).SetValue(null,screens);
                    typeof(LevelManager).GetField("_total_screens",Flags).SetValue(null,1);
                    typeof(Camera).GetField("_current_screen",Flags).SetValue(null,0);
                    var state=new PadState{left=held<0,right=held>0};
                    typeof(PadInstance).GetField("current_state",Flags).SetValue(pad,state);
                    typeof(PadInstance).GetField("last_state",Flags).SetValue(pad,state);
                    var player=ResumePlayer(); player.m_body.Velocity=new Vector2(direction*3.5f,-6);
                    player.m_body.Position+=new Vector2(fraction,0);
                    BodyComp landing; int ticks; string reason;
                    Require(NativeFlight.TryPredict(player.m_body,new FlightWorld(screens,0,0),out landing,out ticks,out reason),reason);
                    var reference=ResumePlayer();
                    NativeFlight.Commit(landing,reference.m_body,NativeFlight.Get<JumpKing.BodyCompBehaviours.BehaviourContext>(reference.m_body,"m_behaviourContext"));
                    // Native landing tick: body has finished; input + BT still run.
                    typeof(Component).GetMethod("LowUpdate",Flags).Invoke(reference.GetComponent<InputComponent>(),new object[]{1f/60f});
                    typeof(Component).GetMethod("LowUpdate",Flags).Invoke(reference.GetComponent<BehaviorTreeComp>(),new object[]{1f/60f});
                    using(var controller=new WarpController(player))
                    {
                        player.UpdateComponents(1f/60f);
                        Require(!player.m_body.Enabled,"Entity-tick warp did not start");
                        for(int i=0;i<30 && !player.m_body.Enabled;i++) player.UpdateComponents(1f/60f);
                        Require(player.m_body.Enabled && player.m_body.Position==landing.Position,"Assembly completion moved body or never unlocked");
                        Vector2 before=player.m_body.Position;
                        player.UpdateComponents(1f/60f); reference.UpdateComponents(1f/60f);
                        Console.WriteLine("[resume] "+floor.GetType().Name+" jump="+direction+" held="+held+" actualStep="+(player.m_body.Position-before)+" nativeStep="+(reference.m_body.Position-before));
                        Require(player.m_body.Position==reference.m_body.Position && player.m_body.Velocity==reference.m_body.Velocity,
                            "Post-assembly body ran before ground controls: "+floor.GetType().Name+" jump="+direction+" held="+held);
                    }
                }
                Console.WriteLine("[OK] Native entity-order handoff: 72 solid/ice/snow cases, integer/quarter-pixel positions, both jump directions, neutral/same/opposite input; no stale-velocity step, ice momentum preserved");
            }
            finally { ControllerManager.instance=previous; if(added) MegaBlockFactory.WarpScreens.Remove(0); }
        }
    }
}
