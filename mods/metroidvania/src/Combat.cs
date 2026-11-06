using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;

namespace Metroidvania
{
    public enum CombatAction { Idle, Light, Heavy, Guard, Hurt, Down }
    public enum CombatCommandKind { Light, Heavy, Guard }
    public struct CombatCommand { public ulong Actor, Serial;public CombatCommandKind Kind;public int Facing; }
    public enum CombatEventKind { Hit, Defeated, Downed, Swing, FallImpact, EnemyCue }
    public struct CombatEvent { public ulong Serial, Actor, Target;public long Tick;public CombatEventKind Kind;public int Amount;public Vector2 Position;public bool Guarded; }
    public sealed class FighterState
    {
        public ulong Id, LastCommand, AttackSerial, HitBy, LastLanding;
        public Vector2 Position;
        public int Health, MaximumHealth, Facing=1, Age, Invulnerable, Cooldown;
        public float Left, Right;
        public CombatAction Action;
        public int Archetype,AttackSlot,BodyWidth=18,BodyHeight=26,AnchorX=9,AnchorY=26;
        public string SpawnKey="";
        public bool Moving,AttackHit;
        public int AnimationTick;
        public FighterState Copy(){return (FighterState)MemberwiseClone();}
        public Rectangle Bounds {get{return new Rectangle((int)Math.Floor(Position.X),(int)Math.Floor(Position.Y),BodyWidth,BodyHeight);}}
    }
    public sealed class BattleSnapshot
    {
        public int Version=4;
        public string Content="";
        public long Tick;
        public ulong EventSerial, NextActor;
        public CombatStats Stats;
        public FighterState Player;
        public FighterState[] Enemies;
    }
    // one local king for now; stable IDs and commands don't depend on an input device or Steam
    public sealed class Battle
    {
        public const int MaximumEnemies=16;
        private readonly List<FighterState> enemies=new List<FighterState>();
        private readonly IList<FighterState> enemyView;
        private readonly CombatEvent[] events=new CombatEvent[256];
        private int eventCount;
        private ulong nextActor=2,eventSerial;
        private readonly EnemyDefinition[] definitions=new EnemyDefinition[33];
        private readonly EnemyDefinition defaultDefinition;
        private readonly string content;
        public long Tick {get;private set;}
        public CombatStats Stats {get;private set;}
        public FighterState Player {get;private set;}
        public IList<FighterState> Enemies {get{return enemyView;}}
        internal List<FighterState> EnemyList {get{return enemies;}}
        public int EventCount {get{return eventCount;}}
        public CombatEvent Event(int index){if(index<0||index>=eventCount)throw new ArgumentOutOfRangeException("index");return events[index];}
        public Battle(CombatStats stats,EnemyDefinition definition):this(stats,new[]{definition}){}
        public Battle(CombatStats stats,EnemyDefinition[] catalogue)
        {
            if(catalogue==null)throw new ArgumentNullException("catalogue");foreach(var definition in catalogue){if(definition==null)continue;if(definition.Index<1||definition.Index>=definitions.Length||definitions[definition.Index]!=null)throw new ArgumentException("Invalid enemy catalogue");if(defaultDefinition==null){defaultDefinition=definition;content=definition.CatalogHash;}else if(content!=definition.CatalogHash)throw new ArgumentException("Mixed content catalogues");definitions[definition.Index]=definition;}
            if(defaultDefinition==null)throw new ArgumentException("Empty enemy catalogue");enemyView=enemies.AsReadOnly();Stats=stats;Player=new FighterState{Id=1,Health=stats.Health,MaximumHealth=stats.Health};
        }
        internal EnemyDefinition Definition(FighterState actor){return definitions[actor.Archetype];}
        public void SetStats(CombatStats stats)
        {int delta=stats.Health-Stats.Health;Stats=stats;Player.MaximumHealth=stats.Health;if(Player.Action!=CombatAction.Down)Player.Health=Math.Max(1,Math.Min(stats.Health,Player.Health+delta));}
        public bool Heal(int amount)
        {if(amount<=0||Player.Action==CombatAction.Down||Player.Health>=Player.MaximumHealth)return false;Player.Health=Math.Min(Player.MaximumHealth,Player.Health+amount);return true;}
        public bool Command(CombatCommand command)
        {
            if(command.Actor!=1||command.Serial==0||command.Serial<=Player.LastCommand||Math.Abs(command.Facing)!=1||command.Kind<CombatCommandKind.Light||command.Kind>CombatCommandKind.Guard)return false;
            Player.LastCommand=command.Serial;
            if(Player.Action!=CombatAction.Idle||command.Kind==CombatCommandKind.Guard&&Stats.Guard==0)return false;
            Player.Facing=command.Facing;Player.Age=0;
            Player.Action=command.Kind==CombatCommandKind.Light?CombatAction.Light:command.Kind==CombatCommandKind.Heavy?CombatAction.Heavy:CombatAction.Guard;
            if(Player.Action==CombatAction.Light||Player.Action==CombatAction.Heavy)Player.AttackSerial++;
            return true;
        }
        public bool Spawn(Vector2 position,float left,float right,string spawnKey="",string type=null)
        {
            if(spawnKey==null||spawnKey.Length>64)return false;
            if(spawnKey!="")foreach(var e in enemies)if(e.SpawnKey==spawnKey)return false;
            var definition=defaultDefinition;if(type!=null){definition=null;foreach(var value in definitions)if(value!=null&&value.Id==type){definition=value;break;}if(definition==null)return false;}var recipe=definition.Recipe;
            int active=0;foreach(var e in enemies)if(e.Health>0)active++;
            if(!Finite(position)||!Finite(left)||!Finite(right)||left>position.X||right<position.X||left>right||right-left>400||active>=MaximumEnemies)return false;
            enemies.RemoveAll(e=>e.Health==0);
            ulong id=nextActor++;enemies.Add(new FighterState{Id=id,Archetype=definition.Index,BodyWidth=recipe.Width,BodyHeight=recipe.Height,AnchorX=recipe.AnchorX,AnchorY=recipe.AnchorY,SpawnKey=spawnKey==""?"training-"+id:spawnKey,Position=position,Health=recipe.Health,MaximumHealth=recipe.Health,Left=left,Right=right});return true;
        }
        public void Step(Vector2 position,Func<Rectangle,bool> blocked,ulong landing=0)
        {
            if(!Finite(position)||blocked==null)throw new ArgumentException("Invalid battle frame");
            Player.Position=position;eventCount=0;Tick++;
            AdvancePlayer();
            if(landing>Player.LastLanding){Player.LastLanding=landing;ApplyFall();}
            if(Player.Action==CombatAction.Light&&Player.Age==Stats.LightStart||Player.Action==CombatAction.Heavy&&Player.Age==Stats.HeavyStart)Emit(CombatEventKind.Swing,1,0,(int)Player.Action,Player.Position);
            foreach(var enemy in enemies) {
                if(enemy.Health==0){var definition=Definition(enemy);if(enemy.Age<definition.DeathTicks)EmitSounds(enemy,definition.Recipe.DeathSounds,++enemy.Age);continue;}
                if(enemy.Invulnerable>0)enemy.Invulnerable--;
                if(enemy.Cooldown>0)enemy.Cooldown--;
                // distant enemies sleep, their attack clock must sleep with them
                if(Math.Abs(enemy.Position.Y-position.Y)>380||Math.Abs(enemy.Position.X-position.X)>640)continue;
                AttackEnemy(enemy,blocked);
                if(enemy.Health==0)continue;
                AdvanceEnemy(enemy,blocked);
            }
        }
        private void AdvancePlayer()
        {
            if(Player.Invulnerable>0)Player.Invulnerable--;
            if(Player.Action==CombatAction.Idle)return;
            Player.Age++;
            int end=Player.Action==CombatAction.Light?Stats.LightStart+Stats.LightActive+Stats.Recovery:Player.Action==CombatAction.Heavy?Stats.HeavyStart+Stats.HeavyActive+Stats.Recovery+Stats.HeavyRecovery:Player.Action==CombatAction.Guard?30:Player.Action==CombatAction.Down?150:20;
            if(Player.Age<end)return;
            if(Player.Action==CombatAction.Down){Player.Health=Player.MaximumHealth;Player.Invulnerable=90;}
            Player.Action=CombatAction.Idle;Player.Age=0;
        }
        private void AttackEnemy(FighterState enemy,Func<Rectangle,bool> blocked)
        {
            bool heavy=Player.Action==CombatAction.Heavy;int start=heavy?Stats.HeavyStart:Stats.LightStart,duration=heavy?Stats.HeavyActive:Stats.LightActive;
            if((Player.Action!=CombatAction.Light&&!heavy)||Player.Age<start||Player.Age>=start+duration||enemy.HitBy==Player.AttackSerial)return;
            var definition=Definition(enemy);var pose=definition.Pose(enemy);bool contact=false;foreach(var box in pose.Hurt)if(CanHit(Player.Bounds,EnemyDefinition.WorldBox(enemy,box),Stats.Reach+(heavy?8:0),Player.Facing,blocked)){contact=true;break;}
            if(!contact)return;
            enemy.HitBy=Player.AttackSerial;int damage=Stats.Damage*(heavy?Stats.HeavyMultiplier:1);enemy.Health=Math.Max(0,enemy.Health-damage);
            enemy.Action=CombatAction.Hurt;enemy.Age=0;
            Emit(CombatEventKind.Hit,1,enemy.Id,damage,enemy.Position);
            EmitSounds(enemy,definition.Recipe.HurtSounds,0);enemy.Moving=false;if(enemy.Health==0)enemy.Action=CombatAction.Down;
            if(enemy.Health==0){EmitSounds(enemy,definition.Recipe.DeathSounds,0);Emit(CombatEventKind.Defeated,1,enemy.Id,25,enemy.Position);}
        }
        private void ApplyFall()
        {
            if(Player.Action==CombatAction.Down)return;
            int damage=FallRules.Damage(Player.MaximumHealth);Player.Health=Math.Max(0,Player.Health-damage);Player.Action=Player.Health==0?CombatAction.Down:CombatAction.Hurt;Player.Age=0;
            Emit(CombatEventKind.FallImpact,0,1,damage,Player.Position);if(Player.Health==0)Emit(CombatEventKind.Downed,0,1,0,Player.Position);
        }
        private bool HurtPlayer(FighterState enemy)
        {
            if(Player.Invulnerable>0)return false;
            bool guard=Player.Action==CombatAction.Guard&&(EnemyDefinition.Anchor(enemy).X-(Player.Position.X+9))*Player.Facing>=0;
            int damage=guard&&Player.Age<=8?0:Math.Max(1,Definition(enemy).Attacks[enemy.AttackSlot].Damage-Stats.Armor-(guard?Stats.Guard+8:0));
            Player.Health=Math.Max(0,Player.Health-damage);Player.Invulnerable=35;
            Emit(CombatEventKind.Hit,enemy.Id,1,damage,Player.Position,guard);
            if(damage==0){enemy.Action=CombatAction.Hurt;enemy.Age=0;EmitSounds(enemy,Definition(enemy).Recipe.HurtSounds,0);return true;}
            Player.Action=Player.Health==0?CombatAction.Down:guard?CombatAction.Guard:CombatAction.Hurt;if(!guard||Player.Health==0)Player.Age=0;
            if(Player.Health==0)Emit(CombatEventKind.Downed,enemy.Id,1,0,Player.Position);
            return true;
        }
        private void EmitSounds(FighterState enemy,SoundMarker[] markers,int age)
        {foreach(var marker in markers)if(marker.Tick==age)Emit(CombatEventKind.EnemyCue,enemy.Id,0,marker.Cue,enemy.Position);}
        private void AdvanceEnemy(FighterState enemy,Func<Rectangle,bool> blocked)
        {
            var definition=Definition(enemy);var recipe=definition.Recipe;
            if(enemy.Action==CombatAction.Hurt){EmitSounds(enemy,recipe.HurtSounds,++enemy.Age);if(enemy.Age>=recipe.Hurt){enemy.Action=CombatAction.Idle;enemy.Age=0;enemy.AnimationTick=0;}return;}
            if(enemy.Action==CombatAction.Light){
                var attack=definition.Attacks[enemy.AttackSlot];int previous=enemy.Age++;float movement=definition.AttackMotion(enemy.AttackSlot,enemy.Age)-definition.AttackMotion(enemy.AttackSlot,previous);MoveEnemy(enemy,movement*enemy.Facing,blocked);EmitSounds(enemy,attack.Sounds,enemy.Age);
                if(enemy.Age>=attack.ActiveTick&&!enemy.AttackHit&&Player.Invulnerable==0&&Player.Action!=CombatAction.Down){var pose=definition.Pose(enemy);var root=EnemyDefinition.Anchor(enemy);foreach(var box in pose.Attack)if(Touches(EnemyDefinition.WorldBox(enemy,box),Player.Bounds,root,blocked)){enemy.AttackHit=HurtPlayer(enemy);break;}}
                if(enemy.Action==CombatAction.Light&&enemy.Age>=definition.ClipTicks(attack.Clip)){enemy.Action=CombatAction.Idle;enemy.Age=0;enemy.AnimationTick=0;enemy.Cooldown=recipe.Cooldown;}return;
            }
            float dx=Player.Position.X+9-EnemyDefinition.Anchor(enemy).X;enemy.Facing=dx<0?-1:1;
            if(Player.Action!=CombatAction.Down&&Math.Abs(dx)<recipe.Range&&Math.Abs(Player.Bounds.Bottom-enemy.Bounds.Bottom)<recipe.HeightRange&&enemy.Cooldown==0){int total=0;foreach(var attack in definition.Attacks)total+=attack.Weight;int choice=(int)(unchecked(enemy.AttackSerial*1103515245UL+enemy.Id)%(ulong)total);enemy.AttackSlot=0;while(choice>=definition.Attacks[enemy.AttackSlot].Weight)choice-=definition.Attacks[enemy.AttackSlot++].Weight;enemy.Action=CombatAction.Light;enemy.Age=0;enemy.AttackSerial++;enemy.AttackHit=false;enemy.Moving=false;EmitSounds(enemy,definition.Attacks[enemy.AttackSlot].Sounds,0);return;}
            bool moved=MoveEnemy(enemy,enemy.Facing*recipe.Walk,blocked);if(enemy.Moving!=moved)enemy.AnimationTick=0;enemy.Moving=moved;EmitSounds(enemy,moved?recipe.WalkSounds:recipe.IdleSounds,enemy.AnimationTick);enemy.AnimationTick=(enemy.AnimationTick+1)%definition.ClipTicks(moved?1:0);
        }
        private static bool MoveEnemy(FighterState enemy,float distance,Func<Rectangle,bool> blocked)
        {
            float next=Math.Max(enemy.Left,Math.Min(enemy.Right,enemy.Position.X+distance));if(next==enemy.Position.X)return false;
            var box=new Rectangle((int)Math.Floor(next),(int)enemy.Position.Y,enemy.BodyWidth,enemy.BodyHeight);int edge=distance>0?box.Right-2:box.Left;
            if(blocked(box)||!blocked(new Rectangle(edge,box.Bottom,2,3)))return false;enemy.Position=new Vector2(next,enemy.Position.Y);return true;
        }
        private static bool Touches(Rectangle from,Rectangle target,Vector2 source,Func<Rectangle,bool> blocked)
        {
            if(from.Width==0||from.Height==0||!from.Intersects(target))return false;
            int y=(Math.Max(from.Top,target.Top)+Math.Min(from.Bottom,target.Bottom))/2,start=(int)source.X,dir=target.Center.X<start?-1:1;
            for(int x=start;x!=target.Center.X;x+=dir)if(blocked(new Rectangle(x,y,1,1)))return false;return true;
        }
        private void Emit(CombatEventKind kind,ulong actor,ulong target,int amount,Vector2 position,bool guarded=false)
        {if(eventCount>=events.Length)throw new InvalidOperationException("Battle event limit");events[eventCount++]=new CombatEvent{Kind=kind,Actor=actor,Target=target,Amount=amount,Position=position,Tick=Tick,Serial=++eventSerial,Guarded=guarded};}
        private static bool CanHit(Rectangle from,Rectangle to,int reach,int facing,Func<Rectangle,bool> blocked)
        {
            if(to.Width==0||to.Height==0)return false;
            Rectangle attack=new Rectangle(facing>0?from.Center.X:from.Center.X-reach,from.Top-3,reach,from.Height+6);
            if(!attack.Intersects(to))return false;
            int y=(Math.Max(from.Top,to.Top)+Math.Min(from.Bottom,to.Bottom))/2;
            int start=from.Center.X,end=to.Center.X,dir=end<start?-1:1;
            for(int x=start;x!=end;x+=dir)if(blocked(new Rectangle(x,y,1,1)))return false;return true;
        }
        public BattleSnapshot Capture()
        {var copy=new FighterState[enemies.Count];for(int i=0;i<copy.Length;i++)copy[i]=enemies[i].Copy();return new BattleSnapshot{Tick=Tick,Content=content,EventSerial=eventSerial,NextActor=nextActor,Stats=Stats,Player=Player.Copy(),Enemies=copy};}
        public static void Validate(BattleSnapshot s)
        {
            if(s==null||s.Version!=4||s.Content==null||s.Content.Length!=64||s.Tick<0||s.Enemies==null||s.Enemies.Length>MaximumEnemies||s.NextActor<2||s.Stats.Health<1||s.Stats.Health>2000||s.Stats.Damage<1||s.Stats.Damage>1000||s.Stats.Reach<1||s.Stats.Reach>128||s.Stats.Recovery<1||s.Stats.Recovery>60||s.Stats.Armor<0||s.Stats.Armor>200||s.Stats.Guard<0||s.Stats.Guard>200||s.Stats.LightStart<1||s.Stats.LightStart>120||s.Stats.LightActive<1||s.Stats.LightActive>120||s.Stats.HeavyStart<1||s.Stats.HeavyStart>120||s.Stats.HeavyActive<1||s.Stats.HeavyActive>120||s.Stats.HeavyRecovery<1||s.Stats.HeavyRecovery>120||s.Stats.HeavyMultiplier<1||s.Stats.HeavyMultiplier>8)throw new InvalidDataException("Invalid battle snapshot");
            var seen=new HashSet<ulong>();ValidateActor(s.Player);if(s.Player.Id!=1||s.Player.Archetype!=0||s.Player.BodyWidth!=18||s.Player.BodyHeight!=26||s.Player.MaximumHealth!=s.Stats.Health)throw new InvalidDataException("Invalid player");seen.Add(1);
            foreach(var e in s.Enemies){ValidateActor(e);if(e.Archetype<1||e.Id<2||e.Id>=s.NextActor||!seen.Add(e.Id)||e.Left>e.Right||e.Right-e.Left>400)throw new InvalidDataException("Invalid enemy");}
        }
        private static void ValidateActor(FighterState a)
        {if(a==null||a.Archetype<0||a.Archetype>32||a.AttackSlot<0||a.AttackSlot>7||a.BodyWidth<1||a.BodyWidth>128||a.BodyHeight<1||a.BodyHeight>128||Math.Abs(a.AnchorX)>128||Math.Abs(a.AnchorY)>128||a.SpawnKey==null||a.SpawnKey.Length>64||a.AnimationTick<0||a.AnimationTick>1800||!Finite(a.Position)||!Finite(a.Left)||!Finite(a.Right)||a.MaximumHealth<1||a.MaximumHealth>2000||a.Health<0||a.Health>a.MaximumHealth||Math.Abs(a.Facing)!=1||a.Age<0||a.Age>1800||a.Invulnerable<0||a.Invulnerable>1000||a.Cooldown<0||a.Cooldown>1000||a.Action<CombatAction.Idle||a.Action>CombatAction.Down)throw new InvalidDataException("Invalid fighter");}
        public void Restore(BattleSnapshot snapshot)
        {
            Validate(snapshot);if(snapshot.Content!=content)throw new InvalidDataException("Different enemy content");foreach(var e in snapshot.Enemies){var d=definitions[e.Archetype];if(d==null||e.AttackSlot>=d.Attacks.Length||e.BodyWidth!=d.Recipe.Width||e.BodyHeight!=d.Recipe.Height||e.AnchorX!=d.Recipe.AnchorX||e.AnchorY!=d.Recipe.AnchorY||e.MaximumHealth!=d.Recipe.Health)throw new InvalidDataException("Different enemy definition");}Player=snapshot.Player.Copy();enemies.Clear();foreach(var e in snapshot.Enemies)enemies.Add(e.Copy());
            Tick=snapshot.Tick;Stats=snapshot.Stats;eventSerial=snapshot.EventSerial;nextActor=snapshot.NextActor;eventCount=0;
        }
        private static bool Finite(float f){return !float.IsNaN(f)&&!float.IsInfinity(f)&&Math.Abs(f)<1000000;}
        private static bool Finite(Vector2 p){return Finite(p.X)&&Finite(p.Y);}
    }
}
