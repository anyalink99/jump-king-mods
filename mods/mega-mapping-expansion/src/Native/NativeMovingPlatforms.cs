using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using JKRuntime;
using JKRuntime.Gameplay;
using JKRuntime.State;
using JKRuntime.World;
using JumpKing;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using MegaMappingExpansion.Construction;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion
{
    internal sealed class MovingPlatformPacket
    {
        [WorldField] internal string Identity;
        [WorldField] internal string Rider;
        [WorldField] internal string[] States;
    }
    internal sealed class NativePlatformPart:IBlock
    {
        internal NativePlatform Platform;
        internal Rectangle Origin,Bounds;
        public Rectangle GetRect(){return Bounds;}
        public BlockCollisionType Intersects(Rectangle box,out Rectangle intersection)
        {
            intersection=Rectangle.Empty;
            if(ReferenceEquals(NativeMovingPlatforms.Ignored,Platform.Motion)||!box.Intersects(Bounds)||NativeMovingPlatforms.Launching!=null&&NativeMovingPlatforms.Launching.Contains(this))return BlockCollisionType.NoCollision;
            if(Platform.Profile.Collision=="oneWay")
            {
                var actor=NativeMovingPlatforms.QueryBody;
                if(actor==null||actor.Velocity.Y<0||NativeMovingPlatforms.QueryStart.Bottom>Bounds.Top+1)return BlockCollisionType.NoCollision;
            }
            intersection=Rectangle.Intersect(box,Bounds);return BlockCollisionType.Collision_Blocking;
        }
    }
    internal sealed class NativePlatform
    {
        internal PlatformDefinition Definition;
        internal PlatformProfile Profile;
        internal PlatformMotionPlayer Motion;
        internal readonly List<NativePlatformPart> Parts=new List<NativePlatformPart>();
        internal void Position()
        {
            var offset=Motion.Position;int x=(int)Math.Round(offset.X,MidpointRounding.AwayFromZero),y=(int)Math.Round(offset.Y,MidpointRounding.AwayFromZero);
            foreach(var part in Parts)part.Bounds=new Rectangle(part.Origin.X+x,part.Origin.Y+y,part.Origin.Width,part.Origin.Height);
        }
    }
    // native collision still resolves walking and landing; this adapter owns moving contacts
    internal sealed partial class NativeMovingPlatforms:IDisposable,IStateParticipant
    {
        internal static NativeMovingPlatforms Current;
        [ThreadStatic] internal static BodyComp QueryBody;
        [ThreadStatic] internal static Rectangle QueryStart;
        [ThreadStatic] internal static PlatformMotionPlayer Ignored;
        [ThreadStatic] internal static HashSet<NativePlatformPart> Launching;
        private static readonly FieldInfo Screens=typeof(LevelManager).GetField("m_screens",OwnedPatches.Members);
        private static readonly FieldInfo Hitboxes=typeof(LevelScreen).GetField("m_hitboxes",OwnedPatches.Members);
        private static readonly FieldInfo Ground=typeof(BodyComp).GetField("_is_on_ground",OwnedPatches.Members);
        private readonly RuntimeScope resources=new RuntimeScope();
        private readonly List<NativePlatform> platforms=new List<NativePlatform>();
        private readonly Dictionary<string,PlatformMotionPlayer> clocks=new Dictionary<string,PlatformMotionPlayer>(StringComparer.Ordinal);
        private readonly Dictionary<LevelScreen,IBlock[]> installed=new Dictionary<LevelScreen,IBlock[]>();
        private readonly Dictionary<LevelScreen,IBlock[]> original=new Dictionary<LevelScreen,IBlock[]>();
        private readonly List<WeakReference> actors=new List<WeakReference>();
        private readonly MovingPlatformPlan plan;
        private readonly string identity;
        private BodyComp master;
        private string rider="";
        private bool disposed,completed;
        private int emitted;
        private RuntimeScope activation;
        private ScenePersistence persistence;
        private int saveRevision;
        internal readonly List<string> Diagnostics=new List<string>();
        internal NativeMovingPlatforms(MovingPlatformPlan definition,string root,bool graphics=true,bool persist=true)
        {
            plan=definition;
            try
            {
                if(Screens==null||Hitboxes==null||Ground==null)throw new NotSupportedException("Native moving-platform collision contracts are unavailable");
                foreach(var d in plan.Platforms.OrderBy(p=>p.Id,StringComparer.Ordinal))
                {
                    var profile=plan.Profiles[d.Profile];string key=d.Group==""?d.Id:"group:"+d.Group;PlatformMotionPlayer motion;
                    if(!clocks.TryGetValue(key,out motion)){motion=new PlatformMotionPlayer(plan.Motions[profile.Motion]);clocks.Add(key,motion);}
                    var platform=new NativePlatform{Definition=d,Profile=profile,Motion=motion};
                    foreach(var part in d.Parts){var rect=new Rectangle(part.X,part.Y-(d.Screen-1)*360,part.Width,part.Height);platform.Parts.Add(new NativePlatformPart{Platform=platform,Origin=rect,Bounds=rect});}
                    platform.Position();platforms.Add(platform);
                }
                string geometry=string.Join(";",platforms.Select(p=>p.Definition.Id+":"+p.Definition.Screen+":"+string.Join("/",p.Parts.Select(b=>b.Origin.ToString()))+":"+string.Join("/",p.Motion.Definition.Points.Select(w=>w.Position.X.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+w.Position.Y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)))));
                using(var hash=SHA256.Create())identity=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(plan.Identity+"\n"+geometry))).Replace("-","");
                if(graphics&&platforms.Count>0)
                {
                    PrepareGraphics(root);
                    using(var hash=SHA256.Create())identity=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity+GraphicsIdentity))).Replace("-","");
                }
                if(graphics&&persist)
                {
                    Dictionary<string,string> saved;PlatformSaveCoordinator.Load(Path.GetFullPath(root),ScenePersistence.NativeDirectory,out persistence,out saved,out saveRevision);
                    if(saved.Count>0)
                    {
                        string savedIdentity;
                        if(saved.TryGetValue("identity",out savedIdentity)&&savedIdentity==identity)
                        {
                            if(saved.Count!=clocks.Count+3||!saved.ContainsKey("rider"))throw new InvalidDataException("Incomplete platform save");
                            Restore(new MovingPlatformPacket{Identity=identity,Rider=saved["rider"],States=Enumerable.Range(0,clocks.Count+1).Select(i=>saved[i.ToString("D3")]).ToArray()});
                        }
                        else Diagnostics.Add("Saved platform state belongs to an older map revision; starting at the authored phase");
                    }
                }
            }
            catch{resources.Dispose();throw;}
        }
        internal IDisposable Activate(PlayerEntity player,bool state=true)
        {
            if(Current!=null||activation!=null||player==null)throw new InvalidOperationException("Moving platforms require one active world");
            var scope=new RuntimeScope();activation=scope;Current=this;master=player.m_body;
            scope.Defer(delegate{Detach();master=null;if(Current==this)Current=null;activation=null;});
            try
            {
                Attach();var hooks=scope.Own(new OwnedPatches("mega-mapping-expansion.platforms"));
                hooks.Add(typeof(BodyComp).GetMethod("Update",OwnedPatches.Members),prefix:Method("BeforeBody"),finalizer:Method("AfterBody"));
                hooks.Add(typeof(LevelScreen).GetMethod("DrawForeground",Type.EmptyTypes),prefix:Method("Draw"));
                if(state)
                {
                    scope.Own(GameState.Snapshots.Register(this));
                    scope.Own(WorldRegistry.Shared.Bind<MovingPlatformPacket>(Id,()=>Packet(),s=>Restore(s),s=>Validate(s)));
                }
                foreach(var pair in clocks){var clock=pair.Value;clock.Arrived=point=>{foreach(var p in platforms.Where(p=>p.Motion==clock))Event(p,"arrived",point);};}
                Publish();
                return scope;
            }
            catch{scope.Dispose();throw;}
        }
        private static MethodInfo Method(string name){return typeof(NativeMovingPlatforms).GetMethod(name,OwnedPatches.Members);}
        private sealed class QueryFrame{internal BodyComp Body;internal Rectangle Start;internal HashSet<NativePlatformPart> Launching;}
        private static void BeforeBody(BodyComp __instance,float p_delta,out QueryFrame __state)
        {
            __state=new QueryFrame{Body=QueryBody,Start=QueryStart,Launching=Launching};QueryBody=__instance;QueryStart=__instance.GetHitbox();Launching=null;
            var owner=Current;if(owner!=null&&__instance.gameObject is PlayerEntity)owner.ObserveBody(__instance);
            if(owner!=null&&!owner.completed&&!NativePause.IsPaused&&!JKRuntime.UI.UIApi.IsOpen&&!WorldControl.Following)owner.TransferMomentum(__instance);
            HashSet<NativePlatformPart> departing=null;
            if(owner!=null&&__instance.Velocity.Y<0)
                departing=new HashSet<NativePlatformPart>(owner.platforms.SelectMany(p=>p.Parts).Where(p=>QueryStart.Right>p.Bounds.Left&&QueryStart.Left<p.Bounds.Right&&Math.Abs(QueryStart.Bottom-p.Bounds.Top)<=1));
            if(owner!=null&&owner.master==__instance&&!owner.completed&&!NativePause.IsPaused&&!JKRuntime.UI.UIApi.IsOpen&&!WorldControl.Following)owner.Tick(__instance,p_delta);
            QueryStart=__instance.GetHitbox();
            // rising decks mustn't trigger the native early-overlap escape on launch
            if(departing!=null)departing.RemoveWhere(p=>QueryStart.Bottom+__instance.Velocity.Y>p.Bounds.Top);
            Launching=departing;
        }
        private static void AfterBody(QueryFrame __state,Exception __exception)
        {
            if(__state==null)return;var owner=Current;
            try
            {
                if(__exception==null&&owner!=null&&QueryBody!=null&&!WorldControl.Following)
                {
                    owner.StabilizeSupport(QueryBody);
                    if(QueryBody==owner.master)owner.ObserveRider(QueryBody);
                }
            }
            finally{QueryBody=__state.Body;QueryStart=__state.Start;Launching=__state.Launching;}
        }
        private void StabilizeSupport(BodyComp body)
        {
            if(body.Velocity.Y<0)return;
            var box=body.GetHitbox();var support=platforms.SelectMany(p=>p.Parts).FirstOrDefault(p=>box.Right>p.Bounds.Left&&box.Left<p.Bounds.Right&&Math.Abs(box.Bottom-p.Bounds.Top)<=1);
            if(support==null)return;
            // a subpixel gravity step can miss native overlap after the deck carries us
            body.Position.Y=support.Bounds.Top-box.Height;
            body.Velocity.Y=Math.Min(body.Velocity.Y,PlayerValues.GRAVITY);
            Ground.SetValue(body,true);
        }
        private void TransferMomentum(BodyComp body)
        {
            if(!body.IsOnGround||body.Velocity.Y>=0)return;
            var box=body.GetHitbox();var support=platforms.FirstOrDefault(p=>p.Parts.Any(b=>box.Right>b.Bounds.Left&&box.Left<b.Bounds.Right&&Math.Abs(box.Bottom-b.Bounds.Top)<=1));
            if(support==null||support.Profile.Momentum==0)return;
            var velocity=support.Motion.Velocity;double scale=support.Profile.Momentum/60;
            if(support.Profile.MomentumAxes!="vertical")body.Velocity.X+=(float)(velocity.X*scale);
            if(support.Profile.MomentumAxes!="horizontal")body.Velocity.Y+=(float)(velocity.Y*scale);
            Ground.SetValue(body,false);
        }
        internal void Tick(BodyComp body,double delta)
        {
            if(delta<=0||delta>0.1||double.IsNaN(delta)||double.IsInfinity(delta))return;
            emitted=0;ObserveBody(body);
            var bodies=actors.Select(a=>a.Target as BodyComp).Where(a=>a!=null&&(a.gameObject==null||a.gameObject.IsAlive)).Distinct().ToArray();
            if(saveRevision!=System.Threading.Volatile.Read(ref PlatformSaveCoordinator.Revision))
            {
                ScenePersistence next;if(PlatformSaveCoordinator.TryResetStore(persistence,out next,out saveRevision))
                {persistence=next;foreach(var c in clocks.Values)c.Reset();foreach(var p in platforms)p.Position();completed=false;rider="";}
            }
            foreach(var entry in clocks.OrderBy(p=>p.Key,StringComparer.Ordinal))
            {
                var clock=entry.Value;var members=platforms.Where(p=>p.Motion==clock).ToArray();
                // bounded steps keep a fast platform from crossing the King between queries
                int steps=Math.Max(1,(int)Math.Ceiling(clock.Definition.Speed*delta));
                var arrivals=new List<int>();var callback=clock.Arrived;clock.Arrived=p=>arrivals.Add(p);
                try
                {
                    for(int step=0;step<steps;step++)
                    {
                        var saved=clock.State.Copy();var previous=members.SelectMany(p=>p.Parts).ToDictionary(p=>p,p=>p.Bounds);
                        var positions=bodies.Select(b=>b.Position).ToArray();var velocities=bodies.Select(b=>b.Velocity).ToArray();
                        int events=arrivals.Count;clock.Advance(delta/steps);foreach(var p in members)p.Position();
                        var first=previous.First();int dx=first.Key.Bounds.X-first.Value.X,dy=first.Key.Bounds.Y-first.Value.Y;
                        if(dx==0&&dy==0)continue;
                        bool obstacle;Ignored=clock;
                        try{obstacle=previous.Keys.Any(p=>Blocked(p.Bounds));}finally{Ignored=null;}
                        if(obstacle||!bodies.All(b=>Displace(b,previous,clock,dx,dy,delta/steps)))
                        {
                            clock.Restore(saved);foreach(var p in members)p.Position();
                            for(int i=0;i<bodies.Length;i++){bodies[i].Position=positions[i];bodies[i].Velocity=velocities[i];}
                            arrivals.RemoveRange(events,arrivals.Count-events);clock.Obstruct();break;
                        }
                    }
                }
                finally{clock.Arrived=callback;}
                if(callback!=null)foreach(int point in arrivals)callback(point);
            }
            Publish();
        }
        internal void ObserveBody(BodyComp body)
        {
            actors.RemoveAll(a=>!a.IsAlive);
            if(!actors.Any(a=>ReferenceEquals(a.Target,body)))
            {if(actors.Count>=128)throw new InvalidOperationException("Moving-platform actor budget exceeded");actors.Add(new WeakReference(body));}
        }
        private static bool Displace(BodyComp body,Dictionary<NativePlatformPart,Rectangle> previous,PlatformMotionPlayer clock,int dx,int dy,double delta)
        {
            var before=body.GetHitbox();bool standing=body.Velocity.Y>=0&&previous.Values.Any(r=>before.Right>r.Left&&before.Left<r.Right&&Math.Abs(before.Bottom-r.Top)<=1);
            var target=before;if(standing)target.Offset(dx,dy);
            foreach(var part in previous.Keys)
            {
                var next=part.Bounds;if(!next.Intersects(target))continue;var old=previous[part];
                if(!standing&&part.Platform.Profile.Collision=="oneWay"&&(body.Velocity.Y<0||before.Bottom>old.Top+1))continue;
                if(before.Bottom<=old.Top+1)
                {
                    // an upward player step can clear the deck after launch
                    if(body.Velocity.Y<0&&before.Bottom+body.Velocity.Y*delta*60<=next.Top)continue;
                    target.Y=next.Top-target.Height;
                }
                else if(part.Platform.Profile.Collision=="oneWay")continue;
                else if(before.Top>=old.Bottom-1)target.Y=next.Bottom;
                else if(before.Right<=old.Left+1)target.X=next.Left-target.Width;
                else if(before.Left>=old.Right-1)target.X=next.Right;
            }
            if(target.X==before.X&&target.Y==before.Y)return true;
            var oldBody=QueryBody;var oldStart=QueryStart;Ignored=clock;bool blocked;
            try{QueryBody=body;QueryStart=before;blocked=Blocked(target);}
            finally{Ignored=null;QueryBody=oldBody;QueryStart=oldStart;}
            if(blocked||target.Left<0||target.Right>480)return false;
            body.Position+=new Vector2(target.X-before.X,target.Y-before.Y);
            if(!standing&&target.Y<before.Y&&body.Velocity.Y>0)body.Velocity=new Vector2(body.Velocity.X,0);
            return true;
        }
        private void ObserveRider(BodyComp body)
        {
            if(WorldControl.Following)return;var box=body.GetHitbox();var support=body.Velocity.Y>=0?platforms.FirstOrDefault(p=>p.Parts.Any(b=>box.Right>b.Bounds.Left&&box.Left<b.Bounds.Right&&Math.Abs(box.Bottom-b.Bounds.Top)<=1)):null;
            string next=support==null?"":support.Definition.Id;if(next==rider)return;
            var old=platforms.FirstOrDefault(p=>p.Definition.Id==rider);rider=next;if(old!=null)Event(old,"leave",-1);if(support!=null)Event(support,"board",-1);
        }
        private void Event(NativePlatform p,string kind,int point)
        {
            var scene=NativeScene.Current;if(scene==null||WorldControl.Following)return;
            string name="platform:"+p.Definition.Id+"."+kind;if(!scene.Program.Signals.ObservesEvent(name))return;
            if(emitted++>=128||scene.Program.Signals.PendingEvents>=128)
            {if(!Diagnostics.Contains("Platform event budget reached; spread out very short routes"))Diagnostics.Add("Platform event budget reached; spread out very short routes");return;}
            scene.Program.Signals.Emit(name,p.Definition.Id,"player",p.Definition.Screen,scene.Program.GameplayTime,new SignalSample("point",SceneValue.Integer(point),ObservationQuality.Exact,0));
        }
        private static bool Blocked(Rectangle target)
        {
            var screens=(LevelScreen[])Screens.GetValue(null);if(screens==null)return false;
            int first=Math.Max(0,(int)Math.Floor(-target.Bottom/360.0)),last=Math.Min(screens.Length-1,(int)Math.Ceiling((360-target.Top)/360.0));
            for(int i=first;i<=last;i++){Rectangle overlap;AdvCollisionInfo info;if(screens[i].TryCollision(target,out overlap,out info))return true;}
            return false;
        }
        internal IEnumerable<string> Describe()
        {return platforms.Select(p=>p.Definition.Id+" / "+p.Profile.Collision+" / "+(p.Motion.State.Playing?"running":"stopped")+" / offset "+p.Motion.Position.X.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+","+p.Motion.Position.Y.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)).Concat(Diagnostics);}
        internal string SourceIdentity{get{return plan.Identity;}}
        internal static void ValidateTargets(MovingPlatformPlan plan,SceneDocument scene,bool resolved)
        {
            if(scene==null)return;
            foreach(var action in NativeScene.Actions(scene).Where(a=>a.Operation!=null&&a.Operation.StartsWith("platform",StringComparison.Ordinal)))
            {
                if(string.IsNullOrEmpty(action.Target))throw new InvalidDataException("Moving-platform action target is missing");
                if(!resolved&&action.Target.StartsWith("paint-",StringComparison.Ordinal)&&plan.Paint.Count>0)continue;
                if(!plan.Platforms.Any(p=>p.Id==action.Target||p.Group==action.Target))throw new InvalidDataException("Unknown moving-platform action target: "+action.Target);
            }
        }
        internal void Command(string target,string command)
        {
            if(WorldControl.Following)throw new InvalidOperationException("A following world cannot command platforms");
            var selected=platforms.Where(p=>p.Definition.Id==target||p.Definition.Group==target).ToArray();if(selected.Length==0)throw new InvalidDataException("Unknown moving platform or group: "+target);
            foreach(var clock in selected.Select(p=>p.Motion).Distinct())clock.Command(command);
            foreach(var p in selected)p.Position();
            Publish();
        }
        private void Attach()
        {
            var screens=(LevelScreen[])Screens.GetValue(null);
            for(int i=0;i<screens.Length;i++)
            {
                int screen=i;var additions=platforms.Where(p=>p.Parts.Any(b=>p.Motion.Definition.Points.Any(w=>b.Origin.Bottom+w.Position.Y>-screen*360&&b.Origin.Top+w.Position.Y<360-screen*360))||Crosses(p,screen)).SelectMany(p=>p.Parts).Cast<IBlock>().ToArray();
                if(additions.Length==0)continue;var old=(IBlock[])Hitboxes.GetValue(screens[i]);original.Add(screens[i],old);var next=old.Concat(additions).ToArray();Hitboxes.SetValue(screens[i],next);installed.Add(screens[i],next);
            }
        }
        private static bool Crosses(NativePlatform p,int screen)
        {double low=p.Parts.Min(b=>b.Origin.Top)+p.Motion.Definition.Points.Min(w=>w.Position.Y),high=p.Parts.Max(b=>b.Origin.Bottom)+p.Motion.Definition.Points.Max(w=>w.Position.Y);return high>-screen*360&&low<360-screen*360;}
        private void Detach()
        {
            foreach(var pair in installed)
            {
                var now=(IBlock[])Hitboxes.GetValue(pair.Key);
                if(ReferenceEquals(now,pair.Value))Hitboxes.SetValue(pair.Key,original[pair.Key]);
                else Hitboxes.SetValue(pair.Key,now.Where(b=>!(b is NativePlatformPart)||!platforms.Contains(((NativePlatformPart)b).Platform)).ToArray());
            }
            installed.Clear();original.Clear();foreach(var clock in clocks.Values)clock.Arrived=null;
        }
        private void Publish()
        {
            if(Current!=this||persistence==null||WorldControl.Following||!WorldExecution.CanPersist)return;
            var packet=Packet();var values=new Dictionary<string,string>{{"identity",identity},{"rider",rider}};for(int i=0;i<packet.States.Length;i++)values.Add(i.ToString("D3"),packet.States[i]);PlatformSaveCoordinator.Publish(persistence,values);
        }
        internal void Complete(){completed=true;Publish();}
        public string Id{get{return "mega.mapping.platforms";}}
        public int Version{get{return 2;}}
        private MovingPlatformPacket Packet()
        {
            return new MovingPlatformPacket{Identity=identity,Rider=rider,States=clocks.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>Encode(p.Value.State)).Concat(new[]{completed?"1":"0"}).ToArray()};
        }
        private static string Encode(PlatformMotionState s)
        {return string.Join("|",new[]{s.From.ToString(),s.To.ToString(),s.Direction.ToString(),s.Distance.ToString("R",System.Globalization.CultureInfo.InvariantCulture),s.Wait.ToString("R",System.Globalization.CultureInfo.InvariantCulture),s.Time.ToString("R",System.Globalization.CultureInfo.InvariantCulture),s.Playing?"1":"0",s.Blocked?"1":"0"});}
        private static PlatformMotionState Decode(string text)
        {
            var p=text.Split('|');var culture=System.Globalization.CultureInfo.InvariantCulture;if(p.Length!=7&&p.Length!=8||p[6]!="0"&&p[6]!="1"||p.Length==8&&p[7]!="0"&&p[7]!="1")throw new InvalidDataException("Invalid platform state packet");
            return new PlatformMotionState{From=int.Parse(p[0],culture),To=int.Parse(p[1],culture),Direction=int.Parse(p[2],culture),Distance=double.Parse(p[3],culture),Wait=double.Parse(p[4],culture),Time=double.Parse(p[5],culture),Playing=p[6]=="1",Blocked=p.Length==8&&p[7]=="1"};
        }
        public object Capture(){return Packet();}
        public void Validate(object value)
        {
            var packet=value as MovingPlatformPacket;if(packet==null||packet.Identity!=identity||packet.States==null||packet.States.Length!=clocks.Count+1||packet.States.Any(s=>s==null||s.Length>256)||packet.States.Last()!="0"&&packet.States.Last()!="1")throw new InvalidDataException("Moving-platform map or state changed");
            if(packet.Rider==null||packet.Rider!=""&&!platforms.Any(p=>p.Definition.Id==packet.Rider))throw new InvalidDataException("Unknown platform rider state");
            int i=0;foreach(var pair in clocks.OrderBy(p=>p.Key,StringComparer.Ordinal))pair.Value.Validate(Decode(packet.States[i++]));
        }
        public void Restore(object value)
        {
            Validate(value);var packet=(MovingPlatformPacket)value;int i=0;foreach(var pair in clocks.OrderBy(p=>p.Key,StringComparer.Ordinal))pair.Value.Restore(Decode(packet.States[i++]));
            completed=packet.States.Last()=="1";foreach(var p in platforms)p.Position();rider=packet.Rider;
            Publish();
        }
        public void Dispose(){if(disposed)return;if(activation!=null)activation.Dispose();resources.Dispose();actors.Clear();disposed=true;}
    }
}
