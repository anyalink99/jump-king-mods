using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using JumpKing;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace JKRuntime.Presentation
{
    /// <summary>Draw-only screen selection for scripted cuts. Never writes the native simulation screen.</summary>
    public static class NativeScreenView
    {
        private static int users;
        private static RuntimeScope resources;
        private static readonly FieldInfo nativeScreen=typeof(Camera).GetField("_current_screen",OwnedPatches.Members);
        private static readonly FieldInfo screens=typeof(LevelManager).GetField("m_screens",OwnedPatches.Members);
        [ThreadStatic] private static DrawScope selected;
        public sealed class DrawScope : IDisposable
        {
            private readonly DrawScope previous;
            private bool disposed;
            public int Screen {get;private set;}
            private Vector2 extra;
            public Vector2 ExtraTranslation
            {
                get{return extra;}
                set{RuntimeApi.Kernel.CheckThread();new CameraTarget(value,Vector2.Zero,0,false).Validate();extra=value;}
            }
            internal DrawScope(int screen,Vector2 extra){previous=selected;Screen=screen;ExtraTranslation=extra;selected=this;}
            public void Dispose(){RuntimeApi.Kernel.CheckThread();if(disposed)return;if(selected!=this)throw new InvalidOperationException("Draw scopes must end in reverse order");selected=previous;disposed=true;}
        }
        public static DrawScope Enter(int screen,Vector2 extraTranslation)
        {
            RuntimeApi.Kernel.CheckThread();if(screen<0)throw new ArgumentOutOfRangeException("screen");
            return new DrawScope(screen,extraTranslation);
        }
        public static void Prepare(RuntimeScope scope)
        {
            RuntimeApi.Kernel.CheckThread();if(scope==null)throw new ArgumentNullException("scope");
            if(nativeScreen==null||nativeScreen.FieldType!=typeof(int)||screens==null||screens.FieldType!=typeof(LevelScreen[]))
                throw new NotSupportedException("Installed native screen-view fields changed");
            var lease=new ActionLease(Release);users++;
            try{scope.Own(lease);if(users==1)Install();}
            catch{lease.Dispose();throw;}
        }
        private static void Install()
        {
            resources=new RuntimeScope();
            var hooks=resources.Own(new OwnedPatches("jk-runtime.native-screen-view"));
            hooks.Add(typeof(JumpGame).GetMethod("Draw"),prefix:Method("Begin"),finalizer:Method("End"));
            hooks.Add(typeof(Camera).GetProperty("CurrentScreen").GetGetMethod(),prefix:Method("Screen0"));
            hooks.Add(typeof(Camera).GetProperty("CurrentScreenIndex1").GetGetMethod(),prefix:Method("Screen1"));
            hooks.Add(typeof(Camera).GetMethod("TransformVector2"),postfix:Method("VectorPostfix"));
            hooks.Add(typeof(Camera).GetMethod("TransformRect",new[]{typeof(Rectangle)}),postfix:Method("RectPostfix"));
            hooks.Add(typeof(Camera).GetMethod("TransformRect",new[]{typeof(Rectangle).MakeByRefType()}),postfix:Method("RectRefPostfix"));
            // the screen getter may already be inlined into JumpGame.Draw
            hooks.ReplaceCalls(typeof(JumpGame).GetMethod("Draw"),typeof(LevelManager).GetProperty("CurrentScreen").GetGetMethod(),Method("DrawScreen"),2);
            var originals=new[]{typeof(Camera).GetProperty("CurrentScreen").GetGetMethod(),typeof(Camera).GetProperty("CurrentScreenIndex1").GetGetMethod(),typeof(Camera).GetMethod("TransformVector2"),typeof(Camera).GetMethod("TransformRect",new[]{typeof(Rectangle)}),typeof(Camera).GetMethod("TransformRect",new[]{typeof(Rectangle).MakeByRefType()})};
            var replacements=new[]{"ReadScreen0","ReadScreen1","Transform","TransformRect","TransformRectRef"};
            foreach(string name in new[]{"JumpKing.MiscEntities.OldManEntity","JumpKing.MiscEntities.Merchant.MerchantEntity","JumpKing.Props.LoopingProp","JumpKing.Player.PlayerEntity","JumpKing.Props.RattmanText.RattmanEntity","JumpKing.MiscSystems.ScreenEvents.FlyingGargoyle","JumpKing.MiscEntities.OldMan.SpeechBubbleFormat"})
            foreach(var method in typeof(Game1).Assembly.GetType(name,true).GetMethods(OwnedPatches.Members|BindingFlags.DeclaredOnly).Where(m=>m.Name=="Draw"||m.Name=="DrawText"||m.Name=="DrawHitbox"||m.Name=="ForegroundDraw"))
            {
                var il=Inspection.GimmickIl.Read(method);
                for(int i=0;i<originals.Length;i++)
                {
                    int count=il.Count(instruction=>Equals(instruction.Operand,originals[i]));
                    if(count>0)hooks.ReplaceCalls(method,originals[i],Method(replacements[i]),count);
                }
            }
        }
        private static MethodInfo Method(string name){return typeof(NativeScreenView).GetMethod(name,OwnedPatches.Members);}
        private static void Release(){if(users>1){users--;return;}if(resources!=null){resources.Dispose();resources=null;}users=0;}
        private static void Begin(out DrawScope __state)
        {
            __state=null;CameraShot shot;
            if(CameraShots.TryRead(out shot)&&!CameraShots.SmoothAvailable&&shot.Screen<LevelManager.TotalScreens)__state=Enter(shot.Screen,Vector2.Zero);
        }
        private static void End(DrawScope __state){if(__state!=null)__state.Dispose();}
        private static bool Screen0(ref int __result){if(selected==null)return true;__result=selected.Screen;return false;}
        private static bool Screen1(ref int __result){if(selected==null)return true;__result=selected.Screen+1;return false;}
        private static Vector2 Correction {get{return selected==null?Vector2.Zero:new Vector2(0,(selected.Screen-(int)nativeScreen.GetValue(null))*360)+selected.ExtraTranslation;}}
        private static void VectorPostfix(ref Vector2 __result){__result+=Correction;}
        private static void RectPostfix(ref Rectangle __result){__result.Offset(Correction.ToPoint());}
        private static void RectRefPostfix(ref Rectangle draw_dst){draw_dst.Offset(Correction.ToPoint());}
        [MethodImpl(MethodImplOptions.NoInlining)] private static LevelScreen DrawScreen(){return selected!=null?((LevelScreen[])screens.GetValue(null))[selected.Screen]:LevelManager.CurrentScreen;}
        [MethodImpl(MethodImplOptions.NoInlining)] private static int ReadScreen0(){return selected!=null?selected.Screen:Camera.CurrentScreen;}
        [MethodImpl(MethodImplOptions.NoInlining)] private static int ReadScreen1(){return selected!=null?selected.Screen+1:Camera.CurrentScreenIndex1;}
        [MethodImpl(MethodImplOptions.NoInlining)] private static Vector2 Transform(Vector2 point){return selected!=null?point+Camera.Offset+new Vector2(0,selected.Screen*360)+selected.ExtraTranslation:Camera.TransformVector2(point);}
        [MethodImpl(MethodImplOptions.NoInlining)] private static Rectangle TransformRect(Rectangle value){if(selected==null)return Camera.TransformRect(value);value.Offset(Camera.Offset.ToPoint());value.Offset(selected.ExtraTranslation.ToPoint());value.Y+=selected.Screen*360;return value;}
        [MethodImpl(MethodImplOptions.NoInlining)] private static void TransformRectRef(ref Rectangle value){value=TransformRect(value);}
    }
}
