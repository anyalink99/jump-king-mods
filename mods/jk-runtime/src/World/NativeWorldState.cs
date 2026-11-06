using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BehaviorTree;
using EntityComponent;
using JumpKing;
using JumpKing.Level;
using JumpKing.MiscEntities;
using JumpKing.Util.DrawBT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JKRuntime.World
{
    public static class NativeWorldState
    {
        private sealed class Bird
        {
            [WorldField] public float X,Y,Cx,Cy;
            [WorldField] public int Sx,Sy,Width,Height,Flip;
        }
        private sealed class State {[WorldField] public Dictionary<string,Bird> Birds;}
        private static readonly Type raven=typeof(Game1).Assembly.GetType("JumpKing.MiscEntities.RavenEntity",true);
        private static readonly FieldInfo settings=ForeignMembers.Field(raven,"m_settings");
        private static readonly FieldInfo distance=ForeignMembers.Field(typeof(IsPlayerClose),"m_distance_squared");
        private static Dictionary<string,Bird> birds=new Dictionary<string,Bird>();
        private static Dictionary<string,Sprite> artwork=new Dictionary<string,Sprite>();
        private static int users;
        private static OwnedPatches hooks;
        private static void Install()
        {
            hooks=new OwnedPatches("jk-runtime.native-world");
            hooks.Add(ForeignMembers.PropertyGetter(typeof(WindManager),"CurrentVelocityRaw"),prefix:Method("Wind"));
            hooks.Add(ForeignMembers.PropertyGetter(typeof(WindManager),"CurrentVelocity"),prefix:Method("Wind"));
            hooks.Add(ForeignMembers.Method(typeof(Entity),"UpdateComponents"),prefix:Method("UpdateEntity"));
            hooks.Add(ForeignMembers.Method(raven,"Draw"),prefix:Method("DrawNative"));
            hooks.Add(ForeignMembers.Method(typeof(EntityManager),"Draw"),postfix:Method("Draw"));
            hooks.Add(ForeignMembers.Method(typeof(IsPlayerClose),"MyRun"),postfix:Method("Close"));
        }
        private static MethodInfo Method(string name) {return ForeignMembers.Method(typeof(NativeWorldState),name);}
        public static void Prepare(RuntimeScope scope)
        {
            RuntimeApi.Kernel.CheckThread();if(scope==null)throw new ArgumentNullException("scope");
            NativeWorldInteractions.Prepare(scope);
            if(users++>0){scope.Defer(ReleaseUser);return;}
            scope.Defer(ReleaseUser);Install();
            var assets=new Dictionary<string,Sprite>();
            var content=Game1.instance.contentManager.ravenSprites;
            foreach(var pair in content.raven_settings)assets.Add(pair.Key,content.GetRavenContent(pair.Value).Blink);
            artwork=assets;birds=new Dictionary<string,Bird>();
            stateLease=WorldRegistry.Shared.Bind<State>("native.ravens",Capture,value=>birds=value.Birds,ValidateBirds);

        }
        private static IDisposable stateLease;
        private static void ReleaseUser()
        {
            if(users>1){users--;return;}
            if(stateLease!=null){stateLease.Dispose();stateLease=null;}
            if(hooks!=null){hooks.Dispose();hooks=null;}
            artwork=new Dictionary<string,Sprite>();Reset();users=0;
        }
        private static State Capture()
        {
            if(WorldControl.Following)return new State{Birds=birds};
            var result=new Dictionary<string,Bird>();
            if(EntityManager.instance!=null)foreach(var e in EntityManager.instance.Entities)if(e.GetType()==raven && e.IsAlive){
                var sprite=(ISpriteEntity)e;var s=sprite.sprite;if(s==null)continue;
                var config=(RavenSettings)settings.GetValue(e);
                result[config.name]=new Bird{X=sprite.Position.X,Y=sprite.Position.Y,Cx=s.center.X,Cy=s.center.Y,Sx=s.source.X,Sy=s.source.Y,Width=s.source.Width,Height=s.source.Height,Flip=(int)sprite.spriteEffects};
            }
            return new State{Birds=result};
        }
        private static void ValidateBirds(State state)
        {
            if(state.Birds==null)throw new InvalidDataException("Missing bird state");
            foreach(var pair in state.Birds){
                Sprite art;var b=pair.Value;
                if(b==null || !artwork.TryGetValue(pair.Key,out art) || Math.Abs(b.X)>1e7 || Math.Abs(b.Y)>1e7 || b.Sx<0 || b.Sy<0 || b.Width<=0 || b.Height<=0 || b.Sx>art.texture.Width-b.Width || b.Sy>art.texture.Height-b.Height || b.Flip<0 || b.Flip>3)
                    throw new InvalidDataException("Incompatible bird assets");
            }
        }
        public static void Reset(){birds=new Dictionary<string,Bird>();}
        private static bool UpdateEntity(Entity __instance)
        {return !WorldControl.Following || __instance.GetType()!=raven;}
        private static bool DrawNative(){return !WorldControl.Following;}
        private static void Draw()
        {
            if(!WorldControl.Following)return;
            foreach(var pair in birds){Sprite art;if(!artwork.TryGetValue(pair.Key,out art))continue;var b=pair.Value;
                // use a private sprite, never mutate the map's shared atlas sprites
                var s=Sprite.CreateSpriteWithCenter(art.texture,new Rectangle(b.Sx,b.Sy,b.Width,b.Height),new Vector2(b.Cx,b.Cy));
                s.Draw(Camera.TransformVector2(new Vector2(b.X,b.Y)),(SpriteEffects)b.Flip);
            }
        }
        private static void Close(IsPlayerClose __instance,ref BTresult __result)
        {
            if(WorldControl.CurrentRole!=WorldRole.Host || __result==BTresult.Success)return;
            float radius=(float)distance.GetValue(__instance);
            foreach(var f in WorldControl.ActorSamples){Vector2 center=f.Position+new Vector2(f.Width/2f,f.Height/2f);
                if(Vector2.DistanceSquared(center,__instance.SpriteEntity.Position)<radius){__result=BTresult.Success;return;}
            }
        }
        public static float WindAt(double seconds,bool active,float intensity,bool? direction)
        {
            if(!active)return 0;
            float phase=(float)seconds*.48124886f;float wave=(float)Math.Sin(phase);wave=(float)Math.Cos(phase)>0?wave*2+1:wave*2-1;
            wave=Math.Max(-1,Math.Min(1,wave));return intensity==0?wave*.1f:direction.HasValue?(direction.Value?-1:1)*.0125f*intensity:wave*.0125f*intensity;
        }
        private static bool Wind(ref float __result)
        {
            if(!WorldControl.Following)return true;var screen=LevelManager.CurrentScreen;
            __result=WindAt(WorldControl.Time,screen.WindEndabled,screen.WindIntensity,screen.WindDirection);return false;
        }
    }
}
