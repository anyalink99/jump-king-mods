using System;
using BehaviorTree;
using MegaMappingExpansion.Api;
using MegaMappingExpansion.Construction;
using JumpKing;
namespace MegaMappingExpansion.Endings
{
    // native ending nodes keep their own control flow; only typed scene data crosses here
    internal sealed class SceneBridgeNode:IBTnode
    {
        private readonly string operation,key,value;
        private readonly Func<SceneProgram> resolve;
        internal SceneBridgeNode(string operation,string key,string value,Func<SceneProgram> resolve){this.operation=operation;this.key=key;this.value=value;this.resolve=resolve;}
        internal static SceneProgram ActiveScene(){if(NativeScene.Current==null)throw new InvalidOperationException("Ending scene operations require an active construction scene");return NativeScene.Current.Program;}
        protected override BTresult MyRun(TickData data)
        {
            if(!MappingSettings.Enabled)return BTresult.Running;var scene=resolve();
            if(operation=="EmitSceneEvent"){scene.Signals.Emit(key,"native.ending","",Camera.CurrentScreenIndex1,scene.GameplayTime);return BTresult.Success;}
            var current=scene.ReadVariable(key);var expected=SceneValue.Parse(current.Kind,value);
            if(operation=="SetSceneVariable"){scene.SetVariable(key,expected);return BTresult.Success;}
            if(operation=="CheckSceneVariable")return current.Equals(expected)?BTresult.Success:BTresult.Failure;
            if(operation=="WaitSceneVariable")return current.Equals(expected)?BTresult.Success:BTresult.Running;
            throw new InvalidOperationException("Unknown ending scene operation: "+operation);
        }
    }
}