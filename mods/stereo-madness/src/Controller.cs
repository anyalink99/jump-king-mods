using System;
using System.Collections.Generic;
using System.Reflection;
using EntityComponent;
using EntityComponent.BT;
using JKRuntime;
using JKRuntime.Gameplay;
using JKRuntime.Input;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.Player;
using JumpKing.XnaWrappers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MegaMappingExpansion.Api;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JKRuntime.State;

namespace StereoMadness
{
    internal sealed class Controller : Component, IDisposable, IStateParticipant
    {
        internal readonly Simulation Model;
        internal bool Released;
        private readonly PlayerEntity player;
        private readonly Resources world;
        private readonly List<IDisposable> control = new List<IDisposable>();
        private IDisposable attachment, pause;
        private KingSprite sprite;
        private Sprite previous;
        private double deathTime;
        private int attempt = 1;
        private bool paused, disposed, finishQueued;
        private Simulation preview;
        private readonly IMappingScene scene;
        private readonly KeyboardInputGate aliasGate = new KeyboardInputGate();
        private bool aliasBlocked;
        private bool focused = true;
        private Vector2 publishedPosition, publishedVelocity;
        private readonly BehaviourContext saveContext;
        private static readonly FieldInfo Behaviours = typeof(BodyComp).GetField("m_behaviours", OwnedPatches.Members);
        private static readonly FieldInfo GroundedField = typeof(BodyComp).GetField("_is_on_ground", OwnedPatches.Members);
        public string Id { get { return "stereo-madness.controller"; } }
        public int Version { get { return 1; } }
        private sealed class Checkpoint { internal Simulation Model; internal double DeathTime; internal int Attempt; }
        public object Capture() { return new Checkpoint { Model=Model.Capture(), DeathTime=deathTime, Attempt=attempt }; }
        public void Validate(object snapshot)
        {
            var value=snapshot as Checkpoint;
            if (Released || disposed || value==null || !ReferenceEquals(value.Model.Course,Model.Course))
                throw new InvalidOperationException("Stereo Madness snapshot is no longer valid");
        }
        public void Restore(object snapshot)
        {
            Validate(snapshot); var value=(Checkpoint)snapshot;
            Model.Restore(value.Model); deathTime=value.DeathTime; attempt=value.Attempt;
            preview=null; aliasBlocked=true; Synchronize(); RestoreAudio();
        }
        private readonly bool renderDiagnostics = Environment.GetEnvironmentVariable("STEREO_MADNESS_RENDER_DIAGNOSTICS") == "1";
        private long diagnosticStart;
        private int diagnosticDraws, diagnosticUpdates, diagnosticPredictions, diagnosticReports;
        private float Alpha { get { return !paused && !Released && preview != null && !Model.Dead && !Model.Complete
            && !preview.Dead && !preview.Complete && preview.Ship == Model.Ship && preview.Ceiling == Model.Ceiling
            && PresentationScheduling.IsHighRefresh("subframe-charge") && focused
            ? Math.Max(0, Math.Min(1, PresentationScheduling.Alpha)) : 0; } }
        internal double ViewX { get { return Model.X + (preview == null ? 0 : (preview.X - Model.X) * Alpha); } }
        private double ViewY { get { return Model.Y + (preview == null ? 0 : (preview.Y - Model.Y) * Alpha); } }
        private double ViewCamera { get { return Model.CameraY + (preview == null ? 0 : (preview.CameraY - Model.CameraY) * Alpha); } }
        internal double ViewRotation { get { return Model.Rotation + (preview == null ? 0 : Math.Atan2(Math.Sin(preview.Rotation - Model.Rotation), Math.Cos(preview.Rotation - Model.Rotation)) * Alpha); } }
        private static readonly FieldInfo SpriteField = typeof(PlayerEntity).GetField("m_sprite", OwnedPatches.Members);
        internal Vector2 PlayerPosition { get { return new Vector2(165 + (float)Math.Min(0, ViewX) * .5f, ProjectY(ViewY)); } }
        private float ProjectY(double y) { return 20 + (float)(460 - y + ViewCamera) * .5f; }
        private float ProjectX(double x) { return 165 + (float)(x - Math.Max(0, ViewX)) * .5f; }
        internal Controller(PlayerEntity target, Resources resources, IMappingScene mappingScene = null)
        {
            if (target == null) throw new InvalidOperationException("Stereo Madness needs the native player");
            player = target; world = resources; scene = mappingScene; Model = new Simulation(resources.Course);
            saveContext = new BehaviourContext(player.m_body);
            try {
                control.Add(PlayerControl.Acquire(player.m_body, "stereo-madness"));
                control.Add(JumpSlot.SuspendChargePolicy("stereo-madness"));
                control.Add(ComponentSuspension.Acquire("stereo-madness", player.m_body));
                control.Add(ComponentSuspension.Acquire("stereo-madness", player.GetComponent<BehaviorTreeComp>()));
                control.Add(PresentationActivity.Begin("stereo-madness", player.m_body));
                previous = (Sprite)SpriteField.GetValue(player);
                sprite = new KingSprite(this);
                attachment = new ComponentAttachment(player, this);
                control.Add(GameState.Snapshots.Register(this));
                pause = NativePause.Subscribe("stereo-madness", OnPause);
                ModEntry.StopNativeMusic();
                Model.X = -330;
                if (player.m_body.Position.Y < 239 && player.m_body.Position.X >= -9)
                    Model.Teleport((player.m_body.Position.X-156)*2, (239-player.m_body.Position.Y)*2, -player.m_body.Velocity.Y/(.9*.5));
                world.SeekMusic(Model.Time); if (paused) world.Music.Pause();
                Synchronize();
            } catch { Dispose(); throw; }
        }
        private void OnPause(bool value, long tick)
        {
            // NativePause reports its state every tick, not only transitions.
            // Repeated observations must preserve the preview built by Update.
            if (paused == value) return;
            paused = value;
            preview = null; aliasBlocked = true;
            foreach (JKSound s in new[] { world.Music, world.Death, world.Finish }) {
                if (value && s.State == JKSoundState.Playing) s.Pause();
                else if (!value && s.State == JKSoundState.Paused) s.Resume();
            }
        }
        protected override void Update(float delta)
        {
            if (Released || paused) return;
            // The native physics component is suspended, but the installed
            // SaveStates input behaviour must still run once per native update.
            saveContext.FrameDelta=delta;
            foreach (IBodyCompBehaviour behaviour in (LinkedList<IBodyCompBehaviour>)Behaviours.GetValue(player.m_body))
                if (behaviour.GetType().FullName=="JumpKingSaveStates.SavestateBehaviour") behaviour.ExecuteBehaviour(saveContext);
            ReadNativePosition();
            focused = Game1.instance.IsActive;
            var keyboard = Keyboard.GetState();
            bool alias = keyboard.IsKeyDown(Keys.Space) || keyboard.IsKeyDown(Keys.Up)
                || Mouse.GetState().LeftButton == ButtonState.Pressed;
            bool gated = aliasGate.Suppress(alias);
            if (!Game1.instance.IsActive || gated) aliasBlocked = true;
            if (!alias) aliasBlocked = false;
            // SFC publishes completed taps into this native pad snapshot even while
            // charge is suspended. Never repoll the native binding or consume its queue.
            bool held = Game1.instance.IsActive && (ActionInputs.Read("native.jump").Down || (alias && !gated && !aliasBlocked));
            Advance(held);
        }
        internal void Advance(bool held)
        {
            if (Released || paused || finishQueued || disposed) return;
            if (renderDiagnostics && diagnosticStart != 0 && diagnosticReports < 2) diagnosticUpdates++;
            preview = null;
            if (Model.Dead) {
                deathTime += 1.0 / 60;
                if (deathTime >= 1) {
                    attempt++; deathTime = 0; Model.Reset();
                    world.SeekMusic(0);
                }
                Synchronize(); return;
            }
            int jumps = Model.Jumps;
            bool entering = Model.X < 0;
            Model.Tick(held && Model.X >= 0);
            if (entering && Model.X > 0) Model.X = 0;
            for (int i = jumps; i < Model.Jumps; i++) { var notify = PlayerEntity.OnJumpCall; if (notify != null) notify(); }
            if (Model.Dead) {
                world.Music.Stop(); world.Death.Stop(); world.Death.Play();
                var notify = PlayerEntity.OnSplatCall; if (notify != null) notify();
            }
            else if (Model.Complete) { Finish(); return; }
            if (!Model.Dead) preview = Model.Preview(held && Model.X >= 0);
            Synchronize();
        }
        protected override void LateUpdate(float delta)
        { if (!Released) { sprite.Refresh(); player.SetSprite(sprite); } }
        private void Synchronize()
        {
            // Body position is in level space. Only the renderer subtracts the
            // scrolling camera; native saves/tools see actual course progress.
            player.m_body.Position = publishedPosition = new Vector2(156 + (float)Model.X * .5f, 239 - (float)Model.Y * .5f);
            player.m_body.Velocity = publishedVelocity = Model.Dead ? Vector2.Zero : new Vector2((float)(Simulation.Speed*.9*.5), (float)(-Model.VY*.9*.5));
            GroundedField.SetValue(player.m_body, Model.Grounded && !Model.Dead);
            Camera.UpdateCamera(player.m_body.GetHitbox().Center);
            player.SetSprite(sprite);
        }
        internal bool ReadNativePosition()
        {
            if (player.m_body.Position==publishedPosition && player.m_body.Velocity==publishedVelocity) return false;
            Vector2 position=player.m_body.Position, velocity=player.m_body.Velocity;
            Model.Teleport((position.X-156)*2, (239-position.Y)*2, -velocity.Y/(.9*.5));
            deathTime=0; preview=null; aliasBlocked=true; Synchronize(); RestoreAudio(); return true;
        }
        private void RestoreAudio()
        {
            world.Death.Stop(); world.Finish.Stop();
            if (Model.Dead || Model.Complete) world.Music.Stop();
            else { world.SeekMusic(Model.Time); if (paused) world.Music.Pause(); }
        }
        internal void Finish()
        {
            if (Released || disposed || finishQueued) return;
            finishQueued = true;
            // Releasing JumpSlot can attach SFC components. Its command pump is a
            // separate entity, outside the player's Update/LateUpdate enumeration.
            // Runtime starts this pump before activating level modules.
            try { JKRuntime.Settings.Commands.Enqueue(CompleteFinish, delegate { finishQueued = false; }); }
            catch { finishQueued = false; throw; }
        }
        private void CompleteFinish()
        {
            finishQueued = false;
            if (Released || disposed || !player.IsAlive || !ReferenceEquals(GameLoop.m_player, player)) return;
            PublishResults();
            world.Music.Stop(); world.Finish.Play();
            ReleaseControl();
            // The next native physics tick establishes grounded contact. GameLoop then
            // consumes NormalEnding.CheckWin and owns victory, save and cutscene state.
            player.m_body.Position = new Vector2(340, -244);
            player.m_body.Velocity = new Vector2(0, 1);
            player.SetDirection(1);
            Camera.UpdateCamera(player.m_body.GetHitbox().Center);
        }
        private void PublishResults()
        {
            if (scene == null || !scene.Available) return;
            var type = typeof(Game1).Assembly.GetType("JumpKing.MiscSystems.Achievements.AchievementManager", true);
            object manager = type.GetField("instance", OwnedPatches.Members).GetValue(null);
            object stats = type.GetMethod("GetCurrentStats", OwnedPatches.Members).Invoke(manager, null);
            foreach (string key in new[] { "falls", "jumps" })
                scene.SetFlag(key, ((int)stats.GetType().GetField(key).GetValue(stats)).ToString(System.Globalization.CultureInfo.InvariantCulture));
            scene.SetFlag("attempts", attempt.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        private void ReleaseControl()
        {
            Released = true;
            if (player != null && SpriteField.GetValue(player) == sprite && previous != null) player.SetSprite(previous);
            for (int i = control.Count - 1; i >= 0; i--) control[i].Dispose();
            control.Clear();
        }
        public void Dispose()
        {
            if (disposed) return;
            ReleaseControl();
            if (pause != null) pause.Dispose();
            if (attachment != null) attachment.Dispose();
            if (sprite != null) sprite.Dispose();
            world.Music.Stop(); world.Death.Stop(); world.Finish.Stop();
            if (ReferenceEquals(ModEntry.Active, this)) ModEntry.Active = null;
            disposed = true;
        }
        private Color Channel(int channel, Color initial)
        {
            Color from = initial, target = initial; double started = 0, duration = 0;
            foreach (var t in world.Course.Colors) {
                if (t.X > ViewX) break;
                if (t.Channel != channel) continue;
                double at = t.X / (Simulation.Speed * .9 * 60);
                from = duration <= 0 ? target : Color.Lerp(from, target, (float)Math.Max(0, Math.Min(1, (at - started) / duration)));
                target = new Color(t.R, t.G, t.B); started = at; duration = t.Duration;
            }
            double now = Math.Max(0, ViewX) / (Simulation.Speed * .9 * 60);
            return duration <= 0 ? target : Color.Lerp(from, target, (float)Math.Max(0, Math.Min(1, (now - started) / duration)));
        }
        private void Rect(int x, int y, int w, int h, Color color)
        { if (w > 0 && h > 0) Game1.spriteBatch.Draw(world.Pixel, new Rectangle(x, y, w, h), color); }
        internal void DrawScene()
        {
            ObserveRenderCadence();
            Color bg = Channel(29, new Color(40, 62, 255)), ground = Channel(30, new Color(0, 19, 200));
            Rect(0, 0, 480, 360, bg);
            int offset = (int)(Math.Max(0, ViewX) * .05) % 256;
            for (int y = -256; y < 360; y += 256) for (int x = -offset; x < 480; x += 256)
                Game1.spriteBatch.Draw(world.Background, new Vector2(x, y), bg);
            DrawObjects(0); DrawObjects(1);
            int floor = (int)Math.Round(ProjectY(Model.Floor));
            Rect(0, floor, 480, 360 - floor, ground); Rect(0, floor, 480, 1, Color.White);
            int scroll = (int)(Math.Max(0, ViewX) * .5) % 128;
            Texture2D groundTile = world.Sprites["groundSquare_01_001.png"];
            for (int y = floor + 1; y < 360; y += groundTile.Height) for (int x = -scroll; x < 480; x += groundTile.Width)
                Game1.spriteBatch.Draw(groundTile, new Vector2(x, y), ground);
            if (Model.Ceiling.HasValue) { int ceiling = (int)Math.Round(ProjectY(Model.Ceiling.Value)); Rect(0, 0, 480, ceiling, ground); Rect(0, ceiling, 480, 1, Color.White); }
            if (Model.Dead) for (int i = 0; i < 18; i++) {
                double a = i * Math.PI * 2 / 18; float distance = (float)deathTime * (30 + i % 4 * 18);
                Vector2 p = PlayerPosition + new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * distance;
                Rect((int)p.X, (int)p.Y, 3, 3, Color.White * (float)Math.Max(0, 1 - deathTime));
            }
            float end = ProjectX(world.Course.EndX);
            if (end < 560) {
                var pillar = world.Sprites["square_02_001.png"];
                for (int y = -240; y < 240; y += 30) Game1.spriteBatch.Draw(pillar, new Vector2(end, ProjectY(240) + y), null,
                    Color.White, -(float)Math.PI / 2, new Vector2(pillar.Width / 2f, pillar.Height / 2f), 1f, SpriteEffects.None, 0);
                var shine = world.Sprites["gradientBar.png"];
                Game1.spriteBatch.Draw(shine, new Rectangle((int)end - 55, 0, 55, 360), new Color(0, 255, 125, 0));
            }
        }
        // Opt-in, bounded console evidence from real scene draws, never input injection.
        private void ObserveRenderCadence()
        {
            if (!renderDiagnostics || diagnosticReports >= 2) return;
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (diagnosticStart == 0) diagnosticStart = now;
            diagnosticDraws++;
            if (Alpha > 0) diagnosticPredictions++;
            double seconds = (now - diagnosticStart) / (double)System.Diagnostics.Stopwatch.Frequency;
            if (seconds < 3) return;
            Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "[Stereo Madness] Render cadence: draws/s={0:F1}, updates/s={1:F1}, predicted/s={2:F1}, SFC240={3}, focused={4}",
                diagnosticDraws / seconds, diagnosticUpdates / seconds, diagnosticPredictions / seconds,
                PresentationScheduling.IsHighRefresh("subframe-charge"), focused));
            diagnosticReports++; diagnosticStart = now;
            diagnosticDraws = diagnosticUpdates = diagnosticPredictions = 0;
        }
        private void DrawObjects(int layer)
        {
            int effect = 0;
            foreach (var trigger in world.Course.Effects) { if (trigger.X > ViewX) break; effect = trigger.Value; }
            float pulse = world.Pulse[Math.Min(world.Pulse.Length - 1, Math.Max(0, (int)(Model.Time * 60)))];
            foreach (var o in world.Course.Objects) {
                if (o.Layer != layer) continue;
                float x = ProjectX(o.X), y = ProjectY(o.Y);
                if (x < -140 || x > 620 || y < -180 || y > 520) continue;
                Texture2D texture; if (!world.Sprites.TryGetValue(o.Frame, out texture)) continue;
                var tint = new Color((o.Tint >> 16) & 255, (o.Tint >> 8) & 255, o.Tint & 255);
                if (o.Additive) tint.A = 0;
                float edge = Math.Max(0, Math.Min(1, Math.Min(x, 480 - x) / 70));
                float scale = (float)o.Scale * (o.Audio ? pulse : 1);
                if (effect == 1) y += 100 * (1 - edge);
                else if (effect == 2) y -= 100 * (1 - edge);
                else if (effect == 3) x -= 100 * (1 - edge);
                else if (effect == 4) x += 100 * (1 - edge);
                else if (effect == 5 && !o.Audio) scale *= edge;
                else if (effect == 6 && !o.Audio) scale *= 1 + (1 - edge) * .75f;
                SpriteEffects flip = (o.FlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None) | (o.FlipY ? SpriteEffects.FlipVertically : SpriteEffects.None);
                Game1.spriteBatch.Draw(texture, new Vector2((float)Math.Round(x), (float)Math.Round(y)), null, tint * edge,
                    (float)(o.Rotation * Math.PI / 180), new Vector2(texture.Width / 2f, texture.Height / 2f), scale, flip, 0);
            }
        }
        internal void DrawHud()
        {
            DrawObjects(2);
            Rect(100, 9, 280, 5, Color.Black * .5f);
            Rect(101, 10, (int)(278 * Math.Max(0, ViewX) / world.Course.EndX), 3, Color.White);
            var font = Game1.instance.contentManager.font.MenuFontSmall;
            Game1.spriteBatch.DrawString(font, ((int)(100 * Math.Max(0, ViewX) / world.Course.EndX)) + "%", new Vector2(390, 5), Color.White);
            if (ViewX < 1800) Game1.spriteBatch.DrawString(font, "ATTEMPT " + attempt, new Vector2(ProjectX(340), 85), Color.White);
        }
    }
}
