using System;
using System.Collections.Generic;
using System.Linq;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JKRuntime.Presentation
{
    /// <summary>One native bridge with ordered, owned visual contributions. Never advances animation or physics.</summary>
    public static class PlayerVisuals
    {
        internal sealed class Actor
        {
            internal PlayerEntity Player;
            internal Sprite Source;
            internal Bridge Bridge;
            internal readonly List<Registration> Layers=new List<Registration>();
            internal Registration[] Snapshot=new Registration[0];
        }
        private static readonly Dictionary<PlayerEntity,Actor> actors=new Dictionary<PlayerEntity,Actor>();
        private static OwnedPatches hooks;
        private static int preparations;
        private static void Install()
        {
            if(hooks!=null)return;
            var pending=new OwnedPatches("jk-runtime.player-visuals");
            try {
                pending.Add(typeof(PlayerEntity).GetMethod("SetSprite"),postfix:typeof(PlayerVisuals).GetMethod("Changed",OwnedPatches.Members));
                // SetSprite is tiny and some callers were already inlined before activation.
                pending.Add(typeof(PlayerEntity).GetMethod("Draw"),prefix:typeof(PlayerVisuals).GetMethod("Changed",OwnedPatches.Members),priority:800);
                hooks=pending;
            }
            catch {pending.Dispose();throw;}
        }
        public static void Prepare(RuntimeScope scope)
        {
            RuntimeApi.Kernel.CheckThread();if(scope==null)throw new ArgumentNullException("scope");
            AppearanceGeometry.Prepare();Install();preparations++;
            scope.Defer(()=>{preparations--;ReleaseHooks();});
            scope.Own(MethodValidity.Watch(typeof(PlayerEntity).GetMethod("Draw"),"WardrobePlus.Presentation","Phoenixx19.HitboxResizer.Harmony"));
        }
        private static void ReleaseHooks() {if(preparations==0&&actors.Count==0&&hooks!=null){hooks.Dispose();hooks=null;}}
        public static Registration Register(PlayerEntity player,string owner,VisualPhase phase,Func<Sprite,Sprite> compose,Func<bool> enabled=null,int order=0)
        {
            RuntimeApi.Kernel.CheckThread();ModuleDefinition.ValidId(owner);
            if(player==null||compose==null)throw new ArgumentNullException(player==null?"player":"compose");
            Install();Actor actor;
            if(!actors.TryGetValue(player,out actor))
            {
                actor=new Actor {Player=player,Source=PlayerAppearance.SpriteField.GetValue(player) as Sprite};
                actor.Bridge=new Bridge(actor);actors.Add(player,actor);
            }
            if(actor.Layers.Any(x=>x.Owner==owner&&x.Phase==phase))throw new InvalidOperationException("Visual phase already registered: "+owner);
            var current=PlayerAppearance.SpriteField.GetValue(player) as Sprite;
            if(!ReferenceEquals(current,actor.Bridge))actor.Source=Unwrap(current);
            var layer=new Registration(actor,owner,phase,compose,enabled,order);actor.Layers.Add(layer);
            actor.Layers.Sort((a,b)=>a.Phase!=b.Phase?a.Phase.CompareTo(b.Phase):a.Order!=b.Order?a.Order.CompareTo(b.Order):string.CompareOrdinal(a.Owner,b.Owner));
            actor.Snapshot=actor.Layers.ToArray();
            PlayerAppearance.SpriteField.SetValue(player,actor.Bridge);return layer;
        }
        private static void Changed(PlayerEntity __instance)
        {
            Actor actor;if(!actors.TryGetValue(__instance,out actor))return;
            var next=PlayerAppearance.SpriteField.GetValue(__instance) as Sprite;
            if(ReferenceEquals(next,actor.Bridge))return;
            actor.Source=next;PlayerAppearance.SpriteField.SetValue(__instance,actor.Bridge);
        }
        public static Sprite Unwrap(Sprite sprite) {var bridge=sprite as Bridge;return bridge==null?sprite:bridge.Actor.Source;}
        internal static Sprite Compose(PlayerEntity player,Sprite source,AppearanceStage stage)
        {
            Actor actor;if(!actors.TryGetValue(player,out actor))return source;
            Sprite result=source;bool form=false;
            foreach(var layer in actor.Snapshot)
            {
                if(layer.Disposed||layer.Faulted||(stage==AppearanceStage.Body&&layer.Phase>=VisualPhase.Replacement))continue;
                try {
                    if(layer.Enabled!=null&&!layer.Enabled())continue;
                    if(layer.Phase==VisualPhase.Form){if(form){layer.LastError="Another form already owns this visual sample";continue;}}
                    result=layer.Compose(result)??result;
                    if(layer.Phase==VisualPhase.Form)form=true;layer.LastError=null;
                } catch(Exception error) {layer.Faulted=true;layer.LastError=error.GetBaseException().Message;Console.WriteLine("[JK Runtime] Visual "+layer.Owner+": "+layer.LastError);}
            }
            return result;
        }
        public sealed class Registration : IDisposable
        {
            private readonly Actor actor;
            internal readonly Func<Sprite,Sprite> Compose;
            internal readonly Func<bool> Enabled;
            internal readonly int Order;
            internal bool Disposed,Faulted;
            public string Owner {get;private set;}
            public VisualPhase Phase {get;private set;}
            public string LastError {get;internal set;}
            internal Registration(Actor value,string owner,VisualPhase phase,Func<Sprite,Sprite> compose,Func<bool> enabled,int order)
            {actor=value;Owner=owner;Phase=phase;Compose=compose;Enabled=enabled;Order=order;}
            public void Dispose()
            {
                RuntimeApi.Kernel.CheckThread();if(Disposed)return;Disposed=true;actor.Layers.Remove(this);
                actor.Snapshot=actor.Layers.ToArray();
                if(actor.Layers.Count!=0)return;
                if(ReferenceEquals(PlayerAppearance.SpriteField.GetValue(actor.Player),actor.Bridge))PlayerAppearance.SpriteField.SetValue(actor.Player,actor.Source);
                actors.Remove(actor.Player);ReleaseHooks();
            }
        }
        internal sealed class Bridge : Sprite
        {
            internal readonly Actor Actor;
            private bool drawing;
            internal Bridge(Actor actor){Actor=actor;}
            public override void Draw(Vector2 position,SpriteEffects effects=SpriteEffects.None)
            {
                if(drawing)return;
                drawing=true;
                try {var frame=PlayerAppearance.Resolve(Actor.Player,AppearanceStage.Complete);frame.Facing=effects;if(frame.Sprite is IAppearanceProjection)position=PlayerAppearance.Project(frame);PlayerAppearance.Draw(frame,position);}
                finally {drawing=false;}
            }
            public override void Draw(float x,float y,SpriteEffects effects=SpriteEffects.None){Draw(new Vector2(x,y),effects);}
            public override void Draw(Point point,SpriteEffects effects=SpriteEffects.None){Draw(point.ToVector2(),effects);}
            public override void Draw(Rectangle rectangle,SpriteEffects effects=SpriteEffects.None){Draw(rectangle.Location.ToVector2(),effects);}
        }
    }
}
