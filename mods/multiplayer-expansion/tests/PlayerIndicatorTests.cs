using System;
using System.Collections;
using System.Linq;
using System.Runtime.Serialization;
using HarmonyLib;
using JumpKingMultiplayer.Models;
using Microsoft.Xna.Framework;
using Steamworks;
using JumpKing.Level;

namespace MultiplayerExpansion
{
    internal static class PlayerIndicatorTests
    {
        internal static void Run()
        {
            var assembly=typeof(MultiplayerManager).Assembly;
            var manager=(MultiplayerManager)FormatterServices.GetUninitializedObject(typeof(MultiplayerManager));
            var field=AccessTools.Field(typeof(MultiplayerManager),"Players");
            var list=(IList)Activator.CreateInstance(field.FieldType);
            field.SetValue(manager,list);
            Check(!PlayerIndicators.ReadPlayers(null).Any() && !PlayerIndicators.ReadPlayers(manager).Any(),"Empty indicator list failed");
            var ghost=(IGhostPlayerData)Activator.CreateInstance(assembly.GetType("JumpKingMultiplayer.Models.GhostPlayer",true));
            ghost.SteamId=new CSteamID(76561198000000001);ghost.LevelId=42;
            ghost.AbsolutePosition=new Vector2(500,-100);ghost.Color=Color.Gold;
            list.Add(ghost);list.Add(null);
            Check(PlayerIndicators.ReadPlayers(manager).Single()==ghost,"Installed Multiplayer player field wasn't read");
            Vector2 target;Color color;
            Check(PlayerIndicators.TryTarget(ghost,42,out target,out color) && target==ghost.AbsolutePosition && color==Color.Gold,"Installed ghost properties weren't read");
            Check(!PlayerIndicators.TryTarget(ghost,43,out target,out color),"Different-map player received an indicator");
            ghost.IsDisposed=true;
            Check(!PlayerIndicators.TryTarget(ghost,42,out target,out color),"Disposed player received an indicator");
            var proximity=Activator.CreateInstance(assembly.GetType("JumpKingMultiplayer.Models.ProximityPlayers",true));
            Check(PlayerIndicators.ReadThreshold(proximity)==3,"Installed proximity threshold wasn't read");
            var hooks=new Harmony("multiplayer-expansion.indicator-contract");
            try { PlayerIndicators.Install(hooks); }
            finally { hooks.UnpatchAll(hooks.Id); }
            Projection();
            WorldDrawing();
            Console.WriteLine("[OK] Indicators: installed Multiplayer manager, ghost interface, lifecycle, map filter and Harmony patch");
        }
        private static void Projection()
        {
            var native=AccessTools.Field(typeof(LevelManager),"m_screens");var old=native.GetValue(null);
            var camera=AccessTools.Field(typeof(JumpKing.Camera),"_current_screen");var oldCamera=camera.GetValue(null);var oldOffset=JumpKing.Camera.Offset;
            var branch=new LevelScreen[3];
            for(int i=0;i<3;i++) branch[i]=new LevelScreen(i,new IBlock[0],new LevelScreen.Graphics(),false,new TeleportLink[0],0,null);
            branch[0]=new LevelScreen(0,new IBlock[]{new BoxBlock(new Rectangle(0,0,8,360))},new LevelScreen.Graphics(),false,new[]{new TeleportLink(3)},0,null);
            branch[2]=new LevelScreen(2,new IBlock[]{new BoxBlock(new Rectangle(472,-720,8,360)),new BoxBlock(new Rectangle(0,-368,480,8))},new LevelScreen.Graphics(),false,new[]{new TeleportLink(1)},0,null);
            try {
                native.SetValue(null,branch);JKRuntime.Geometry.MapTopology.Invalidate();
                camera.SetValue(null,0);JumpKing.Camera.Offset=Vector2.Zero;
                Vector2 input=Vector2.Zero;float range;
                Check(PlayerIndicators.ProjectTarget(new Vector2(240,180),new Vector2(21,-620),out range)==new Vector2(510,113) && range<1,
                    "Native camera lost the right-side branch while Smooth Camera was disabled");
                using(JKRuntime.FrameComposition.RegisterPresenter(()=>true,point=>{input=point;return new Vector2(510,180);})) {
                    var canonical=new Vector2(21,-620);
                    Check(PlayerIndicators.ProjectTarget(new Vector2(240,180),canonical,out range)==new Vector2(510,180)
                        && input==canonical+new Vector2(9,13) && range<1,"Indicator passed an unfolded chart point to a canonical camera projector");
                    Vector2 position;float rotation;
                    Check(PlayerIndicators.Place(new Vector2(510,180),out position,out rotation) && position==new Vector2(471,180),"Projected right-side indicator isn't on the viewport edge");
                    Check(!PlayerIndicators.Place(new Vector2(400,180),out position,out rotation),"Visible remote player received an offscreen indicator");
                }
            } finally {native.SetValue(null,old);camera.SetValue(null,oldCamera);JumpKing.Camera.Offset=oldOffset;JKRuntime.Geometry.MapTopology.Invalidate();}
        }
        private static void Check(bool value,string message) { if(!value) throw new Exception(message); }
        private static readonly System.Collections.Generic.List<Vector2> worldPositions=new System.Collections.Generic.List<Vector2>();
        private static bool RecordWorldGhost(object __instance)
        {worldPositions.Add(((IGhostPlayerData)__instance).RelativePosition);return false;}
        private static void WorldDrawing()
        {
            var old=MultiplayerManager.instance;var lobby=AccessTools.Field(typeof(AdvancedSession),"lobby");var oldLobby=lobby.GetValue(null);
            var hooks=new Harmony("multiplayer-expansion.world-draw-fixture");
            try {
                var manager=(MultiplayerManager)FormatterServices.GetUninitializedObject(typeof(MultiplayerManager));
                var field=AccessTools.Field(typeof(MultiplayerManager),"Players");var list=(IList)Activator.CreateInstance(field.FieldType);field.SetValue(manager,list);
                var type=typeof(MultiplayerManager).Assembly.GetType("JumpKingMultiplayer.Models.GhostPlayer",true);
                var player=(IGhostPlayerData)Activator.CreateInstance(type);player.SteamId=new CSteamID(999);player.AbsolutePosition=new Vector2(100,-20);list.Add(player);
                MultiplayerManager.instance=manager;lobby.SetValue(null,1UL);worldPositions.Clear();
                hooks.Patch(AccessTools.Method(typeof(MultiplayerManager),"DrawPlayers"),prefix:new HarmonyMethod(AccessTools.Method(typeof(RemotePresentation),"LegacyDraw")));
                hooks.Patch(AccessTools.Method(type,"DrawFromOutside"),prefix:new HarmonyMethod(AccessTools.Method(typeof(PlayerIndicatorTests),"RecordWorldGhost")));
                using(JKRuntime.FrameComposition.RegisterWorldDraw(RemotePresentation.WorldDraw))
                using(JKRuntime.FrameComposition.RegisterPresenter(()=>true,p=>p+new Vector2(1000,1000))) {
                    foreach(int screen in new[]{0,1})using(new JKRuntime.FrameComposition.WorldPass(new Vector2(0,screen*360))) {
                        // the installed rayman-wall hook calls this during entity drawing
                        AccessTools.Method(typeof(MultiplayerManager).Assembly.GetType("JumpKingMultiplayer.MultiplayerPatches",true),"DrawPlayers").Invoke(null,null);
                        Check(worldPositions.Count==screen,"Legacy wall hook drew into the wrong world pass");
                        JKRuntime.FrameComposition.DrawWorld();
                    }
                    Check(worldPositions.SequenceEqual(new[]{new Vector2(109,6),new Vector2(109,366)}),"World actor used another screen's coordinates or late viewport coordinates");
                    MultiplayerManager.DrawPlayers();Check(worldPositions.Count==2,"Late manager draw duplicated the composited actor");
                }
                Check(!(bool)AccessTools.Field(typeof(MultiplayerManager),"_alreadyDrawedPlayers").GetValue(manager),"World passes changed the original per-frame suppression flag");
                MultiplayerManager.DrawPlayers();Check(worldPositions.Count==3,"Native camera lost its ordinary player draw");
            } finally {hooks.UnpatchAll(hooks.Id);lobby.SetValue(null,oldLobby);MultiplayerManager.instance=old;worldPositions.Clear();}
        }
    }
}
