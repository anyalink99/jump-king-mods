using System;
using System.Collections.Generic;
using JKRuntime;
using JKRuntime.Gameplay;
using JKRuntime.Presentation;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaGameplayExpansion
{
    // AirDashVisual owns this effect and releases it on cancellation or player destruction.
    internal sealed class DashTrail : IDisposable
    {
        private struct Echo { internal Vector2 Anchor; internal float Age; }
        private readonly PlayerEntity player;
        private readonly float lifetime;
        private readonly int capacity;
        private readonly Func<float, Color> tint;
        private readonly RuntimeScope scope = new RuntimeScope();
        private readonly List<Echo> echoes = new List<Echo>();
        private AppearanceImage image;
        private Vector2 anchorOffset;
        private IAppearanceProjection projection;
        private bool disposed;
        public int Count { get { return echoes.Count; } }
        public string LastError { get; private set; }
        /// <summary>Tint receives remaining life from 1 to 0. Default is white with linear fade. Order controls the Behind visual contribution.</summary>
        internal DashTrail(PlayerEntity player, string owner, float lifetime = .18f, int capacity = 8, Func<float, Color> tint = null, int order = 0)
        {
            if (player == null) throw new ArgumentNullException("player");
            if (float.IsNaN(lifetime) || float.IsInfinity(lifetime) || lifetime <= 0) throw new ArgumentOutOfRangeException("lifetime");
            if (capacity < 1 || capacity > 256) throw new ArgumentOutOfRangeException("capacity");
            this.player = player; this.lifetime = lifetime; this.capacity = capacity;
            this.tint = tint ?? (remaining => Color.White * remaining);
            try
            {
                scope.Defer(Clear);
                scope.Own(PlayerUpdates.Register(player, owner, PlayerUpdatePhase.AfterInput, Advance, -100));
                scope.Own(GameplayEvents.Subscribe(owner, value => { if (value.Kind == GameplayEventKind.RestoreStarted) Clear(); }));
                TrailSprite wrapper = null;
                scope.Own(PlayerVisuals.Register(player, owner, VisualPhase.Behind,
                    source => { if (wrapper == null) wrapper = new TrailSprite(this); wrapper.Source = source; return wrapper; }, () => image != null, order));
            }
            catch { scope.Dispose(); throw; }
        }
        /// <summary>Replace the action's image and emit its first echo. Call with an idle SpriteBatch; optional failures clear the trail and return false.</summary>
        public bool Capture(int size = 128)
        {
            if (disposed) throw new ObjectDisposedException("DashTrail");
            if (size < 32 || size > 512) throw new ArgumentOutOfRangeException("size");
            Clear();
            try
            {
                var frame = PlayerAppearance.Resolve(player, AppearanceStage.Body);
                image = AppearanceCapture.Freeze(frame, size);
                anchorOffset = frame.Geometry.AnchorOffset; projection = frame.Sprite as IAppearanceProjection;
                LastError = null; Emit(); return true;
            }
            catch (Exception error) { Clear(); LastError = error.GetBaseException().Message; return false; }
        }
        /// <summary>Add the player's current world position using the frozen image. No capture or pixel readback.</summary>
        public void Emit()
        {
            if (disposed) throw new ObjectDisposedException("DashTrail");
            if (image == null || !image.IsValid) { Clear(); return; }
            if (echoes.Count == capacity) echoes.RemoveAt(0);
            echoes.Add(new Echo { Anchor = player.m_body.Position + anchorOffset });
        }
        private void Advance(float delta)
        {
            if (image == null) return;
            if (!image.IsValid) { Clear(); return; }
            if (delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            // Bound a stalled frame like the native cosmetic consumers do.
            float step = Math.Min(.05f, delta);
            for (int i = echoes.Count - 1; i >= 0; i--)
            {
                var echo = echoes[i]; echo.Age += step;
                if (echo.Age >= lifetime) echoes.RemoveAt(i); else echoes[i] = echo;
            }
            if (echoes.Count == 0) Clear();
        }
        public void Clear()
        {
            if (image != null) image.Dispose();
            image = null; projection = null; echoes.Clear();
        }
        private void Draw()
        {
            if (image == null || !image.IsValid) return;
            foreach (var echo in echoes)
                image.Draw(projection == null ? Camera.TransformVector2(echo.Anchor) : projection.Project(echo.Anchor), tint(Math.Max(0, 1 - echo.Age / lifetime)));
        }
        public void Dispose() { if (disposed) return; scope.Dispose(); disposed = true; }
        private sealed class TrailSprite : Sprite, IAppearanceProjection
        {
            private readonly DashTrail owner;
            internal Sprite Source;
            internal TrailSprite(DashTrail value) { owner = value; }
            public Vector2 Project(Vector2 worldAnchor)
            { var custom = Source as IAppearanceProjection; return custom == null ? Camera.TransformVector2(worldAnchor) : custom.Project(worldAnchor); }
            public override void Draw(Vector2 position, SpriteEffects effects = SpriteEffects.None)
            { owner.Draw(); if (Source != null) Source.Draw(position, effects); }
            public override void Draw(float x, float y, SpriteEffects effects = SpriteEffects.None) { Draw(new Vector2(x, y), effects); }
            public override void Draw(Point point, SpriteEffects effects = SpriteEffects.None) { Draw(point.ToVector2(), effects); }
            public override void Draw(Rectangle rectangle, SpriteEffects effects = SpriteEffects.None) { Draw(rectangle.Location.ToVector2(), effects); }
        }
    }
}
