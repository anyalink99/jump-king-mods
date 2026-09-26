using System.Collections.Generic;
using JumpKing;
using JumpKing.API;
using JKRuntime.Gameplay;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;

namespace MegaGameplayExpansion
{
    // Certify ordinary controlled walking from the real update. Do not replay
    // foreign callbacks: ModifyXVelocity is allowed to mutate mod state.
    internal sealed class WalkMotionGuard : System.IDisposable
    {
        private readonly Dictionary<System.Type,IBlockBehaviour> blocks;
        internal readonly PlusWalkSupport Plus;
        private MotionObserver observer;
        private long sequence;
        private float startX, beforeX, velocity, expectedX, afterX, endX;
        private bool started, stepMatched, stopped, ended;
        internal bool Verified { get; private set; }
        internal int CommandDirection { get; private set; }
        internal WalkMotionGuard(BodyComp body)
        { blocks=NativeFlight.Get<Dictionary<System.Type,IBlockBehaviour>>(body,"m_blockBehaviourLookup"); Plus=new PlusWalkSupport(blocks); Bind(body); }
        internal void Bind(BodyComp body)
        { if(observer==null && ModEntry.MotionScope!=null) { observer=ModEntry.MotionScope.Observe(body); observer.Enabled=false; } }
        public void Dispose() { if(observer!=null) observer.Dispose(); observer=null; }
        internal void Restore(bool verified, int command) { Verified=verified; CommandDirection=command; started=ended=false; }
        internal void Start(BodyComp body)
        {
            if (ended && body.Position.X!=endX) Verified=false;
            startX=body.Position.X; started=true; stepMatched=stopped=false;
        }
        internal bool Before(BehaviourContext context, bool enabled)
        {
            var body=context.BodyComp;
            beforeX=body.Position.X; velocity=body.Velocity.X;
            if(observer!=null) { observer.Enabled=enabled; sequence=observer.Read().Sequence; }
            // Body runs before this frame's Walk node. Its velocity belongs to
            // the PREVIOUS command, even though InputComponent reads new pads.
            float delta=CommandDirection*PlayerValues.WALK_SPEED;
            // A stationary body needs no edge correction; keep its latch through
            // snapshot restoration or a native stop without inventing movement.
            if (velocity==0) delta=0;
            bool ordinary=velocity==delta;
            stepMatched=started && enabled && observer!=null && body.Position.X==startX && ordinary;
            return Verified && stepMatched;
        }
        internal bool After(BodyComp body)
        {
            afterX=body.Position.X;
            var sample=observer==null?default(MotionSample):observer.Read();
            expectedX=beforeX+sample.Step;
            stepMatched &= sample.Sequence!=sequence && sample.Kind==MotionKind.ControlledOnly;
            stepMatched &= body.Position.X==expectedX && body.Velocity.X==velocity;
            if (!stepMatched) Verified=false;
            return stepMatched;
        }
        internal void Stopped(BodyComp body) { stopped=true; afterX=body.Position.X; }
        internal void AfterMaterials(BodyComp body)
        {
            // Friction/inertia is visible here before Walk applies next tick's
            // input. A conveyor offset was already caught by After; wind by
            // Before. No gimmick names or block-specific exclusion handlers.
            Verified=started && stepMatched && body.IsOnGround && body.Position.X==afterX
                && body.Velocity.X==(stopped ? 0 : velocity);
            endX=body.Position.X; ended=true; started=false;
        }
        internal void End(BodyComp body, int direction)
        {
            CommandDirection=direction;
            if (!ended || body.Position.X!=endX) Verified=false;
            endX=body.Position.X; ended=true;
        }
    }
}
