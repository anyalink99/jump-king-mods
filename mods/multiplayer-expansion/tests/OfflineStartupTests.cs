using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using JumpKing.API;
using JumpKing.GameManager;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    internal static class OfflineStartupTests
    {
        private static int reads;
        private static bool Identity(ref ulong? __result) {reads++;__result=44;return false;}
        private static void Check(bool value,string message) {if(!value)throw new Exception(message);}
        internal static void Run()
        {
            var hooks=new Harmony("multiplayer-expansion.offline-startup-test");
            var identity=typeof(JumpKingMultiplayer.Models.MultiplayerManager).Assembly
                .GetType("JumpKingMultiplayer.Extensions.PlayerSpriteStateExtensions",true).GetMethod("GetLevelId");
            hooks.Patch(identity,prefix:new HarmonyMethod(typeof(OfflineStartupTests),"Identity"));
            var before=AccessTools.Method(typeof(AdvancedSession),"BeforeBody");
            var after=AccessTools.Method(typeof(AdvancedSession),"AfterBody");
            var sessionBody=AccessTools.Field(typeof(AdvancedSession),"body");
            var lobby=AccessTools.Field(typeof(AdvancedSession),"lobby");
            var body=new BodyComp(new Vector2(100,100),18,26);
            var player=(PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));GC.SuppressFinalize(player);
            AccessTools.Field(typeof(PlayerEntity),"m_body").SetValue(player,body);
            GameLoop.m_player=player;
            var solver=(PlayerContacts)AccessTools.Field(typeof(AdvancedSession),"solver").GetValue(null);
            var contacts=(List<InteractionPeer>)AccessTools.Field(typeof(AdvancedSession),"contacts").GetValue(null);
            Action step=delegate {
                AdvancedSession.BeginFrame();
                var args=new object[]{body,Vector2.Zero};before.Invoke(null,args);
                after.Invoke(null,new object[]{body,args[1]});
            };
            try {
                Check(!AdvancedSession.Connected,"Cold fixture unexpectedly has a lobby");
                var native=((LinkedList<IBodyCompBehaviour>)AccessTools.Field(typeof(BodyComp),"m_behaviours").GetValue(body)).ToArray();
                using(new ContactPipeline(body,solver,()=>contacts,()=>InteractionRules.Ghosts,()=>0,r=>false)) {
                    var phases=((LinkedList<IBodyCompBehaviour>)AccessTools.Field(typeof(BodyComp),"m_behaviours").GetValue(body)).Except(native).ToArray();
                    var initial=body.Position;
                    for(int i=0;i<2;i++){step();foreach(var phase in phases)phase.ExecuteBehaviour(null);}
                    Check(reads==0,"Offline first frames queried the saved map identity");
                    Check((int)AccessTools.Field(typeof(PlayerContacts),"tick").GetValue(solver)==0,"Empty contact phases entered the solver");
                    Check(body.Position==initial && body.Velocity==Vector2.Zero,"Idle phases changed the native body");
                    Check(sessionBody.GetValue(null)==null,"Offline update attached session state");
                    // joining during the same attempt must still establish the real body and map
                    lobby.SetValue(null,1UL);step();
                    Check(reads==1 && ReferenceEquals(sessionBody.GetValue(null),body),"Joining didn't refresh body and map");
                    solver.Support=2;contacts.Add(new InteractionPeer());
                    lobby.SetValue(null,0UL);step();
                    Check(reads==1 && sessionBody.GetValue(null)==null && solver.Support==0 && contacts.Count==0,"Leaving kept contact state or read the map again");
                    lobby.SetValue(null,2UL);step();
                    Check(reads==2 && ReferenceEquals(sessionBody.GetValue(null),body),"Rejoining the same attempt reused stale identity");
                    lobby.SetValue(null,0UL);step();
                }
                Check(((LinkedList<IBodyCompBehaviour>)AccessTools.Field(typeof(BodyComp),"m_behaviours").GetValue(body)).SequenceEqual(native),"Idle pipeline leaked a body phase");
                Console.WriteLine("[OK] Cold offline startup: no identity reads or solver calls; join, leave and rejoin refresh session state");
            } finally {lobby.SetValue(null,0UL);GameLoop.m_player=null;hooks.UnpatchAll(hooks.Id);}
        }
    }
}
