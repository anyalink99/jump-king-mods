using System;
using System.Collections.Generic;
using System.IO;
using JKRuntime;
using JumpKing;
using JumpKing.Level;

namespace Prism
{
    internal sealed class World : IDisposable
    {
        private readonly RuntimeScope owned = new RuntimeScope();
        private readonly Dictionary<LevelScreen, ScreenArt> screens = new Dictionary<LevelScreen, ScreenArt>();
        internal readonly string ThemeId;
        internal Audio Music;
        internal Renderer Renderer;
        internal readonly WindPresentation Wind = new WindPresentation();
        internal World(string directory, string themeId = "event-horizon")
        {
            ThemeId = themeId;
            try
            {
                Beatmap chart;
                using (RuntimeApi.MeasureStartup("prism.beatmap")) chart = Beatmap.Load(Path.Combine(directory, "score.osu"));
                using (RuntimeApi.MeasureStartup("prism.audio")) Music = owned.Own(new Audio(Path.Combine(directory, "music.wav")));
                using (RuntimeApi.MeasureStartup("prism.collision-art"))
                    foreach (var screen in JKRuntime.Geometry.NativeWorldGeometry.ReadScreens())
                    { screens.Add(screen, owned.Own(ScreenArt.Read(screen, Game1.instance.GraphicsDevice))); Wind.Register(screen.GetIndex0()); }
                Renderer = owned.Own(new Renderer(Game1.instance.GraphicsDevice, Game1.spriteBatch, chart));
            }
            catch { Dispose(); throw; }
        }
        internal ScreenArt Art(LevelScreen screen) { ScreenArt value; return screens.TryGetValue(screen, out value) ? value : null; }
        public void Dispose() { owned.Dispose(); }
    }
}
