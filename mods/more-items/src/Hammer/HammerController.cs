using System;
using System.Collections.Generic;
using System.Reflection;
using EntityComponent;
using EntityComponent.BT;
using JKRuntime.Gameplay;
using JKRuntime.State;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace HammerKing
{
    internal sealed class NativeWorld : IHammerWorld
    {
        private static readonly FieldInfo Screens = typeof(LevelManager).GetField("m_screens", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo Blocks = typeof(LevelScreen).GetField("m_hitboxes", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo Lines = typeof(SlopeBlock).GetField("m_lines", BindingFlags.Instance | BindingFlags.NonPublic);
        private struct Shape { internal IBlock Block; internal Rectangle Bounds; internal ErikMaths.Line[] Edges; }
        private readonly List<Shape> nearby = new List<Shape>();
        // Native queries use integer boxes, so many neighbouring substeps ask
        // exactly the same question. Invalidate on every Prepare; never carry
        // collision results across geometry updates or screen transitions.
        private readonly Dictionary<Rectangle, bool> solidCache = new Dictionary<Rectangle, bool>();
        // Reuse 16-pixel buckets instead of scanning the whole nearby map for
        // every sample along the shaft. Large/out-of-grid queries have a fallback.
        private readonly List<int>[] cells = new List<int>[24 * 24];
        private int cellX, cellY;
        internal int NarrowQueries { get; private set; }
        internal static void ValidateContract()
        {
            if (Screens == null || Screens.FieldType != typeof(LevelScreen[]) || Blocks == null
                || Blocks.FieldType != typeof(IBlock[]) || Lines == null || Lines.FieldType != typeof(ErikMaths.Line[]))
                throw new NotSupportedException("Jump King hammer geometry contract changed");
        }
        internal void Prepare(Vector2 center)
        {
            nearby.Clear();
            solidCache.Clear(); NarrowQueries = 0;
            for (int i = 0; i < cells.Length; i++) if (cells[i] != null) cells[i].Clear();
            cellX = ((int)Math.Floor(center.X) >> 4) - 12;
            cellY = ((int)Math.Floor(center.Y) >> 4) - 12;
            var screens = (LevelScreen[])Screens.GetValue(null);
            Rectangle range = Box(center, new Vector2(180f));
            int index = -(int)Math.Floor(center.Y / 360f);
            for (int i = Math.Max(0, index - 1); i <= Math.Min(screens.Length - 1, index + 1); i++)
                foreach (IBlock block in (IBlock[])Blocks.GetValue(screens[i]))
                    if (block.GetRect().Intersects(range))
                    {
                        Rectangle bounds = block.GetRect();
                        int shape = nearby.Count;
                        nearby.Add(new Shape { Block = block, Bounds = bounds, Edges = block is SlopeBlock ? (ErikMaths.Line[])Lines.GetValue(block) : null });
                        for (int y = Math.Max(0, (bounds.Top >> 4) - cellY); y <= Math.Min(23, ((bounds.Bottom - 1) >> 4) - cellY); y++)
                        for (int x = Math.Max(0, (bounds.Left >> 4) - cellX); x <= Math.Min(23, ((bounds.Right - 1) >> 4) - cellX); x++)
                        {
                            int indexInGrid = y * 24 + x;
                            if (cells[indexInGrid] == null) cells[indexInGrid] = new List<int>();
                            cells[indexInGrid].Add(shape);
                        }
                    }
        }
        internal static Rectangle Box(Vector2 center, Vector2 halfSize)
        {
            int left = (int)Math.Floor(center.X - halfSize.X);
            int top = (int)Math.Floor(center.Y - halfSize.Y);
            return new Rectangle(left, top, (int)Math.Ceiling(center.X + halfSize.X) - left,
                (int)Math.Ceiling(center.Y + halfSize.Y) - top);
        }
        public bool Solid(Vector2 center, Vector2 halfSize)
        {
            Rectangle box = Box(center, halfSize);
            bool solid;
            if (solidCache.TryGetValue(box, out solid)) return solid;
            solid = QuerySolid(box);
            // Keep the cache bounded even for unusual custom collision requests.
            if (solidCache.Count < 8192) solidCache.Add(box, solid);
            return solid;
        }
        private bool QuerySolid(Rectangle box)
        {
            int left = (box.Left >> 4) - cellX, right = ((box.Right - 1) >> 4) - cellX;
            int top = (box.Top >> 4) - cellY, bottom = ((box.Bottom - 1) >> 4) - cellY;
            if (left < 0 || top < 0 || right >= 24 || bottom >= 24)
            {
                foreach (Shape shape in nearby) if (Intersects(shape, box)) return true;
                return false;
            }
            for (int y = top; y <= bottom; y++)
            for (int x = left; x <= right; x++)
            {
                var bucket = cells[y * 24 + x];
                if (bucket == null) continue;
                foreach (int index in bucket) if (Intersects(nearby[index], box)) return true;
            }
            return false;
        }
        private bool Intersects(Shape shape, Rectangle box)
        {
            if (!shape.Bounds.Intersects(box)) return false;
            NarrowQueries++;
            if (shape.Edges != null) return TriangleOverlaps(box, shape.Edges);
            Rectangle overlap;
            return shape.Block.Intersects(box, out overlap) == BlockCollisionType.Collision_Blocking;
        }
        public float Friction(Vector2 center)
        {
            Rectangle box = Box(center, HammerPhysics.HeadSize + Vector2.One);
            bool snow = false;
            foreach (Shape shape in nearby)
            {
                if (!shape.Block.GetRect().Intersects(box)) continue;
                if (shape.Block is IceBlock) return 0.012f;
                if (shape.Block is SnowBlock) snow = true;
            }
            return snow ? 4.5f : 1.25f;
        }
        public bool Snow(Vector2 center, Vector2 halfSize)
        {
            Rectangle box = Box(center, halfSize);
            foreach (Shape shape in nearby)
                if (shape.Block is SnowBlock && shape.Block.GetRect().Intersects(box)) return true;
            return false;
        }
        private static bool TriangleOverlaps(Rectangle box, ErikMaths.Line[] edges)
        {
            // SAT against the actual native triangle, including full containment.
            // The native king's slope collider is left untouched.
            Vector2 center = new Vector2(box.X + box.Width / 2f, box.Y + box.Height / 2f);
            Vector2 half = new Vector2(box.Width / 2f, box.Height / 2f);
            foreach (ErikMaths.Line edge in edges)
            {
                Vector2 line = (edge.p1 - edge.p0).ToVector2();
                Vector2 axis = new Vector2(-line.Y, line.X);
                float minimum = float.MaxValue, maximum = float.MinValue;
                foreach (ErikMaths.Line vertex in edges)
                {
                    float projection = Vector2.Dot(vertex.p0.ToVector2(), axis);
                    minimum = Math.Min(minimum, projection); maximum = Math.Max(maximum, projection);
                }
                float middle = Vector2.Dot(center, axis);
                float radius = Math.Abs(axis.X) * half.X + Math.Abs(axis.Y) * half.Y;
                if (middle + radius <= minimum || middle - radius >= maximum) return false;
            }
            return true;
        }
    }

    internal sealed class HammerController : IBodyCompBehaviour, IDisposable, IStateParticipant
    {
        private static readonly FieldInfo SpriteField = typeof(PlayerEntity).GetField("m_sprite", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly PlayerEntity player;
        private readonly HammerPhysics physics = new HammerPhysics();
        private readonly NativeWorld world = new NativeWorld();
        private readonly MouseDeltaGate mouse = new MouseDeltaGate();
        private readonly List<IDisposable> leases = new List<IDisposable>();
        private HammerVisual visual;
        private HammerNativeControl nativeControl;
        private HammerSound sounds;
        private bool captured, previousVisibility, disposed;
        private sealed class AfterMovement : IBodyCompBehaviour
        {
            private readonly HammerController owner;
            internal AfterMovement(HammerController value) { owner = value; }
            public bool ExecuteBehaviour(BehaviourContext context)
            {
                if (Vector2.DistanceSquared(owner.Center, owner.physics.LastBody) > 96f * 96f) owner.world.Prepare(owner.Center);
                owner.physics.AfterBody(owner.Center, owner.world); return true;
            }
        }

        internal static void ValidateContract()
        {
            if (SpriteField == null || SpriteField.FieldType != typeof(Sprite))
                throw new NotSupportedException("Jump King player sprite contract changed");
            NativePause.ValidateContract();
            NativeWorld.ValidateContract();
            HammerNativeControl.ValidateContract();
        }

        private HammerController(PlayerEntity value) { player = value; }
        private Vector2 Center { get { return player.m_body.Position + new Vector2(9f, 13f); } }
        internal static HammerController Install(PlayerEntity player)
        {
            ValidateContract();
            var controller = new HammerController(player);
            try
            {
                BehaviorTreeComp tree = player.GetComponent<BehaviorTreeComp>();
                if (tree == null || !player.m_body.Enabled)
                    throw new InvalidOperationException("Enable Hammer King when the player body is active");
                controller.nativeControl = new HammerNativeControl(player, tree);
                controller.leases.Add(controller.nativeControl);
                controller.sounds = MoreItems.ModEntry.PreparedHammerSound;
                if (controller.sounds == null) { controller.sounds = new HammerSound(); controller.leases.Add(controller.sounds); }
                else { var audio = new JKRuntime.RuntimeScope(); audio.Defer(controller.sounds.Stop); controller.leases.Add(audio); }
                // Equipping immediately replaces movement, so unauthorized use
                // counts from installation, even before the first mouse swing.
                var pipeline = new BodyPipeline(player.m_body, !MoreItems.HammerLevelPermission.AllowsHammer(), -10000, "more-items");
                controller.leases.Add(pipeline);
                pipeline.Register(BodyPhase.BeforeWind, controller);
                HammerBodyResponse.Register(pipeline);
                pipeline.Register(BodyPhase.AfterGravity, new AfterMovement(controller));
                controller.world.Prepare(controller.Center);
                controller.physics.Reset(controller.Center, controller.world);
                controller.visual = player.GetComponent<HammerVisual>();
                if (controller.visual == null)
                {
                    controller.visual = new HammerVisual(player);
                    controller.leases.Add(new ComponentAttachment(player, controller.visual));
                }
                controller.visual.Attach(controller.physics);
                controller.leases.Add(GameState.Snapshots.Register(controller));
                controller.leases.Add(NativePause.Subscribe("more-items", delegate(bool paused, long timestamp) {
                    if (paused || !Game1.instance.IsActive) { controller.ReleaseMouse(); controller.sounds.Stop(); }
                }));
                return controller;
            }
            catch { controller.Dispose(); throw; }
        }

        public bool ExecuteBehaviour(BehaviourContext context)
        {
            if (NativePause.IsPaused || !Game1.instance.IsActive)
            {
                ReleaseMouse();
                sounds.Stop();
                // Native pause usually skips the whole entity; guard focus loss
                // here as well so an inactive window cannot simulate this body.
                return false;
            }
            Vector2 delta = ReadMouse();
            world.Prepare(Center);
            if (!nativeControl.AllowsHammer(delta)) { sounds.Stop(); return true; }
            player.m_body.Velocity = physics.Step(Center, player.m_body.Velocity, delta, world, Settings.PhysicsStrength(Settings.Strength.Value));
            sounds.Update(physics.Impacts);
            player.SetDirection(physics.Head.X >= Center.X ? 1 : -1);
            return true;
        }

        private Vector2 ReadMouse()
        {
            var bounds = Game1.instance.Window.ClientBounds;
            if (bounds.Width < 1 || bounds.Height < 1) { ReleaseMouse(); return Vector2.Zero; }
            if (!captured)
            {
                physics.Drive = Vector2.Zero;
                previousVisibility = Game1.instance.IsMouseVisible;
                Game1.instance.IsMouseVisible = false;
                captured = true;
            }
            MouseState sample = Mouse.GetState();
            Vector2 delta = mouse.Read(new Point(sample.X, sample.Y), true);
            Point center = new Point(bounds.Width / 2, bounds.Height / 2);
            Mouse.SetPosition(center.X, center.Y);
            mouse.Recenter(center);
            float scale = Math.Max(0.1f, Math.Min(bounds.Width / 480f, bounds.Height / 360f));
            return delta * (Settings.Sensitivity / scale);
        }

        private void ReleaseMouse()
        {
            mouse.Release();
            physics.Drive = Vector2.Zero;
            if (!captured) return;
            Game1.instance.IsMouseVisible = previousVisibility;
            captured = false;
        }

        public void Dispose()
        {
            if (disposed) return;
            var errors = new List<Exception>();
            try { ReleaseMouse(); } catch (Exception error) { errors.Add(error); }
            try { if (visual != null) visual.Detach(); } catch (Exception error) { errors.Add(error); }
            for (int i = leases.Count - 1; i >= 0; i--)
            {
                if (i == 0 && errors.Count != 0) break;
                try { leases[i].Dispose(); leases.RemoveAt(i); }
                catch (Exception error) { errors.Add(error); }
            }
            if (errors.Count != 0) throw new AggregateException(errors);
            disposed = true;
        }

        public string Id { get { return "hammer-king.state"; } }
        public int Version { get { return 3; } }
        private sealed class Snapshot
        {
            internal Vector2 Head, Target, LastBody, Drive;
            internal bool Contact, Ready;
        }
        public object Capture()
        {
            return new Snapshot { Head = physics.Head, Target = physics.Target, LastBody = physics.LastBody,
                Contact = physics.Contact, Ready = physics.Ready, Drive = physics.Drive };
        }
        public void Validate(object value)
        {
            var snapshot = value as Snapshot;
            if (snapshot == null || !Finite(snapshot.Head) || !Finite(snapshot.Target) || !Finite(snapshot.LastBody) || !Finite(snapshot.Drive)
                || snapshot.Drive.Length() > 48.1f
                || snapshot.Target.Length() > HammerPhysics.Reach + 0.1f)
                throw new ArgumentException("Invalid Hammer King snapshot");
        }
        private static bool Finite(Vector2 vector)
        { return !float.IsNaN(vector.X) && !float.IsNaN(vector.Y) && !float.IsInfinity(vector.X) && !float.IsInfinity(vector.Y); }
        public void Restore(object value)
        {
            Validate(value);
            var snapshot = (Snapshot)value;
            ReleaseMouse();
            physics.Head = snapshot.Head; physics.Target = snapshot.Target; physics.LastBody = snapshot.LastBody;
            physics.Drive = snapshot.Drive;
            physics.Contact = snapshot.Contact; physics.Ready = snapshot.Ready;
            if (sounds != null) sounds.Stop();
        }
    }
}
