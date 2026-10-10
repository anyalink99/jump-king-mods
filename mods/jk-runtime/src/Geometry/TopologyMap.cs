using System;
using System.Collections.Generic;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace JKRuntime.Geometry
{
    /// <summary>A vertical column in the default spatial map. Native coordinates plus Offset place its screens.</summary>
    public sealed class TopologyRegion
    {
        public int FirstScreen {get;internal set;}
        public int LastScreen {get;internal set;}
        public int Group {get;internal set;}
        public Vector2 Offset {get;internal set;}
    }
    /// <summary>A physical side connection. Winding is its displacement from the default flat map.</summary>
    public sealed class TopologyConnection
    {
        public int Source {get;internal set;}
        public int Target {get;internal set;}
        public bool Left {get;internal set;}
        public bool Preferred {get;internal set;}
        public Vector2 Winding {get;internal set;}
        internal int From,To;
        internal Vector2 Shift;
    }
    /// <summary>A deterministic spatial map. Authored entrances place whole vertical regions; conflicting cycles remain separate connections.</summary>
    public sealed class TopologyMap
    {
        private sealed class Arc {internal int To;internal Vector2 Shift;internal bool Preferred;}
        private sealed class Pending : IComparable<Pending>
        {
            internal int Region,Fallbacks,Hops,Serial;
            internal Vector2 Offset;
            public int CompareTo(Pending other)
            {
                int order=Fallbacks.CompareTo(other.Fallbacks);if(order!=0)return order;
                order=Hops.CompareTo(other.Hops);return order!=0 ? order : Serial.CompareTo(other.Serial);
            }
        }
        private readonly TopologyRegion[] regions;
        private readonly TopologyConnection[] connections;
        private readonly int[] membership;
        public TopologyRegion[] Regions {get{return (TopologyRegion[])regions.Clone();}}
        public TopologyConnection[] Connections {get{return (TopologyConnection[])connections.Clone();}}
        internal TopologyMap(LevelScreen[] screens)
        {
            membership=new int[screens.Length];var columns=new List<TopologyRegion>();
            for(int screen=0;screen<screens.Length;screen++) {
                if(screen==0 || !TopologyOpenings.Vertical(screens,screen-1))
                    columns.Add(new TopologyRegion{FirstScreen=screen,LastScreen=screen,Group=-1});
                membership[screen]=columns.Count-1;columns[columns.Count-1].LastScreen=screen;
            }
            regions=columns.ToArray();var edges=new List<TopologyConnection>();
            for(int screen=0;screen<screens.Length;screen++) {
                if(screens[screen]==null)continue;
                for(int side=0;side<2;side++) {
                    bool left=side==0;if(!TopologyOpenings.OpenSide(screens,screen,left))continue;
                    int target=MapTopology.NativeDestination(screens,screen,left);
                    if(target>=0) {
                        var links=screens[screen].teleport;
                        bool preferred=links!=null && side<links.Length && links[side]!=null && links[side].IsEnabled && links[side].GetIndex0()==target;
                        Add(edges,screen,target,left,preferred);
                    } else if(ExpansionPortals.HasLinks(screens[screen]))
                        foreach(int destination in ExpansionPortals.Targets(screens,screen,left)) Add(edges,screen,destination,left,true);
                }
            }
            connections=edges.ToArray();Place();
        }
        private void Add(List<TopologyConnection> edges,int source,int target,bool left,bool preferred)
        {
            edges.Add(new TopologyConnection{Source=source,Target=target,Left=left,Preferred=preferred,
                From=membership[source],To=membership[target],Shift=new Vector2(left ? -480 : 480,(target-source)*360)});
        }
        private void Place()
        {
            var adjacent=new List<Arc>[regions.Length];
            for(int i=0;i<adjacent.Length;i++) adjacent[i]=new List<Arc>();
            foreach(var edge in connections) {
                if(edge.From==edge.To)continue;
                adjacent[edge.From].Add(new Arc{To=edge.To,Shift=edge.Shift,Preferred=edge.Preferred});
                adjacent[edge.To].Add(new Arc{To=edge.From,Shift=-edge.Shift,Preferred=edge.Preferred});
            }
            var pending=new SortedSet<Pending>();int serial=0,group=0;
            for(int root=0;root<regions.Length;root++) {
                if(regions[root].Group>=0)continue;
                pending.Add(new Pending{Region=root,Serial=serial++});
                while(pending.Count!=0) {
                    var next=pending.Min;pending.Remove(next);
                    var region=regions[next.Region];if(region.Group>=0)continue;
                    region.Group=group;region.Offset=next.Offset;
                    foreach(var edge in adjacent[next.Region]) if(regions[edge.To].Group<0)
                        pending.Add(new Pending{Region=edge.To,Offset=next.Offset+edge.Shift,
                            Fallbacks=next.Fallbacks+(edge.Preferred ? 0 : 1),Hops=next.Hops+1,Serial=serial++});
                }
                group++;
            }
            foreach(var edge in connections) edge.Winding=regions[edge.From].Offset+edge.Shift-regions[edge.To].Offset;
        }
        /// <summary>Express a canonical point in the observer's default chart. False means no known connection joins their regions.</summary>
        public bool TryPosition(Vector2 observer,Vector2 target,out Vector2 position)
        {
            position=target;if(membership.Length==0)return false;
            var from=regions[membership[MapTopology.ScreenAt(observer.Y,membership.Length)]];
            var to=regions[membership[MapTopology.ScreenAt(target.Y,membership.Length)]];
            if(from.Group!=to.Group)return false;
            position+=to.Offset-from.Offset;return true;
        }
    }
}
