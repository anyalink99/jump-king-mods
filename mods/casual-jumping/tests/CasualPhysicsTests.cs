using System;
using System.Reflection;
using System.Reflection.Emit;
using CasualJumping;

internal static class CasualPhysicsTests
{
    private const float Gravity = 0.2571428571428571f;
    private const float FullUpwardSpeed = 8.742857142857142f;
    private const float HorizontalSpeed = 3.5f;

    private static int failures;

    private static int Main()
    {
        TestMinimumJumpUsesSmoothReleaseGravity();
        TestFullJumpPreservesStockLimits();
        TestEveryHoldDurationIsMonotonicAndBounded();
        TestTakeoffAccelerationEndsNearGround();
        TestAirInputRespectsSpeedLimit();
        TestWindLayersOverAirControl();
        TestEarlyReleaseRetainsUpwardMomentum();
        TestSlopeLockWaitsForFreeFall();
        TestWallBounceTransitionsIntoSlide();
        TestLandingJumpBuffer();
        TestCoyoteJumpWindow();
        TestSandSupportsJumping();
        TestLevelPermission();
        TestLateOptionalApiResolution();
        TestSubframeChargeCoordination();
        if (failures != 0)
        {
            Console.Error.WriteLine("Casual Jumping tests failed: " + failures);
            return 1;
        }
        Console.WriteLine("[OK] Casual Jumping physics tests");
        return 0;
    }

    private static void TestMinimumJumpUsesSmoothReleaseGravity()
    {
        JumpResult tap = SimulateCasualPlus(1);
        AssertTrue(tap.Height > 3f, "minimum jump must leave the ground visibly");
        AssertTrue(tap.Height < 6f, "minimum jump must remain small");
    }

    private sealed class Rules : JKRuntime.Gameplay.IMovementRules
    { public JKRuntime.Gameplay.MovementMode Mode { get { return JKRuntime.Gameplay.MovementMode.AirControl; } } }
    private static void TestLateOptionalApiResolution()
    {
        AssertTrue(JKRuntime.Gameplay.GameFeatures.Movement == JKRuntime.Gameplay.MovementMode.Vanilla, "absent movement provider");
        using (JKRuntime.Gameplay.GameFeatures.RegisterMovement(new Rules()))
            AssertTrue(JKRuntime.Gameplay.GameFeatures.Movement == JKRuntime.Gameplay.MovementMode.AirControl, "late typed registration");
        AssertTrue(JKRuntime.Gameplay.GameFeatures.Movement == JKRuntime.Gameplay.MovementMode.Vanilla, "provider lease released");
    }

    private static void TestSubframeChargeCoordination()
    {
        string trace = "";
        using (JKRuntime.Gameplay.JumpSlot.RegisterChargePolicy(delegate { trace += "install,"; }, delegate { trace += "remove,"; }))
            JKRuntime.Gameplay.JumpSlot.Recompose(delegate { trace += "controller,"; });
        AssertTrue(trace == "install,remove,controller,install,remove,", "owned charge policy releases before controller mutation");
    }

    private static void TestFullJumpPreservesStockLimits()
    {
        float stockHeight = CasualPhysics.BallisticRise(FullUpwardSpeed, Gravity);
        int poweredFrames = CasualPhysics.PoweredFrameCount(FullUpwardSpeed, Gravity);
        JumpResult full = SimulateCasualPlus(100);
        AssertNear(153f, stockHeight, 0.0002f, "audited stock height");
        AssertNear(stockHeight, full.Height, 0.0002f, "full jump height");
        AssertEqual(77, full.AirFrames, "full jump air frames");
        AssertNear(
            245f,
            full.AirFrames
                * HorizontalSpeed
                * CasualPhysics.CasualPlusHorizontalSpeedScale,
            0.0002f,
            "full range");
    }

    private static void TestEveryHoldDurationIsMonotonicAndBounded()
    {
        float stockHeight = CasualPhysics.BallisticRise(FullUpwardSpeed, Gravity);
        int poweredFrames = CasualPhysics.PoweredFrameCount(FullUpwardSpeed, Gravity);
        float previous = -1f;
        for (int hold = 1; hold <= 50; hold++)
        {
            JumpResult result = SimulateCasualPlus(hold);
            AssertTrue(result.Height + 0.0002f >= previous, "hold height must be monotonic");
            AssertTrue(result.Height <= stockHeight + 0.0002f, "hold height exceeds stock");
            AssertTrue(result.AirFrames <= 77, "hold duration exceeds full air time");
            previous = result.Height;
        }
        AssertEqual(35, poweredFrames, "powered frame count");
        AssertTrue(
            SimulateCasualPlus(1).Height < 6f,
            "tap must remain a small jump");
        AssertTrue(
            SimulateCasualPlus(2).Height < 14f,
            "two-frame hold must remain a small jump");
        AssertTrue(
            SimulateCasualPlus(2).Height > SimulateCasualPlus(1).Height + 5f,
            "holding must increase jump height clearly");
    }

    private static void TestTakeoffAccelerationEndsNearGround()
    {
        float previous = 0f;
        float previousIncrease = float.MaxValue;
        for (int frame = 1; frame <= CasualPhysics.TakeoffFrameCount; frame++)
        {
            float speed = CasualPhysics.TakeoffUpwardSpeed(
                frame,
                FullUpwardSpeed,
                Gravity);
            AssertTrue(speed > previous, "takeoff impulse must build during push-off");
            float increase = speed - previous;
            if (frame > 1)
            {
                AssertTrue(
                    increase <= previousIncrease + 0.0001f,
                    "takeoff acceleration must ease out near the end");
            }
            previousIncrease = increase;
            previous = speed;
        }
        float heldGravity = CasualPhysics.HeldAscentGravity(
            FullUpwardSpeed,
            Gravity);
        float afterStockGravity = -previous + Gravity;
        float nextVelocity = CasualPhysics.ApplyHeldAscentGravity(
            afterStockGravity,
            Gravity,
            heldGravity);
        AssertTrue(
            Math.Abs(nextVelocity) < previous,
            "upward speed must only decrease after takeoff");
    }

    private static void TestAirInputRespectsSpeedLimit()
    {
        float velocity = HorizontalSpeed;
        for (int frame = 0; frame < 30; frame++)
        {
            velocity = CasualPhysics.ApplyDirectionalControl(
                velocity,
                -1,
                HorizontalSpeed,
                HorizontalSpeed / 6f);
            AssertTrue(Math.Abs(velocity) <= HorizontalSpeed, "air speed exceeds stock cap");
        }
        AssertNear(-HorizontalSpeed, velocity, 0.0001f, "air direction reversal");
    }

    private static void TestWindLayersOverAirControl()
    {
        AirControlState state = new AirControlState();
        float speedLimit = HorizontalSpeed
            * CasualPhysics.CasualPlusHorizontalSpeedScale;
        float acceleration = speedLimit / 6f;
        float wind = 0.1f;
        float velocity = state.Apply(
            speedLimit,
            1,
            speedLimit,
            acceleration);
        for (int frame = 1; frame <= 5; frame++)
        {
            velocity -= wind;
            AssertNear(
                speedLimit - wind * frame,
                velocity,
                0.0001f,
                "headwind must accumulate after air control");
            velocity = state.Apply(
                velocity,
                1,
                speedLimit,
                acceleration);
        }

        state.Reset();
        velocity = state.Apply(
            speedLimit,
            1,
            speedLimit,
            acceleration);
        for (int frame = 1; frame <= 5; frame++)
        {
            velocity += wind;
            AssertNear(
                speedLimit + wind * frame,
                velocity,
                0.0001f,
                "tailwind must accumulate after air control");
            velocity = state.Apply(
                velocity,
                1,
                speedLimit,
                acceleration);
        }
    }

    private static void TestEarlyReleaseRetainsUpwardMomentum()
    {
        float velocity = -6f;
        AssertNear(
            velocity + Gravity * 1.5f,
            CasualPhysics.ApplyEarlyReleaseGravity(velocity, Gravity),
            0.0001f,
            "early release gravity");
        AssertNear(
            2f,
            CasualPhysics.ApplyEarlyReleaseGravity(2f, Gravity),
            0.0001f,
            "falling velocity must be unchanged");
    }

    private static void TestSlopeLockWaitsForFreeFall()
    {
        SurfaceControlState state = new SurfaceControlState();
        state.BeginFrame(1, false, 0, false);
        state.ObserveSlopeContact(1);
        AssertTrue(
            !state.AllowsAirControl(1),
            "slope contact must block uphill air control");
        AssertTrue(
            state.AllowsAirControl(-1),
            "slope contact must allow movement away from the slope");

        state.BeginFrame(1, false, 0, false);
        state.ReleaseSlopeIfDetached(true);
        AssertTrue(
            !state.AllowsAirControl(1),
            "an adjacent slope must keep uphill air control locked");
        state.BeginFrame(1, false, 0, false);
        state.ReleaseSlopeIfDetached(false);
        AssertTrue(
            !state.AllowsAirControl(1),
            "one collision gap must not release slope control");
        state.BeginFrame(1, false, 0, false);
        state.ReleaseSlopeIfDetached(false);
        AssertTrue(
            state.AllowsAirControl(1),
            "air control must return after confirmed free fall");

        state.ObserveSlopeContact(1);
        state.BeginFrame(1, false, 0, true);
        AssertTrue(
            state.AllowsAirControl(1),
            "flat ground must clear the slope lock");
    }

    private static void TestWallBounceTransitionsIntoSlide()
    {
        SurfaceControlState state = new SurfaceControlState();
        state.BeginFrame(1, false, 0, false);
        AssertTrue(
            !state.ObserveWallCollision(1, 1),
            "first wall impact must keep the stock bounce");
        AssertTrue(!state.WallSliding, "first wall impact must not slide");

        state.BeginFrame(1, false, 0, false);
        AssertTrue(
            state.ObserveWallCollision(1, 1),
            "second held impact must suppress the bounce");
        AssertTrue(state.WallSliding, "second held impact must start wall sliding");
        AssertTrue(
            !state.AllowsAirControl(1),
            "wall slide must block input into the wall");

        state.ReleaseWallIfDetached(true);
        AssertTrue(state.WallSliding, "an adjacent wall must keep the slide active");
        state.ReleaseWallIfDetached(false);
        AssertTrue(!state.WallSliding, "the end of a wall must release the slide");
        AssertTrue(
            state.AllowsAirControl(1),
            "air control must return past the wall edge");

        state.ObserveWallCollision(1, 1);
        state.ObserveWallCollision(1, 1);
        state.BeginFrame(-1, false, 0, false);
        AssertTrue(
            state.AllowsAirControl(-1),
            "moving away must release the wall slide");
        AssertTrue(!state.WallSliding, "moving away must clear wall state");
    }

    private static void TestLandingJumpBuffer()
    {
        LandingJumpBuffer buffer = new LandingJumpBuffer();
        buffer.Update(true, true, true);
        for (int frame = 0; frame < 300; frame++)
        {
            buffer.Update(true, true, false);
        }
        buffer.Update(false, true, false);
        AssertTrue(
            buffer.TryConsumeOnGround(true, true),
            "held jump must remain buffered until landing");
        AssertTrue(
            !buffer.TryConsumeOnGround(true, true),
            "landing buffer must be consumed only once");

        buffer.Update(true, true, true);
        buffer.Update(true, false, false);
        AssertTrue(
            !buffer.TryConsumeOnGround(true, true),
            "releasing jump must cancel the landing buffer");
    }

    private static void TestCoyoteJumpWindow()
    {
        CoyoteJumpWindow window = new CoyoteJumpWindow();
        window.Update(true, true, false);
        for (int frame = 1; frame <= CoyoteJumpWindow.DurationFrames; frame++)
        {
            window.Update(true, false, false);
            AssertTrue(
                window.Available,
                "coyote window must last six airborne frames");
        }
        window.Update(true, false, false);
        AssertTrue(
            !window.Available,
            "coyote window must expire after six frames");

        window.Update(true, true, false);
        window.Update(true, false, false);
        AssertTrue(
            window.TryConsume(true),
            "coyote jump must consume an eligible press");
        AssertTrue(
            !window.TryConsume(true),
            "coyote jump must be available only once");

        window.Update(true, true, false);
        window.Update(true, false, true);
        AssertTrue(
            !window.Available,
            "an assisted takeoff must not create a second airborne jump");
    }

    private static void TestSandSupportsJumping()
    {
        AssertTrue(
            JumpSupport.IsSupported(true, false, false),
            "ground must support jumping");
        AssertTrue(
            JumpSupport.IsSupported(false, true, false),
            "active sand state must support jumping");
        AssertTrue(
            JumpSupport.IsSupported(false, false, true),
            "direct sand contact must support jumping");
        AssertTrue(
            !JumpSupport.IsSupported(false, false, false),
            "free fall must not support jumping");
    }

    private static void TestLevelPermission()
    {
        AssertTrue(
            !LevelPermission.AllowsCasual(null),
            "missing level tags must reject Casual Jumping");
        AssertTrue(
            !LevelPermission.AllowsCasual(new[] { "AllowSprinting" }),
            "unrelated level tags must not allow Casual Jumping");
        AssertTrue(
            LevelPermission.AllowsCasual(
                new[] { "AllowSprinting", "AllowCasualJumping" }),
            "the Casual Jumping level tag must allow the mod");
        AssertTrue(
            !LevelPermission.AllowsCasual(
                new[] { "AllowJetpack" }),
            "the Jetpack tag must not allow other assisted modes");
    }

    private static JumpResult SimulateCasualPlus(int heldFrames)
    {
        float position = 0f;
        float velocity = -CasualPhysics.TakeoffUpwardSpeed(
            1,
            FullUpwardSpeed,
            Gravity);
        float apex = 0f;
        int frame = 0;
        bool released = false;
        float heldGravity = CasualPhysics.HeldAscentGravity(
            FullUpwardSpeed,
            Gravity);
        while (frame < 1000)
        {
            frame++;
            if (frame >= 2
                && frame <= heldFrames
                && frame <= CasualPhysics.TakeoffFrameCount)
            {
                velocity = -CasualPhysics.TakeoffUpwardSpeed(
                    frame,
                    FullUpwardSpeed,
                    Gravity);
            }
            else if (frame > CasualPhysics.TakeoffFrameCount
                && frame <= heldFrames
                && velocity < 0f)
            {
                velocity = CasualPhysics.ApplyHeldAscentGravity(
                    velocity,
                    Gravity,
                    heldGravity);
            }
            if (frame == heldFrames + 1)
            {
                released = true;
            }
            if (released && velocity < 0f)
            {
                velocity = CasualPhysics.ApplyEarlyReleaseGravity(
                    velocity,
                    Gravity);
            }
            position += velocity;
            apex = Math.Min(apex, position);
            velocity = Math.Min(velocity + Gravity, 10f);
            if (position >= 0f && frame > 1)
            {
                return new JumpResult(-apex, frame);
            }
        }
        throw new InvalidOperationException("simulation did not land");
    }

    private static void AssertNear(float expected, float actual, float tolerance, string name)
    {
        if (Math.Abs(expected - actual) > tolerance)
        {
            failures++;
            Console.Error.WriteLine(name + ": expected " + expected + ", got " + actual);
        }
    }

    private static void AssertEqual(int expected, int actual, string name)
    {
        if (expected != actual)
        {
            failures++;
            Console.Error.WriteLine(name + ": expected " + expected + ", got " + actual);
        }
    }

    private static void AssertTrue(bool condition, string name)
    {
        if (!condition)
        {
            failures++;
            Console.Error.WriteLine(name);
        }
    }

    private struct JumpResult
    {
        internal readonly float Height;
        internal readonly int AirFrames;

        internal JumpResult(float height, int airFrames)
        {
            Height = height;
            AirFrames = airFrames;
        }
    }
}
