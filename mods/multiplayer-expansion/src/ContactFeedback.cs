using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Player;

namespace MultiplayerExpansion
{
    internal static class ContactFeedback
    {
        private sealed class Pending { internal bool Bump; }
        private static readonly ConditionalWeakTable<BodyComp, Pending> pending = new ConditionalWeakTable<BodyComp, Pending>();
        private static readonly System.Reflection.FieldInfo contextField = AccessTools.Field(typeof(BodyComp), "m_behaviourContext");
        private static readonly System.Reflection.FieldInfo behavioursField = AccessTools.Field(typeof(BodyComp), "m_behaviours");

        internal static void Request(BodyComp body) { pending.GetOrCreateValue(body).Bump = true; }
        internal static void Cancel(BodyComp body) { pending.Remove(body); }
        internal static void PrepareNative(BodyComp body)
        {
            Pending value;
            if(pending.TryGetValue(body,out value) && value.Bump)
                ((BehaviourContext)contextField.GetValue(body))[PlayBumpSFXBehaviour.PlayBumpSFXFlag]=true;
        }

        internal static void Flush(BodyComp body)
        {
            Pending value;
            if (!pending.TryGetValue(body, out value) || !value.Bump) return;
            value.Bump = false;
            var context = (BehaviourContext)contextField.GetValue(body);
            // the native wall may already have played this frame's impact
            if (context.ContainsKey(PlayBumpSFXBehaviour.PlayBumpSFXFlag)) return;
            foreach (var behaviour in (LinkedList<IBodyCompBehaviour>)behavioursField.GetValue(body))
            {
                if (!(behaviour is PlayBumpSFXBehaviour)) continue;
                context[PlayBumpSFXBehaviour.PlayBumpSFXFlag] = true;
                behaviour.ExecuteBehaviour(context);
                break;
            }
        }
    }
}
