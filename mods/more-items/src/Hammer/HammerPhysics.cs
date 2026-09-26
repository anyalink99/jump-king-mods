using System;
using Microsoft.Xna.Framework;

namespace HammerKing
{
    internal interface IHammerWorld
    {
        bool Solid(Vector2 center, Vector2 halfSize);
        float Friction(Vector2 center);
        bool Snow(Vector2 center, Vector2 halfSize);
    }

    // Game velocity is measured in pixels per native 60 Hz update.
    // This controller applies only contact reaction; the native body integrates
    // movement, gravity and materials afterwards.
    internal sealed class HammerPhysics
    {
        internal const float Reach = 28f;
        internal const float MinimumReach = 8f;
        internal const float HandleLength = 24f;
        internal const float MaximumTail = 4f;
        internal const float HandleRadius = 1f;
        internal const float MaximumSpeed = 12f;
        private const float LoadedMotorRatio = 0.57f;
        private const float MaximumMotorAcceleration = 1.28f;
        private const float BodyResponse = 3f;
        private const float DriveSmoothing = .4f;
        private const float FullDriveSpeed = 4f;
        internal static readonly Vector2 HeadSize = new Vector2(3f, 2f);
        internal Vector2 Head, Target, LastBody, Drive;
        internal bool Ready, Contact;
        internal HammerImpacts Impacts;

        internal void Reset(Vector2 center, IHammerWorld world)
        {
            Ready = true;
            Contact = false;
            Impacts = new HammerImpacts();
            Drive = Vector2.Zero;
            LastBody = center;
            Target = new Vector2(21f, -15f);
            Head = center;
            // Prefer a useful extended pose, but never spawn through a wall.
            for (int i = 0; i < 16; i++)
            {
                float angle = -0.6f + i * MathHelper.TwoPi / 16f;
                Vector2 candidate = center + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 26f;
                if (world.Solid(candidate, HeadSize) || ShaftBlocked(center, candidate, world)) continue;
                Head = candidate;
                Target = candidate - center;
                return;
            }
        }

        internal Vector2 Step(Vector2 center, Vector2 velocity, Vector2 mouse, IHammerWorld world, float strength = 1f)
        {
            strength = float.IsNaN(strength) || float.IsInfinity(strength) ? 1f : MathHelper.Clamp(strength, .5f, 1.5f);
            if (!Ready || Vector2.DistanceSquared(center, LastBody) > 96f * 96f) Reset(center, world);
            else AfterBody(center, world);
            // Resolve contact from a short velocity filter, not from a stored
            // aim error. Uneven mouse samples must not become instant reversals.
            Drive += (Limit(mouse, 48f) - Drive) * DriveSmoothing;
            if (Drive.LengthSquared() < .000001f) Drive = Vector2.Zero;
            Vector2 offset = Head - center;
            Target = ClampReach(Target + Limit(mouse, 48f));
            // Contact rebases the target each frame. Compare in the same reach
            // domain so the minimum/maximum reach cannot act as a powered spring
            // while the mouse is still.
            Vector2 motor = Limit(Target - (Contact ? ClampReach(offset) : offset), 10f);
            // Solve this frame's relative motion, rather than repeatedly adding
            // an impulse for an old positional error (an undamped spring).
            Vector2 start = Head;
            HammerImpacts impacts;
            bool contact;
            Vector2 movement = ResolveMotion(center, start, velocity + motor, world, out contact, out impacts);
            Vector2 result = velocity;
            if (contact)
            {
                // Only current hand motion may power a contact. A free hammer's
                // positional catch-up error must not launch the body on landing.
                // Fine corrections ease into the loaded drive; deliberate swings
                // retain the full strength range. Passive support stays firm.
                Vector2 relative = ClampReach(offset);
                motor = Limit(ClampReach(relative + Drive) - relative, 10f);
                motor *= LoadedMotorRatio * strength * BodyResponse * Math.Min(1f, motor.Length() / FullDriveSpeed);
                movement = ResolveMotion(center, start, velocity + motor, world, out contact, out impacts);
                bool passiveContact;
                HammerImpacts ignored;
                Vector2 passive = ResolveMotion(center, start, velocity, world, out passiveContact, out ignored);
                Vector2 goal = movement - motor;
                result = Limit(passive + Limit(goal - passive, MaximumMotorAcceleration * strength * BodyResponse), MaximumSpeed);
            }
            Impacts = impacts;
            Contact = contact;
            Head = start + movement;
            // Moving the king rotates/translates the shaft too, even when its
            // head is planted. Sweep that motion before native body integration.
            result = SweepRoot(center, Head, result, world);
            LastBody = center + result;
            return result;
        }

        private static Vector2 ResolveMotion(Vector2 center, Vector2 start, Vector2 desired, IHammerWorld world,
            out bool contact, out HammerImpacts impacts)
        {
            Vector2 movement = Sweep(center, start, desired, world, out impacts);
            Vector2 swept = movement;
            bool supported = false;
            foreach (Vector2 normal in ContactNormals)
            {
                float normalMotion = Vector2.Dot(desired, normal);
                // Outward motion immediately releases the head, including an
                // explicit lift off a floor. Probes do not glue it to a wall.
                if (normalMotion > 0.001f) continue;
                Vector2 probe = start + movement - normal * 0.75f;
                Vector2 contactPoint = probe;
                bool headContact = world.Solid(probe, HeadSize);
                if (!headContact && !ShaftContact(center, probe, world, out contactPoint)) continue;
                float friction = MathHelper.Clamp(world.Friction(contactPoint), 0f, 5f) * (headContact ? 1.5f : 1f);
                supported = true;
                Vector2 tangent = new Vector2(-normal.Y, normal.X);
                float slide = Vector2.Dot(movement, tangent);
                float load = Math.Max(0f, -normalMotion);
                // A head resting on an upward-facing surface retains static
                // grip between input samples. Vertical walls need real pressure.
                float restingLoad = normal.Y < -0.5f ? 2.5f : 0f;
                float staticBudget = friction * (load + restingLoad);
                // A continuous Coulomb cap: crossing the slip threshold must
                // not suddenly remove most of the resistance in one update.
                float resistance = Math.Min(Math.Abs(slide), staticBudget);
                movement -= tangent * Math.Sign(slide) * resistance;
            }
            if (movement != swept) movement = Sweep(center, start, movement, world);
            Vector2 blocked = desired - movement;
            contact = supported || blocked.LengthSquared() > 0.0001f;
            return movement;
        }

        private static readonly Vector2[] ContactNormals = {
            new Vector2(0f, -1f), new Vector2(0f, 1f), new Vector2(-1f, 0f), new Vector2(1f, 0f)
        };

        internal void AfterBody(Vector2 center, IHammerWorld world)
        {
            if (!Ready) return;
            if (Vector2.DistanceSquared(center, LastBody) > 96f * 96f) { Reset(center, world); return; }
            // Native body collisions may shorten the predicted move. Carry a
            // free hammer by that correction; keep a supported head in place.
            if (!Contact) Head += Sweep(center, Head, center - LastBody, world);
            if (Contact) Target = ClampReach(Head - center);
            LastBody = center;
        }

        internal static Vector2 ClampReach(Vector2 value)
        {
            float length = value.Length();
            if (length < 0.001f) return new Vector2(MinimumReach, 0);
            return value * (MathHelper.Clamp(length, MinimumReach, Reach) / length);
        }

        internal static Vector2 Limit(Vector2 value, float maximum)
        {
            float length = value.Length();
            return length > maximum ? value * (maximum / length) : value;
        }

        internal static Vector2 Sweep(Vector2 center, Vector2 start, Vector2 movement, IHammerWorld world)
        {
            HammerImpacts ignored;
            return Sweep(center, start, movement, world, out ignored);
        }

        internal static Vector2 Sweep(Vector2 center, Vector2 start, Vector2 movement, IHammerWorld world, out HammerImpacts impacts)
        {
            impacts = new HammerImpacts();
            if (movement == Vector2.Zero) return Vector2.Zero;
            Vector2 position = start;
            for (float progress = 0; progress < 1f; )
            {
                float fraction = Math.Min(1f - progress, MotionFraction(center, position, movement));
                Vector2 step = movement * fraction;
                progress += fraction;
                Vector2 before = position;
                Vector2 candidate = position + new Vector2(step.X, 0f);
                if (step.X != 0 && !Blocked(center, candidate, world, Math.Abs(movement.X), ref impacts)) position = candidate;
                candidate = position + new Vector2(0f, step.Y);
                if (step.Y != 0 && !Blocked(center, candidate, world, Math.Abs(movement.Y), ref impacts)) position = candidate;
                if (position == before) break;
            }
            return position - start;
        }

        private static bool Blocked(Vector2 center, Vector2 candidate, IHammerWorld world, float speed, ref HammerImpacts impacts)
        {
            if (world.Solid(candidate, HeadSize))
            {
                if (world.Snow(candidate, HeadSize)) impacts.Snow = Math.Max(impacts.Snow, speed);
                else impacts.Stone = Math.Max(impacts.Stone, speed);
                return true;
            }
            if (ShaftBlocked(center, candidate, world)) { impacts.Wood = Math.Max(impacts.Wood, speed); return true; }
            return false;
        }

        private static bool ShaftBlocked(Vector2 center, Vector2 head, IHammerWorld world)
        {
            Vector2 ignored;
            return ShaftContact(center, head, world, out ignored);
        }

        private static bool ShaftContact(Vector2 center, Vector2 head, IHammerWorld world, out Vector2 contactPoint)
        {
            contactPoint = head;
            Vector2 tail = HandleTail(center, head);
            float length = Vector2.Distance(head, tail);
            Vector2 direction = (head - tail) / length;
            // Overlapping samples cover the complete visible handle, including
            // its sliding tail behind the grip. No uncollidable root section.
            for (float distance = 0f; distance <= length - 2f; distance += 1f)
            {
                Vector2 point = tail + direction * distance;
                // Half a sample of padding covers the gaps along diagonals.
                if (world.Solid(point, new Vector2(HandleRadius + .5f))) { contactPoint = point; return true; }
            }
            return false;
        }

        internal static Vector2 HandleTail(Vector2 center, Vector2 head)
        {
            Vector2 direction = head - center;
            float length = direction.Length();
            direction = length > .001f ? direction / length : Vector2.UnitX;
            // Retraction is hidden inside the king. Keep the rear end inside
            // the body's silhouette instead of allowing an inverted wooden foot.
            return head - direction * Math.Min(HandleLength, length + MaximumTail);
        }

        private static float MotionFraction(Vector2 center, Vector2 head, Vector2 delta)
        {
            // Re-evaluate the local radius after each small move. Using the
            // smallest radius of the entire requested path made every far-away
            // segment pay the cost of a near-hinge rotation (thousands of steps).
            float radius = Math.Max(1f, Vector2.Distance(head, center) - .4f);
            return Math.Min(1f, .4f / (delta.Length() * (1f + HandleLength / radius)));
        }

        internal static Vector2 SweepRoot(Vector2 center, Vector2 head, Vector2 delta, IHammerWorld world)
        {
            Vector2 root = center;
            for (int axisIndex = 0; axisIndex < 2; axisIndex++)
            {
                Vector2 axis = axisIndex == 0 ? new Vector2(delta.X, 0) : new Vector2(0, delta.Y);
                if (axis == Vector2.Zero) continue;
                for (float progress = 0; progress < 1f; )
                {
                    float fraction = Math.Min(1f - progress, MotionFraction(root, head, axis));
                    Vector2 step = axis * fraction;
                    if (ShaftBlocked(root + step, head, world)) break;
                    root += step;
                    progress += fraction;
                }
            }
            return root - center;
        }
    }

    internal sealed class MouseDeltaGate
    {
        private bool ready;
        private Point previous;
        internal void Release() { ready = false; }
        internal Vector2 Read(Point sample, bool active)
        {
            if (!active) { Release(); return Vector2.Zero; }
            Vector2 delta = ready ? (sample - previous).ToVector2() : Vector2.Zero;
            previous = sample;
            ready = true;
            return delta;
        }
        internal void Recenter(Point center) { previous = center; }
    }
}
