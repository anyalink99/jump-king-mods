using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MorphBallMod;
using JumpKing.Level;
using Microsoft.Xna.Framework;

internal static partial class MorphBallTests
{
    private static int failures;

    private static int Main(string[] args)
    {
        TestTransition();
        TestOutfitCanvas();
        TestPipelineContract();
        TestRuntimeStateLifecycle();
        TestSurfaceDirections();
        TestBlockCollisionClassification();
        TestSlopeMotion();
        TestContourGeometry();
        TestCustomGeometryProvider();
        if (args.Length > 0)
        {
            TestMouldingManorGeometry(args[0]);
        }
        TestSlopeContact();
        TestTopSurfaceGeometry();
        TestBallSlopePolicyIsolation();
        TestJumpCurve();
        TestWallBounceLimit();
        TestLandingJumpBuffer();
        TestCoyoteJumpWindow();
        TestExternalAirVelocity();
        TestSurfaceMomentum();
        TestBouncePhysics();
        TestPermission();
        TestMapPixels();
        if (failures != 0)
        {
            Console.Error.WriteLine("Ball King tests failed: " + failures);
            return 1;
        }
        Console.WriteLine("[OK] Ball King tests");
        return 0;
    }

    private static void TestOutfitCanvas()
    {
        var native = OutfitCanvas.Bounds(new Point(48, 48), new Vector2(.5f, 1f));
        var padded = OutfitCanvas.Bounds(new Point(112, 112), new Vector2(.5f, 80f / 112f));
        var canvas = Rectangle.Union(native, padded);
        AssertTrue(native.Location - canvas.Location == new Point(32, 32), "padded accessories retain base placement in the ball canvas");
        AssertTrue(padded.Location == canvas.Location, "padded accessory uses its own anchor");
        AssertTrue(native == new Rectangle(-24, -48, 48, 48), "standard outfit anchor remains unchanged");
    }

    private static void TestTransition()
    {
        MorphTransition transition = new MorphTransition();
        transition.Toggle(true);
        for (int frame = 0; frame < MorphTransition.TransitionFrames; frame++)
        {
            transition.Update();
        }
        AssertNear(1f, transition.Amount, "fold amount");
        AssertTrue(transition.UsesBallHitbox, "folded hitbox");
        transition.Toggle(true);
        for (int frame = 0; frame < MorphTransition.TransitionFrames; frame++)
        {
            transition.Update();
        }
        AssertNear(0f, transition.Amount, "unfold amount");
    }

    private static void TestSurfaceDirections()
    {
        AssertVector(new Vector2(1f, 0f), SurfaceMotion.GetTangent(MorphSurface.Ground), "ground tangent");
        AssertVector(new Vector2(0f, -1f), SurfaceMotion.GetTangent(MorphSurface.RightWall), "right wall tangent");
        AssertVector(new Vector2(-1f, 0f), SurfaceMotion.GetTangent(MorphSurface.Ceiling), "ceiling tangent");
        AssertVector(new Vector2(0f, 1f), SurfaceMotion.GetTangent(MorphSurface.LeftWall), "left wall tangent");
    }

    private static void TestSlopeMotion()
    {
        Vector2 normal = Vector2.Normalize(new Vector2(-1f, -1f));
        Vector2 tangent = SurfaceMotion.GetTangent(normal);
        AssertTrue(tangent.X > 0f && tangent.Y < 0f, "slope tangent");
        AssertTrue(
            SurfaceMotion.FromNormal(new Vector2(-1f, 0f))
                == MorphSurface.RightWall,
            "right wall normal");
        AssertTrue(
            SurfaceMotion.FromNormal(new Vector2(0f, 1f))
                == MorphSurface.Ceiling,
            "ceiling normal");
        AssertTrue(
            SurfaceMotion.FromNormal(normal) == MorphSurface.RightWall,
            "diagonal normal has a stable dominant side");
        AssertTrue(
            SurfaceMotion.IsHorizontalGround(new Vector2(0f, -1f)),
            "flat top is horizontal ground");
        AssertTrue(
            !SurfaceMotion.IsHorizontalGround(normal),
            "slope is not horizontal ground");
        Vector2 flatContact = SurfaceMotion.ClosestPointOnRectangle(
            new Rectangle(0, 19, 18, 8),
            new Vector2(9f, 9f));
        Vector2 flatOffset;
        AssertTrue(
            SurfaceMotion.TryGetContactOffset(
                new Vector2(9f, 9f),
                flatContact,
                new Vector2(0f, -1f),
                9f,
                6f,
                out flatOffset),
            "flat contact gap is measurable");
        AssertVector(
            new Vector2(0f, 1f),
            flatOffset,
            "flat contact closes only the real gap");
        Vector2 ledgeOffset;
        AssertTrue(
            SurfaceMotion.TryGetContactOffset(
                new Vector2(20f, 9f),
                new Vector2(18f, 19f),
                new Vector2(0f, -1f),
                9f,
                6f,
                out ledgeOffset),
            "ledge contact gap is measurable");
        AssertVector(
            new Vector2(0f, 1f),
            ledgeOffset,
            "ledge contact never pulls the ball along the surface");
        Vector2 slopeContact = SurfaceMotion.ClosestPointOnSlope(
            new Rectangle(0, 0, 18, 18),
            normal,
            Vector2.Zero);
        Vector2 slopeOffset;
        AssertTrue(
            SurfaceMotion.TryGetContactOffset(
                Vector2.Zero,
                slopeContact,
                normal,
                9f,
                6f,
                out slopeOffset),
            "slope contact gap is measurable");
        AssertNear(
            9f * ((float)Math.Sqrt(2f) - 1f),
            slopeOffset.Length(),
            "circle meets the actual diagonal boundary");
        AssertTrue(
            SurfaceMotion.GetUphillDirection(
                new Vector2(1f, -1f)) == -1,
            "right-descending slope blocks leftward climbing");
        AssertTrue(
            SurfaceMotion.GetUphillDirection(
                new Vector2(-1f, -1f)) == 1,
            "left-descending slope blocks rightward climbing");
        Vector2 descendingRight = Vector2.Normalize(
            new Vector2(-1f, -1f));
        Vector2 descendingLeft = Vector2.Normalize(
            new Vector2(1f, -1f));
        AssertTrue(
            MorphSlopeInputPolicy.FilterDirection(
                1,
                true,
                descendingRight) == 0
                && MorphSlopeInputPolicy.FilterDirection(
                    -1,
                    true,
                    descendingLeft) == 0,
            "slope contact blocks uphill air input symmetrically");
        AssertTrue(
            MorphSlopeInputPolicy.FilterDirection(
                -1,
                true,
                descendingRight) == -1
                && MorphSlopeInputPolicy.FilterDirection(
                    1,
                    false,
                    descendingRight) == 1,
            "downhill and detached air input remain available");
        AssertVector(
            Vector2.Zero,
            MorphSlopeInputPolicy.RemoveUphillControlFromCollision(
                new Vector2(2f, 0f),
                1,
                descendingRight),
            "horizontal input cannot become an uphill native slope step");
        Vector2 fallingSlopeContact =
            MorphSlopeInputPolicy.RemoveUphillControlFromCollision(
                new Vector2(2f, 4f),
                1,
                descendingRight);
        AssertTrue(
            fallingSlopeContact.X < 0f
                && fallingSlopeContact.Y > 0f,
            "falling contact keeps only physical downhill projection");
        AssertVector(
            new Vector2(0f, -3f),
            MorphSlopeInputPolicy.RemoveUphillControlFromCollision(
                new Vector2(2f, -3f),
                1,
                descendingRight),
            "upward flight loses slope-directed control without losing lift");
        Vector2 circleCorrection =
            SurfaceMotion.AabbToCircleContactOffset(
                Vector2.Normalize(new Vector2(1f, -1f)),
                9f,
                9f,
                9f);
        AssertNear(
            9f * ((float)Math.Sqrt(2f) - 1f),
            circleCorrection.Length(),
            "slope visual converts AABB support to circle support exactly");
        AssertTrue(
            circleCorrection.X < 0f && circleCorrection.Y > 0f,
            "circle correction points from the AABB corner toward the slope");
        Vector2 ignored;
        AssertTrue(
            !SurfaceMotion.TryNormalize(Vector2.Zero, out ignored),
            "zero surface normal is rejected before jump physics");
        AssertTrue(
            !SurfaceMotion.TryNormalize(
                new Vector2(float.NaN, -1f),
                out ignored),
            "non-finite surface normal is rejected before camera physics");
        AssertVector(
            Vector2.Zero,
            SurfaceMotion.GetTangent(new Vector2(float.NaN, 0f)),
            "non-finite normal cannot create a tangent velocity");
    }

    private static void TestSlopeContact()
    {
        SlopeBlock descendingRight = new SlopeBlock(
            new Rectangle(0, 0, 16, 16),
            SlopeType.TopRight);
        AdvCollisionInfo support = new AdvCollisionInfo(
            new List<IBlock> { descendingRight },
            false,
            SlopeType.TopRight,
            descendingRight.GetNormal());
        MorphSlopeContact contact =
            MorphSlopeContactDetector.Evaluate(support, false);
        AssertTrue(contact.Supported, "top slope supports the ball");
        AssertVector(
            Vector2.Normalize(descendingRight.GetNormal()),
            contact.Normal,
            "slope contact keeps its physical normal");
        Vector2 collisionNormal;
        AssertTrue(
            MorphSlopeContactDetector.TryGetTopSlopeNormal(
                support,
                out collisionNormal),
            "native X collision exposes its top-slope normal");
        AssertVector(
            Vector2.Normalize(descendingRight.GetNormal()),
            collisionNormal,
            "native X collision slope normal is stable");
        SlopeBlock opposite = new SlopeBlock(
            new Rectangle(16, 0, 16, 16),
            SlopeType.TopLeft);
        AssertTrue(
            !MorphSlopeContactDetector.TryGetTopSlopeNormal(
                new AdvCollisionInfo(
                    new List<IBlock> { descendingRight, opposite },
                    false,
                    SlopeType.None,
                    Vector2.Zero),
                out collisionNormal),
            "conflicting slope contacts never choose an arbitrary direction");

        MorphSlopeContact flat =
            MorphSlopeContactDetector.Evaluate(
                new AdvCollisionInfo(),
                true);
        AssertTrue(!flat.Supported, "nearby geometry is not support");
        MorphSlopeContact invalidSlope =
            MorphSlopeContactDetector.Evaluate(
                new AdvCollisionInfo(
                    new List<IBlock>(),
                    false,
                    SlopeType.TopRight,
                    Vector2.Zero),
                false);
        AssertTrue(
            !invalidSlope.Supported,
            "slope metadata without a finite normal is rejected");
        MorphSlopeContact corner =
            MorphSlopeContactDetector.Evaluate(support, true);
        AssertTrue(
            !corner.Supported,
            "slope below a flat corner does not block movement");

        Rectangle hitbox = new Rectangle(0, 0, 18, 18);
        Rectangle probe = new Rectangle(0, 17, 18, 3);
        BoxBlock flatBlock = new BoxBlock(
            new Rectangle(0, 18, 8, 8));
        AdvCollisionInfo flatSupport = new AdvCollisionInfo(
            new List<IBlock> { flatBlock },
            false,
            SlopeType.None,
            Vector2.Zero);
        AssertTrue(
            MorphSlopeContactDetector.HasFlatTopSupport(
                flatSupport,
                hitbox,
                probe),
            "actual flat top wins at a slope corner");
        BoxBlock lowerBlock = new BoxBlock(
            new Rectangle(0, 22, 18, 8));
        AdvCollisionInfo lowerGeometry = new AdvCollisionInfo(
            new List<IBlock> { lowerBlock },
            false,
            SlopeType.None,
            Vector2.Zero);
        AssertTrue(
            !MorphSlopeContactDetector.HasFlatTopSupport(
                lowerGeometry,
                hitbox,
                probe),
            "geometry below the contact plane is not flat support");
    }

    private static void TestTopSurfaceGeometry()
    {
        SlopeBlock southWest = new SlopeBlock(
            new Rectangle(100, 100, 16, 16),
            SlopeType.BottomLeft);
        List<MorphContourSegment> southWestContour =
            new List<MorphContourSegment>();
        MorphContourGeometry.AddSegments(
            southWest,
            18,
            18,
            southWestContour);
        Vector2 southWestNormal = Vector2.Normalize(
            southWest.GetNormal());
        bool hasSouthWestFace = false;
        foreach (MorphContourSegment segment in southWestContour)
        {
            if (Vector2.Dot(segment.Normal, southWestNormal) > 0.999f)
            {
                hasSouthWestFace = true;
            }
        }
        AssertTrue(
            hasSouthWestFace,
            "south-west contour preserves the declared playable face");
        AssertNear(
            1f,
            Vector2.Dot(
                Vector2.Normalize(new Vector2(-1f, 1f)),
                southWestNormal),
            "south-west contour uses its stable gameplay normal");

        SlopeBlock southEast = new SlopeBlock(
            new Rectangle(100, 100, 16, 16),
            SlopeType.BottomRight);
        Rectangle nativeSample = new Rectangle(101, 101, 3, 3);
        Rectangle southWestOverlap;
        Rectangle southEastOverlap;
        AssertTrue(
            ((IBlock)southWest).Intersects(
                nativeSample,
                out southWestOverlap)
                == ((IBlock)southEast).Intersects(
                    nativeSample,
                    out southEastOverlap),
            "Jump King builds both bottom slope collision triangles alike");
        AssertTrue(
            Vector2.Dot(
                southWest.GetNormal(),
                southEast.GetNormal()) < 0.01f,
            "bottom-left declares a different normal despite shared geometry");
    }

    private static void TestBallSlopePolicyIsolation()
    {
        using (JKRuntime.RuntimeApi.Geometry.Register("morph-ball", BallKingGeometryApi.RuntimeProfile,
            BallKingGeometryApi.RuntimeProfileVersion, MorphContourGeometry.ExportSolidPolygon))
        {
            var block = new SlopeBlock(new Rectangle(0, 0, 16, 16), SlopeType.BottomLeft);
            Vector2[] native = JKRuntime.Geometry.NativeWorldGeometry.ReadSlopeVertices(block), exported;
            AssertTrue(JKRuntime.RuntimeApi.Geometry.TryGetPolygon(BallKingGeometryApi.RuntimeProfile, 1, block, out exported)
                && exported[2] == new Vector2(16, 16), "Runtime profile exposes the corrected ball-only face");
            AssertTrue(!JKRuntime.RuntimeApi.Geometry.TryGetPolygon("native.contour", 1, block, out exported), "Ball registration is not a global native override");
            using (BallKingGeometryApi.Register(delegate(IBlock b, out Vector2[] vertices) {
                vertices = new[] { Vector2.Zero, new Vector2(7, 0), new Vector2(0, 7) }; return true;
            }))
            {
                AssertTrue(JKRuntime.RuntimeApi.Geometry.TryGetPolygon(BallKingGeometryApi.RuntimeProfile, 1, block, out exported)
                    && exported[1] == new Vector2(7, 0), "Existing third-party provider reaches Runtime profile without a new adapter");
            }
            Vector2[] after = JKRuntime.Geometry.NativeWorldGeometry.ReadSlopeVertices(block);
            AssertTrue(native[0] == after[0] && native[1] == after[1] && native[2] == after[2], "Runtime profile mutated native lines");
        }
        foreach (int size in new[] { 8, 16, 32 })
        {
            Rectangle rectangle = new Rectangle(-24, 100, size, size);
            var left = new SlopeBlock(rectangle, SlopeType.BottomLeft);
            var right = new SlopeBlock(rectangle, SlopeType.BottomRight);
            var probes = new List<Rectangle>();
            var native = new List<BlockCollisionType>();
            var overlaps = new List<Rectangle>();
            for (int y = rectangle.Top - 4; y <= rectangle.Bottom + 4; y += 2)
                for (int x = rectangle.Left - 4; x <= rectangle.Right + 4; x += 2)
                    foreach (int width in new[] { 1, 3, 18 })
                    {
                        var probe = new Rectangle(x, y, width, width);
                        Rectangle leftOverlap, rightOverlap;
                        BlockCollisionType leftHit = ((IBlock)left).Intersects(probe, out leftOverlap);
                        BlockCollisionType rightHit = ((IBlock)right).Intersects(probe, out rightOverlap);
                        AssertTrue(leftHit == rightHit && leftOverlap == rightOverlap,
                            "installed native bottom slopes retain identical collision triangles");
                        probes.Add(probe);
                        native.Add(leftHit);
                        overlaps.Add(leftOverlap);
                    }

            var leftContour = new List<MorphContourSegment>();
            var rightContour = new List<MorphContourSegment>();
            MorphContourGeometry.AddSegments(left, 18, 18, leftContour);
            MorphContourGeometry.AddSegments(right, 18, 18, rightContour);
            bool leftFace = false, rightFace = false, wrongLeftFace = false;
            foreach (MorphContourSegment segment in leftContour)
            {
                leftFace |= Vector2.Dot(segment.Normal, left.GetNormal()) > 0.999f;
                wrongLeftFace |= Vector2.Dot(segment.Normal, right.GetNormal()) > 0.999f;
            }
            foreach (MorphContourSegment segment in rightContour)
                rightFace |= Vector2.Dot(segment.Normal, right.GetNormal()) > 0.999f;
            AssertTrue(leftFace && rightFace && !wrongLeftFace,
                "ball contours keep distinct declared bottom-facing diagonals");

            for (int i = 0; i < probes.Count; i++)
            {
                Rectangle leftOverlap, rightOverlap;
                AssertTrue(((IBlock)left).Intersects(probes[i], out leftOverlap) == native[i]
                    && leftOverlap == overlaps[i], "ball contour must not repair the shared native left slope");
                AssertTrue(((IBlock)right).Intersects(probes[i], out rightOverlap) == native[i]
                    && rightOverlap == overlaps[i], "ball contour must not change other actors' collision geometry");
            }
        }
    }

    private static void TestCustomGeometryProvider()
    {
        BoxBlock custom = new BoxBlock(new Rectangle(0, 0, 16, 16));
        IDisposable registration = BallKingGeometryApi.Register(
            delegate(IBlock block, out Vector2[] vertices)
            {
                vertices = null;
                if (!object.ReferenceEquals(block, custom))
                {
                    return false;
                }
                vertices = new[]
                {
                    new Vector2(0f, 16f),
                    new Vector2(16f, 16f),
                    new Vector2(16f, 0f)
                };
                return true;
            });
        List<MorphContourSegment> supplied =
            MorphContourGeometry.BuildExterior(
                new IBlock[] { custom },
                18,
                18);
        registration.Dispose();
        List<MorphContourSegment> fallback =
            MorphContourGeometry.BuildExterior(
                new IBlock[] { custom },
                18,
                18);
        AssertTrue(
            HasDiagonalSegment(supplied),
            "custom geometry provider contributes its diagonal");
        AssertTrue(
            !HasDiagonalSegment(fallback),
            "disposing a geometry provider restores rectangle fallback");
    }

    private static void TestRuntimeStateLifecycle()
    {
        MorphSurfaceRuntimeState surfaceState =
            new MorphSurfaceRuntimeState
            {
                Surface = MorphSurface.Ceiling,
                ActiveNormal = new Vector2(0f, 1f),
                ActiveTangent = new Vector2(-1f, 0f),
                ActiveBlock = new BoxBlock(new Rectangle(0, 0, 8, 8)),
                HasActiveSegment = true,
                RollingSpeed = 2f,
                AngularVelocity = 1f,
                ManualGroundContact = true,
                ReleasedFromGroundSurface = true
            };
        surfaceState.ResetForUnmorph();
        AssertTrue(
            surfaceState.Surface == MorphSurface.None
                && surfaceState.ActiveBlock == null
                && !surfaceState.HasActiveSegment
                && surfaceState.RollingSpeed == 0f
                && surfaceState.AngularVelocity == 0f
                && !surfaceState.ManualGroundContact
                && !surfaceState.ReleasedFromGroundSurface,
            "surface runtime reset owns persistent contact state");

        MorphFrameRuntimeState frameState = new MorphFrameRuntimeState
        {
            HasTeleportCarry = true,
            TeleportCarryVelocity = Vector2.One,
            NativeContactProbeActive = true,
            NativeCapSampleActive = true,
            SuppressContinuousStickyLandingSound = true,
            TeleportedDuringNativeMovement = true
        };
        frameState.ResetForUnmorph();
        AssertTrue(
            !frameState.HasTeleportCarry
                && frameState.TeleportCarryVelocity == Vector2.Zero
                && !frameState.NativeContactProbeActive
                && !frameState.NativeCapSampleActive
                && !frameState.SuppressContinuousStickyLandingSound
                && !frameState.TeleportedDuringNativeMovement,
            "frame runtime reset owns native samples");

        MorphJumpRuntimeState jumpState = new MorphJumpRuntimeState
        {
            DoubleJumpAvailable = true,
            DepartureNormal = Vector2.One,
            DepartureTangent = Vector2.One,
            DepartureRollingSpeed = 2f,
            HasDepartureSurface = true,
            TrackingFall = true,
            FallApexY = 50f,
            PendingFloorBounce = true
        };
        jumpState.ResetForUnmorph();
        AssertTrue(
            !jumpState.DoubleJumpAvailable
                && jumpState.DepartureNormal == Vector2.Zero
                && jumpState.DepartureTangent == Vector2.Zero
                && jumpState.DepartureRollingSpeed == 0f
                && !jumpState.HasDepartureSurface
                && !jumpState.TrackingFall
                && jumpState.FallApexY == 0f
                && !jumpState.PendingFloorBounce,
            "jump runtime reset owns jump continuity");
    }

    private static bool HasDiagonalSegment(
        List<MorphContourSegment> segments)
    {
        for (int index = 0; index < segments.Count; index++)
        {
            Vector2 direction = segments[index].End - segments[index].Start;
            if (Math.Abs(direction.X) > 0.1f
                && Math.Abs(direction.Y) > 0.1f)
            {
                return true;
            }
        }
        return false;
    }

    private static void TestPipelineContract()
    {
        MorphPipelineContract.Validate();
        IList<MorphPipelineStep> steps = MorphPipelineContract.Steps;
        AssertTrue(steps.Count == 8, "native pipeline phase count");
        AssertTrue(
            steps[0].Phase == MorphPipelinePhase.Controller
                && steps[0].Placement == MorphPipelinePlacement.Before,
            "controller runs before optional movement controllers");
        AssertTrue(
            steps[1].Phase == MorphPipelinePhase.PreMovement
                && steps[1].Placement == MorphPipelinePlacement.Before,
            "velocity is captured before native movement");
        AssertTrue(
            steps[2].Phase == MorphPipelinePhase.XCollision
                && steps[2].Placement == MorphPipelinePlacement.After,
            "X contact is observed after native collision");
        AssertTrue(
            steps[3].Phase == MorphPipelinePhase.Teleport
                && steps[3].Placement == MorphPipelinePlacement.After,
            "teleports are observed after native handling");
        AssertTrue(
            steps[4].Phase == MorphPipelinePhase.YCollision
                && steps[4].Placement == MorphPipelinePlacement.After,
            "Y contact is observed after native collision");
        AssertTrue(
            steps[5].Phase == MorphPipelinePhase.PrePositionCap
                && steps[6].Phase == MorphPipelinePhase.PostPositionCap,
            "position cap is bracketed by observers");
        AssertTrue(
            steps[7].Phase == MorphPipelinePhase.BumpSound
                && steps[7].Placement == MorphPipelinePlacement.Before,
            "continuous contacts suppress bump sound before playback");
    }

    private static void TestBlockCollisionClassification()
    {
        Rectangle probe = new Rectangle(0, 0, 8, 8);
        AssertTrue(
            MorphCollisionWorld.IsBlockingContact(
                new BoxBlock(probe),
                probe),
            "solid block contributes collision geometry");
        AssertTrue(
            !MorphCollisionWorld.IsBlockingContact(
                new NonBlockingTestBlock(probe),
                probe),
            "non-blocking trigger does not contribute collision geometry");
    }

    private static void TestContourGeometry()
    {
        List<IBlock> adjacentBlocks = new List<IBlock>
        {
            new BoxBlock(new Rectangle(0, 16, 16, 16)),
            new BoxBlock(new Rectangle(16, 16, 16, 16))
        };
        List<MorphContourSegment> adjacentExterior =
            MorphContourGeometry.BuildExterior(adjacentBlocks, 18, 18);
        bool foundInternalSeam = false;
        foreach (MorphContourSegment segment in adjacentExterior)
        {
            Vector2 seamMiddle = (segment.Start + segment.End) * 0.5f;
            if (Math.Abs(seamMiddle.X + 2f) < 0.01f
                && Math.Abs(segment.Normal.X) > 0.9f)
            {
                foundInternalSeam = true;
            }
        }
        AssertTrue(
            !foundInternalSeam,
            "adjacent blocks expose no internal contact seam");

        AssertControlledSlopeEntryStops(
            new SlopeBlock(
                new Rectangle(0, 0, 16, 16),
                SlopeType.TopLeft),
            new BoxBlock(new Rectangle(-16, 16, 16, 16)),
            1,
            "left-low slope");
        AssertControlledSlopeEntryStops(
            new SlopeBlock(
                new Rectangle(0, 0, 16, 16),
                SlopeType.TopRight),
            new BoxBlock(new Rectangle(16, 16, 16, 16)),
            -1,
            "right-low slope");

        SlopeBlock cappedSlope = new SlopeBlock(
            new Rectangle(0, 0, 16, 16),
            SlopeType.TopLeft);
        List<MorphContourSegment> unsupportedCap =
            MorphContourGeometry.BuildExterior(
                new IBlock[] { cappedSlope },
                18,
                18);
        bool foundArtificialCap = false;
        foreach (MorphContourSegment segment in unsupportedCap)
        {
            if (ReferenceEquals(segment.Block, cappedSlope)
                && segment.Normal.Y < -0.99f
                && Math.Abs(segment.Normal.X) < 0.01f)
            {
                foundArtificialCap = true;
                AssertTrue(
                    !segment.RollingSurface,
                    "unsupported slope AABB cap is not rolling ground");
            }
        }
        AssertTrue(foundArtificialCap, "slope AABB cap was identified");

        BoxBlock highPlatform = new BoxBlock(
            new Rectangle(16, 0, 16, 16));
        List<MorphContourSegment> supportedCap =
            MorphContourGeometry.BuildExterior(
                new IBlock[] { cappedSlope, highPlatform },
                18,
                18);
        bool foundSupportedFlat = false;
        foreach (MorphContourSegment segment in supportedCap)
        {
            Vector2 midpoint = (segment.Start + segment.End) * 0.5f;
            if (segment.Normal.Y < -0.99f
                && midpoint.X > -2f
                && midpoint.X < 16f)
            {
                foundSupportedFlat = true;
                AssertTrue(
                    segment.RollingSurface,
                    "real high platform validates overlapping flat ground");
                AssertTrue(
                    ReferenceEquals(segment.Block, highPlatform),
                    "real high platform owns the overlapping flat ground");
            }
        }
        AssertTrue(foundSupportedFlat, "supported high flat was identified");

        List<MorphContourSegment> isolatedExterior =
            MorphContourGeometry.BuildExterior(
                new IBlock[]
                {
                    new BoxBlock(new Rectangle(0, 16, 16, 16))
                },
                18,
                18);
        MorphContourSegment isolatedTop = FindContourSegment(
            isolatedExterior,
            new Vector2(0f, -1f));
        Vector2 isolatedMiddle =
            (isolatedTop.Start + isolatedTop.End) * 0.5f;
        MorphContourSegment orientedContact;
        AssertTrue(
            MorphContourGeometry.TrySelectOrientedContact(
                isolatedExterior,
                isolatedMiddle + isolatedTop.Normal * 1.5f,
                isolatedTop.Normal,
                6f,
                out orientedContact),
            "native support resolves to its exact oriented contour");
        AssertTrue(
            Vector2.Dot(orientedContact.Normal, isolatedTop.Normal) > 0.999f,
            "oriented contour keeps the native support normal");
        MorphContourSegment continuousContact;
        AssertTrue(
            MorphContourGeometry.TrySelectContinuousContact(
                isolatedExterior,
                isolatedMiddle + isolatedTop.Normal * 1.5f,
                isolatedTop.Normal,
                2f,
                out continuousContact),
            "nearby stationary contact remains attached");
        AssertTrue(
            !MorphContourGeometry.TrySelectContinuousContact(
                isolatedExterior,
                isolatedMiddle + isolatedTop.Normal * 20f,
                isolatedTop.Normal,
                2f,
                out continuousContact),
            "distant stationary contact releases without movement input");
        MorphContourSegment impactContact;
        float impactFraction;
        AssertTrue(
            MorphContourGeometry.TrySweepContact(
                isolatedExterior,
                isolatedMiddle + isolatedTop.Normal * 1.5f,
                -isolatedTop.Normal * 5f,
                out impactContact,
                out impactFraction),
            "swept collision acquires the crossed contour");
        AssertTrue(
            impactFraction > 0f && impactFraction < 1f,
            "swept collision reports the first impact fraction");
        AssertTrue(
            !MorphContourGeometry.TrySweepContact(
                isolatedExterior,
                isolatedMiddle + isolatedTop.Normal * 0.5f,
                isolatedTop.Normal * 5f,
                out impactContact,
                out impactFraction),
            "motion away from a surface cannot immediately reattach");

        BoxBlock touchingFirst = new BoxBlock(
            new Rectangle(0, 0, 8, 8));
        BoxBlock touchingSecond = new BoxBlock(
            new Rectangle(26, 26, 8, 8));
        List<MorphContourSegment> touchingExterior =
            MorphContourGeometry.BuildExterior(
                new IBlock[] { touchingFirst, touchingSecond },
                18,
                18);
        MorphContourSegment incomingTouch = default(MorphContourSegment);
        foreach (MorphContourSegment segment in touchingExterior)
        {
            if (ReferenceEquals(segment.Block, touchingFirst)
                && segment.Normal.X > 0.99f)
            {
                incomingTouch = segment;
                break;
            }
        }
        Vector2 touchStart = incomingTouch.End
            - incomingTouch.Tangent * 0.25f;
        Vector2 touchResult;
        MorphContourSegment touchContinuation;
        AssertTrue(
            MorphContourGeometry.TryAdvance(
                touchingExterior,
                incomingTouch,
                touchStart,
                0.5f,
                out touchResult,
                out touchContinuation),
            "touching contours remain traversable");
        AssertTrue(
            ReferenceEquals(touchContinuation.Block, touchingFirst)
                && touchContinuation.Tangent.X < -0.99f,
            "touching contours keep the occupied region on one side");
        MorphContourSegment reverseTouch = default(MorphContourSegment);
        foreach (MorphContourSegment segment in touchingExterior)
        {
            if (ReferenceEquals(segment.Block, touchingFirst)
                && segment.Normal.Y > 0.99f)
            {
                reverseTouch = segment;
                break;
            }
        }
        touchStart = reverseTouch.Start
            + reverseTouch.Tangent * 0.25f;
        AssertTrue(
            MorphContourGeometry.TryAdvance(
                touchingExterior,
                reverseTouch,
                touchStart,
                -0.5f,
                out touchResult,
                out touchContinuation),
            "touching contours remain traversable in reverse");
        AssertTrue(
            ReferenceEquals(touchContinuation.Block, touchingFirst)
                && touchContinuation.Tangent.Y > 0.99f,
            "reverse traversal keeps the same contour component");

        List<IBlock> steppedBlocks = new List<IBlock>
        {
            new BoxBlock(new Rectangle(0, 32, 32, 16)),
            new BoxBlock(new Rectangle(24, 16, 16, 16))
        };
        List<MorphContourSegment> steppedExterior =
            MorphContourGeometry.BuildExterior(steppedBlocks, 18, 18);
        MorphContourSegment steppedTop = FindContourSegment(
            steppedExterior,
            new Vector2(0f, -1f));
        Vector2 steppedPosition = (steppedTop.Start + steppedTop.End) * 0.5f;
        Vector2 steppedAdvance;
        MorphContourSegment steppedContinuation;
        AssertTrue(
            MorphContourGeometry.TryAdvance(
                steppedExterior,
                steppedTop,
                steppedPosition,
                (steppedTop.End - steppedPosition).Length() + 0.5f,
                out steppedAdvance,
                out steppedContinuation),
            "step contour crosses its exact exterior vertex");

        List<MorphContourSegment> southWest =
            new List<MorphContourSegment>();
        MorphContourGeometry.AddSegments(
            new SlopeBlock(
                new Rectangle(0, 0, 16, 16),
                SlopeType.BottomLeft),
            18,
            18,
            southWest);
        MorphContourSegment southWestFace = FindContourSegment(
            southWest,
            Vector2.Normalize(new Vector2(-1f, 1f)));
        Vector2 middle = (southWestFace.Start + southWestFace.End) / 2f;
        Vector2 southWestAdvance;
        MorphContourSegment southWestContinuation;
        AssertTrue(
            MorphContourGeometry.TryAdvance(
                southWest,
                southWestFace,
                middle,
                0.5f,
                out southWestAdvance,
                out southWestContinuation),
            "south-west face has a stable directed travel interval");
    }

    private static void AssertControlledSlopeEntryStops(
        SlopeBlock slope,
        BoxBlock lowPlatform,
        int uphillDirection,
        string name)
    {
        List<MorphContourSegment> exterior =
            MorphContourGeometry.BuildExterior(
                new IBlock[] { slope, lowPlatform },
                18,
                18);
        MorphContourSegment slopeFace = default(MorphContourSegment);
        MorphContourSegment flatFace = default(MorphContourSegment);
        bool foundSlope = false;
        bool foundFlat = false;
        foreach (MorphContourSegment segment in exterior)
        {
            if (ReferenceEquals(segment.Block, slope)
                && Math.Abs(segment.Normal.X) > 0.5f
                && segment.Normal.Y < -0.5f)
            {
                slopeFace = segment;
                foundSlope = true;
            }
            if (ReferenceEquals(segment.Block, lowPlatform)
                && Math.Abs(segment.Normal.X) < 0.01f
                && segment.Normal.Y < -0.99f)
            {
                flatFace = segment;
                foundFlat = true;
            }
        }
        AssertTrue(foundSlope && slopeFace.RollingSurface, name + " slope face");
        AssertTrue(foundFlat && flatFace.RollingSurface, name + " low flat");
        Vector2 junction;
        int contourDirection;
        if ((flatFace.End - slopeFace.Start).LengthSquared() < 0.001f
            || (flatFace.End - slopeFace.End).LengthSquared() < 0.001f)
        {
            junction = flatFace.End;
            contourDirection = 1;
        }
        else
        {
            junction = flatFace.Start;
            contourDirection = -1;
        }
        Vector2 start = junction
            - flatFace.Tangent * contourDirection * 0.25f;
        Vector2 result;
        MorphContourSegment resultSegment;
        float remaining;
        bool blocked;
        bool advanced = MorphContourGeometry.TryAdvanceRolling(
            exterior,
            flatFace,
            start,
            contourDirection * 0.5f,
            true,
            uphillDirection,
            out result,
            out resultSegment,
            out remaining,
            out blocked);
        AssertTrue(!advanced && blocked, name + " controlled uphill entry stops");
        AssertVector(junction, result, name + " stops at contour junction");
        AssertTrue(remaining > 0f, name + " preserves blocked distance");

        advanced = MorphContourGeometry.TryAdvanceRolling(
            exterior,
            flatFace,
            start,
            contourDirection * 0.5f,
            true,
            0,
            out result,
            out resultSegment,
            out remaining,
            out blocked);
        AssertTrue(
            advanced && !blocked && Math.Abs(resultSegment.Normal.X) > 0.5f,
            name + " uncontrolled inertia may enter the slope");
    }

    private static void TestJumpCurve()
    {
        float jump = 7f;
        float gravity = 0.3f;
        float shortest = MorphJumpPhysics.TakeoffSpeed(1, jump, gravity);
        float longest = MorphJumpPhysics.TakeoffSpeed(
            MorphJumpPhysics.TakeoffFrameCount,
            jump,
            gravity);
        AssertTrue(shortest > 0f, "short jump starts upward");
        AssertTrue(shortest < longest, "held jump gains height");
        AssertTrue(
            MorphJumpPhysics.HeldAcceleration(jump, gravity) > 0f,
            "held jump counters gravity");
        AssertTrue(
            MorphJumpPhysics.EarlyReleaseDeceleration(gravity) > gravity,
            "released jump keeps a decelerating rise");
        MorphJumpProfile restricted =
            MorphJumpPhysics.CreateProfile(0.75f);
        AssertTrue(
            restricted.TakeoffFrames < MorphJumpPhysics.TakeoffFrameCount,
            "restricted jump shortens takeoff");
        AssertTrue(
            restricted.SustainedFrames < MorphJumpPhysics.SustainedFrames,
            "restricted jump shortens held ascent");
        AssertTrue(
            restricted.SustainedFrames == 27,
            "75 percent jump uses a 75 percent hold duration");
        MorphJumpProfile restrictedDouble =
            MorphJumpPhysics.CreateProfile(0.35f);
        AssertTrue(
            restrictedDouble.TakeoffFrames == 2
                && restrictedDouble.SustainedFrames == 13,
            "35 percent double jump shortens its complete ascent profile");
        AssertTrue(
            MorphJumpPhysics.TakeoffSpeed(
                restricted.TakeoffFrames,
                jump,
                gravity,
                restricted) < longest,
            "restricted jump lowers peak speed");
    }

    private static void TestWallBounceLimit()
    {
        MorphWallBounceState state = new MorphWallBounceState();
        state.BeginFrame(1, false, false);
        AssertTrue(
            !state.ObserveCollision(1, 1),
            "first wall impact keeps bounce");
        state.BeginFrame(1, true, false);
        AssertTrue(
            state.ObserveCollision(1, 1),
            "ground contact does not reset a held wall bounce");
        AssertTrue(state.Sliding, "wall slide remains active");
        state.ReleaseIfDetached(false);
        AssertTrue(!state.Sliding, "wall end releases slide");

        state.ObserveCollision(-1, -1);
        state.BeginFrame(1, false, false);
        AssertTrue(!state.Sliding, "opposite input resets wall state");
        state.ObserveCollision(1, 1);
        state.BeginFrame(1, false, true);
        AssertTrue(!state.Sliding, "sticky mode clears bounce limit");
    }

    private static void TestLandingJumpBuffer()
    {
        MorphLandingJumpBuffer buffer = new MorphLandingJumpBuffer();
        buffer.Update(true, true, true, true);
        AssertTrue(buffer.Buffered, "air press enters jump buffer");
        AssertTrue(
            !buffer.TryConsume(false, true),
            "airborne buffer waits for contact");
        AssertTrue(
            buffer.TryConsume(true, true),
            "held buffer jumps on contact");

        buffer.Update(false, true, true, true);
        AssertTrue(
            !buffer.Buffered,
            "available double jump blocks landing buffer");
        buffer.Update(true, true, true, true);
        buffer.Update(true, true, false, false);
        AssertTrue(!buffer.Buffered, "jump release clears buffer");
    }

    private static void TestCoyoteJumpWindow()
    {
        MorphCoyoteJumpWindow window = new MorphCoyoteJumpWindow();
        window.Update(true, false);
        window.Update(false, false);
        AssertTrue(window.Available, "leaving a surface opens coyote time");

        for (int frame = 1;
            frame < MorphCoyoteJumpWindow.DurationFrames;
            frame++)
        {
            window.Update(false, false);
        }
        AssertTrue(window.Available, "coyote time includes its sixth frame");
        window.Update(false, false);
        AssertTrue(!window.Available, "coyote time expires after six frames");

        window.Update(true, false);
        window.Update(false, false);
        AssertTrue(window.TryConsume(true), "coyote jump is consumed once");
        AssertTrue(!window.TryConsume(true), "coyote jump cannot repeat");

        window.Update(true, false);
        window.Update(false, true);
        AssertTrue(
            !window.Available,
            "a surface jump does not open another aerial jump");
    }

    private static void TestExternalAirVelocity()
    {
        MorphAirControlState state = new MorphAirControlState();
        float first = state.Apply(1f, 1, 2f, 0.2f, 0.05f);
        AssertNear(1.2f, first, "air control acceleration");
        state.Reset();
        float withWind = state.Apply(
            1.7f,
            1,
            2f,
            0.2f,
            0.05f);
        AssertNear(1.9f, withWind, "wind remains external to control");
        float released = state.Apply(
            withWind,
            0,
            2f,
            0.2f,
            0.05f);
        AssertNear(1.85f, released, "released input uses gentle drag");
    }

    private static void TestSurfaceMomentum()
    {
        float coasting = MorphSurfaceMomentum.Update(3f, 0, 3f, 1f);
        float reversing = MorphSurfaceMomentum.Update(3f, -1, 3f, 1f);
        AssertTrue(coasting > reversing, "coasting brakes more gently than reversing");
        AssertTrue(coasting < 3f && coasting > 2.8f, "surface release preserves momentum");
        float continuedCoast = MorphSurfaceMomentum.Update(coasting, 0, 3f, 1f);
        AssertTrue(continuedCoast > 2.7f, "coasting state survives another frame");
        float longCoast = 3f;
        for (int frame = 0; frame < 30; frame++)
        {
            longCoast = MorphSurfaceMomentum.Update(
                longCoast,
                0,
                3f,
                1f);
        }
        AssertTrue(
            longCoast > 1f,
            "rolling inertia remains visible after half a second");
        AssertNear(
            2f,
            MorphSurfaceMomentum.ProjectLandingSpeed(
                new Vector2(2f, 5f),
                new Vector2(1f, 0f),
                3f),
            "landing preserves tangent speed");
        AssertVector(
            new Vector2(2.5f, -0.5f),
            MorphSurfaceMomentum.ResolveLandingVelocity(
                true,
                new Vector2(2.5f, -0.5f),
                false,
                0f,
                Vector2.Zero),
            "teleport carry wins over stale native landing velocity");
        AssertVector(
            new Vector2(-1.5f, 4f),
            MorphSurfaceMomentum.ResolveLandingVelocity(
                false,
                Vector2.Zero,
                false,
                -1.5f,
                new Vector2(7f, 4f)),
            "regular landing combines pre-collision horizontal speed");
        AssertNear(
            0.75f,
            MorphSurfaceMomentum.LimitSpeed(3f, 0.75f),
            "snow caps rolling speed to one quarter");
        Vector2 descendingRight = Vector2.Normalize(
            new Vector2(1f, -1f));
        float slopeTerminal = MorphSurfaceMomentum.SlopeTerminalSpeed(
            descendingRight,
            10f);
        AssertNear(
            10f / (float)Math.Sqrt(0.5f),
            slopeTerminal,
            "slope terminal speed respects vertical fall limit");
        AssertNear(
            6f + 0.3f * (float)Math.Sqrt(0.5f),
            MorphSurfaceMomentum.ApplySlopeGravity(
                6f,
                descendingRight,
                0.3f,
                slopeTerminal),
            "gravity rolls the ball down a right slope");
        AssertTrue(
            MorphSurfaceMomentum.ApplySlopeGravity(
                0f,
                Vector2.Normalize(new Vector2(-1f, -1f)),
                0.3f,
                3f) < 0f,
            "gravity rolls the ball down a left slope");
        float coastingUphill = MorphSurfaceMomentum.ApplySlopeGravity(
            -1f,
            descendingRight,
            0.3f,
            slopeTerminal);
        AssertNear(
            coastingUphill,
            MorphSurfaceMomentum.ApplySlopeMotion(
                -1f,
                0,
                descendingRight,
                0.3f,
                slopeTerminal),
            "released uphill inertia remains physical momentum");
        AssertNear(
            MorphSurfaceMomentum.ApplySlopeGravity(
                0f,
                descendingRight,
                0.3f,
                slopeTerminal),
            MorphSurfaceMomentum.ApplySlopeMotion(
                -1f,
                -1,
                descendingRight,
                0.3f,
                slopeTerminal),
            "held uphill input cannot preserve an uphill control impulse");
        AssertTrue(
            MorphSurfaceMomentum.ApplySlopeMotion(
                0f,
                -1,
                descendingRight,
                0.3f,
                slopeTerminal) > 0f,
            "uphill input from rest still yields only downhill gravity");
        AssertTrue(
            MorphSurfaceMomentum.ApplySlopeMotion(
                0f,
                1,
                descendingRight,
                0.3f,
                slopeTerminal) > MorphSurfaceMomentum.ApplySlopeGravity(
                    0f,
                    descendingRight,
                    0.3f,
                    slopeTerminal),
            "downhill input adds slope drive");
    }

    private static void TestBouncePhysics()
    {
        Vector2 leftSlope = Vector2.Normalize(new Vector2(-1f, -1f));
        Vector2 rightSlope = Vector2.Normalize(new Vector2(1f, -1f));
        AssertTrue(
            MorphSurfaceJumpPolicy.AllowsJump(
                new Vector2(0f, -1f),
                false,
                false),
            "regular ball can jump from flat ground");
        AssertTrue(
            !MorphSurfaceJumpPolicy.AllowsJump(leftSlope, false, true)
                && !MorphSurfaceJumpPolicy.AllowsJump(
                    rightSlope,
                    false,
                    true),
            "regular ball cannot jump from either slope direction");
        AssertTrue(
            MorphSurfaceJumpPolicy.AllowsJump(leftSlope, true, true)
                && MorphSurfaceJumpPolicy.AllowsJump(
                    rightSlope,
                    true,
                    true),
            "sticky ball can jump from either slope direction");
        AssertTrue(
            !MorphSurfaceJumpPolicy.AllowsJump(
                new Vector2(0f, -1f),
                false,
                true),
            "regular ball cannot jump from a slope AABB cap");
        AssertTrue(
            MorphBouncePhysics.IsFloorBounceSurface(
                new Vector2(0f, -1f)),
            "flat floor permits a fall bounce");
        AssertTrue(
            !MorphBouncePhysics.IsFloorBounceSurface(leftSlope)
                && !MorphBouncePhysics.IsFloorBounceSurface(rightSlope),
            "neither slope direction permits a fall bounce");
        AssertTrue(
            !MorphBouncePhysics.ShouldHandleWallCollision(
                false,
                false,
                false),
            "ordinary king keeps the vanilla wall bounce");
        AssertTrue(
            MorphBouncePhysics.ShouldHandleWallCollision(
                true,
                false,
                false),
            "airborne non-sticky ball owns the wall bounce");
        AssertTrue(
            !MorphBouncePhysics.ShouldHandleWallCollision(
                true,
                true,
                false),
            "sticky ball does not use the free wall bounce");
        AssertNear(
            -2.7f,
            MorphBouncePhysics.WallVelocity(3f),
            "wall bounce returns ninety percent of impact speed");
        float gravity = 0.3f;
        AssertNear(
            0f,
            MorphBouncePhysics.FloorSpeed(99.999f, gravity),
            "short falls do not bounce");
        float speed = MorphBouncePhysics.FloorSpeed(100f, gravity);
        AssertNear(
            20f,
            speed * speed / (2f * gravity),
            "floor bounce reaches one fifth of fall height");
        AssertTrue(
            MorphBouncePhysics.IsSplatImpact(10f, 10f),
            "maximum fall speed is a vanilla splat impact");
        AssertTrue(
            !MorphBouncePhysics.IsSplatImpact(9.5f, 10f),
            "ordinary landing is below vanilla splat speed");
        float splatHeight = MorphBouncePhysics.SplatFallHeight(
            0.4f,
            10f);
        AssertNear(
            120f,
            splatHeight,
            "splat height follows discrete vanilla gravity");
        AssertTrue(
            MorphBouncePhysics.FloorBounceCount(99f, 0.4f, 10f) == 0,
            "short fall has no floor bounce");
        AssertTrue(
            MorphBouncePhysics.FloorBounceCount(100f, 0.4f, 10f) == 1,
            "regular bounce begins at one hundred pixels");
        AssertTrue(
            MorphBouncePhysics.FloorBounceCount(
                splatHeight,
                0.4f,
                10f) == 2,
            "vanilla splat height starts a two-bounce sequence");
        MorphFloorBounceState bounceState =
            new MorphFloorBounceState();
        bounceState.BeginFall(true);
        AssertTrue(
            bounceState.PrepareImpact(2),
            "splat-height fall starts bouncing");
        AssertTrue(
            bounceState.Remaining == 2 && !bounceState.IsContinuation,
            "first splat-height impact owns two bounces");
        bounceState.Consume();
        AssertTrue(
            bounceState.IsContinuation
                && bounceState.PrepareImpact(0),
            "second bounce survives below the ordinary height threshold");
        bounceState.Consume();
        AssertTrue(
            bounceState.Remaining == 0,
            "two-bounce sequence ends after its second impact");
        Vector2 slopeNormal = Vector2.Normalize(
            new Vector2(-1f, -1f));
        Vector2 surfaceVelocity = MorphBouncePhysics.SurfaceVelocity(
            new Vector2(3f, 0f),
            slopeNormal,
            2f);
        AssertNear(
            2f,
            Vector2.Dot(surfaceVelocity, slopeNormal),
            "floor bounce follows the contact normal");
        AssertNear(
            Vector2.Dot(
                new Vector2(3f, 0f),
                SurfaceMotion.GetTangent(slopeNormal)),
            Vector2.Dot(
                surfaceVelocity,
                SurfaceMotion.GetTangent(slopeNormal)),
            "floor bounce preserves tangent momentum");
    }

    private static void TestPermission()
    {
        AssertTrue(!LevelPermission.AllowsBallKing(null), "missing tag");
        AssertTrue(LevelPermission.AllowsBallKing(new[] { "AllowBallKing" }), "allowed tag");
        AssertTrue(!LevelPermission.AllowsBallKing(new[] { "AllowMorphBall" }), "old tag is not accepted");
        AssertTrue(!LevelPermission.AllowsBallKing(new[] { "AllowJetpack" }), "separate tag");
    }

    private static void TestMapPixels()
    {
        BallKingBlockFactory.ValidateContract();
        BallKingMapRules.Clear();
        AssertNear(
            1f,
            BallKingMapRules.JumpHeightScale(3, false),
            "ordinary screen jump height scale");
        BallKingMapRules.RestrictScreen(
            3,
            BallKingScreenRestriction.JumpAndSticky);
        AssertNear(
            0.75f,
            BallKingMapRules.JumpHeightScale(3, false),
            "restricted screen jump height scale");
        AssertNear(
            0.35f,
            BallKingMapRules.JumpHeightScale(3, true),
            "restricted screen double-jump height scale");
        AssertTrue(
            BallKingMapRules.DisablesSticky(3),
            "restricted screen disables sticky");
        AssertTrue(
            BallKingMapRules.DoubleJumpEnabled(3, true),
            "basic restriction preserves configured double jump");
        BallKingMapRules.RestrictScreen(
            4,
            BallKingScreenRestriction.NoDoubleJump);
        AssertTrue(
            !BallKingMapRules.DoubleJumpEnabled(4, true),
            "no-double restriction disables double jump");
        AssertNear(
            0.75f,
            BallKingMapRules.JumpHeightScale(
                4,
                BallKingMapRules.DoubleJumpEnabled(4, true)),
            "disabled double-jump mode keeps the first jump at 75 percent");
        BallKingMapRules.RestrictScreen(
            5,
            BallKingScreenRestriction.ForceDoubleJump);
        AssertTrue(
            BallKingMapRules.DoubleJumpEnabled(5, false),
            "force-double restriction enables double jump");
        AssertNear(
            0.35f,
            BallKingMapRules.JumpHeightScale(
                5,
                BallKingMapRules.DoubleJumpEnabled(5, false)),
            "forced double-jump mode makes the first jump 35 percent");
        AssertTrue(
            BallKingMapPixels.RestrictedScreen
                == new Color(160, 64, 255, 255),
            "restricted screen pixel colour");
        AssertTrue(
            BallKingMapPixels.RestrictedNoDoubleJump
                == new Color(255, 96, 64, 255),
            "no-double restriction pixel colour");
        AssertTrue(
            BallKingMapPixels.RestrictedForceDoubleJump
                == new Color(64, 128, 255, 255),
            "force-double restriction pixel colour");
        AssertTrue(
            BallKingMapPixels.StickySurface
                == new Color(255, 64, 160, 255),
            "sticky surface pixel colour");
        AssertTrue(
            BallKingMapRules.IsStickySurface(
                new BallStickyBoxBlock(new Rectangle(0, 0, 8, 8))),
            "sticky box marker");
        AssertTrue(
            BallKingMapRules.IsStickySurface(
                new BallStickySlopeBlock(
                    new Rectangle(0, 0, 8, 8),
                    SlopeType.TopLeft)),
            "sticky slope marker");
        AssertTrue(
            !BallKingMapRules.IsStickySurface(
                new BoxBlock(new Rectangle(0, 0, 8, 8))),
            "ordinary solid is not forced sticky");
    }

    private static void AssertNear(float expected, float actual, string name)
    {
        if (Math.Abs(expected - actual) > 0.0001f)
        {
            failures++;
            Console.Error.WriteLine(name + ": expected " + expected + ", got " + actual);
        }
    }

    private static void AssertVector(Vector2 expected, Vector2 actual, string name)
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
}

internal sealed class NonBlockingTestBlock : IBlock
{
    private readonly Rectangle rectangle;

    internal NonBlockingTestBlock(Rectangle value)
    {
        rectangle = value;
    }

    public BlockCollisionType Intersects(
        Rectangle hitbox,
        out Rectangle overlap)
    {
        overlap = Rectangle.Intersect(rectangle, hitbox);
        return overlap.Width > 0 && overlap.Height > 0
            ? BlockCollisionType.Collision_NonBlocking
            : BlockCollisionType.NoCollision;
    }

    public Rectangle GetRect()
    {
        return rectangle;
    }
}
