using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JumpKing;
using JumpKing.GameManager;
using JumpKingMultiplayer.Models;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    internal static class PlayerIndicators
    {
        private static readonly Type proximity=typeof(MultiplayerManager).Assembly.GetType("JumpKingMultiplayer.Models.ProximityPlayers",true);
        private static readonly FieldInfo players=RequireField(typeof(MultiplayerManager),"Players",typeof(IEnumerable));
        private static readonly FieldInfo threshold=RequireField(proximity,"Threshold",typeof(int));
        private static FieldInfo RequireField(Type type,string name,Type expected)
        {
            var field=AccessTools.Field(type,name);
            if(field==null || !expected.IsAssignableFrom(field.FieldType)) throw new MissingFieldException(type.FullName,name);
            return field;
        }
        internal static void Install(Harmony hooks)
        { hooks.Patch(AccessTools.Method(proximity,"Draw"),prefix:new HarmonyMethod(typeof(PlayerIndicators),"Draw")); }

        internal static IEnumerable<IGhostPlayerData> ReadPlayers(MultiplayerManager manager)
        {
            if(manager==null) yield break;
            var list=(IEnumerable)players.GetValue(manager);
            if(list==null) yield break;
            foreach(IGhostPlayerData ghost in list) if(ghost!=null) yield return ghost;
        }
        internal static int ReadThreshold(object instance) { return (int)threshold.GetValue(instance); }
        internal static bool TryTarget(IGhostPlayerData ghost,ulong? map,out Vector2 target,out Color color)
        {
            target=Vector2.Zero;color=Color.Transparent;
            if(ghost==null || ghost.IsDisposed) return false;
            var peer=AdvancedSession.VisualPeer(ghost.SteamId.m_SteamID);
            if(peer!=null) {
                var frame=AdvancedSession.VisualFrame(peer);
                if(!frame.Active || frame.Map!=map) return false;
                target=frame.Position;
            } else {
                if(ghost.LevelId!=map) return false;
                target=ghost.AbsolutePosition;
            }
            color=ghost.Color;
            return true;
        }

        internal static bool Place(Vector2 projected,out Vector2 position,out float rotation)
        {
            position=Vector2.Zero;rotation=0;
            if(projected.X>=0 && projected.X<=480 && projected.Y>=0 && projected.Y<=360) return false;
            Vector2 direction=projected-new Vector2(240,180);
            float scale=Math.Min(Math.Abs(direction.X)>.001f ? 231/Math.Abs(direction.X) : float.PositiveInfinity,
                Math.Abs(direction.Y)>.001f ? 171/Math.Abs(direction.Y) : float.PositiveInfinity);
            position=new Vector2(240,180)+direction*scale;
            rotation=(float)Math.Atan2(direction.Y,direction.X)+MathHelper.Pi;
            return true;
        }
        internal static Vector2 ProjectTarget(Vector2 observer,Vector2 target,out float screens)
        {
            Vector2 chart;
            if(!JKRuntime.Geometry.MapTopology.TryChartPosition(observer,target,out chart)) {screens=float.PositiveInfinity;return Vector2.Zero;}
            Vector2 distance=chart-observer;
            screens=Math.Max(Math.Abs(distance.X)/480,Math.Abs(distance.Y)/360);
            // projection owns the camera chart; don't feed an unfolded point through it again
            return JKRuntime.FrameComposition.ProjectWorld(target+new Vector2(9,13));
        }
        private static bool Draw(object __instance)
        {
            if(!AdvancedSession.Connected) return true;
            if(JumpKingMultiplayer.ModEntry.Preferences==null || !JumpKingMultiplayer.ModEntry.Preferences.IsProximityPlayersEnabled || GameLoop.m_player==null || GameLoop.m_player.m_body==null) return false;
            var manager=MultiplayerManager.instance;if(manager==null) return false;
            int range=ReadThreshold(__instance);
            var observer=GameLoop.m_player.m_body.Position;
            foreach(var ghost in ReadPlayers(manager)) {
                Vector2 target;Color tint;
                if(!TryTarget(ghost,AdvancedSession.CurrentMap,out target,out tint)) continue;
                float screens;Vector2 projected=ProjectTarget(observer,target,out screens);
                if(screens>range) continue;
                Vector2 position;float rotation;
                if(!Place(projected,out position,out rotation)) continue;
                var sprite=Game1.instance.contentManager.gui.ArrowLeft;
                var color=tint*Math.Max(.14f,.8f-.22f*screens);
                Game1.spriteBatch.Draw(sprite.texture,position,sprite.source,color,rotation,
                    new Vector2(sprite.source.Width*.5f,sprite.source.Height*.5f),2,Microsoft.Xna.Framework.Graphics.SpriteEffects.None,0);
            }
            return false;
        }
    }
}
