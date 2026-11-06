using System;

namespace JKRuntime.World
{
    public enum WorldTreeResult { Idle, Running, Success, Failure, Cancelled, Faulted }
    public struct WorldTreeNodeState
    {
        [WorldField] public WorldTreeResult Result;
        [WorldField] public int Cursor, Iterations;
        [WorldField] public double Started;
        [WorldField] public bool Armed, Signalled;
        [WorldField] public long Effect;
    }
    public sealed class WorldTreeState
    {
        [WorldField] public WorldTreeResult Result;
        [WorldField] public bool Fired;
        [WorldField] public WorldTreeNodeState[] Nodes;
        public WorldTreeState Clone() {return new WorldTreeState{Result=Result,Fired=Fired,Nodes=Nodes==null?null:(WorldTreeNodeState[])Nodes.Clone()};}
    }
    public enum WorldTreeOp { Leaf, Sequence, Selector, Parallel, Repeat, Invert }
    public sealed class WorldTreeInstruction
    {
        public WorldTreeOp Op {get;private set;}
        public int[] Children {get{return (int[])children.Clone();}}
        internal readonly int[] children;
        public int Count {get;private set;}
        public WorldTreeInstruction(WorldTreeOp op,int[] children,int count)
        {if(!Enum.IsDefined(typeof(WorldTreeOp),op)||children==null||count<0)throw new ArgumentException("Invalid tree instruction");Op=op;this.children=(int[])children.Clone();Count=count;}
    }
    public delegate WorldTreeResult WorldTreeLeaf(int node,ref WorldTreeNodeState state,ref int budget);
    public sealed class WorldTreeProgram
    {
        private readonly WorldTreeInstruction[] code;
        public WorldTreeProgram(WorldTreeInstruction[] nodes)
        {
            if(nodes==null||nodes.Length<1||nodes.Length>8192)throw new ArgumentException("Invalid tree size");
            code=(WorldTreeInstruction[])nodes.Clone();var visited=new bool[code.Length];Validate(0,visited,0);
            for(int i=0;i<visited.Length;i++)if(!visited[i])throw new ArgumentException("Unreachable tree instruction");
        }
        private void Validate(int index,bool[] visited,int depth)
        {
            if(index<0||index>=code.Length||visited[index]||depth>64||code[index]==null)throw new ArgumentException("Invalid tree graph");visited[index]=true;
            var node=code[index];int count=node.children.Length;
            if(node.Op==WorldTreeOp.Leaf?count!=0:(node.Op==WorldTreeOp.Repeat||node.Op==WorldTreeOp.Invert)?count!=1:count<1)throw new ArgumentException("Invalid tree arity");
            foreach(int child in node.children)Validate(child,visited,depth+1);
        }
        public WorldTreeResult Step(WorldTreeState tree,int index,ref int budget,WorldTreeLeaf leaf,Action<int> reset)
        {
            if(tree==null||tree.Nodes==null||tree.Nodes.Length!=code.Length||leaf==null||reset==null)throw new ArgumentException("Invalid tree execution state");
            if(index<0||index>=code.Length)throw new ArgumentOutOfRangeException("index");
            if(--budget<0)throw new InvalidOperationException("Behavior tree tick budget exhausted");
            var node=code[index];var state=tree.Nodes[index];WorldTreeResult result;
            switch(node.Op) {
                case WorldTreeOp.Sequence:
                    if(state.Cursor<0||state.Cursor>node.children.Length)throw new InvalidOperationException("Invalid sequence cursor");
                    result=WorldTreeResult.Success;
                    while(state.Cursor<node.children.Length){result=Step(tree,node.children[state.Cursor],ref budget,leaf,reset);if(result!=WorldTreeResult.Success)break;state.Cursor++;}break;
                case WorldTreeOp.Selector:
                    result=WorldTreeResult.Failure;
                    for(int i=0;i<node.children.Length;i++){
                        int child=node.children[i];if(tree.Nodes[child].Result==WorldTreeResult.Failure)reset(child);
                        result=Step(tree,child,ref budget,leaf,reset);if(result==WorldTreeResult.Failure){reset(child);continue;}
                        for(int j=i+1;j<node.children.Length;j++)reset(node.children[j]);state.Cursor=i;break;
                    }break;
                case WorldTreeOp.Parallel:
                    result=WorldTreeResult.Success;
                    foreach(int child in node.children){var current=tree.Nodes[child].Result;if(current!=WorldTreeResult.Success)current=Step(tree,child,ref budget,leaf,reset);if(current==WorldTreeResult.Failure){result=current;break;}if(current==WorldTreeResult.Running)result=current;}break;
                case WorldTreeOp.Repeat:
                    result=Step(tree,node.children[0],ref budget,leaf,reset);
                    if(result==WorldTreeResult.Success){state.Iterations=checked(state.Iterations+1);if(node.Count==0||state.Iterations<node.Count){reset(node.children[0]);result=WorldTreeResult.Running;}}break;
                case WorldTreeOp.Invert:
                    result=Step(tree,node.children[0],ref budget,leaf,reset);if(result!=WorldTreeResult.Running)result=result==WorldTreeResult.Success?WorldTreeResult.Failure:WorldTreeResult.Success;break;
                default:result=leaf(index,ref state,ref budget);break;
            }
            state.Result=result;tree.Nodes[index]=state;return result;
        }
    }
}
