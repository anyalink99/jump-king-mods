using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JumpKing;
using JumpKing.JKMemory;
using JumpKing.JKMemory.KingSpriteLayers;
using JumpKing.Player;
using JumpKing.Player.Skins;
using JumpKing.MiscEntities.WorldItems;
using JumpKingMultiplayer;
using JumpKingMultiplayer.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Steamworks;

namespace MultiplayerExpansion
{
    internal static class RemotePresentation
    {
        internal static long Updates, Draws;
        internal static ulong LastEquipment;
        private static readonly Type ghost = typeof(MultiplayerManager).Assembly.GetType("JumpKingMultiplayer.Models.GhostPlayer", true);
        private static readonly Type extensions = typeof(MultiplayerManager).Assembly.GetType("JumpKingMultiplayer.Extensions.PlayerSpriteStateExtensions", true);
        private static readonly MethodInfo fromPlayer = AccessTools.Method(extensions, "FromPlayer");
        private static readonly PropertyInfo tracker = AccessTools.Property(ghost, "tracker"), trackerData=AccessTools.Property(tracker.PropertyType,"data");
        private static readonly MethodInfo tag = AccessTools.Method(ghost, "DrawTag");
        private static readonly MethodInfo outsideDraw = AccessTools.Method(ghost,"DrawFromOutside");
        private static readonly FieldInfo alreadyDrawn=AccessTools.Field(typeof(MultiplayerManager),"_alreadyDrawedPlayers");
        private static readonly Type trackerType = ghost.GetNestedType("Tracker",BindingFlags.Public|BindingFlags.NonPublic);
        private static readonly FieldInfo trackedPlayer = AccessTools.Field(trackerType,"player");
        private static readonly FieldInfo synchronousReplay=AccessTools.Field(trackerType,"enableSync");
        private static readonly Type skins = typeof(Game1).Assembly.GetType("JumpKing.Player.Skins.SkinManager", true);
        private static readonly FieldInfo applied = AccessTools.Field(skins, "m_applied_skins"), art = AccessTools.Field(skins, "m_king_sprites"), settings = AccessTools.Field(skins, "m_settings");
        private static readonly Type layered = typeof(Game1).Assembly.GetType("JumpKing.XnaWrappers.LayeredSprite", true);
        private static readonly PropertyInfo layers = AccessTools.Property(layered, "Sprites");
        // multiplayer's pose enum has a different order from native sprite keys
        private static readonly int[] poseKeys = {7,5,6,8,0,1,2,3,4,12,9,10,11};
        private static readonly SkinLayer[] layerOrder = {SkinLayer.Cape,SkinLayer.Boots,SkinLayer.Shirt,SkinLayer.SnakeRing,SkinLayer.Hat};
        internal static void Install(Harmony hooks)
        {
            hooks.Patch(AccessTools.Method(ghost,"Update"), prefix:new HarmonyMethod(typeof(RemotePresentation),"Update"), transpiler:new HarmonyMethod(typeof(RemotePresentation),"WorldInterpolation"));
            hooks.Patch(AccessTools.Method(ghost,"DrawFromOutside"), prefix:new HarmonyMethod(typeof(RemotePresentation),"Draw"));
            hooks.Patch(AccessTools.Method(typeof(MultiplayerManager),"DrawPlayers"),prefix:new HarmonyMethod(typeof(RemotePresentation),"LegacyDraw"));
            hooks.Patch(AccessTools.Method(trackerType,"Track"), prefix:new HarmonyMethod(typeof(RemotePresentation),"Track"));
            hooks.Patch(AccessTools.Method(typeof(Game1),"Draw"),prefix:new HarmonyMethod(typeof(RemotePresentation),"BeginFrame"));
            hooks.Patch(AccessTools.Method(typeof(JumpKing.GameManager.GameLoop),"Draw"),prefix:new HarmonyMethod(typeof(RemotePresentation),"BeforeMenu"));
        }
        internal static void BeginFrame()
        { AdvancedSession.BeginPresentation();if(MultiplayerManager.instance!=null) alreadyDrawn.SetValue(MultiplayerManager.instance,false); }
        private static void BeforeMenu()
        {
            // the original end-of-frame draw is skipped during pause; draw under the menu
            if(AdvancedSession.Connected && MultiplayerManager.instance!=null) MultiplayerManager.DrawPlayers();
        }
        private static bool LegacyDraw()
        {
            // rayman walls call this inside every atlas screen; the world layer owns those draws
            return !AdvancedSession.Connected || !JKRuntime.FrameComposition.InWorldPass && !JKRuntime.FrameComposition.HasExternalPresentation;
        }
        internal static void WorldDraw()
        {
            if(!AdvancedSession.Connected)return;
            foreach(var player in PlayerIndicators.ReadPlayers(MultiplayerManager.instance)) {
                if(player.IsDisposed)continue;
                if(!Draw(player))continue;
                player.RelativePosition=JKRuntime.FrameComposition.ProjectWorld(player.AbsolutePosition+new Vector2(9,26));
                outsideDraw.Invoke(player,null);
            }
        }
        private static bool Track(object __instance)
        {
            // paused entities don't Update, so don't feed their unused legacy replay queue
            if(Peer(trackedPlayer.GetValue(__instance)) != null) return false;
            var queue=(IList)trackerData.GetValue(__instance,null);
            bool synchronous=(bool)synchronousReplay.GetValue(null);
            lock(queue) {
                // live ghosts need the newest pose, not a replay of a pause-long backlog
                int keep=synchronous ? 31 : 0;
                while(queue.Count>keep) queue.RemoveAt(0);
            }
            return true;
        }
        private static IEnumerable<CodeInstruction> WorldInterpolation(IEnumerable<CodeInstruction> code)
        {
            var screen=AccessTools.PropertyGetter(ghost,"RelativePosition");
            var world=AccessTools.PropertyGetter(ghost,"AbsolutePosition");
            foreach(var instruction in code) {
                // the native targets are world coordinates, even when the camera moves
                if(instruction.Calls(screen)) instruction.operand=world;
                yield return instruction;
            }
        }
        internal static void Capture(PlayerEntity player, InteractionFrame frame)
        {
            if (player != null)
            {
                var pose = (TrackData)fromPlayer.Invoke(null, new object[]{player});
                frame.Pose = (byte)pose.sprite; frame.Flip = (byte)pose.flip;
            }
            var worn = applied.GetValue(null) as List<Skin>;
            if (worn != null) foreach (var item in worn) if ((int)item.item >= 0 && (int)item.item < 18) frame.Equipment |= 1UL << (int)item.item;
        }
        private static InteractionPeer Peer(object instance) { return AdvancedSession.VisualPeer(((IGhostPlayerData)instance).SteamId.m_SteamID); }
        private static bool Update(object __instance)
        {
            var peer = Peer(__instance);
            var data=(IGhostPlayerData)__instance;
            if (peer == null || data.IsDisposed) return true;
            var tracking = tracker.GetValue(__instance,null);
            var queued = (IList)trackerData.GetValue(tracking,null);
            queued.Clear();
            var frame = AdvancedSession.Presented(peer);
            Updates++;
            var canonical=JKRuntime.Geometry.MapTopology.Normalize(frame.Position,frame.Width,frame.Height);
            data.AbsolutePosition=canonical;
            data.RelativePosition=JKRuntime.FrameComposition.ProjectWorld(canonical+new Vector2(9,26));
            return false;
        }
        internal static Sprite BaseSprite(Sprite sprite)
        {
            // never borrow the receiver's equipped layers for another player
            for (int i=0; i<8 && sprite != null && layered.IsInstanceOfType(sprite); i++)
            { var list=(IList)layers.GetValue(sprite,null); sprite=list.Count == 0 ? null : list[0] as Sprite; }
            return sprite;
        }
        private static bool Draw(object __instance)
        {
            var peer=Peer(__instance); if(peer==null) return true;
            var frame=AdvancedSession.VisualFrame(peer);
            if(!frame.Active && frame.Presence!=PlayerPresence.Teleporting) return false;
            Draws++; LastEquipment=frame.Equipment;
            if(frame.Map != AdvancedSession.CurrentMap) return false;
            var canonical=JKRuntime.Geometry.MapTopology.Normalize(frame.Position,frame.Width,frame.Height);
            Vector2 position=JKRuntime.FrameComposition.ProjectWorld(canonical+new Vector2(9,26));
            var data=(IGhostPlayerData)__instance;
            data.AbsolutePosition=canonical;
            data.RelativePosition=position;
            var prefs=ModEntry.Preferences;
            int display=(int)prefs.GhostPlayerDisplayType;
            Color color=(display >= 2 ? data.Color : Color.White)*prefs.GhostPlayerOpacity;
            var key=(Regular.SpriteKey)poseKeys[frame.Pose];
            var effect=frame.Flip==1 ? SpriteEffects.FlipHorizontally : frame.Flip==2 ? SpriteEffects.FlipVertically : SpriteEffects.None;
            DrawSprite(BaseSprite(Game1.instance.contentManager.playerSprites._CurrentSprites.regular.GetSprite(key)),position,effect,color);
            var available=art.GetValue(null) as Dictionary<Items,KingSprites>;
            var definition=(SkinSettings)settings.GetValue(null);
            if(available!=null && definition.skins!=null)
                foreach(var layer in layerOrder)
                    foreach(var skin in definition.skins)
                    {
                        KingSprites sprites; int item=(int)skin.item;
                        if(item>=0 && item<18 && (frame.Equipment & (1UL<<item))!=0 && skin.layers.Length>0 && skin.layers[0]==layer && available.TryGetValue(skin.item,out sprites))
                            DrawSprite(sprites.regular.GetSprite(key),position,effect,color);
                    }
            if(display==1 || display==3) tag.Invoke(__instance,null);
            return false;
        }
        private static void DrawSprite(Sprite sprite,Vector2 position,SpriteEffects effect,Color color)
        {
            if(sprite==null || sprite.texture==null || sprite.texture.IsDisposed) return;
            // draw with the remote tint without mutating shared native sprite state
            Game1.spriteBatch.Draw(sprite.texture,SpritePosition(sprite,position),sprite.source,color,0,Vector2.Zero,1,effect,0);
        }
        internal static Vector2 SpritePosition(Sprite sprite,Vector2 position)
        {
            // native Sprite.Draw snaps after applying the anchor, including negative coords
            return (position-sprite.source.Size.ToVector2()*sprite.center).ToPoint().ToVector2();
        }
    }
}
