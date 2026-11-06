using System;
using System.Collections;
using System.Reflection;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace JKRuntime.Presentation
{
    public static class AppearanceGeometry
    {
        private static Type resizer;
        private static PropertyInfo active,width,height;
        private static int assemblyCount=-1;
        private static readonly MethodInfo Draw=typeof(PlayerEntity).GetMethod("Draw");
        private static long patchEpoch=-1;
        private static bool cachedCustom,cachedUnknown;
        /// <summary>Resolve adapters before player activation. Does not attach behavior or retain a player.</summary>
        public static void Prepare()
        {
            RuntimeApi.Kernel.CheckThread();
            var assemblies=AppDomain.CurrentDomain.GetAssemblies();
            if(assemblies.Length==assemblyCount)return;
            assemblyCount=assemblies.Length;
            patchEpoch=-1;
            foreach(var assembly in assemblies)
            {
                var type=assembly.GetType("HitboxResizer.Patches.PrefixPlayerEntityDraw");
                if(type==null)continue;
                resizer=type;active=type.GetProperty("IsCustomHitbox");width=type.GetProperty("Width");height=type.GetProperty("Height");
            }
        }
        public static VisualGeometry Resolve(PlayerEntity player)
        {
            RuntimeApi.Kernel.CheckThread();if(player==null||player.m_body==null)throw new ArgumentNullException("player");
            var result=new VisualGeometry {AnchorOffset=new Vector2(9,26),CenterOffset=new Vector2(9,13),CollisionBounds=player.m_body.GetHitbox(),Coverage=AppearanceCoverage.Exact};
            bool custom=cachedCustom,unknown=cachedUnknown;
            if(patchEpoch!=MethodValidity.MutationEpoch) {
            custom=unknown=false;
            try {
                var engine=OwnedPatches.SharedEngine();
                var info=engine.GetType("HarmonyLib.Harmony",true).GetMethod("GetPatchInfo",new[]{typeof(MethodBase)}).Invoke(null,new object[]{Draw});
                if(info!=null)foreach(string kind in new[]{"Prefixes","Postfixes","Transpilers","Finalizers"})
                    foreach(var patch in (IEnumerable)info.GetType().GetField(kind).GetValue(info))
                    {
                        var method=patch.GetType().GetProperty("PatchMethod").GetValue(patch,null) as MethodInfo;
                        string owner=(string)patch.GetType().GetField("owner").GetValue(patch);
                        if(method!=null && method.DeclaringType==resizer && method.Name=="Prefix")custom=true;
                        else if(owner!="WardrobePlus.Presentation" && owner!="jk-runtime.player-visuals")unknown=true;
                    }
            } catch {unknown=true;}
            cachedCustom=custom;cachedUnknown=unknown;patchEpoch=MethodValidity.MutationEpoch;
            }
            if(custom && active!=null && width!=null && height!=null && (bool)active.GetValue(null,null))
            {
                int w=(int)width.GetValue(null,null),h=(int)height.GetValue(null,null);
                if(w>0&&h>0&&w<=4096&&h<=4096){result.AnchorOffset=new Vector2(w/2,h);result.CenterOffset=new Vector2(w/2,h*.5f);}
                else unknown=true;
            }
            if(unknown){result.Coverage=AppearanceCoverage.Approximate;result.Reason="Player Draw has an unreviewed or unavailable patch graph";}
            return result;
        }
    }
}
