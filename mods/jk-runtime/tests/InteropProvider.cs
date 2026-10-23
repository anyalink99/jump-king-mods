using System;
using System.Runtime.CompilerServices;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace UnfamiliarInterop
{
    public sealed class Surface : BoxBlock
    {
        public long State=1;
        public int Reads;
        public Surface(Rectangle rect) : base(rect) { }
        public static long Capture(IBlock value) { var block=(Surface)value; block.Reads++; return block.State; }
    }
    public sealed class Handler : IBlockBehaviour
    {
        public int Calls;
        public bool Throw,Collision;
        public float BlockPriority { get { return 2; } }
        public bool IsPlayerOnBlock { get; set; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public float ModifyXVelocity(float input,BehaviourContext context) { Calls++; if(Throw)throw new InvalidOperationException("fixture"); return input*.5f; }
        public float ModifyYVelocity(float input,BehaviourContext context) { return input; }
        public float ModifyGravity(float input,BehaviourContext context) { return input; }
        public bool ExecuteBlockBehaviour(BehaviourContext context) { return true; }
        public bool AdditionalXCollisionCheck(AdvCollisionInfo info,BehaviourContext context) { return false; }
        public bool AdditionalYCollisionCheck(AdvCollisionInfo info,BehaviourContext context) { Calls++; return Collision && context.BodyComp.Position.Y>10; }
    }
}
