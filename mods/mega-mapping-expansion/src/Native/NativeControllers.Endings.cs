using System;
using System.Collections.Generic;
using System.Reflection;
using JKRuntime;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion.Construction
{
    internal sealed partial class NativeControllers
    {
        private readonly Dictionary<Type,string> endingIds=new Dictionary<Type,string>();
        private void PrepareEndings()
        {
            var ids=Ids("ending");if(ids.Length==0)return;
            foreach(string id in ids)
            {
                if(Number(id,"screen")>JumpKing.Level.LevelManager.TotalScreens)throw new System.IO.InvalidDataException(id+": ending screen is outside the map");
                string role=Text(id,"role");string name=role=="main"?"NormalEnding.NormalEnding":role=="nbp"?"NewBabePlusEnding.NewBabePlusEnding":"OwlEnding.OwlEnding";
                var type=typeof(Game1).Assembly.GetType("JumpKing.GameManager.MultiEnding."+name,true);
                var method=type.GetMethod("CheckWin",new[]{typeof(PlayerEntity)});
                if(method==null||method.ReturnType!=typeof(bool))throw new NotSupportedException("Native ending check contract changed: "+role);
                endingIds.Add(type,id);
                PreparedNativeHooks.Acquire("mega-mapping-expansion.win-conditions."+role,resources,hooks=>hooks.Add(method,postfix:Callback("AfterCheckWin")));
            }
        }
        internal static bool CombineWin(string mode,bool native,bool authored)
        {return mode=="and"?native&&authored:mode=="or"?native||authored:authored;}
        private static void AfterCheckWin(object __instance,PlayerEntity p_player,ref bool __result)
        {
            var owner=Current;string id;
            if(owner==null||!owner.active||JKRuntime.World.WorldControl.Following||!owner.endingIds.TryGetValue(__instance.GetType(),out id))return;
            owner.scene.ComposeCurrentValues();
            bool condition=owner.Flag(id,"condition");int screen=(int)owner.Number(id,"screen");
            if(screen!=0&&screen!=Camera.CurrentScreenIndex1)condition=false;
            if(owner.Flag(id,"requireGrounded")&&!p_player.m_body.IsOnGround)condition=false;
            var size=owner.scene.Properties.Read(id,"size");var position=owner.scene.Properties.Read(id,"position");
            if(size.X!=0||size.Y!=0)
            {
                condition=condition&&size.X>=1&&size.Y>=1;
                int localScreen=screen==0?Camera.CurrentScreenIndex1:screen;
                var area=new Rectangle((int)position.X,(int)position.Y-(localScreen-1)*360,(int)size.X,(int)size.Y);
                condition=condition&&area.Intersects(p_player.m_body.GetHitbox());
            }
            __result=CombineWin(owner.Text(id,"mode"),__result,condition);
        }
    }
}
