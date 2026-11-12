using JKRuntime;
using JKRuntime.World;

namespace MultiplayerExpansion
{
    public static class WorldLifecycle
    {
        public static void Prepare(RuntimeScope scope)
        {
            AdvancedSession.BeginFrame();
            using(RuntimeApi.MeasureStartup("multiplayer.native-world"))NativeWorldState.Prepare(scope);
            using(RuntimeApi.MeasureStartup("multiplayer.switch-blocks-world"))SwitchBlocksWorld.Prepare(scope);
        }
        public static void Attempt(RuntimeScope scope)
        {AdvancedSession.BeginFrame();SwitchBlocksWorld.Release();scope.Defer(SwitchBlocksWorld.Release);}
        public static void Activate(ModuleContext context)
        {
            AdvancedSession.AttachBody(context);PlayerActionBindings.Activate(context);WorldSession.Ready=true;
            context.Track(new RuntimeScope()).Defer(delegate{WorldSession.Reset();WorldSession.Ready=false;});
        }
    }
}
