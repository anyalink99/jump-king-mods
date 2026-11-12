using System;
using JKRuntime.Gameplay;
using JumpKing.Player;

namespace JumpPromptExample
{
    public sealed class EditorPrompt : IDisposable
    {
        private IDisposable lease;
        public void Open(PlayerEntity player, Func<bool> pointerIsOverWorld)
        {
            Dispose();
            lease = DebugJumpPrompt.Acquire(player, pointerIsOverWorld);
        }
        public void Dispose()
        {
            if (lease != null) { lease.Dispose(); lease = null; }
        }
    }
}
