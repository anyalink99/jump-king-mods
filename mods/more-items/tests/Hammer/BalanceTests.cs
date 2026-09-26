using System;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace HammerKing
{
    internal static partial class Tests
    {
        private static void Balance()
        {
            var floor = new World(); floor.Blocks.Add(new Rectangle(-100, 22, 200, 20));
            foreach (Vector2 oldTarget in new[] { new Vector2(0, 28), new Vector2(12, 24), new Vector2(-12, 24) })
            {
                var landing = Pose(Vector2.Zero, new Vector2(0, 20)); landing.Target = oldTarget;
                Near(landing.Step(Vector2.Zero, new Vector2(0, .35f), Vector2.Zero, floor, 1.2f), Vector2.Zero,
                    "Reacquiring support cannot turn a free-swing aim error into a powered idle launch");
            }
            foreach (float material in new[] { .012f, 1.25f, 4.5f })
            {
                floor.Grip = material;
                float previous = 0;
                for (int sample = 0; sample <= 400; sample++)
                {
                    float input = sample * .025f;
                    var state = Pose(Vector2.Zero, new Vector2(0, 20)); state.Drive = new Vector2(input, 0);
                    state.Step(Vector2.Zero, Vector2.Zero, new Vector2(input, 0), floor);
                    float slip = state.Head.X;
                    Check(slip >= previous - .01f && slip - previous < .15f,
                        "Sliding begins continuously without a sudden loss of grip: " + material + ", " + input);
                    previous = slip;
                }
            }
            foreach (float strength in new[] { .5f, 1f, 1.2f, 1.5f })
            foreach (float extension in new[] { 16f, 20f, 27f })
            foreach (float amplitude in new[] { .25f, .5f, 1f, 2f })
            foreach (int pattern in new[] { 0, 1, 2 })
            {
                Scene(new BoxBlock(new Rectangle(0, 300, 480, 60)));
                var body = new BodyComp(new Vector2(190, 285 - extension), 18, 26) { Velocity = new Vector2(0, .2571429f) };
                var physics = Pose(body.Position + new Vector2(9, 13), new Vector2(0, extension)); physics.Contact = true;
                var world = new NativeWorld();
                float initialY = body.Position.Y, speed = 0, jerk = 0, lift = 0, drift = 0;
                Vector2 previous = Vector2.Zero;
                using (Responses(body))
                for (int frame = 0; frame < 360; frame++)
                {
                    Vector2 center = body.Position + new Vector2(9, 13); world.Prepare(center);
                    Vector2 mouse = Vector2.Zero;
                    if (frame < 240)
                    {
                        // Real hand corrections include vertical motion and uneven samples.
                        if (pattern == 0) mouse = new Vector2((float)Math.Cos(frame * .17), (float)Math.Sin(frame * .17)) * amplitude;
                        if (pattern == 1) mouse = new Vector2(frame / 6 % 2 == 0 ? amplitude : -amplitude, frame % 2 == 0 ? amplitude * .2f : -amplitude * .2f);
                        if (pattern == 2 && frame % 3 == 0) mouse = new Vector2(frame / 12 % 2 == 0 ? amplitude * 3 : -amplitude * 3, 0);
                    }
                    body.Velocity = physics.Step(center, body.Velocity, mouse, world, strength);
                    speed = Math.Max(speed, body.Velocity.Length());
                    jerk = Math.Max(jerk, (body.Velocity - previous).Length());
                    previous = body.Velocity;
                    Tick(body); physics.AfterBody(body.Position + new Vector2(9, 13), world);
                    lift = Math.Max(lift, initialY - body.Position.Y);
                    drift = Math.Max(drift, Math.Abs(body.Position.X - 190));
                    Check(body.GetHitbox().Bottom <= 300 && HandleClear(body.Position + new Vector2(9, 13), physics.Head, world),
                        "Two-dimensional corrections keep both body and shaft outside the native floor");
                    if (frame >= 300) Check(Math.Abs(body.Velocity.X) < .01f, "Releasing balance input leaves no continuing sideways drive");
                }
                if (amplitude <= .5f) Check(speed < 1f && jerk < 1f && lift < 2f && drift < 8f,
                    "Subpixel corrections remain small across contact transitions");
                if (amplitude <= 1f) Check(speed < 2f && jerk < 1.5f && lift < 8f && drift < 32f,
                    "One-pixel balance corrections cannot throw the king across the screen");
                if (pattern != 0) Check(lift < .25f, "Sideways balancing tolerates alternating vertical hand noise without launching");
                Console.WriteLine("[BALANCE] strength=" + strength + " reach=" + extension + " input=" + amplitude + " pattern=" + pattern
                    + " speed=" + speed.ToString("F2") + " dv=" + jerk.ToString("F2") + " lift=" + lift.ToString("F2") + " drift=" + drift.ToString("F2"));
            }
        }
    }
}
