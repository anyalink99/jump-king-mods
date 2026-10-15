using System;
using JKRuntime.World;

namespace MultiplayerExpansion
{
    // keep third-party discovery while Runtime owns the actual world state registry
    public static class WorldState
    {
        public static IDisposable Register(string id,ulong schema,Func<byte[]> capture,Func<byte[],Action> prepare)
        {return WorldRegistry.Shared.Register(id,schema,capture,prepare);}
        public static bool IsReceiving {get{return WorldSession.Follower;}}
        public static bool IsHost {get{return WorldSession.Enabled && AdvancedSession.Connected && AdvancedSession.CanEdit;}}
    }
}
