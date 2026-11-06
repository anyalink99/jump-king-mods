using System;
using System.Collections.Generic;
using JKRuntime;
using JKRuntime.Modules;
using JKRuntime.Presentation;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace PresentationExample
{
    [RuntimeModule("example.presentation", "Appearance example")]
    public static class Module
    {
        [OnWorldReady]
        public static void Prepare(RuntimeScope scope) { PlayerVisuals.Prepare(scope); }
    }

    // Example mod code, not a Runtime service. The host owns this in its actor scope,
    // ticks Update once per native update, draws it behind the player, and clears on restore.
    public sealed class Afterimages : IDisposable
    {
        private struct Echo { internal Vector2 Anchor; internal float Age; }
        private readonly List<Echo> echoes = new List<Echo>();
        private AppearanceImage image;
        private Vector2 anchorOffset;
        private IAppearanceProjection projection;

        // Capture once per action with an idle SpriteBatch; later echoes reuse the image.
        public bool Capture(PlayerEntity player)
        {
            Clear();
            var frame = PlayerAppearance.Resolve(player, AppearanceStage.Body);
            try { image = AppearanceCapture.Freeze(frame, 256); }
            catch (InvalidOperationException) { return false; }
            anchorOffset = frame.Geometry.AnchorOffset;
            projection = frame.Sprite as IAppearanceProjection;
            Emit(player);
            return true;
        }

        public void Emit(PlayerEntity player)
        {
            if (image == null || !image.IsValid) { Clear(); return; }
            if (echoes.Count == 8) echoes.RemoveAt(0);
            echoes.Add(new Echo { Anchor = player.m_body.Position + anchorOffset });
        }

        public void Update(float delta)
        {
            if (image == null) return;
            if (!image.IsValid) { Clear(); return; }
            if (delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            for (int i = echoes.Count - 1; i >= 0; i--)
            {
                var echo = echoes[i]; echo.Age += delta;
                if (echo.Age >= .18f) echoes.RemoveAt(i); else echoes[i] = echo;
            }
            if (echoes.Count == 0) Clear();
        }

        // Drawing only projects saved world coordinates. It never samples or ticks.
        public void Draw()
        {
            if (image == null || !image.IsValid) return;
            foreach (var echo in echoes)
                image.Draw(projection == null ? Camera.TransformVector2(echo.Anchor) : projection.Project(echo.Anchor),
                    Color.White * (1 - echo.Age / .18f));
        }

        public void Clear()
        {
            if (image != null) image.Dispose();
            image = null; projection = null; echoes.Clear();
        }
        public void Dispose() { Clear(); }
    }
}
