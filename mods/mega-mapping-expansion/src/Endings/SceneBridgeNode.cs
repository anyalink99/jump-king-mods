using System;
using BehaviorTree;

namespace MegaMappingExpansion.Endings
{
    // Native ending control flow stays native. Only map-state operations cross this boundary.
    internal sealed class SceneBridgeNode : IBTnode
    {
        private readonly string operation, key, value;
        private readonly Func<SceneBehaviorEngine> resolve;
        internal SceneBridgeNode(string operation, string key, string value, Func<SceneBehaviorEngine> resolve)
        { this.operation = operation; this.key = key; this.value = value; this.resolve = resolve; }
        internal static SceneBehaviorEngine ActiveScene()
        {
            if (SceneHost.Current == null) throw new InvalidOperationException("Ending scene operation requires the prepared MME scene to be active");
            return SceneHost.Current.behaviors;
        }
        protected override BTresult MyRun(TickData data)
        {
            if (!MappingSettings.Enabled) return BTresult.Running;
            SceneBehaviorEngine scene = resolve();
            switch (operation)
            {
                case "EmitSceneEvent": scene.Emit(key); return BTresult.Success;
                case "SetSceneFlag": scene.SetFlag(key, value); return BTresult.Success;
                case "CheckSceneFlag": return scene.GetFlag(key) == value ? BTresult.Success : BTresult.Failure;
                case "WaitSceneFlag": return scene.GetFlag(key) == value ? BTresult.Success : BTresult.Running;
                default: throw new InvalidOperationException("Unknown ending scene operation: " + operation);
            }
        }
    }
}
