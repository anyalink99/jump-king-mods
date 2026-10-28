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

namespace NoWalkOff
{
    internal static partial class Tests
    {
        private sealed class ResumeTestPlayer : PlayerEntity
        {
            // only omit disk-save bookkeeping. Entity.UpdateComponents itself
            // and all component dispatch remain the installed game's methods
            protected override void Update(float delta) { }
            protected override void OnDestroy() { }
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
            // the installed PlayerEntity.SetComponents order, not manual Advance calls
            player.AddComponents(player.m_body,new InputComponent(),brain);
            player.SetSprite(Game1.instance.contentManager.playerSprites.jump_charge);
            return player;
        }
    }
}
