using System;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace HammerKing
{
    internal static partial class Tests
    {
        private static bool HandleClear(Vector2 root, Vector2 head, IHammerWorld world)
        {
            Vector2 tail = HammerPhysics.HandleTail(root, head);
            for (int i = 0; i <= 200; i++)
                if (world.Solid(Vector2.Lerp(tail, head, i / 200f), new Vector2(HammerPhysics.HandleRadius))) return false;
            return !world.Solid(head, HammerPhysics.HeadSize);
        }

        private static void SlidingHandle()
        {
            // Retraction keeps the rear end inside the king's silhouette.
            foreach (float reach in new[] { 8f, 10f, 28f, HammerPhysics.Reach })
            {
                Vector2 tail = HammerPhysics.HandleTail(Vector2.Zero, new Vector2(0, reach));
                Check(tail.Y >= -HammerPhysics.MaximumTail && Vector2.Distance(new Vector2(0, reach), tail) <= HammerPhysics.HandleLength + .001f,
                    "Retraction bounds both the pole and its rear overhang");
            }
            var world = new World();
            world.Blocks.Add(new Rectangle(-100, 13, 200, 20));
            foreach (float reach in new[] { 1f, HammerPhysics.MinimumReach, 16f, HammerPhysics.Reach })
            for (int angle = 0; angle <= 24; angle++)
            {
                float radians = MathHelper.Pi + angle * MathHelper.Pi / 24f;
                Vector2 offset = new Vector2((float)Math.Cos(radians), (float)Math.Sin(radians)) * reach;
                var raised = Pose(Vector2.Zero, offset);
                Near(raised.Step(Vector2.Zero, Vector2.Zero, Vector2.Zero, world), Vector2.Zero,
                    "Raised/retracted head cannot propel the king using a wooden foot");
                Check(raised.Impacts.Wood == 0 && !raised.Contact, "Rear wood stays clear of the ground below the king");
            }
            world = new World();
            world.Blocks.Add(new Rectangle(-6, -4, 3, 1));
            foreach (float speed in new[] { 4f, 20f, 100f })
            {
                Vector2 head = new Vector2(8, 0);
                head += HammerPhysics.Sweep(Vector2.Zero, head, new Vector2(0, speed), world);
                Check(head.Y < 4f && HandleClear(Vector2.Zero, head, world), "Fast tail rotation catches a one-pixel platform");
            }
            // The head does not move: the king moving under it still sweeps wood.
            Vector2 fixedHead = new Vector2(8, 0);
            Vector2 movedRoot = HammerPhysics.SweepRoot(Vector2.Zero, fixedHead, new Vector2(0, -30), world);
            Check(movedRoot.Y > -4f && HandleClear(movedRoot, fixedHead, world), "Body movement cannot rotate the tail through a platform");
            world = new World(); world.Blocks.Add(new Rectangle(4, 5, 1, 1));
            Vector2 nearRoot = new Vector2(18, 0);
            nearRoot += HammerPhysics.Sweep(Vector2.Zero, nearRoot, new Vector2(0, 24), world);
            Check(nearRoot.Y < 20f && HandleClear(Vector2.Zero, nearRoot, world), "Former uncollidable root section catches geometry: " + nearRoot + " clear=" + HandleClear(Vector2.Zero, nearRoot, world));

            Scene(new BoxBlock(new Rectangle(0, 300, 480, 60)));
            var body = new BodyComp(new Vector2(190, 274), 18, 26);
            var physics = Pose(body.Position + new Vector2(9, 13), new Vector2(0, 10));
            var native = new NativeWorld();
            float highest = body.Position.Y;
            using (Responses(body))
            for (int frame = 0; frame < 90; frame++)
            {
                Vector2 root = body.Position + new Vector2(9, 13); native.Prepare(root);
                body.Velocity = physics.Step(root, body.Velocity, frame < 6 ? new Vector2(0, 10) : Vector2.Zero, native);
                Tick(body); physics.AfterBody(body.Position + new Vector2(9, 13), native);
                highest = Math.Min(highest, body.Position.Y);
                Check(HandleClear(body.Position + new Vector2(9, 13), physics.Head, native), "Under-body push keeps the whole hammer above the floor");
            }
            Check(highest < 254f, "Retracted hammer pushes directly from beneath the native king");

            // Exercise simultaneous native body correction and pole rotation.
            Scene(new BoxBlock(new Rectangle(0, 300, 480, 60)), new BoxBlock(new Rectangle(220, 245, 35, 1)),
                new BoxBlock(new Rectangle(150, 230, 1, 70)));
            body = new BodyComp(new Vector2(190, 274), 18, 26);
            physics = new HammerPhysics();
            using (Responses(body))
            for (int frame = 0; frame < 360; frame++)
            {
                Vector2 root = body.Position + new Vector2(9, 13); native.Prepare(root);
                var input = new Vector2((float)Math.Cos(frame * .17f), (float)Math.Sin(frame * .23f)) * 18f;
                body.Velocity = physics.Step(root, body.Velocity, input, native);
                Tick(body); physics.AfterBody(body.Position + new Vector2(9, 13), native);
                Check(HandleClear(body.Position + new Vector2(9, 13), physics.Head, native), "Moving native king and shaft remain outside thin platforms: frame " + frame);
            }

            foreach (bool snow in new[] { false, true })
            {
                Scene(snow ? (IBlock)new SnowBlock(new Rectangle(0, 300, 480, 60)) : new BoxBlock(new Rectangle(0, 300, 480, 60)));
                native.Prepare(new Vector2(190, 278));
                physics = Pose(new Vector2(190, 278), new Vector2(8, 20));
                physics.Step(new Vector2(190, 278), Vector2.Zero, new Vector2(0, 10), native);
                Check(snow ? physics.Impacts.Snow > 0 && physics.Impacts.Stone == 0 : physics.Impacts.Stone > 0 && physics.Impacts.Snow == 0,
                    "Head impact routes the actual native surface to snow or stone SFX");
            }
            Console.WriteLine("[OK] Sliding pole: tail/root sweeps, thin geometry, under-body native push and snow impact routing");
        }
    }
}
