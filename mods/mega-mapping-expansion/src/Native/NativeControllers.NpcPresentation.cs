using System;
using BehaviorTree;
using EntityComponent;
using EntityComponent.BT;
using JumpKing;
using JumpKing.Util.DrawBT;
using JumpKing.MiscEntities.OldMan;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion.Construction
{
    internal sealed partial class NativeControllers
    {
        // freeze the selected quote when a conversation starts, not halfway through a line
        private static bool BeforeQuote(FetchQuote __instance,ref BTresult __result)
        {
            var owner=Current;var npc=owner==null?null:owner.Bind(__instance.game_object);if(npc==null)return true;
            npc.QuoteLines=null;if(!owner.Flag(npc.Id,"quoteOverride"))return true;
            npc.QuoteLines=owner.Text(npc.Id,"quote").Replace("\r","").Split('\n');
            new SetBBKeyNode<OldManQuote>(__instance.game_object,"BB_QUOTE_KEY",new OldManQuote{lines=npc.QuoteLines}).Run(new TickData(0,0));
            __result=BTresult.Success;return false;
        }
        private static bool BeforeQuoteLine(TargetLine __instance,int ___m_index,ref BTresult __result)
        {
            var owner=Current;var npc=owner==null?null:owner.Bind(__instance.game_object);if(npc==null||npc.QuoteLines==null)return true;
            if(___m_index>=npc.QuoteLines.Length){npc.QuoteLines=null;__result=BTresult.Failure;return false;}
            new SetBBKeyNode<string>(__instance.game_object,"BB_LINE_KEY",SpeechBubbleFormat.FormatString(npc.QuoteLines[___m_index])).Run(new TickData(0,0));
            __result=BTresult.Success;return false;
        }
        private static void HasSceneQuote(OldManSettings ___m_settings,ref bool __result)
        {var owner=Current;Npc npc;if(owner!=null&&owner.npcs.TryGetValue(___m_settings.name,out npc)&&owner.Flag(npc.Id,"quoteOverride"))__result=true;}
        private struct NpcDrawState
        {internal ISpriteEntity Entity;internal Sprite Sprite;internal Vector2 Position;internal Npc Npc;}
        private static void BeforeNpcDraw(ISpriteEntity __instance,out NpcDrawState __state)
        {
            __state=new NpcDrawState();var owner=Current;var npc=owner==null?null:owner.Bind(__instance);if(npc==null)return;
            __state=new NpcDrawState{Entity=__instance,Sprite=__instance.sprite,Position=__instance.Position,Npc=npc};
            if(npc.DrawSprite==null)npc.DrawSprite=Sprite.CreateSpriteWithCenter(__instance.sprite.texture,__instance.sprite.source,__instance.sprite.center);
            var sprite=npc.DrawSprite;sprite.texture=__instance.sprite.texture;sprite.source=__instance.sprite.source;sprite.center=__instance.sprite.center;
            var tint=owner.scene.Properties.Read(npc.Id,"tint");var color=new Vector4((float)tint.X,(float)tint.Y,(float)tint.Z,(float)tint.W);
            sprite.SetColor(new Color(__instance.sprite.GetColor().ToVector4()*color)*(owner.Flag(npc.Id,"visible")?(float)owner.Number(npc.Id,"opacity"):0));
            __instance.SetSprite(sprite);var offset=owner.scene.Properties.Read(npc.Id,"offset");__instance.Position+=new Vector2((float)Math.Round(offset.X),(float)Math.Round(offset.Y));npc.Drawing++;
        }
        private static void AfterNpcDraw(NpcDrawState __state)
        {if(__state.Entity==null)return;__state.Entity.SetSprite(__state.Sprite);__state.Entity.Position=__state.Position;__state.Npc.Drawing--;}
        private static bool BeforeNpcText(ISpriteEntity __instance,out NpcDrawState __state)
        {
            __state=new NpcDrawState();var owner=Current;var npc=owner==null?null:owner.Bind(__instance);if(npc==null)return true;
            if(!owner.Flag(npc.Id,"textVisible"))return false;
            if(npc.Drawing==0){__state.Entity=__instance;__state.Position=__instance.Position;var offset=owner.scene.Properties.Read(npc.Id,"offset");__instance.Position+=new Vector2((float)Math.Round(offset.X),(float)Math.Round(offset.Y));}return true;
        }
        private static void AfterNpcText(NpcDrawState __state){if(__state.Entity!=null)__state.Entity.Position=__state.Position;}
    }
}
