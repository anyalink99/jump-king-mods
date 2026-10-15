using System;
using System.Collections.Generic;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace JKRuntime.Geometry
{
    /// <summary>One image of a native screen in an unfolded map. Loops can repeat a screen at different offsets.</summary>
    public sealed class TopologyScreen
    {
        public int Screen { get; private set; }
        public Vector2 Offset { get; private set; }
        public int Depth { get; private set; }
        internal TopologyScreen(int screen,Vector2 offset,int depth) { Screen=screen;Offset=offset;Depth=depth; }
    }

    /// <summary>A bounded unfolding. Truncated means more images exist beyond the requested budget.</summary>
    public sealed class TopologyLayout
    {
        public TopologyScreen[] Screens { get; private set; }
        public bool Truncated { get; private set; }
        internal TopologyLayout(TopologyScreen[] screens,bool truncated) { Screens=screens;Truncated=truncated; }
    }

    /// <summary>Native map coordinates and seamless side links shared by physics and presentation.</summary>
    public static class MapTopology
    {
        private struct ImageKey : IEquatable<ImageKey>
        {
            private readonly int screen,x,y;
            internal ImageKey(int index,Vector2 offset) {screen=index;x=(int)offset.X;y=(int)offset.Y;}
            public bool Equals(ImageKey other) {return screen==other.screen && x==other.x && y==other.y;}
            public override bool Equals(object other) {return other is ImageKey && Equals((ImageKey)other);}
            public override int GetHashCode() {unchecked{return (screen*397^x)*397^y;}}
        }
        private static LevelScreen[] observed;
        private static LevelScreen[] mapScreens;
        private static TopologyMap map;
        private static readonly Dictionary<int,TopologyLayout> layouts=new Dictionary<int,TopologyLayout>();
        private static LevelScreen[] Current
        {
            get {
                var screens=NativeWorldGeometry.LoadedScreens;
                if(!ReferenceEquals(observed,screens)) { Invalidate();observed=screens; }
                return screens;
            }
        }
        /// <summary>Drop observed edge geometry after a world update. Native link targets are always read live.</summary>
        public static void Invalidate() { TopologyOpenings.Reset();layouts.Clear();map=null;mapScreens=null; }
        internal static void ClearWorld() { observed=null;Invalidate(); }
        public static int ScreenAt(float y,int count)
        { return count<=0 ? -1 : Math.Max(0,Math.Min(count-1,(int)Math.Floor((360-y)/360))); }

        public static int Destination(LevelScreen[] screens,int source,bool left,float y=float.NaN)
        {
            if(screens==null || source<0 || source>=screens.Length || screens[source]==null) return -1;
            int index=NativeDestination(screens,source,left);
            return index>=0 ? index : ExpansionPortals.Destination(screens,source,left,y);
        }
        internal static int NativeDestination(LevelScreen[] screens,int source,bool left)
        {
            var links=screens[source].teleport;
            TeleportLink first=links!=null && links.Length>0 ? links[0] : null, second=links!=null && links.Length>1 ? links[1] : null;
            var link=first!=null && first.IsEnabled && second!=null && second.IsEnabled
                ? (left ? first : second) : first!=null && first.IsEnabled ? first : second;
            int index=link==null || !link.IsEnabled ? -1 : link.GetIndex0();
            return index>=0 && index<screens.Length ? index : -1;
        }
        private static Vector2 ExitShift(LevelScreen[] screens,int source,int target,bool left)
        {
            // MultiWarp deliberately stops 0.2 px inside the next edge
            float width=NativeDestination(screens,source,left)==target ? 480 : 479.8f;
            return new Vector2(left ? width : -width,(source-target)*360);
        }
        public static bool Connects(LevelScreen[] screens,int source,bool left,float y,int destination)
        {
            return screens!=null && source>=0 && source<screens.Length && screens[source]!=null && destination>=0 && destination<screens.Length
                && (Destination(screens,source,left,y)==destination || ExpansionPortals.Connects(screens,source,left,y,destination));
        }
        /// <summary>A rendering hint from known solid edge geometry, not proof that a player can fit.</summary>
        public static int PreviewDestination(LevelScreen[] screens,int source,bool left,float y=float.NaN)
        { return TopologyOpenings.Destination(screens,source,left,y); }

        public static TopologyLayout Unfold(LevelScreen[] screens,int anchor,int maxDepth=8,int maxImages=256)
        {
            if(screens==null || anchor<0 || anchor>=screens.Length) throw new ArgumentOutOfRangeException("anchor");
            if(maxDepth<0 || maxDepth>64 || maxImages<1 || maxImages>8192) throw new ArgumentOutOfRangeException("maxImages");
            var images=new List<TopologyScreen>();var seen=new HashSet<ImageKey>();bool truncated=false;
            images.Add(new TopologyScreen(anchor,Vector2.Zero,0));seen.Add(new ImageKey(anchor,Vector2.Zero));
            for(int i=0;i<images.Count;i++) {
                var image=images[i];
                if(image.Screen>0 && TopologyOpenings.Vertical(screens,image.Screen-1)) AddImage(images,seen,image.Screen-1,image.Offset,image.Depth+1,maxDepth,maxImages,ref truncated);
                if(image.Screen+1<screens.Length && TopologyOpenings.Vertical(screens,image.Screen)) AddImage(images,seen,image.Screen+1,image.Offset,image.Depth+1,maxDepth,maxImages,ref truncated);
                for(int side=0;side<2;side++) {
                    bool left=side==0;
                    if(screens[image.Screen]==null || !TopologyOpenings.OpenSide(screens,image.Screen,left)) continue;
                    int native=NativeDestination(screens,image.Screen,left);
                    if(native>=0) AddSide(images,seen,image,native,left,maxDepth,maxImages,ref truncated);
                    else if(ExpansionPortals.HasLinks(screens[image.Screen]))
                        foreach(int target in ExpansionPortals.Targets(screens,image.Screen,left))
                            AddSide(images,seen,image,target,left,maxDepth,maxImages,ref truncated);
                }
            }
            return new TopologyLayout(images.ToArray(),truncated);
        }
        private static void AddSide(List<TopologyScreen> images,HashSet<ImageKey> seen,TopologyScreen image,int target,bool left,int maxDepth,int maxImages,ref bool truncated)
        {
            AddImage(images,seen,target,image.Offset+new Vector2(left ? -480 : 480,(target-image.Screen)*360),image.Depth+1,maxDepth,maxImages,ref truncated);
        }
        private static void AddImage(List<TopologyScreen> images,HashSet<ImageKey> seen,int screen,Vector2 offset,int depth,int maxDepth,int maxImages,ref bool truncated)
        {
            var key=new ImageKey(screen,offset);
            if(seen.Contains(key)) return;
            if(depth>maxDepth || images.Count>=maxImages) {truncated=true;return;}
            seen.Add(key);images.Add(new TopologyScreen(screen,offset,depth));
        }

        /// <summary>Contiguous native screens without a known solid boundary between them. Does not execute mod collision callbacks.</summary>
        public static void VerticalRange(LevelScreen[] screens,int anchor,out int first,out int last)
        {
            if(screens==null || anchor<0 || anchor>=screens.Length || screens[anchor]==null) throw new ArgumentOutOfRangeException("anchor");
            first=last=anchor;
            while(first>0 && TopologyOpenings.Vertical(screens,first-1)) first--;
            while(last+1<screens.Length && TopologyOpenings.Vertical(screens,last)) last++;
        }
        /// <summary>Build the default spatial map for the current geometry observation. Loops are reported without moving already placed regions.</summary>
        public static TopologyMap BuildMap(LevelScreen[] screens)
        {
            if(screens==null)throw new ArgumentNullException("screens");
            if(map==null || !ReferenceEquals(mapScreens,screens)) {map=new TopologyMap(screens);mapScreens=screens;}
            return map;
        }
        public static bool TryChartPosition(Vector2 observer,Vector2 target,out Vector2 position)
        {var screens=Current;position=target;return screens!=null && BuildMap(screens).TryPosition(observer,target,out position);}
        public static Vector2 ChartPosition(Vector2 observer,Vector2 target)
        {Vector2 position;TryChartPosition(observer,target,out position);return position;}

        /// <summary>Express a target in the closest reachable chart to the observer. Does not move either actor.</summary>
        public static Vector2 NearestImage(LevelScreen[] screens,Vector2 observer,Vector2 target,int maxDepth=64)
        {
            if(screens==null || screens.Length==0) return target;
            int anchor=ScreenAt(observer.Y,screens.Length), targetScreen=ScreenAt(target.Y,screens.Length);
            Vector2 best=target;float distance=Vector2.DistanceSquared(best,observer);
            if(maxDepth==1) {
                // contacts only need adjacent charts, don't allocate a graph per king per tick
                for(int side=0;side<2;side++) {
                    int destination=Destination(screens,anchor,side==0,observer.Y);
                    if(destination<0 || Math.Abs(targetScreen-destination)>1) continue;
                    var candidate=target+new Vector2(side==0 ? -480 : 480,(destination-anchor)*360);
                    float d=Vector2.DistanceSquared(candidate,observer);
                    if(d<distance) {distance=d;best=candidate;}
                }
                return best;
            }
            distance=float.PositiveInfinity;
            TopologyLayout layout;
            bool shared=ReferenceEquals(screens,Current) && maxDepth==64;
            if(!shared || !layouts.TryGetValue(anchor,out layout)) {
                layout=Unfold(screens,anchor,maxDepth);if(shared) layouts[anchor]=layout;
            }
            foreach(var image in layout.Screens) if(image.Screen==targetScreen) {
                Vector2 candidate=target+image.Offset;float d=Vector2.DistanceSquared(candidate,observer);
                if(d<distance) {distance=d;best=candidate;}
            }
            return best;
        }
        public static Vector2 NearestImage(Vector2 observer,Vector2 target)
        { return NearestImage(Current,observer,target); }
        public static Vector2 NearestImage(Vector2 observer,Vector2 target,int maxDepth)
        { return NearestImage(Current,observer,target,maxDepth); }

        /// <summary>Recognize a crossed seam, including self links, without treating arbitrary teleports as continuous.</summary>
        public static bool TryCrossing(LevelScreen[] screens,Vector2 before,Vector2 after,out Vector2 shift)
        {
            shift=Vector2.Zero;if(screens==null || screens.Length==0) return false;
            bool left=before.X<120 && after.X>360, right=before.X>360 && after.X<120;
            if(!left && !right) return false;
            int source=ScreenAt(before.Y,screens.Length),target=ScreenAt(after.Y,screens.Length);
            if(!Connects(screens,source,left,before.Y+13,target)) return false;
            shift=ExitShift(screens,source,target,left);
            if(Vector2.DistanceSquared(before+shift,after)>64*64) {shift=Vector2.Zero;return false;}
            return true;
        }
        public static bool TryCrossing(Vector2 before,Vector2 after,out Vector2 shift)
        { return TryCrossing(Current,before,after,out shift); }

        /// <summary>Normalize a body through known side seams. Bounds may straddle an open seam.</summary>
        public static Vector2 Normalize(Vector2 position,int width=18,int height=26)
        { return Normalize(Current,position,width,height); }
        public static Vector2 Normalize(LevelScreen[] screens,Vector2 position,int width=18,int height=26)
        { return Normalize(screens,position,position+new Vector2(width/2f,height/2f),width); }
        /// <summary>Keep an attached actor on its carrier's route, even if the actor is outside the portal's height band.</summary>
        public static Vector2 Normalize(Vector2 position,Vector2 routeAnchor,int width=18)
        { return Normalize(Current,position,routeAnchor,width); }
        public static Vector2 Normalize(LevelScreen[] screens,Vector2 position,Vector2 routeAnchor,int width=18)
        {
            if(screens==null || screens.Length==0) return position;
            for(int i=0;i<8 && (position.X+width/2f<0 || position.X+width/2f>480);i++) {
                int source=ScreenAt(routeAnchor.Y,screens.Length);
                bool left=position.X+width/2f<0;int target=Destination(screens,source,left,routeAnchor.Y);
                if(target<0) break;
                var shift=ExitShift(screens,source,target,left);
                position+=shift;routeAnchor+=shift;
            }
            return position;
        }

        /// <summary>Native blocking queries at the queried screen, independent of Camera.CurrentScreen. Game thread only.</summary>
        public static bool IsBlocked(Rectangle bounds)
        { return IsBlocked(Current,bounds); }
        /// <summary>Use a carrier's route for the entire attached body when a portal has a height band.</summary>
        public static bool IsBlocked(Rectangle bounds,Vector2 routeAnchor)
        { return IsBlocked(Current,bounds,routeAnchor); }
        public static bool IsBlocked(LevelScreen[] screens,Rectangle bounds)
        { return IsBlocked(screens,bounds,bounds.Center.ToVector2()); }
        public static bool IsBlocked(LevelScreen[] screens,Rectangle bounds,Vector2 routeAnchor)
        {
            if(screens==null || screens.Length==0) return true;
            int source=ScreenAt(bounds.Center.Y,screens.Length);
            if(bounds.Left<0 || bounds.Right>480) {
                int route=ScreenAt(routeAnchor.Y,screens.Length);
                bool left=bounds.Left<0;int target=Destination(screens,route,left,routeAnchor.Y);
                if(target<0) return true;
                var part=left ? new Rectangle(bounds.Left,bounds.Top,Math.Min(0,bounds.Right)-bounds.Left,bounds.Height)
                    : new Rectangle(Math.Max(480,bounds.Left),bounds.Top,bounds.Right-Math.Max(480,bounds.Left),bounds.Height);
                part.Offset(left ? 480 : -480,(route-target)*360);
                if(Query(screens,part,target)) return true;
                int l=Math.Max(0,bounds.Left),r=Math.Min(480,bounds.Right);
                if(r<=l) return false;
                bounds=new Rectangle(l,bounds.Top,r-l,bounds.Height);
            }
            return Query(screens,bounds,source);
        }
        private static bool Query(LevelScreen[] screens,Rectangle bounds,int screen)
        {
            for(int i=Math.Max(0,screen-1);i<=Math.Min(screens.Length-1,screen+1);i++) {
                Rectangle overlap;AdvCollisionInfo info;
                if(screens[i]!=null && screens[i].TryCollision(bounds,out overlap,out info)) return true;
            }
            return false;
        }
    }
}
