using System;
using JumpKing.Player;

namespace MegaMappingExpansion
{
    // one map owner, with the old arithmetic kept as an explicit compatibility path
    internal sealed class MapPhysics:IDisposable
    {
        private readonly NativePhysicsFrame reference;
        private readonly PhysicsCoordinates symmetric;
        internal MapPhysics(MapLayout layout,string root)
        {
            if(layout.Physics.Symmetric)symmetric=new PhysicsCoordinates(root);
            else if(layout.PhysicsFrames.Count>0)reference=new NativePhysicsFrame(layout);
        }
        internal bool ReferenceActive{get{return reference!=null&&reference.IsActive;}}
        internal bool Symmetric{get{return symmetric!=null;}}
        internal string Coverage{get{return symmetric==null?"Original reference-screen arithmetic":symmetric.Coverage;}}
        internal IDisposable Attach(BodyComp body)
        {return symmetric!=null?symmetric.Attach(body):reference.Attach(body);}
        public void Dispose(){if(symmetric!=null)symmetric.Dispose();if(reference!=null)reference.Dispose();}
    }
}
