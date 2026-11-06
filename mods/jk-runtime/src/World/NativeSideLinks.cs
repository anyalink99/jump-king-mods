using System;
using System.Collections.Generic;
using System.Linq;
using JumpKing.Level;

namespace JKRuntime.World
{
    public sealed class WorldSideLink
    {
        [WorldField] public int Screen, Side, Target;
        public WorldSideLink() { }
        public WorldSideLink(int screen,int side,int target) {Screen=screen;Side=side;Target=target;}
    }

    public static partial class NativeSideLinks
    {
        private sealed class State {[WorldField] public WorldSideLink[] Links;}
        private static string owner;
        private static Action<int,int,int> updateOwned;
        /// <summary>Updates one exit in the current ownership lease, retaining its teardown and shared-state bookkeeping.</summary>
        public static void UpdateOwned(string id, int screen, int side, int target)
        {
            RuntimeApi.Kernel.CheckThread();
            if(owner!=id||updateOwned==null)throw new InvalidOperationException("Native side links aren't owned by "+id);
            updateOwned(screen,side,target);
        }
        public static void SetPair(TeleportLink[] native,int left,int right)
        {
            RuntimeApi.Kernel.CheckThread();
            if(native==null||native.Length!=2||left< -1||right< -1)throw new ArgumentException("Invalid native side links");
            if(left>0)native[0]=new TeleportLink(left);if(right>0)native[1]=new TeleportLink(right);
            Geometry.MapTopology.Invalidate();
        }
        /// <summary>Owns authored native links. atlasSide=0 requires an exact content count; positive values require a square screen array.</summary>
        public static IDisposable Apply(string id,IEnumerable<WorldSideLink> values,int contentScreens,int atlasSide)
        {
            RuntimeApi.Kernel.CheckThread();ModuleDefinition.ValidId(id);
            if(owner!=null)throw new InvalidOperationException("Native side links already owned by "+owner);
            var screens=(LevelScreen[])ForeignMembers.Field(typeof(LevelManager),"m_screens").GetValue(null);
            // zero means an exact-count loader rather than a padded square atlas
            if(contentScreens<1||atlasSide<0||atlasSide>1000||screens==null||screens.Length!=(atlasSide==0?contentScreens:atlasSide*atlasSide)||contentScreens>screens.Length)
                throw new ArgumentException("Native screen topology differs from authored content");
            var links=values.ToArray();Validate(links,contentScreens);
            var originals=links.Select(l=>screens[l.Screen].teleport[l.Side]).ToArray();
            var written=new TeleportLink[links.Length];
            var scope=new RuntimeScope();owner=id;
            scope.Defer(delegate {
                for(int i=0;i<links.Length;i++){var link=links[i];if(ReferenceEquals(screens[link.Screen].teleport[link.Side],written[i]))screens[link.Screen].teleport[link.Side]=originals[i];}
                owner=null;updateOwned=null;
                Geometry.MapTopology.Invalidate();
            });
            Action<State> apply=state=>{
                for(int i=0;i<links.Length;i++) {var link=state.Links[i];written[i]=new TeleportLink(link.Target);screens[link.Screen].teleport[link.Side]=written[i];}
                Geometry.MapTopology.Invalidate();
            };
            updateOwned=(screen,side,target)=>{
                int index=Array.FindIndex(links,l=>l.Screen==screen&&l.Side==side);
                if(index<0||target<1||target>contentScreens)throw new ArgumentException("Exit isn't owned or destination is outside authored content");
                written[index]=new TeleportLink(target);screens[screen].teleport[side]=written[index];
                Geometry.MapTopology.Invalidate();
            };
            try {
                apply(new State{Links=links});
                scope.Own(WorldRegistry.Shared.Bind<State>("native.side-links",()=>new State{Links=links.Select(l=>new WorldSideLink(l.Screen,l.Side,screens[l.Screen].teleport[l.Side].GetIndex1())).ToArray()},apply,state=>{
                    Validate(state.Links,contentScreens);
                    if(state.Links.Length!=links.Length||state.Links.Where((l,i)=>l.Screen!=links[i].Screen||l.Side!=links[i].Side).Any())throw new ArgumentException("Different side-link ownership");
                }));
                return scope;
            }catch{scope.Dispose();throw;}
        }
        private static void Validate(WorldSideLink[] links,int screens)
        {
            if(links==null||links.Length>512||links.Any(l=>l==null||l.Screen<0||l.Screen>=screens||l.Side<0||l.Side>1||l.Target<1||l.Target>screens)
                ||links.Select(l=>l.Screen*2+l.Side).Distinct().Count()!=links.Length)throw new ArgumentException("Invalid authored side links");
        }
    }
}
