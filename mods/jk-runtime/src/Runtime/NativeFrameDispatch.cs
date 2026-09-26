using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace JKRuntime
{
    /// <summary>Explicit non-inlined native frame boundaries for presentation schedulers. Does not own a clock, change delta, or add updates.</summary>
    public static class NativeFrameDispatch
    {
        private static readonly Action<Game, GameTime> update = Bind("DoUpdate");
        private static readonly Action<Game, GameTime> draw = Bind("DoDraw");
        private static Action<Game, GameTime> Bind(string name)
        {
            var method = typeof(Game).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new NotSupportedException("Native frame boundary unavailable: " + name);
            return (Action<Game, GameTime>)Delegate.CreateDelegate(typeof(Action<Game, GameTime>), method);
        }
        /// <summary>Dispatch exactly one permitted native update, including installed native boundary hooks. Caller owns pacing and must supply native simulation time.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Update(Game game, GameTime time) { update(game, time); }
        /// <summary>Dispatch one presentation frame through the native draw pipeline. Caller owns pacing; world simulation must not be advanced here.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Draw(Game game, GameTime time) { draw(game, time); }
    }
}
