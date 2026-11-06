using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JKRuntime.Presentation
{
    /// <summary>Shared actor appearance, generation notifications and scoped draw context. Game-thread API.</summary>
    public static class PlayerAppearance
    {
        internal static readonly FieldInfo SpriteField=typeof(PlayerEntity).GetField("m_sprite",OwnedPatches.Members);
        private static readonly FieldInfo FlipField=typeof(PlayerEntity).GetField("m_flip",OwnedPatches.Members);
        private static readonly List<Func<AppearanceFrame,IDisposable>> contexts=new List<Func<AppearanceFrame,IDisposable>>();
        private static Func<AppearanceFrame,IDisposable>[] drawContexts=new Func<AppearanceFrame,IDisposable>[0];
        private static readonly List<Action<long>> observers=new List<Action<long>>();
        private static readonly List<Action<PresentationSignal>> signals=new List<Action<PresentationSignal>>();
        private static long revision;
        public static long Revision { get { return revision; } }
        public static IDisposable RegisterContext(string owner,Func<AppearanceFrame,IDisposable> begin)
        { RuntimeApi.Kernel.CheckThread();ModuleDefinition.ValidId(owner);if(begin==null)throw new ArgumentNullException("begin");contexts.Add(begin);drawContexts=contexts.ToArray();return new ActionLease(()=>{contexts.Remove(begin);drawContexts=contexts.ToArray();}); }
        public static IDisposable Subscribe(Action<long> changed)
        { RuntimeApi.Kernel.CheckThread();if(changed==null)throw new ArgumentNullException("changed");observers.Add(changed);return new ActionLease(()=>observers.Remove(changed)); }
        public static void Publish()
        { RuntimeApi.Kernel.CheckThread();revision++;foreach(var callback in observers.ToArray())try{callback(revision);}catch(Exception e){Console.WriteLine("[JK Runtime] Appearance observer: "+e.Message);} }
        public static IDisposable SubscribeSignals(Action<PresentationSignal> callback)
        { RuntimeApi.Kernel.CheckThread();if(callback==null)throw new ArgumentNullException("callback");signals.Add(callback);return new ActionLease(()=>signals.Remove(callback)); }
        public static void Signal(PlayerEntity player,string trigger,PresentationDelivery delivery=PresentationDelivery.Live)
        {
            RuntimeApi.Kernel.CheckThread();
            if(string.IsNullOrEmpty(trigger)||trigger.Length>128||trigger.IndexOf('/')<1)throw new ArgumentException("Use an owner/event ID","trigger");
            var value=new PresentationSignal(player,trigger,delivery);
            foreach(var callback in signals.ToArray())try{callback(value);}catch(Exception e){Console.WriteLine("[JK Runtime] Presentation observer: "+e.Message);}
        }
        public static Sprite BaseSprite(PlayerEntity player)
        { RuntimeApi.Kernel.CheckThread();return player==null?null:PlayerVisuals.Unwrap(SpriteField.GetValue(player) as Sprite); }
        public static AppearanceFrame Resolve(PlayerEntity player,AppearanceStage stage=AppearanceStage.Body,Sprite outfitPose=null,string pose=null)
        {
            RuntimeApi.Kernel.CheckThread();if(player==null)throw new ArgumentNullException("player");
            var geometry=AppearanceGeometry.Resolve(player);
            var source=outfitPose??BaseSprite(player);
            if(stage==AppearanceStage.Body)source=Filter(source,AppearanceLayerRole.WorldEffect);
            return new AppearanceFrame { Player=player,Sprite=stage==AppearanceStage.Outfit?source:PlayerVisuals.Compose(player,source,stage),
                WorldAnchor=geometry.Anchor(player.m_body.Position),Facing=(SpriteEffects)FlipField.GetValue(player),Geometry=geometry,
                Pose=pose,Revision=revision,Coverage=geometry.Coverage,Reason=geometry.Reason };
        }
        public static AppearanceFrame FromSprite(Sprite sprite,Vector2 worldAnchor,SpriteEffects facing=SpriteEffects.None,string pose=null)
        { RuntimeApi.Kernel.CheckThread();if(sprite==null)throw new ArgumentNullException("sprite");return new AppearanceFrame {Sprite=sprite,WorldAnchor=worldAnchor,Facing=facing,Pose=pose,Revision=revision,Coverage=AppearanceCoverage.Exact}; }
        internal static IDisposable Begin(AppearanceFrame frame)
        {
            var scope=new RuntimeScope();
            try { foreach(var begin in drawContexts) {var lease=begin(frame);if(lease!=null)scope.Own(lease);}return scope; }
            catch {scope.Dispose();throw;}
        }
        public static Vector2 Project(AppearanceFrame frame)
        { RuntimeApi.Kernel.CheckThread();if(frame==null)throw new ArgumentNullException("frame");var projection=frame.Sprite as IAppearanceProjection;return projection==null?Camera.TransformVector2(frame.WorldAnchor):projection.Project(frame.WorldAnchor); }
        public static void Draw(AppearanceFrame frame,Vector2 screenAnchor)
        { RuntimeApi.Kernel.CheckThread();using(Begin(frame))if(frame.Sprite!=null)frame.Sprite.Draw(screenAnchor,frame.Facing); }
        /// <summary>Shared fallback signature for native providers without publication. Includes origin and nested layer identity.</summary>
        public static int Signature(Sprite sprite) { return Signature(sprite,0); }
        private static int Signature(Sprite sprite,int depth)
        {
            if(sprite==null||depth>8)return 0;
            unchecked {
                int hash=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(sprite)*397^sprite.center.GetHashCode()^sprite.source.GetHashCode()^sprite.GetColor().GetHashCode();
                if(sprite.texture!=null)hash=hash*397^System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(sprite.texture);
                foreach(var part in Layers(sprite))if(!ReferenceEquals(part,sprite))hash=hash*397^Signature(part,depth+1);
                return hash;
            }
        }
        public static Sprite[] Layers(Sprite sprite)
        {
            if(sprite==null)return new Sprite[0];
            var property=sprite.GetType().GetProperty("Sprites");
            var parts=property==null?null:property.GetValue(sprite,null) as IEnumerable;
            return parts==null?new[]{sprite}:parts.Cast<Sprite>().ToArray();
        }
        public static AppearanceLayerRole RoleOf(Sprite sprite)
        {var declared=sprite as IAppearanceLayer;return declared==null?AppearanceLayerRole.Body:declared.Role;}
        public static bool IsStatic(Sprite sprite) {return IsStatic(sprite,0);}
        private static bool IsStatic(Sprite sprite,int depth)
        {
            if(sprite==null||depth>8)return false;
            if(sprite.GetType()==typeof(Sprite))return true;
            if(sprite.GetType().FullName!="JumpKing.XnaWrappers.LayeredSprite"&&!(sprite is FilteredSprite))return false;
            foreach(var part in Layers(sprite))if(!IsStatic(part,depth+1))return false;return true;
        }
        public static Sprite Filter(Sprite sprite,AppearanceLayerRole excluded)
        {return Filter(sprite,excluded,0);}
        private static Sprite Filter(Sprite sprite,AppearanceLayerRole excluded,int depth)
        {
            if(sprite==null||depth>8||(RoleOf(sprite)&excluded)!=0)return null;
            var parts=Layers(sprite);if(parts.Length==1&&ReferenceEquals(parts[0],sprite))return sprite;
            var filtered=parts.Select(p=>Filter(p,excluded,depth+1)).Where(p=>p!=null).ToArray();
            return parts.SequenceEqual(filtered)?sprite:new FilteredSprite(filtered);
        }
        private sealed class FilteredSprite:Sprite
        {
            public Sprite[] Sprites {get;private set;}
            internal FilteredSprite(Sprite[] sprites){Sprites=sprites;}
            public override void Draw(Vector2 position,SpriteEffects effects=SpriteEffects.None){foreach(var part in Sprites)part.Draw(position,effects);}
        }
    }
}
