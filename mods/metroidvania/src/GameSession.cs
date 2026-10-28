using System;
using System.Reflection;
using EntityComponent;
using JKRuntime;
using JKRuntime.Gameplay;
using JKRuntime.Presentation;
using JKRuntime.State;
using JKRuntime.UI;
using JKRuntime.World;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Player;
using JumpKing.Util.Tags;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Metroidvania
{
    internal sealed class GameSession : Entity, IDisposable, IForeground
    {
        private static readonly FieldInfo Ground=typeof(BodyComp).GetField("_is_on_ground",BindingFlags.Instance|BindingFlags.NonPublic);
        private static readonly FieldInfo Knocked=typeof(BodyComp).GetField("_knocked",BindingFlags.Instance|BindingFlags.NonPublic);
        private static readonly FieldInfo Flip=typeof(PlayerEntity).GetField("m_flip",BindingFlags.Instance|BindingFlags.NonPublic);
        private readonly CollisionWorld world;
        private readonly BodyPipeline pipeline;
        private readonly PoseSync presentation;
        private readonly Func<Rectangle,bool> blocked;
        private readonly FallLanding fall=new FallLanding();
        private readonly QuestAudio audio;
        private readonly EnemyArt enemyArt;
        private readonly IDisposable audioBinding;
        private readonly HitFlash[] flashes=new HitFlash[24];
        private int flashHead,revision;
        private ulong cues;
        private ulong commandSerial;
        private float accumulator,idleSave;
        private Vector2 pendingImpulse,lastPosition;
        private bool disposed;
        internal readonly PlayerEntity Player;
        internal readonly Battle Battle;
        internal byte[] VisualState {get;private set;}
        private struct HitFlash { internal Vector2 Position;internal long Until;internal bool Guard; }
        private sealed class PoseSync : Component
        {
            private readonly GameSession owner;
            internal PoseSync(GameSession value){owner=value;}
            protected override void LateUpdate(float delta){if(!owner.disposed)owner.UpdateVisual();}
        }
        private sealed class Impulse : IBodyCompBehaviour
        {
            private readonly GameSession owner;
            internal Impulse(GameSession session){owner=session;}
            public bool ExecuteBehaviour(BehaviourContext context)
            {
                if(owner.pendingImpulse!=Vector2.Zero&&owner.CanSimulate){context.BodyComp.Velocity+=owner.pendingImpulse;Ground.SetValue(context.BodyComp,false);Knocked.SetValue(context.BodyComp,true);owner.pendingImpulse=Vector2.Zero;}return true;
            }
        }
        private sealed class FallObserver : IBodyCompBehaviour
        {
            private readonly GameSession owner;
            private readonly bool before;
            internal FallObserver(GameSession value,bool first){owner=value;before=first;}
            public bool ExecuteBehaviour(BehaviourContext context)
            {
                var body=context.BodyComp;long epoch=GameState.Snapshots.RestoreEpoch;owner.fall.Synchronize(body.Position,epoch);
                if(!owner.CanSimulate){owner.fall.Suspend();return true;}
                bool teleported=context.ContainsKey(HandlePlayerTeleportBehaviour.TeleportedPlayerFlag);
                if(before)owner.fall.Before(body.Position,body.IsOnGround,body.Velocity.Y,PlayerValues.MAX_FALL,epoch,teleported);
                else owner.fall.After(body.Position,body.IsOnGround,epoch,teleported);
                return true;
            }
        }
        internal static void ValidateNative()
        {if(Ground==null||Ground.FieldType!=typeof(bool)||Knocked==null||Knocked.FieldType!=typeof(bool)||Flip==null||Flip.FieldType!=typeof(SpriteEffects))throw new NotSupportedException("Native combat body contract unavailable");}
        internal GameSession(PlayerEntity player,CollisionWorld collision,EncounterFile encounters,QuestAudio sounds,EnemyArt enemies)
        {
            Player=player;world=collision;audio=sounds;enemyArt=enemies;blocked=world.Blocked;Battle=new Battle(CombatStats.From(ModEntry.Store.Current),enemies.Prepared());revision=ModEntry.Store.Revision;lastPosition=player.m_body.Position;
            pipeline=new BodyPipeline(player.m_body,true,0,"metroidvania");
            try {
                pipeline.Register(BodyPhase.BeforeWind,new Impulse(this));
                pipeline.Register(BodyPhase.AfterXCollision,new FallObserver(this,true));
                pipeline.Register(BodyPhase.AfterYCollision,new FallObserver(this,false));
                audioBinding=audio.BindToAttempt();
                foreach(var spawn in encounters.Enemies){var recipe=ContentCatalog.Enemy(spawn.Type??ContentCatalog.DefaultEnemy);var p=new Vector2(spawn.X,spawn.Y-(spawn.Screen-1)*360);if(!world.Placement(p,recipe.Width,recipe.Height))throw new InvalidOperationException("Enemy needs flat support: "+spawn.Id);if(!Battle.Spawn(p,Math.Max(0,p.X-spawn.Patrol),Math.Min(480-recipe.Width,p.X+spawn.Patrol),spawn.Id,recipe.Id))throw new InvalidOperationException("Invalid enemy patrol: "+spawn.Id);}
                presentation=new PoseSync(this);player.AddComponents(presentation);
                UpdateVisual();
            }catch{if(presentation!=null)presentation.Enabled=false;if(audioBinding!=null)audioBinding.Dispose();pipeline.Dispose();Destroy();throw;}
        }
        internal bool CanSimulate {get{
            if(disposed||!Player.IsAlive||!Player.m_body.Enabled||NativePause.IsPaused||UIApi.IsOpen||WorldControl.CurrentRole!=WorldRole.Local||PlayerControl.Owner(Player.m_body)!=null)return false;
            var bounds=Player.m_body.GetHitbox();return bounds.Width==18&&bounds.Height==26;
        }}
        internal bool CanAct {get{return CanSimulate;}}
        internal void Command(CombatCommandKind kind)
        {
            if(!CanAct)return;
            int facing=(SpriteEffects)Flip.GetValue(Player)==SpriteEffects.None?1:-1;
            Battle.Command(new CombatCommand{Actor=1,Serial=++commandSerial,Kind=kind,Facing=facing});UpdateVisual();
        }
        internal void RefreshStats(){Battle.SetStats(CombatStats.From(ModEntry.Store.Current));revision=ModEntry.Store.Revision;UpdateVisual();}
        protected override void Update(float delta)
        {
            fall.Synchronize(Player.m_body.Position,GameState.Snapshots.RestoreEpoch);
            if(!CanSimulate){fall.Suspend();accumulator=0;pendingImpulse=Vector2.Zero;return;}
            if(revision!=ModEntry.Store.Revision)RefreshStats();
            var position=Player.m_body.Position;
            // drop unfinished swings after a teleport, not after an ordinary fast jump
            if(Vector2.DistanceSquared(position,lastPosition)>128*128){fall.Cut(position,GameState.Snapshots.RestoreEpoch);if(Battle.Player.Action!=CombatAction.Down){Battle.Player.Action=CombatAction.Idle;Battle.Player.Age=0;}}
            lastPosition=position;
            accumulator+=Math.Max(0,Math.Min(.067f,delta));int steps=0;
            cues=0;while(accumulator>=1f/60&&steps++<4){accumulator-=1f/60;Battle.Step(position,blocked,fall.Take());ConsumeEvents();}audio.Play(cues);
            if(ModEntry.Store.Dirty&&Battle.Player.Action==CombatAction.Idle&&!HasNearbyEnemy()){
                idleSave+=delta;if(idleSave>=1){ModEntry.Store.Save();idleSave=ModEntry.Store.Error==""?0:-9;}
            }else idleSave=0;
        }
        private void ConsumeEvents()
        {
            for(int i=0;i<Battle.EventCount;i++){
                var e=Battle.Event(i);
                cues=CombatAudio.Add(cues,CombatAudio.Select(e,ModEntry.Store.Current.Weapon,Battle.Player.Action));
                if(e.Kind==CombatEventKind.Defeated){ModEntry.Store.Edit(p=>{p.Victory();return true;});continue;}
                if(e.Kind!=CombatEventKind.Hit&&e.Kind!=CombatEventKind.FallImpact)continue;
                flashes[flashHead++%flashes.Length]=new HitFlash{Position=e.Position+new Vector2(10,8),Until=Battle.Tick+10,Guard=e.Amount==0};
                if(e.Kind==CombatEventKind.Hit&&e.Target==1&&e.Amount>0){
                    FighterState enemy=null;foreach(var value in Battle.EnemyList)if(value.Id==e.Actor){enemy=value;break;}
                    if(enemy!=null)pendingImpulse+=new Vector2(Player.m_body.Position.X<enemy.Position.X?-3:3,-2);
                }
            }
        }
        internal bool HasNearbyEnemy(){foreach(var e in Battle.EnemyList)if(e.Health>0&&Vector2.DistanceSquared(e.Position,Player.m_body.Position)<180*180)return true;return false;}
        internal bool Rest()
        {if(HasNearbyEnemy()||Battle.Player.Action==CombatAction.Down)return false;Battle.Heal(Battle.Player.MaximumHealth);ModEntry.Store.Save();return true;}
        internal bool SpawnTraining()
        {
            if(!Player.IsAlive||!Player.m_body.IsOnGround||WorldControl.CurrentRole!=WorldRole.Local)return false;
            var king=Player.m_body.GetHitbox();
            foreach(int sign in new[]{1,-1})for(int distance=60;distance<=120;distance+=20){
                var recipe=enemyArt.Definition.Recipe;var p=new Vector2(king.X+sign*distance,king.Bottom-recipe.Height);
                if(!world.Placement(p,recipe.Width,recipe.Height))continue;
                if(Battle.Spawn(p,Math.Max(0,p.X-48),Math.Min(480-recipe.Width,p.X+48))){ModEntry.Status="";return true;}
            }
            ModEntry.Status="No clear platform.";return false;
        }
        private void UpdateVisual()
        {
            var bounds=Player.m_body.GetHitbox();
            if(bounds.Width!=18||bounds.Height!=26||PlayerControl.Owner(Player.m_body)!=null){if(VisualState!=null){VisualState=null;ActorVisuals.Changed(Player);}return;}
            var p=ModEntry.Store.Current;var held=Catalog.Find(p.Weapon);var guard=Catalog.Find(p.Shield);var coat=Catalog.Find(p.Body);byte weapon=(byte)(held==null?0:held.VisualId),shield=(byte)(guard==null?0:guard.VisualId),action=(byte)Battle.Player.Action,age=(byte)Math.Min(255,Battle.Player.Age),body=(byte)(coat==null?0:coat.VisualId),pose=EquipmentArt.Pose(PlayerAppearance.BaseSprite(Player));
            if(VisualState!=null&&VisualState[0]==weapon&&VisualState[1]==shield&&VisualState[2]==action&&VisualState[3]==age&&VisualState[4]==body&&VisualState[5]==pose)return;
            VisualState=new[]{weapon,shield,action,age,body,pose};ActorVisuals.Changed(Player);
        }
        public override void Draw()
        {
            if(disposed)return;
            foreach(var enemy in Battle.EnemyList){
                var definition=enemyArt.For(enemy);if(enemy.Health==0&&enemy.Age>=definition.DeathTicks)continue;
                var anchor=Camera.TransformVector2(EnemyDefinition.Anchor(enemy));if(anchor.Y< -80||anchor.Y>440)continue;
                float fade=enemy.Health==0?Math.Min(1,(definition.DeathTicks-enemy.Age)/30f):1;enemyArt.Draw(enemy,anchor,Color.White*fade);
                if(enemy.Health>0){var bounds=Camera.TransformRect(enemy.Bounds);Pixel(new Rectangle(bounds.X,bounds.Y-6,bounds.Width,2),new Color(51,42,43));Pixel(new Rectangle(bounds.X,bounds.Y-6,bounds.Width*enemy.Health/enemy.MaximumHealth,2),new Color(214,95,92));}
            }
            int direction=(SpriteEffects)Flip.GetValue(Player)==SpriteEffects.None?1:-1;
            EquipmentVisual.DrawEquipment(Camera.TransformVector2(Player.m_body.Position+new Vector2(9,26)),direction,VisualState,Color.White);
            for(int i=0;i<flashes.Length;i++)if(flashes[i].Until>Battle.Tick){var p=Camera.TransformVector2(flashes[i].Position);int size=(int)(flashes[i].Until-Battle.Tick);Color color=flashes[i].Guard?new Color(112,219,232):new Color(250,218,123);Pixel(new Rectangle((int)p.X-size,(int)p.Y,2*size,2),color);Pixel(new Rectangle((int)p.X,(int)p.Y-size,2,2*size),color);}
        }
        public void ForegroundDraw()
        {
            if(disposed||UIApi.IsOpen)return;
            Pixel(new Rectangle(12,12,106,7),new Color(29,24,31));Pixel(new Rectangle(13,13,104*Battle.Player.Health/Battle.Player.MaximumHealth,5),new Color(198,83,90));
            string text=Battle.Player.Action==CombatAction.Down?"Recovering...":"Lv "+ModEntry.Store.Current.Level+"  HP "+Battle.Player.Health+"/"+Battle.Player.MaximumHealth;
            UiTheme.TextLine(text,new Vector2(12,24),Color.White,true);
        }
        internal static void Pixel(Rectangle rect,Color color){Game1.spriteBatch.Draw(Game1.instance.contentManager.Pixel.texture,rect,color);}
        public void Dispose(){if(disposed)return;disposed=true;pendingImpulse=Vector2.Zero;if(presentation!=null)presentation.Enabled=false;audioBinding.Dispose();pipeline.Dispose();ModEntry.Store.Save();if(IsAlive)Destroy();ActorVisuals.Changed(Player);}
    }
    internal sealed class EquipmentVisual : IActorVisual
    {
        private IDisposable art;
        public Sprite Compose(Sprite outfit,Sprite chargeOutfit,byte[] state)
        {Validate(state);if(art==null)art=EquipmentArt.Acquire();return ActorVisuals.Layers(outfit,new EquipmentSprite((byte[])state.Clone()));}
        public void Dispose(){if(art!=null){art.Dispose();art=null;}}
        internal static void Validate(byte[] state){if(state==null||state.Length!=6||state[0]>0&&Catalog.Visual(GearSlot.Weapon,state[0])==null||state[1]>0&&Catalog.Visual(GearSlot.Shield,state[1])==null||state[2]>(byte)CombatAction.Down||state[4]>0&&Catalog.Visual(GearSlot.Body,state[4])==null||state[5]>12)throw new InvalidOperationException("Invalid Metroidvania visual state");}
        private sealed class EquipmentSprite : Sprite
        {
            private readonly byte[] state;
            internal EquipmentSprite(byte[] value){state=value;}
            public override void Draw(Vector2 anchor,SpriteEffects facing=SpriteEffects.None){DrawEquipment(anchor,facing==SpriteEffects.None?1:-1,state,GetColor());}
        }
        internal static void DrawEquipment(Vector2 anchor,int facing,byte[] state,Color tint)
        {EquipmentArt.Draw(anchor,facing,state,tint);}
    }
}
