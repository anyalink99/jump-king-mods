using System;
using System.Collections.Generic;
using System.Reflection;
using EntityComponent;
using JumpKing;
using JumpKing.GameManager.MultiEnding;
using JumpKing.Util.DrawBT;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    // native cutscene actors replace the player; their names are adapter roles, not scene objects
    internal sealed class NativeActorObservations
    {
        private static readonly string[] Roles={"king","babe","cherub","hanging_babe","crow","gargoyle","bird"};
        private static readonly FieldInfo Ending=typeof(JumpGame).GetField("m_ending",BindingFlags.Instance|BindingFlags.NonPublic);
        private readonly Dictionary<string,string> types=new Dictionary<string,string>(StringComparer.Ordinal);
        private readonly Dictionary<string,ISpriteEntity> actors=new Dictionary<string,ISpriteEntity>(StringComparer.Ordinal);
        private readonly SceneProgram scene;
        internal NativeActorObservations(SceneProgram program)
        {
            AssertContracts();scene=program;
            foreach(var binding in Endings.NativeEndings.Bindings)
            {
                if(!binding.Actor)continue;int split=binding.Role.IndexOf('_');string role=binding.Role.Substring(split+1);
                if(role=="cherubs")role="cherub";
                types.Add(binding.TypeName,role);
            }
        }
        internal static void AssertContracts()
        {if(Ending==null||Ending.FieldType!=typeof(GameEnding))throw new NotSupportedException("Native ending lifecycle is unavailable");}
        internal static bool IsEnding{get{var game=JumpGame.instance;var ending=game==null?null:(GameEnding)Ending.GetValue(game);return ending!=null&&ending.IsRunning();}}
        internal static void Declare(SignalHub hub)
        {
            foreach(string role in Roles)
            {
                string id="actor:ending."+role;
                hub.Declare(new SignalDescriptor(id+".position",SceneValueKind.Vector2,"px",true),SceneValue.Vector(0,0));
                hub.Declare(new SignalDescriptor(id+".present",SceneValueKind.Boolean,observation:true),SceneValue.Boolean(false));
            }
        }
        internal void Sample()
        {
            actors.Clear();var manager=EntityManager.instance;
            if(manager!=null)foreach(var entity in manager.Entities)
            {
                string role;var actor=entity as ISpriteEntity;
                if(actor!=null&&actor.IsAlive&&types.TryGetValue(actor.GetType().FullName,out role))actors[role]=actor;
            }
            foreach(string role in Roles)
            {
                ISpriteEntity actor;bool present=actors.TryGetValue(role,out actor);string id="actor:ending."+role;
                scene.Signals.Publish(id+".present",SceneValue.Boolean(present),ObservationQuality.Sampled);
                scene.Signals.Publish(id+".position",present?SceneValue.Vector(actor.Position.X,actor.Position.Y+(Camera.CurrentScreenIndex1-1)*360):SceneValue.Vector(0,0),present?ObservationQuality.Sampled:ObservationQuality.Unavailable);
            }
        }
        internal void Draw(string role)
        {ISpriteEntity actor;if(actors.TryGetValue(role,out actor)&&actor.IsAlive)actor.Draw();}
        internal static bool ValidRole(string role){return Array.IndexOf(Roles,role)>=0;}
    }
}
