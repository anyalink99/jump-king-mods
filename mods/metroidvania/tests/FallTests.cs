using System;
using System.Collections.Generic;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace Metroidvania
{
    internal static partial class MetroidvaniaTests
    {
        private static void Landing(FallLanding fall,float speed,bool grounded=false,long epoch=0,bool teleport=false)
        {fall.Before(King,grounded,speed,PlayerValues.MAX_FALL,epoch,teleport);fall.After(King,true,epoch,teleport);}
        private static void Falls()
        {
            Check(FallRules.Damage(100)==8&&FallRules.Damage(104)==9&&FallRules.Damage(1)==1,"Quest fall damage rounds 8% max HP up");
            var fall=new FallLanding();Landing(fall,0,true);Landing(fall,PlayerValues.MAX_FALL-.01f);Check(fall.Take()==0,"Below terminal speed is safe");
            Landing(fall,PlayerValues.MAX_FALL);ulong id=fall.Take();Check(id==1&&fall.Take()==0,"One hard landing yields one numbered event");
            Landing(fall,PlayerValues.MAX_FALL,true);Check(fall.Take()==0,"Ground support isn't another landing");
            Landing(fall,PlayerValues.MAX_FALL);fall.Suspend();Check(fall.Take()==2,"Pausing after collision preserves its pending landing");
            fall.Before(King,false,PlayerValues.MAX_FALL,PlayerValues.MAX_FALL,0,false);fall.Suspend();fall.After(King,true,0,false);Check(fall.Take()==0,"Paused half-collision can't produce a landing");
            Landing(fall,PlayerValues.MAX_FALL, false,0,true);Check(fall.Take()==0,"Native teleport flag suppresses fall impact");
            Landing(fall,PlayerValues.MAX_FALL);fall.Synchronize(King,1);Check(fall.Take()==0,"Restore drops pending impacts");
            Landing(fall,PlayerValues.MAX_FALL,false,1);Check(fall.Take()==0,"First collision after restore can't fake a fall");
            Landing(fall,PlayerValues.MAX_FALL,false,1);Check(fall.Take()>0,"Real subsequent fall still works after restore");
            fall.Before(new Vector2(2000,0),false,PlayerValues.MAX_FALL,PlayerValues.MAX_FALL,1,false);fall.After(new Vector2(2000,0),true,1,false);Check(fall.Take()==0,"Unannounced large relocation suppresses impact");
            fall.Cut(King,1);Landing(fall,PlayerValues.MAX_FALL,false,1);Check(fall.Take()==0,"Cut discards stale air state");
            fall.Before(King,false,PlayerValues.MAX_FALL,PlayerValues.MAX_FALL,1,false);fall.After(new Vector2(2000,0),true,1,false);Check(fall.Take()==0,"Relocation during collision can't become fall damage");

            // use installed native jump and gravity values, including their actual rounding
            fall=new FallLanding();Landing(fall,0,true);float y=0,v=PlayerValues.JUMP;int ticks=0;
            do{var p=new Vector2(0,y);fall.Before(p,false,v,PlayerValues.MAX_FALL,0,false);y+=v;bool landed=y>=0&&v>0;fall.After(new Vector2(0,landed?0:y),landed,0,false);v=Math.Min(PlayerValues.MAX_FALL,v+PlayerValues.GRAVITY);ticks++;if(landed)break;}while(ticks<200);
            Check(ticks<200&&fall.Take()==0,"Full native jump back to its takeoff height causes no damage");
            var b=new Battle(CombatStats.From(new CharacterProfile()),CrawlerData);Send(b,1,CombatCommandKind.Guard);b.Player.Invulnerable=90;b.Step(King,Floor,1);
            Check(b.Player.Health==92&&b.Player.Action==CombatAction.Hurt,"Fall bypasses guard, armor and enemy-hit invulnerability");
            b.Step(King,Floor,1);Check(b.Player.Health==92,"Duplicate landing cannot deal damage again");
            var saved=b.Capture();b.Step(King,Floor,2);Check(b.Player.Health==84,"Separate landing deals separate damage");b.Restore(saved);b.Step(King,Floor,1);Check(b.Player.Health==92,"Snapshots preserve consumed landing identity");
            b.Player.Health=1;b.Step(King,Floor,2);Check(b.Player.Health==0&&b.Player.Action==CombatAction.Down,"Lethal fall uses normal combat recovery");
            b.Step(King,Floor,3);Check(b.Player.Health==0&&b.Player.Age==1,"Falling while down doesn't restart recovery");
            Step(b,149);Check(b.Player.Health==100,"Recovery after lethal fall completes");
            NativeLanding();
        }
        private sealed class LandingFloor : ICollisionQuery
        {
            private readonly AdvCollisionInfo info=new AdvCollisionInfo();
            public AdvCollisionInfo GetCollisionInfo(Rectangle box){return info;}
            public bool CheckCollision(Rectangle box,out Rectangle overlap,out AdvCollisionInfo value){overlap=Rectangle.Empty;value=info;return box.Bottom>100;}
            public bool CheckCollision(Rectangle box,out Rectangle overlap,out AggregateCollisionInfo value){throw new Exception("Unexpected aggregate collision");}
            public bool IsInWater(Rectangle box){return false;}
        }
        private static void NativeLanding()
        {
            var handlers=new LinkedList<IBlockBehaviour>();var body=new BodyComp(King,18,26);var context=new BehaviourContext(body);var floor=new LandingFloor();
            var resolve=(ResolveYCollisionBehaviour)Activator.CreateInstance(typeof(ResolveYCollisionBehaviour),Members,null,new object[]{floor,handlers},null);
            var move=new UpdateYPositionFromVelocityBehaviour(handlers);var gravity=new ApplyGravityBehaviour(handlers);var fall=new FallLanding();
            Landing(fall,0,true);body.Position=new Vector2(0,-400);body.Velocity=Microsoft.Xna.Framework.Vector2.Zero;fall.Cut(body.Position,0);int ticks=0;ulong impact=0;
            do{fall.Before(body.Position,body.IsOnGround,body.Velocity.Y,PlayerValues.MAX_FALL,0,false);move.ExecuteBehaviour(context);resolve.ExecuteBehaviour(context);fall.After(body.Position,body.IsOnGround,0,false);gravity.ExecuteBehaviour(context);impact=fall.Take();ticks++;}while(impact==0&&ticks<200);
            Check(impact==1&&ticks<200&&body.IsOnGround&&body.Position.Y==King.Y,"Actual native Y collision reports a terminal landing once");
            fall.Before(body.Position,body.IsOnGround,body.Velocity.Y,PlayerValues.MAX_FALL,0,false);move.ExecuteBehaviour(context);resolve.ExecuteBehaviour(context);fall.After(body.Position,body.IsOnGround,0,false);Check(fall.Take()==0,"Actual native support frame cannot repeat fall damage");
        }
    }
}
