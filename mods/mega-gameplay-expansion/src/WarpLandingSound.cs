using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviorTree;
using JumpKing.Player;

namespace MegaGameplayExpansion
{
    internal sealed class WarpLandingSound
    {
        private readonly IsOnGround node;
        private readonly FieldInfo result;
        private readonly Action<IsOnGround,BodyComp,Func<IEnumerable<Type>>> play;
        internal WarpLandingSound(PlayerEntity player)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var field=typeof(PlayerEntity).GetField("m_is_on_ground_state",flags);
            var method=typeof(IsOnGround).GetMethod("HandleSounds",flags,null,new[]{typeof(BodyComp),typeof(Func<IEnumerable<Type>>)},null);
            result=typeof(IBTnode).GetField("m_last_result",flags);
            if(field==null || method==null || result==null)
                throw new InvalidOperationException("Native landing sound contract unavailable");
            node=field.GetValue(player) as IsOnGround;
            if(node==null) throw new InvalidOperationException("Native landing sound node unavailable");
            play=(Action<IsOnGround,BodyComp,Func<IEnumerable<Type>>>)Delegate.CreateDelegate(typeof(Action<IsOnGround,BodyComp,Func<IEnumerable<Type>>>),method);
        }
        internal void Play(BodyComp body)
        {
            try
            {
                // The player's actual node retains registered custom land sounds;
                // native HandleSounds selects water/ice/snow/sand/boots itself.
                play(node,body,body.OnBlocks);
            }
            catch(Exception error)
            {
                // An audio device / third-party sound failure must not lock controls.
                WarpDiagnostics.Write("Landing audio failed: "+error.Message);
            }
            finally
            {
                // Ground was really reached while the BT was paused. Acknowledge
                // it so its next ordinary evaluation does not play a second sound.
                result.SetValue(node,BTresult.Success);
            }
        }
    }
}
