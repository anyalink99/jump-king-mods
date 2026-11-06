using System;
using System.Collections.Generic;
using HarmonyLib;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using SubframeCharge;

internal static partial class PerformanceTests
{
    private sealed class ContactBlock : BoxBlock { internal ContactBlock() : base(new Rectangle(80,200,80,8)) { } }
    private sealed class ContactMaterial : IBlockBehaviour
    {
        internal int Calls,Entries;
        internal float Time,Friction;
        internal bool Stopped,Throw;
        public float BlockPriority { get { return 1; } }
        public bool IsPlayerOnBlock { get; set; }
        public float ModifyXVelocity(float v,BehaviourContext c) { return v; }
        public float ModifyYVelocity(float v,BehaviourContext c) { return v; }
        public float ModifyGravity(float v,BehaviourContext c) { return v; }
        public bool AdditionalXCollisionCheck(AdvCollisionInfo i,BehaviourContext c) { return false; }
        public bool AdditionalYCollisionCheck(AdvCollisionInfo i,BehaviourContext c) { return false; }
        public bool ExecuteBlockBehaviour(BehaviourContext c)
        {
            Calls++; Time+=c.FrameDelta; Friction+=PlayerValues.ICE_FRICTION;
            if(Throw) throw new InvalidOperationException("contact fixture");
            IsPlayerOnBlock=c.CollisionInfo.PreResolutionCollisionInfo.IsCollidingWith<ContactBlock>();
            if(!IsPlayerOnBlock) Stopped=false;
            else if(!Stopped) { Stopped=true; Entries++; c.BodyComp.Velocity=Vector2.Zero; }
            return !IsPlayerOnBlock;
        }
    }
    private static void MaterialCadenceTests()
    {
        var aggregate=AccessTools.Method(typeof(BehaviourContextCollisionInfo),"AggregateCollisionInfo");
        float nativeFriction=PlayerValues.ICE_FRICTION;
        foreach(int phase in new[]{0,1,2,3}) {
            var body=PhysicsBody(new Vector2(100,150),Vector2.Zero);
            var handler=new ContactMaterial(); var block=new ContactBlock();
            body.RegisterBlockBehaviour(typeof(ContactBlock),handler);
            PhysicsDynamics.Reset();
            for(int tick=0;tick<241;tick++) {
                bool contact=tick==8+phase || tick>=20 && tick<35;
                var c=new BehaviourContext(body) {FrameDelta=1f/240};
                aggregate.Invoke(c.CollisionInfo,new object[]{new AdvCollisionInfo(new List<IBlock>(contact?new IBlock[]{block}:new IBlock[0]),false,SlopeType.None,Vector2.Zero)});
                body.Velocity=new Vector2(3,-2);
                PhysicsDynamics.Begin(body,c.FrameDelta);
                try {
                    bool result=PhysicsCallbacks.Block(handler,c);
                    if(tick==8+phase) Check(handler.Entries==1 && body.Velocity==Vector2.Zero && !result,"Short contact was lost at phase "+phase);
                    if(tick==9+phase) Check(!handler.IsPlayerOnBlock && result,"Exit retained a cached stop at phase "+phase);
                } finally { PhysicsDynamics.End(); }
                Check(c.FrameDelta==1f/240,"Material scheduler leaked callback delta");
            }
            Check(handler.Entries==2,"Contact state wasn't re-armed exactly once per entry");
            Check(handler.Calls>=61 && handler.Calls<=65,"Stable material callback accelerated with physics cadence");
            Check(Math.Abs(handler.Time-(1+1f/60))<.00002,"Contact events accelerated elapsed time");
            Check(Math.Abs(handler.Friction-nativeFriction*handler.Time*60)<.0002,"Shared friction rate accelerated on contact events");
        }
        var failing=new ContactMaterial {Throw=true}; var target=PhysicsBody(Vector2.Zero,Vector2.Zero);
        target.RegisterBlockBehaviour(typeof(ContactBlock),failing);
        var context=new BehaviourContext(target) {FrameDelta=1f/240};
        aggregate.Invoke(context.CollisionInfo,new object[]{new AdvCollisionInfo()});
        PhysicsDynamics.Reset(); PhysicsDynamics.Begin(target,context.FrameDelta);
        try { try { PhysicsCallbacks.Block(failing,context); throw new Exception("Material failure was swallowed"); } catch(InvalidOperationException) { } }
        finally { PhysicsDynamics.End(); }
        Check(context.FrameDelta==1f/240 && PhysicsCallbacks.MaterialRate==1 && PlayerValues.ICE_FRICTION==nativeFriction,"Throwing material leaked timing/rate state");
        PhysicsDynamics.Reset();
        var cadence=new MaterialCadence();float elapsed;
        cadence.Due(failing,context,1f/240,true,out elapsed);
        for(int i=0;i<1000;i++) cadence.Due(failing,context,1f/240,false,out elapsed);
        var counter=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",Type.EmptyTypes);
        if(counter!=null) {
            var bytes=(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),counter);
            long before=bytes();
            for(int i=0;i<10000;i++) cadence.Due(failing,context,1f/240,false,out elapsed);
            Check(bytes()-before<1024,"Steady material dispatch allocates per callback");
        }
        Console.WriteLine("[OK] Material cadence: entry/exit in every subframe phase, cached false release, bounded periodic calls, elapsed time/friction budgets and exception cleanup");
    }
}
