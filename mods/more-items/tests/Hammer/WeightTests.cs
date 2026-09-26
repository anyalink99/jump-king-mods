using System;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace HammerKing
{
    internal static partial class Tests
    {
        private static float LaunchHeight(float input, int burst, float strength = 1f)
        {
            Scene(new BoxBlock(new Rectangle(0, 300, 480, 60)));
            var body = new BodyComp(new Vector2(190, 274), 18, 26);
            var physics = Pose(body.Position + new Vector2(9, 13), new Vector2(16, 11));
            var world = new NativeWorld();
            float highest = body.Position.Y;
            using (Responses(body))
            for (int frame = 0; frame < 180; frame++)
            {
                var center = body.Position + new Vector2(9, 13); world.Prepare(center);
                var mouse = frame < burst ? new Vector2(0, input) : frame == burst ? new Vector2(0, -45) : Vector2.Zero;
                body.Velocity = physics.Step(center, body.Velocity, mouse, world, strength);
                Tick(body); physics.AfterBody(body.Position + new Vector2(9, 13), world);
                highest = Math.Min(highest, body.Position.Y);
                Check(body.GetHitbox().Bottom <= 300, "Weighted push does not penetrate the native floor");
            }
            Check(body.IsOnGround, "Weighted launch returns to native ground");
            return 274f - highest;
        }

        private static void WeightAndMaterials()
        {
            var responseWorld = new World(); responseWorld.Blocks.Add(new Rectangle(-100, 22, 200, 20));
            var gentleResponse = Pose(Vector2.Zero, new Vector2(0, 20));
            Vector2 firstCorrection = gentleResponse.Step(Vector2.Zero, Vector2.Zero, new Vector2(0, 1), responseWorld);
            Check(firstCorrection.Y < 0 && firstCorrection.Y > -.1f, "One-pixel correction starts gently instead of instantly launching the body");
            var cappedResponse = Pose(Vector2.Zero, new Vector2(0, 20));
            cappedResponse.Drive = new Vector2(0, 8);
            Near(cappedResponse.Step(Vector2.Zero, Vector2.Zero, new Vector2(0, 8), responseWorld), new Vector2(0, -3.84f),
                "Sustained strong drive retains the configured force cap");
            float small = LaunchHeight(.5f, 4), single = LaunchHeight(10f, 1), strong = LaunchHeight(10f, 6);
            // Corrected contact still permits strong, bounded deliberate launches.
            Check(small < 16f, "Small hand adjustment stays bounded at default force: " + small);
            Check(single < 48f, "Single-sample launch stays bounded at default force: " + single);
            Check(strong > 90f && strong < 160f && strong > small + 20f, "Deliberate sustained push remains useful and bounded: " + strong);

            // A minimum/maximum reach correction must not become motor input.
            foreach (float reach in new[] { HammerPhysics.MinimumReach, HammerPhysics.Reach })
            {
                var world = new World(); world.Blocks.Add(new Rectangle(-100, (int)reach + 2, 200, 20));
                var physics = Pose(Vector2.Zero, new Vector2(0, reach)); physics.Contact = true;
                for (int i = 0; i < 120; i++)
                    Near(physics.Step(Vector2.Zero, new Vector2(0, .35f), Vector2.Zero, world), Vector2.Zero,
                        "Idle reach correction does not propel a supported king");
            }

            float[] drifts = new float[3];
            float[] gentle = new float[3];
            IBlock[] materials = {
                new IceBlock(new Rectangle(0, 300, 480, 60)),
                new BoxBlock(new Rectangle(0, 300, 480, 60)),
                new SnowBlock(new Rectangle(0, 300, 480, 60))
            };
            for (int i = 0; i < materials.Length; i++)
            {
                Scene(materials[i]);
                var center = new Vector2(190, 278);
                var physics = Pose(center, new Vector2(8, 20)); physics.Contact = true;
                physics.Drive = new Vector2(8, 0);
                var world = new NativeWorld(); world.Prepare(center);
                Vector2 start = physics.Head;
                Vector2 reaction = physics.Step(center, Vector2.Zero, new Vector2(8, 0), world);
                drifts[i] = physics.Head.X - start.X;
                Check(reaction.Length() <= 3.85f, "Even snow cannot bypass body acceleration limit");
                var slow = Pose(center, new Vector2(8, 20)); slow.Contact = true;
                slow.Drive = new Vector2(2, 0);
                slow.Step(center, Vector2.Zero, new Vector2(2, 0), world);
                gentle[i] = slow.Head.X - start.X;
                // Lifting away from any material must release without a pull
                // toward the surface, regardless of its tangential friction.
                physics.AfterBody(center, world);
                Near(physics.Step(center, Vector2.Zero, new Vector2(0, -15), world), Vector2.Zero, "Lift releases each material without adhesion");
                Check(!physics.Contact, "Head separates from ice, stone and snow");
            }
            Check(drifts[0] > 5f && drifts[0] > drifts[1] + .4f, "Ice loses much less motion to kinetic friction than dry stone: " + string.Join(",", drifts));
            Check(gentle[0] > 1f && Math.Abs(gentle[1]) < .01f && Math.Abs(gentle[2]) < .01f,
                "Gentle drag slips on ice while both dry stone and snow hold");
            Check(Math.Abs(drifts[2]) < .01f && drifts[1] > 1f, "Snow strongly holds a drag that slides on dry stone");
            float[] shaftDrifts = new float[2];
            for (int i = 0; i < 2; i++)
            {
                // The ledge touches only the middle of a horizontal shaft. The
                // head is clear, so material must be read at the actual contact.
                var ledge = new Rectangle(196, 248, 6, 10);
                Scene(i == 0 ? (IBlock)new IceBlock(ledge) : new SnowBlock(ledge));
                var center = new Vector2(190, 246.5f);
                var world = new NativeWorld(); world.Prepare(center);
                var physics = Pose(center, new Vector2(18, 0)); physics.Contact = true;
                physics.Drive = new Vector2(6, 0);
                Vector2 start = physics.Head;
                Check(!world.Solid(start, HammerPhysics.HeadSize), "Shaft-only material fixture leaves head clear");
                physics.Step(center, Vector2.Zero, new Vector2(6, 0), world);
                shaftDrifts[i] = physics.Head.X - start.X;
            }
            Check(shaftDrifts[0] > 5f && Math.Abs(shaftDrifts[1]) < .01f,
                "Shaft friction uses its contact material: " + string.Join(",", shaftDrifts));
            Console.WriteLine("[OK] Weighted native launches: small=" + small.ToString("F1") + ", single=" + single.ToString("F1") + ", sustained=" + strong.ToString("F1") + " px");
            Console.WriteLine("[OK] Same drag on installed materials: ice=" + drifts[0].ToString("F2") + ", stone=" + drifts[1].ToString("F2") + ", snow=" + drifts[2].ToString("F2") + " px; no idle reach spring or sticky release");
        }
    }
}
