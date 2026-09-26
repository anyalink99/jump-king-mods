using System;
using System.Collections.Generic;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace HammerKing
{
    internal static partial class Tests
    {
        private static IEnumerable<float> RockingStrengths()
        {
            // Retain the original solver-unit range independently of UI labels.
            for (int value = 50; value <= 150; value += 5)
                yield return value / 100f;
        }
        private static void Rocking()
        {
            foreach (float material in new[] { .012f, 1.25f, 4.5f })
            {
                var headWorld = new World { Grip = material };
                headWorld.Blocks.Add(new Rectangle(-100, 22, 200, 20));
                var head = Pose(Vector2.Zero, new Vector2(0, 20));
                head.Drive = new Vector2(10, 0);
                head.Step(Vector2.Zero, Vector2.Zero, new Vector2(10, 0), headWorld);
                var shaftWorld = new World { Grip = material };
                shaftWorld.Blocks.Add(new Rectangle(10, 2, 2, 10));
                var shaft = Pose(new Vector2(0, .5f), new Vector2(18, 0));
                shaft.Drive = new Vector2(10, 0);
                shaft.Step(new Vector2(0, .5f), Vector2.Zero, new Vector2(10, 0), shaftWorld);
                float headLoss = 17.1f - head.Head.X, shaftLoss = 17.1f - (shaft.Head.X - 18f);
                Check(Math.Abs(headLoss - shaftLoss * 1.5f) < .003f,
                    "Head grip is exactly 1.5 times shaft grip on ice, dry material and snow: " + material + ": " + headLoss + ", " + shaftLoss);
                Check(Math.Abs(shaftLoss - material * 2.5f) < .003f, "Sliding friction retains the full support load without a threshold drop");
            }
            var flat = new World(); flat.Blocks.Add(new Rectangle(-100, 22, 200, 20));
            foreach (float grip in new[] { .012f, 1.25f, 4.5f })
            foreach (float strength in RockingStrengths())
            foreach (float speed in new[] { -8f, -5f, -3.4f, -1f, 1f, 3.4f, 5f, 8f })
            foreach (float input in new[] { .25f, 1f, 2f, 4f })
            {
                flat.Grip = grip;
                var state = Pose(Vector2.Zero, new Vector2(0, 20)); state.Contact = true;
                Vector2 result = state.Step(Vector2.Zero, new Vector2(speed, .35f), new Vector2(Math.Sign(speed) * input, 0), flat, strength);
                Check(result.X * Math.Sign(speed) <= Math.Abs(speed) + .01f,
                    "Opposing motor input cannot accelerate existing sideways motion across the static/kinetic threshold");
            }
            foreach (float strength in RockingStrengths())
            foreach (float input in new[] { 0f, 1f, 2f, 4f })
            {
                Scene(new BoxBlock(new Rectangle(0, 300, 480, 60)));
                var body = new BodyComp(new Vector2(190, 265), 18, 26) { Velocity = new Vector2(0, .35f) };
                var physics = Pose(body.Position + new Vector2(9, 13), new Vector2(0, 20)); physics.Contact = true;
                var world = new NativeWorld();
                float maxSpeed = 0, maxIdle = 0, highest = body.Position.Y, drift = 0, oldX = body.Position.X;
                using (Responses(body))
                for (int frame = 0; frame < 240; frame++)
                {
                    Vector2 center = body.Position + new Vector2(9, 13); world.Prepare(center);
                    Vector2 mouse = frame < 120 ? new Vector2((frame / 6 % 2 == 0 ? 1 : -1) * input, 0) : Vector2.Zero;
                    body.Velocity = physics.Step(center, body.Velocity, mouse, world, strength);
                    maxSpeed = Math.Max(maxSpeed, Math.Abs(body.Velocity.X));
                    if (frame >= 150) maxIdle = Math.Max(maxIdle, Math.Abs(body.Velocity.X));
                    Tick(body); physics.AfterBody(body.Position + new Vector2(9, 13), world);
                    highest = Math.Min(highest, body.Position.Y); drift = Math.Max(drift, Math.Abs(body.Position.X - oldX));
                    Check(body.GetHitbox().Bottom <= 300 && HandleClear(body.Position + new Vector2(9, 13), physics.Head, world),
                        "Repeated rocking does not penetrate the floor with either the body or hammer");
                }
                Check(maxSpeed <= Math.Min(HammerPhysics.MaximumSpeed, 1.71f * strength * input) + .05f,
                    "Rocking cannot amplify sideways speed beyond the requested drive");
                Check(maxIdle < .01f && 265 - highest < 1f, "Sideways rocking cannot create upward launch or continuing motion after release");
                if (input <= 2f) Check(drift < 45f, "Small back-and-forth movements remain local instead of carrying the king away");
                Console.WriteLine("[ROCK] strength=" + strength + " input=" + input + " vx=" + maxSpeed.ToString("F2") + " idle=" + maxIdle.ToString("F2")
                    + " lift=" + (265 - highest).ToString("F2") + " drift=" + drift.ToString("F2"));
            }
        }
    }
}
