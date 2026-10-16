using System;
using System.Linq;
using System.Reflection;
using JKRuntime.Geometry;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace JKRuntime
{
    internal static class MapTopologyTests
    {
        private static int checks;
        private static void Check(bool value,string message) {checks++;if(!value) throw new Exception(message);}
        private static LevelScreen Screen(int index,int link,params IBlock[] blocks)
        { return new LevelScreen(index,blocks,new LevelScreen.Graphics(),false,link<0 ? new TeleportLink[0] : new[]{new TeleportLink(link+1)},0,null); }
        public static int Main()
        {
            try {
                var self=new[]{Screen(0,0)};
                var layout=MapTopology.Unfold(self,0,3,32);
                Check(layout.Truncated && layout.Screens.Length==7,"Self loops must unfold into repeated images with a bounded frontier");
                Check(layout.Screens.Any(s=>s.Screen==0 && s.Offset.X==1440) && layout.Screens.Any(s=>s.Offset.X==-1440),"Both self-link directions retain their winding");
                Check(MapTopology.NearestImage(self,new Vector2(5,100),new Vector2(470,100)).X==-10,"Self loop chooses the nearby image across the seam");
                Vector2 shift;
                Check(MapTopology.TryCrossing(self,new Vector2(470,100),new Vector2(-6,100),out shift) && shift.X==-480,"Right self crossing is continuous");
                Check(MapTopology.TryCrossing(self,new Vector2(-8,100),new Vector2(468,100),out shift) && shift.X==480,"Left self crossing is continuous");
                Check(!MapTopology.TryCrossing(self,new Vector2(470,100),new Vector2(20,200),out shift),"A discontinuous relocation isn't a seamless wrap");
                Check(MapTopology.Normalize(self,new Vector2(479,100)).X==-1,"Normalize waits for the center and preserves seam straddling");
                Check(!MapTopology.IsBlocked(self,new Rectangle(475,100,18,26)),"An open self seam mustn't block a passenger");
                var loop=new[]{Screen(0,2),Screen(1,-1),Screen(2,0)};
                Check(MapTopology.TryCrossing(loop,new Vector2(470,100),new Vector2(-6,-620),out shift) && shift==new Vector2(-480,-720),"Cross-screen seam rebases both coordinates");
                Check(MapTopology.Unfold(loop,0,6,20).Screens.Length<=20,"Cyclic layouts respect the image budget");
                BranchLayout();
                DefaultMap();
                var far=Enumerable.Range(0,10).Select(i=>Screen(i,-1)).ToArray();
                far[9]=Screen(9,-1,new BoxBlock(new Rectangle(100,-3200,40,40)));
                Check(MapTopology.IsBlocked(far,new Rectangle(110,-3190,18,26)),"Placement queries the target screen even when the camera is elsewhere");
                Check(!MapTopology.IsBlocked(far,new Rectangle(200,-3190,18,26)),"Remote screen collision doesn't invent a wall");
                far[0]=Screen(0,9);MapTopology.Invalidate();
                far[9]=Screen(9,-1,new BoxBlock(new Rectangle(0,-3140,8,80)));
                Check(MapTopology.IsBlocked(far,new Rectangle(475,100,18,26)),"Seam query checks destination terrain after rebasing");
                Check(MapTopology.IsBlocked(new[]{Screen(0,-1)},new Rectangle(475,100,18,26)),"Unlinked map edge remains solid");
                for(int i=0;i<1000;i++) MapTopology.NearestImage(self,new Vector2(5,100),new Vector2(470,100),1);
                var timer=System.Diagnostics.Stopwatch.StartNew();int collections=GC.CollectionCount(0);
                for(int i=0;i<100000;i++) {
                    var near=MapTopology.NearestImage(self,new Vector2(5,100),new Vector2(470,100),1);
                    if(near.X!=-10) throw new Exception("Contact chart changed during repeated wraps");
                }
                timer.Stop();Check(GC.CollectionCount(0)==collections,"Adjacent chart queries allocated enough to collect during steady contacts");
                Console.WriteLine("[PERF] Map topology: 100000 adjacent contact queries in "+timer.Elapsed.TotalMilliseconds.ToString("F1")+" ms, no gen0 collections");
                FullMapWork();
                var point=new Vector2(40,50);var native=JumpKing.Camera.TransformVector2(point);bool ready=false;
                using(FrameComposition.RegisterPresenter(()=>ready,p=>p+new Vector2(17,-30))) {
                    Check(FrameComposition.ProjectWorld(point)==native,"Dormant presenter changed native overlay coordinates");
                    ready=true;Check(FrameComposition.ProjectWorld(point)==new Vector2(57,20),"Late overlay didn't use the active camera projection");
                    int draws=0;
                    using(FrameComposition.RegisterWorldDraw(()=>draws++))
                    using(new FrameComposition.WorldPass(new Vector2(0,360))) {
                        Check(FrameComposition.ProjectWorld(point)==new Vector2(40,410),"World pass borrowed the late viewport projection");
                        using(new FrameComposition.WorldPass(new Vector2(480,720)))
                            Check(FrameComposition.ProjectWorld(point)==new Vector2(520,770),"Nested world projection didn't own its translation");
                        FrameComposition.DrawWorld();
                        Check(draws==1 && FrameComposition.ProjectWorld(point)==new Vector2(40,410),"Nested world pass lost its parent");
                    }
                    using(new FrameComposition.WorldPass(Vector2.Zero))FrameComposition.DrawWorld();
                    Check(draws==1 && !FrameComposition.InWorldPass,"Disposed actor registration or world pass survived its scope");
                    bool refused=false;try {FrameComposition.RegisterPresenter(()=>true,p=>p);} catch(InvalidOperationException) {refused=true;}
                    Check(refused && FrameComposition.ProjectWorld(point)==new Vector2(57,20),"Competing projection replaced its owner");
                }
                Check(!FrameComposition.HasExternalPresentation && FrameComposition.ProjectWorld(point)==native,"Projection survived its owning camera scope");
                Console.WriteLine("[OK] Map topology: "+checks+" checks (self loops, bounded winding, coordinate rebases, remote terrain and seamless bounds)");return 0;
            } catch(Exception error) {Console.Error.WriteLine(error);return 1;}
        }
        private static void BranchLayout()
        {
            var branch=Enumerable.Range(0,6).Select(i=>Screen(i,-1)).ToArray();
            branch[1]=Screen(1,4,new BoxBlock(new Rectangle(0,-360,8,360)));
            branch[4]=Screen(4,1,new BoxBlock(new Rectangle(472,-1440,8,360)),new BoxBlock(new Rectangle(0,-1088,480,8)));
            var forest=new Vector2(20,-1340);
            Check(MapTopology.NearestImage(branch,new Vector2(40,-260),forest)==new Vector2(500,-260),"A right-only entrance placed its branch on the left through a sealed wall");
            Check(MapTopology.NearestImage(branch,forest,new Vector2(40,-260))==new Vector2(-440,-1340),"Return through the left entrance didn't preserve branch orientation");
            int first,last;MapTopology.VerticalRange(branch,4,out first,out last);
            Check(first==4 && last==5,"A branch floor joined unrelated consecutive screen numbers");
            Check(!MapTopology.Unfold(branch,1,8).Screens.Any(s=>s.Screen==4 && s.Offset.X==0),"A sealed vertical seam invented a zero-offset branch image");
            var adjacent=new[]{Screen(0,1,new BoxBlock(new Rectangle(0,0,8,360))),
                Screen(1,0,new BoxBlock(new Rectangle(472,-360,8,360)),new BoxBlock(new Rectangle(0,-8,480,8)))};
            Check(MapTopology.NearestImage(adjacent,new Vector2(0,100),new Vector2(0,-260))==new Vector2(480,100),"Raw native coordinates beat the only reachable image in an adjacent branch");
        }
        private static void FullMapWork()
        {
            var native=typeof(LevelManager).GetField("m_screens",BindingFlags.Static|BindingFlags.NonPublic);
            object original=native.GetValue(null);
            var dense=Enumerable.Range(0,30).Select(index=>Screen(index,-1,
                Enumerable.Range(0,512).Select(i=>(IBlock)new BoxBlock(new Rectangle(i%60*8,-index*360+80+i/60*8,8,8))).ToArray())).ToArray();
            var observer=new Vector2(240,-15*360+100);var target=new Vector2(400,-18*360+100);
            try {
                native.SetValue(null,dense);MapTopology.Invalidate();
                MapTopology.NearestImage(observer,target);
                var method=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",Type.EmptyTypes);
                var allocated=method==null ? null : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>),method);
                long before=allocated==null ? 0 : allocated();var timer=System.Diagnostics.Stopwatch.StartNew();
                for(int i=0;i<1000;i++) {
                    MapTopology.Invalidate();
                    if(MapTopology.NearestImage(observer,target)!=target) throw new Exception("Unlinked dense map changed the target chart");
                    Vector2 chart;
                    if(!MapTopology.BuildMap(dense).TryPosition(observer,target,out chart) || chart!=target) throw new Exception("Default map lost its connected vertical column");
                }
                long bytes=allocated==null ? 0 : allocated()-before;
                Console.WriteLine("[PERF] Map topology: 1000 full updates / 30 screens / 512 blocks each = "+timer.Elapsed.TotalMilliseconds.ToString("F1")+" ms, "+bytes+" bytes");
                Check(bytes<20000000,"Full map updates cloned loaded block arrays or rebuilt unbounded temporary graphs");
                var copy=NativeWorldGeometry.ReadBlocks(dense[0]);copy[0]=null;
                Check(NativeWorldGeometry.ReadBlocks(dense[0])[0]!=null,"Public geometry reader exposed the borrowed block array");
            } finally {native.SetValue(null,original);MapTopology.ClearWorld();}
        }
        private static LevelScreen DirectedScreen(int index,int target,bool left,params IBlock[] blocks)
        {
            var links=new[]{new TeleportLink(),new TeleportLink()};links[left ? 0 : 1]=new TeleportLink(target+1);
            return new LevelScreen(index,blocks,new LevelScreen.Graphics(),false,links,0,null);
        }
        private static void DefaultMap()
        {
            // the upper entrance also works backwards in native code; that loop mustn't move the whole forest
            var screens=new[]{DirectedScreen(0,2,false,new BoxBlock(new Rectangle(0,0,8,360))),Screen(1,-1),
                DirectedScreen(2,0,true,new BoxBlock(new Rectangle(472,-720,8,360)),new BoxBlock(new Rectangle(0,-368,480,8))),
                DirectedScreen(3,4,false),DirectedScreen(4,3,true,new BoxBlock(new Rectangle(472,-1440,8,360)),new BoxBlock(new Rectangle(0,-1088,480,8))),Screen(5,-1)};
            MapTopology.Invalidate();var map=MapTopology.BuildMap(screens);Vector2 position;
            foreach(float observer in new[]{5f,240f,470f}) foreach(float target in new[]{20f,400f}) {
                Check(map.TryPosition(new Vector2(observer,100),new Vector2(target,-620),out position) && position==new Vector2(target+480,100),"A distant winding moved the right-hand branch to the left");
                Check(map.TryPosition(new Vector2(observer,-260),new Vector2(target,-980),out position) && position==new Vector2(target+480,-260),"The upper branch screen lost its entrance column alignment");
            }
            Check(map.TryPosition(new Vector2(400,-620),new Vector2(20,100),out position) && position==new Vector2(-460,-620),"Default map isn't reciprocal across regions");
            Check(map.Regions.Length==3 && map.Regions[1].Offset==new Vector2(480,720) && map.Regions[2].Offset==new Vector2(960,1080),"Authored entrances didn't place whole vertical regions");
            Check(map.Connections.Any(edge=>edge.Winding!=Vector2.Zero),"Conflicting physical cycles disappeared from the map");
            Check(map.Connections.Where(edge=>edge.Preferred).All(edge=>edge.Winding==Vector2.Zero),"A fallback exit displaced an authored entrance");
            Check(Object.ReferenceEquals(map,MapTopology.BuildMap(screens)),"Steady chart reads rebuilt the region graph");
            var self=MapTopology.BuildMap(new[]{DirectedScreen(0,0,false)});
            Check(self.TryPosition(new Vector2(5,100),new Vector2(470,100),out position) && position==new Vector2(470,100),"A self-link changed the default screen image");
            Check(self.Connections.Any(edge=>edge.Winding.X==480) && self.Connections.Any(edge=>edge.Winding.X==-480),"Self-link winding wasn't retained separately");
            var isolated=MapTopology.BuildMap(new[]{Screen(0,-1),Screen(1,-1,new BoxBlock(new Rectangle(0,-8,480,8)))});
            Check(!isolated.TryPosition(new Vector2(5,100),new Vector2(5,-260),out position),"Disconnected regions invented a spatial relationship");
            MapTopology.Invalidate();
        }
    }
}
