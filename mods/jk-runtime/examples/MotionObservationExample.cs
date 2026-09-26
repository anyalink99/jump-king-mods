using System;
using JKRuntime;
using JKRuntime.Gameplay;
using JKRuntime.Modules;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;

namespace RuntimeExamples
{
    [RuntimeModule("example.motion-observation", "Motion observation example")]
    public static class MotionObservationExample
    {
        private static MotionObservationScope prepared;
        public static MotionSample LastSample { get; private set; }

        [OnWorldReady]
        public static void Prepare(RuntimeScope scope)
        {
            // The example has one declared provider. A general consumer may use
            // PrepareLoaded here, only when its feature needs observation.
            var value=scope.Own(MotionObservation.Prepare(new[]{typeof(DeclaredSpeed)}));
            prepared=value;scope.Defer(delegate {if(ReferenceEquals(prepared,value))prepared=null;});
        }
        [OnLevelStart]
        public static void Start(ModuleContext context)
        {
            var body=JumpKing.GameManager.GameLoop.m_player.m_body;
            var observer=context.Track(prepared.Observe(body));
            var pipeline=context.Track(new BodyPipeline(body,false,0,"example.motion-observation"));
            pipeline.Register(BodyPhase.AfterXCollision,new ReadSample(observer));
        }
        private sealed class ReadSample : IBodyCompBehaviour
        {
            private readonly MotionObserver observer;
            internal ReadSample(MotionObserver value){observer=value;}
            public bool ExecuteBehaviour(BehaviourContext context){LastSample=observer.Read();return true;}
        }
        public sealed class DeclaredSpeed : IBlockBehaviour
        {
            public float Multiplier=1;
            public float Carry;
            public float BlockPriority {get{return 2;}}
            public bool IsPlayerOnBlock {get;set;}
            public float ModifyXVelocity(float input,BehaviourContext context)
            {
                float factor=Multiplier,offset=Carry; // One read of each world value.
                float result=input*factor+offset;
                MotionObservation.Report(this,result,MotionObservation.GetNeutralInput(this)*factor+offset);
                return result;
            }
            public float ModifyYVelocity(float input,BehaviourContext context){return input;}
            public float ModifyGravity(float input,BehaviourContext context){return input;}
            public bool AdditionalXCollisionCheck(AdvCollisionInfo info,BehaviourContext context){return false;}
            public bool AdditionalYCollisionCheck(AdvCollisionInfo info,BehaviourContext context){return false;}
            public bool ExecuteBlockBehaviour(BehaviourContext context){return true;}
        }
    }
}
