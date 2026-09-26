using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Player;

namespace JKRuntime.Simulation
{
    /// <summary>Reviewed scoped collision world for native ballistic flight. Reject unmodelled gameplay. Not a complete player controller.</summary>
    public interface INativeFlightWorld : ICollisionQuery
    {
        int Screen { get; }
        bool HasTeleport { get; }
        void ValidateScreen();
        void AdvanceCamera(BodyComp body);
    }

    /// <summary>Optional native wind forecast. Tick zero is the captured game tick, not wall time.</summary>
    public interface INativeFlightWind
    {
        bool WindEnabled { get; }
        float WindVelocity(int tick);
    }

    /// <summary>Optional side-exit teleport adapter. Called at the native teleport stage, after X movement/capping. Modify only the shadow body/context and private world; never the live camera or world.</summary>
    public interface INativeFlightTeleports
    {
        void HandleTeleport(BehaviourContext context);
    }
}
