using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using JKRuntime.Presentation;
using JumpKing;
using JumpKing.Player;

namespace JKRuntime
{
    internal static class ActorVisualTests
    {
        private sealed class TestSprite : Sprite
        {public override void Draw(Microsoft.Xna.Framework.Vector2 anchor,Microsoft.Xna.Framework.Graphics.SpriteEffects facing=Microsoft.Xna.Framework.Graphics.SpriteEffects.None){}}
        private sealed class Animation : IActorVisual,IActorVisualAnimation
        {
            internal int Updates,Composes;
            public void Advance(ActorVisualFrame frame,byte[] state){Updates++;}
            public Sprite Compose(Sprite outfit,Sprite charge,byte[] state){Composes++;return outfit;}
            public void Dispose(){}
        }
        private sealed class Visual : IActorVisual
        {
            internal int Value,Calls;
            internal bool Disposed;
            public Sprite Compose(Sprite outfit,Sprite charge,byte[] state){Value=state[0];Calls++;if(Value==255)throw new InvalidDataException("fixture");return outfit;}
            public void Dispose(){Disposed=true;}
        }
        private static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
        private static void Main()
        {
            var player=(PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
            var sprite=new TestSprite();byte[] state=null;int captures=0,factories=0;
            var created=new System.Collections.Generic.List<Visual>();
            using(var registration=ActorVisuals.Register("test.form",1,VisualPhase.Form,p=>{captures++;return state;},()=>{factories++;var visual=new Visual();created.Add(visual);return visual;}))
            {
                byte[] empty=ActorVisuals.Capture(player);
                for(int i=0;i<1000;i++)Check(ReferenceEquals(empty,ActorVisuals.Capture(player)),"inactive capture must reuse the empty snapshot");
                Check(captures==1&&factories==0,"idle consumers must not poll providers or create renderers");
                state=new byte[]{3};ActorVisuals.Changed(player);var first=ActorVisuals.Capture(player);
                state[0]=7;Check(ActorVisuals.Valid(first),"captured snapshot owns its serialized bytes");
                ActorVisuals.Changed(player);var second=ActorVisuals.Capture(player);
                using(var a=ActorVisuals.Create())using(var b=ActorVisuals.Create())
                {
                    a.Present(first,sprite,sprite);b.Present(second,sprite,sprite);
                    Check(factories==2,"each remote/replay actor must own a renderer");
                    Check(created[0].Value==3&&created[1].Value==7,"actor states must not overwrite each other");
                    int before=factories;
                    var timer=Stopwatch.StartNew();for(int i=0;i<100000;i++)a.Present(first,sprite,sprite);timer.Stop();
                    Check(factories==before,"repeated draws/presents must not recreate renderer state");
                    Check(created[0].Calls==1,"unchanged appearance must not recompute the provider");
                    Console.WriteLine("[PERF] 100000 unchanged actor presentations: "+timer.Elapsed.TotalMilliseconds.ToString("F2")+" ms");
                    a.Present(empty,sprite,sprite);a.Present(second,sprite,sprite);Check(factories==3,"leaving and reentering a form releases old actor state");
                    Check(created[0].Disposed,"removing a form must dispose its renderer");
                    var bad=(byte[])first.Clone();bad[1]=9;Check(!ActorVisuals.Valid(bad),"provider count is bounded");
                    bad=(byte[])first.Clone();bad[0]=9;Check(!ActorVisuals.Valid(bad),"unknown snapshot envelope is rejected");
                    for(int length=1;length<first.Length;length++) {var truncated=new byte[length];Array.Copy(first,truncated,length);Check(!ActorVisuals.Valid(truncated),"truncated payload must not reach a renderer");}
                }
                var receivers=new ActorVisuals.Actor[16];for(int i=0;i<receivers.Length;i++)receivers[i]=ActorVisuals.Create();
                try {
                    var timer=Stopwatch.StartNew();
                    for(int tick=0;tick<2000;tick++){
                        state[0]=(byte)(tick%200);ActorVisuals.Changed(player);var snapshot=ActorVisuals.Capture(player);
                        foreach(var actor in receivers)actor.Present(snapshot,sprite,sprite);
                    }
                    timer.Stop();Console.WriteLine("[PERF] capture + 16 scalar actors: "+(timer.Elapsed.TotalMilliseconds/2000).ToString("F4")+" ms/update (no drawing)");
                    state[0]=255;ActorVisuals.Changed(player);receivers[0].Present(ActorVisuals.Capture(player),sprite,sprite);
                    Check(created[created.Count-16].Disposed,"a failed provider must release its actor resources");
                }finally{foreach(var actor in receivers)actor.Dispose();}
            }
            using(var unknown=ActorVisuals.Create())Check(ReferenceEquals(unknown.Present(new byte[]{1,0},sprite,sprite),sprite),"missing providers preserve the ordinary outfit");
            var animation=new Animation();
            using(ActorVisuals.Register("test.animation",1,VisualPhase.Attachment,p=>new byte[]{1},()=>animation))
            using(var actor=ActorVisuals.Create())
            {
                var snapshot=ActorVisuals.Capture(player);
                for(int i=0;i<60;i++)actor.Present(snapshot,sprite,sprite,new ActorVisualFrame(i/60d,Microsoft.Xna.Framework.Vector2.Zero,Microsoft.Xna.Framework.Vector2.Zero,Microsoft.Xna.Framework.Graphics.SpriteEffects.None));
                Check(animation.Updates==60&&animation.Composes==1,"local clocks must advance effects without retransmitting state or rebuilding sprites");
                for(int i=0;i<100;i++)ActorVisuals.Draw(sprite,Microsoft.Xna.Framework.Vector2.Zero,Microsoft.Xna.Framework.Graphics.SpriteEffects.None,Microsoft.Xna.Framework.Color.White);
                Check(animation.Updates==60,"Draw must not advance visual simulation");
                actor.Present(snapshot,sprite,sprite,new ActorVisualFrame(59/60d,Microsoft.Xna.Framework.Vector2.Zero,Microsoft.Xna.Framework.Vector2.Zero,Microsoft.Xna.Framework.Graphics.SpriteEffects.None));
                Check(animation.Updates==60,"a paused clock must not duplicate emission");
            }
            Console.WriteLine("[OK] Actor visual snapshot caching, bounds, isolation and lifecycle");
        }
    }
}
