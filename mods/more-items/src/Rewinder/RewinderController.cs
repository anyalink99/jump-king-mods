using System;
using System.Reflection;
using EntityComponent;
using EntityComponent.BT;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MoreItems
{
    internal sealed class RewindFrame
    {
        internal Vector2 Position;
        internal Vector2 Velocity;
        internal RewindFrame(Vector2 position, Vector2 velocity) { Position = position; Velocity = velocity; }
    }

    internal sealed class RewindFrameBuffer
    {
        private readonly RewindFrame[] tail;
        private RewindFrame anchor;
        private int tailStart;
        private int tailCount;

        internal RewindFrameBuffer(int capacity)
        {
            if (capacity < 2) throw new ArgumentOutOfRangeException("capacity");
            tail = new RewindFrame[capacity - 1];
        }

        internal int Count { get { return anchor == null ? 0 : tailCount + 1; } }

        internal RewindFrame this[int index]
        {
            get
            {
                if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException("index");
                return index == 0 ? anchor : tail[(tailStart + index - 1) % tail.Length];
            }
        }

        internal void Add(RewindFrame frame)
        {
            if (anchor == null)
            {
                anchor = frame;
                return;
            }
            if (tailCount < tail.Length)
            {
                tail[(tailStart + tailCount) % tail.Length] = frame;
                tailCount++;
                return;
            }
            tail[tailStart] = frame;
            tailStart = (tailStart + 1) % tail.Length;
        }

        internal void Clear()
        {
            anchor = null;
            tailStart = 0;
            tailCount = 0;
        }
    }

    internal sealed class RewinderController : Entity, IBodyCompBehaviour
    {
        private const int MaxFrames = 4200;
        private readonly PlayerEntity player;
        private readonly BodyComp body;
        private readonly RewindFrameBuffer frames = new RewindFrameBuffer(MaxFrames);
        private readonly IBodyCompBehaviour firstBehaviour;
        private readonly JKRuntime.Gameplay.BodyPipeline registry;
        private readonly FieldInfo groundedField;
        private readonly FieldInfo knockedField;
        private readonly FieldInfo lastVelocityField;
        private bool wasGrounded;
        private bool recording;
        private bool hasJump;
        private bool rewinding;
        private Vector2 lastGroundPosition;
        private float rewindTime;
        private float rewindDuration;
        private Vector2 rewindStart;
        private bool behaviourInstalled;
        private JKRuntime.State.StateSnapshot lastGroundState, anchorState;

        internal bool IsRewinding { get { return rewinding; } }
        internal float RewindProgress { get { return rewindDuration <= 0f ? 0f : Math.Min(1f, rewindTime / rewindDuration); } }

        internal RewinderController(PlayerEntity playerEntity)
        {
            player = playerEntity;
            body = player.m_body;
            wasGrounded = body.IsOnGround;
            lastGroundPosition = body.Position;
            foreach (IBodyCompBehaviour behaviour in body.GetBehaviourList()) { firstBehaviour = behaviour; break; }
            if (firstBehaviour == null) throw new InvalidOperationException("Could not locate Jump King body behaviour chain");
            registry = new JKRuntime.Gameplay.BodyPipeline(
                body,
                !RewinderLevelPermission.AllowsRewinder());
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            groundedField = typeof(BodyComp).GetField("_is_on_ground", flags);
            knockedField = typeof(BodyComp).GetField("_knocked", flags);
            lastVelocityField = typeof(BodyComp).GetField("_last_velocity", flags);
            if (groundedField == null || knockedField == null || lastVelocityField == null)
                throw new InvalidOperationException("Jump King body state is unavailable");
        }

        protected override void Update(float delta)
        {
            bool grounded = body.IsOnGround;
            if (!rewinding && behaviourInstalled)
            {
                registry.Remove(this);
                behaviourInstalled = false;
            }
            if (!rewinding)
            {
                if (wasGrounded && !grounded)
                {
                    frames.Clear();
                    frames.Add(new RewindFrame(lastGroundPosition, Vector2.Zero));
                    anchorState = lastGroundState;
                    frames.Add(new RewindFrame(body.Position, body.Velocity));
                    recording = true;
                    hasJump = true;
                }
                else if (recording)
                {
                    AppendFrame();
                    if (!wasGrounded && grounded) recording = false;
                }

                if (grounded) { lastGroundPosition = body.Position; lastGroundState = JKRuntime.State.GameState.Snapshots.Capture(); }
            }
            wasGrounded = grounded;
        }

        private void AppendFrame()
        {
            frames.Add(new RewindFrame(body.Position, body.Velocity));
        }

        internal bool CanRewind() { return hasJump && frames.Count > 1 && !rewinding && (anchorState == null || JKRuntime.State.GameState.Snapshots.IsCurrent(anchorState)); }

        internal bool BeginRewind()
        {
            if (!CanRewind()) return false;
            if (!registry.RegisterBefore(this, firstBehaviour)) return false;
            behaviourInstalled = true;
            recording = false;
            rewinding = true;
            rewindTime = 0f;
            rewindStart = body.Position;
            rewindDuration = MathHelper.Clamp(frames.Count / 105f, 0.42f, 1.85f);
            body.Velocity = Vector2.Zero;
            return true;
        }

        public bool ExecuteBehaviour(BehaviourContext context)
        {
            if (!rewinding) return true;
            if (anchorState != null && !JKRuntime.State.GameState.Snapshots.IsCurrent(anchorState))
            {
                rewinding = hasJump = recording = false;
                frames.Clear(); anchorState = lastGroundState = null;
                body.Velocity = Vector2.Zero; SetBodyState(false, false);
                return true;
            }
            rewindTime += Math.Max(0f, context.FrameDelta);
            float t = Math.Min(1f, rewindTime / rewindDuration);
            float eased = t * t * (3f - 2f * t);
            float index = (frames.Count - 1) * (1f - eased);
            int low = Math.Max(0, Math.Min(frames.Count - 1, (int)Math.Floor(index)));
            int high = Math.Max(0, Math.Min(frames.Count - 1, low + 1));
            float blend = index - low;
            Vector2 pathPosition = Vector2.Lerp(frames[low].Position, frames[high].Position, blend);
            if (t < 0.12f)
            {
                float entry = t / 0.12f;
                entry = entry * entry * (3f - 2f * entry);
                pathPosition = Vector2.Lerp(rewindStart, pathPosition, entry);
            }
            body.Position = pathPosition;
            body.Velocity = -Vector2.Lerp(frames[low].Velocity, frames[high].Velocity, blend);
            SetBodyState(false, false);
            if (t >= 1f) FinishRewind();
            return false;
        }

        private void FinishRewind()
        {
            if (anchorState != null) JKRuntime.State.GameState.Snapshots.Restore(anchorState);
            RewindFrame anchor = frames[0];
            body.Position = anchor.Position;
            body.Velocity = Vector2.Zero;
            SetBodyState(false, false);
            BehaviorTreeComp tree = player.GetComponent<BehaviorTreeComp>();
            if (tree != null) tree.Reset();
            rewinding = false;
            hasJump = false;
            recording = false;
            frames.Clear();
            wasGrounded = false;
            anchorState = lastGroundState = null;
        }

        private void SetBodyState(bool grounded, bool knocked)
        {
            groundedField.SetValue(body, grounded);
            knockedField.SetValue(body, knocked);
            lastVelocityField.SetValue(body, body.Velocity);
        }

        public override void Draw()
        {
            if (!rewinding || frames.Count == 0) return;
            SettingsStore.EnsureLoaded();
            if (!SettingsStore.Current.RenderRewind) return;
            Texture2D pixel = Game1.instance.contentManager.Pixel.texture;
            float t = RewindProgress;
            float cursor = (frames.Count - 1) * (1f - t);
            for (int i = 1; i <= 11; i++)
            {
                int index = Math.Max(0, (int)cursor - i * 3);
                Vector2 point = Camera.TransformVector2(frames[index].Position + new Vector2(9f, 13f));
                int size = i < 4 ? 3 : 2;
                byte alpha = (byte)Math.Max(25, 190 - i * 13);
                Color color = i < 3
                    ? new Color((byte)235, (byte)255, (byte)255, alpha)
                    : new Color((byte)75, (byte)215, (byte)255, alpha);
                Game1.spriteBatch.Draw(pixel, new Rectangle((int)point.X - size / 2, (int)point.Y - size / 2, size, size), color);
            }
        }

        protected override void OnDestroy()
        {
            if (body != null && behaviourInstalled) registry.Remove(this);
        }
    }
}
