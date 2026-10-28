using System;
using JumpKing;
using JumpKing.Level;
using JKRuntime.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SmoothCamera
{
    // cheap geometry and a primitive subject, with the production camera and compositor
    internal sealed class CameraPreview : IDisposable
    {
        private readonly CameraSession session = new CameraSession();
        private readonly SubjectTimeline timeline = new SubjectTimeline();
        private readonly ScreenCompositor compositor = new ScreenCompositor();
        private CameraProfile currentProfile = new CameraProfile();
        private MapRules rules;
        private RenderTarget2D canvas;
        private Texture2D transparent;
        private Vector2 position, velocity;
        private float accumulator;
        private int scenario, ticks, groundedTicks, revision;
        private bool disposed, initialized;
        internal bool Paused, Manual, Guides = true;
        private int direction;
        private bool jump, armed;
        internal void Control(int move, bool held)
        { direction = move; if (!held) armed = true; jump = armed && held; }
        internal bool Ready { get { return initialized; } }
        internal string ScenarioName { get { return PracticeWorld.Names[scenario]; } }
        internal void NextScenario() { scenario = (scenario + 1) % PracticeWorld.Names.Length; Restart(); }
        internal void Restart()
        {
            session.Reset(); timeline.Reset(); position = PracticeWorld.Start(scenario); velocity = Vector2.Zero;
            session.Scene.Screens = PracticeWorld.Create(scenario); session.Scene.Practice = true;
            rules = MapRules.Parse(null, session.Scene.Screens.Length, false, false);
            accumulator = 0; ticks = groundedTicks = 0; initialized = true;
            armed = jump = false; Capture(true); Present(0);
        }
        private void Capture(bool cut)
        {
            int screen = JKRuntime.Geometry.MapTopology.ScreenAt(position.Y + 13, session.Scene.Screens.Length);
            timeline.Observe(new SubjectSample { Center = position + new Vector2(9, 13), Velocity = velocity * 60, Screen = screen, Cut = cut }, session.Scene.Screens);
        }
        internal void Update(CameraProfile profile, float delta)
        {
            if (disposed || delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            currentProfile = profile;
            if (!initialized) Restart();
            if (Paused) return;
            delta = Math.Min(.1f, delta); accumulator += delta;
            while (accumulator >= 1f / 60)
            {
                accumulator -= 1f / 60; Tick(); Capture(false);
            }
            Present(delta);
        }
        private void Present(float delta)
        {
            var options = new CameraSettings(); options.TakeProfile(currentProfile);
            var sample = timeline.Sample(delta, true, accumulator * 60);
            session.Observe(sample, session.Scene.Screens, options, rules);
            session.Advance(delta, CameraMode.Normal); revision++;
        }
        private void Tick()
        {
            ticks++;
            float oldBottom = position.Y + 26;
            velocity.X = Manual ? direction * 2.4f : scenario == 4 || scenario == 1 ? 0 : 2.4f;
            velocity.Y = Math.Min(9, velocity.Y + .26f);
            position += velocity;
            bool grounded = false;
            foreach (var screen in session.Scene.Screens)
            foreach (var block in JKRuntime.Geometry.NativeWorldGeometry.ReadBlocks(screen))
            {
                var r = block.GetRect();
                if (velocity.Y >= 0 && oldBottom <= r.Top + .01f && position.Y + 26 >= r.Top
                    && position.X + 18 > r.Left && position.X < r.Right)
                { position.Y = r.Top - 26; velocity.Y = 0; grounded = true; }
            }
            if (grounded && (Manual ? jump : ++groundedTicks > 35 && scenario != 4))
            { velocity.Y = -10; groundedTicks = 0; armed = jump = false; }
            int current = JKRuntime.Geometry.MapTopology.ScreenAt(position.Y + 13, session.Scene.Screens.Length);
            if (position.X + 9 < 0 || position.X + 9 > 480)
            {
                bool left = position.X + 9 < 0;
                int target = JKRuntime.Geometry.MapTopology.Destination(session.Scene.Screens, current, left, position.Y + 13);
                if (target >= 0) position = JKRuntime.Geometry.MapTopology.Normalize(session.Scene.Screens, position, 18, 26);
                else { position.X = CameraMotion.Clamp(position.X, -9, 471); }
            }
            if ((!Manual && ticks >= 600) || position.Y > 360) Restart();
        }
        internal void Prepare()
        {
            if (!initialized) return;
            var device = Game1.spriteBatch.GraphicsDevice;
            if (canvas == null)
            {
                canvas = new RenderTarget2D(device, 480, 360);
                transparent = new Texture2D(device, 1, 1); transparent.SetData(new[] { Color.Transparent });
            }
            var old = device.GetRenderTargets(); var viewport = device.Viewport; var scissor = device.ScissorRectangle;
            Game1.instance.EndBatch();
            try
            {
                device.SetRenderTarget(canvas); device.Clear(new Color(17, 24, 33)); Game1.instance.StartBatch();
                compositor.DrawPlan(session.Images, session.Pose, DrawScreen, true, revision, false);
                Game1.instance.EndBatch(); compositor.PresentPreview(new Rectangle(0, 0, 480, 360), transparent, session.Pose.Y, session.Pose.X);
            }
            finally { device.SetRenderTargets(old); device.Viewport = viewport; device.ScissorRectangle = scissor; Game1.instance.StartBatch(); }
        }
        internal void Draw(Rectangle bounds, CameraProfile profile)
        {
            CameraDiagram.Fill(bounds, new Color(15, 19, 25));
            if (canvas != null) Game1.spriteBatch.Draw(canvas, bounds, Color.White);
            if (Guides) CameraDiagram.Window(bounds, profile); CameraDiagram.Outline(bounds, UiTheme.Border);
        }
        private void DrawScreen(int index)
        {
            var batch = Game1.spriteBatch; var pixel = Game1.instance.contentManager.Pixel.texture;
            batch.Draw(pixel, new Rectangle(0, 0, 480, 360), index % 2 == 0 ? new Color(20, 31, 39) : new Color(27, 32, 45));
            for (int x = 0; x < 480; x += 48) batch.Draw(pixel, new Rectangle(x, 0, 1, 360), new Color(31, 44, 51));
            foreach (var block in JKRuntime.Geometry.NativeWorldGeometry.ReadBlocks(session.Scene.Screens[index]))
            {
                var r = block.GetRect(); r.Y += index * 360;
                batch.Draw(pixel, r, new Color(82, 103, 112));
                batch.Draw(pixel, new Rectangle(r.X, r.Y, r.Width, 3), new Color(151, 177, 164));
            }
            var point = session.Subject.Center + new Vector2(-9, -13 + index * 360) - RenderContext.ImageShift;
            batch.Draw(pixel, new Rectangle((int)point.X, (int)point.Y + 7, 18, 19), new Color(221, 222, 206));
            batch.Draw(pixel, new Rectangle((int)point.X - 2, (int)point.Y + 1, 22, 6), UiTheme.Gold);
            batch.Draw(pixel, new Rectangle((int)point.X + 12, (int)point.Y + 9, 3, 3), new Color(30, 41, 47));
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; compositor.Dispose();
            if (canvas != null) canvas.Dispose(); if (transparent != null) transparent.Dispose();
        }
    }
}
