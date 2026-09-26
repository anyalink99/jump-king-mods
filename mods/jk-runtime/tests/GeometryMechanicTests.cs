using System;
using System.Linq;
using System.Reflection;
using JKRuntime.Gameplay;
using JKRuntime.Geometry;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace JKRuntime
{
    internal static class GeometryMechanicTests
    {
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private static void Throws<T>(Action action) where T : Exception
        { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
        private static Vector2[] triangle = { Vector2.Zero, Vector2.UnitX, Vector2.UnitY };
        private static bool Polygon(IBlock block, out Vector2[] vertices) { vertices = triangle; return true; }

        private static void Profiles()
        {
            var registry = new GeometryRegistry();
            var block = new BoxBlock(new Rectangle(0, 0, 16, 16));
            Vector2[] result;
            var lease = registry.Register("test.ball", "test.contour", 1, Polygon);
            Check(!registry.TryGetPolygon("test.native", 1, block, out result), "Profile leaked to native");
            Check(!registry.TryGetPolygon("test.contour", 2, block, out result), "Version mismatch accepted");
            Check(registry.TryGetPolygon("test.contour", 1, block, out result), "Matching profile missing");
            result[0] = new Vector2(999, 999);
            Check(triangle[0] == Vector2.Zero, "Provider array leaked");
            Throws<InvalidOperationException>(() => registry.Register("test.ball", "test.contour", 1, Polygon));
            var conflict = registry.Register("test.other", "test.contour", 1, Polygon);
            Throws<InvalidOperationException>(() => registry.TryGetPolygon("test.contour", 1, block, out result));
            conflict.Dispose(); lease.Dispose(); lease.Dispose();
            Check(!registry.TryGetPolygon("test.contour", 1, block, out result), "Released profile still present");
            var bad = registry.Register("test.bad", "test.bad", 1, delegate(IBlock b, out Vector2[] v) { v = new[] { Vector2.Zero }; return true; });
            Throws<InvalidOperationException>(() => registry.TryGetPolygon("test.bad", 1, block, out result)); bad.Dispose();
            var mutation = registry.Register("test.mutation", "test.mutation", 1, delegate(IBlock b, out Vector2[] v) { v = triangle; registry.Invalidate(); return true; });
            Throws<InvalidOperationException>(() => registry.TryGetPolygon("test.mutation", 1, block, out result)); mutation.Dispose();
            Check(registry.Generation == 8, "Registration generation changed unexpectedly");
        }

        private static void NativeShape()
        {
            NativeWorldGeometry.ValidateContract();
            Rectangle bounds; var expected=new Rectangle(3,7,19,31);
            foreach(IBlock block in new IBlock[]{new BoxBlock(expected),new IceBlock(expected),new SnowBlock(expected),new SandBlock(expected),new WaterBlock(expected),new NoWindBlock(expected),new QuarkBlock(expected),new SlopeBlock(expected,SlopeType.TopLeft)})
                Check(NativeWorldGeometry.TryReadNativeBounds(block,out bounds) && bounds==expected,"Exact native bounds must be available without virtual geometry calls");
            Check(!NativeWorldGeometry.TryReadNativeBounds(new ForeignBounds(),out bounds),"Unknown bounds must fail closed without calling mod code");
            Check(!NativeWorldGeometry.TryReadNativeBounds(new ForeignBox(),out bounds),"A custom BoxBlock subclass is not audited native geometry");
            Check(!NativeWorldGeometry.TryReadNativeBounds(null,out bounds),"Null is unknown geometry");
            var original = new SlopeBlock(new Rectangle(0, 0, 16, 16), SlopeType.BottomLeft);
            var copy = NativeWorldGeometry.CopySlopeCollision(original);
            Check(NativeWorldGeometry.ReadSlopeVertices(original).SequenceEqual(NativeWorldGeometry.ReadSlopeVertices(copy)), "Native lines changed");
            for (int y = -2; y < 18; y++)
                for (int x = -2; x < 18; x++)
                {
                    var box = new Rectangle(x, y, 2, 2);
                    Rectangle originalOverlap, copyOverlap;
                    Check(((IBlock)original).Intersects(box, out originalOverlap) == ((IBlock)copy).Intersects(box, out copyOverlap)
                        && originalOverlap == copyOverlap, "Native intersection changed");
                }
            var field = typeof(SlopeBlock).GetField("m_lines", BindingFlags.NonPublic | BindingFlags.Instance);
            Check(!ReferenceEquals(field.GetValue(original), field.GetValue(copy)), "Snapshot shares mutable lines");
            // A loaded slope correction must be copied verbatim, not rebuilt from the enum.
            field.SetValue(original, field.GetValue(new SlopeBlock(new Rectangle(0, 0, 16, 16), SlopeType.TopLeft)));
            var patched = NativeWorldGeometry.CopySlopeCollision(original);
            Check(NativeWorldGeometry.ReadSlopeVertices(original).SequenceEqual(NativeWorldGeometry.ReadSlopeVertices(patched)), "Loaded correction lost");
        }
        private sealed class ForeignBounds : IBlock
        {
            public Rectangle GetRect() { throw new Exception("Foreign geometry callback executed"); }
            public BlockCollisionType Intersects(Rectangle box,out Rectangle overlap) { throw new Exception("Foreign collision executed"); }
        }
        private sealed class ForeignBox : BoxBlock { internal ForeignBox() : base(new Rectangle(0,0,10,10)) { } }

        private static void Mechanics()
        {
            int reads = 0;
            var registry = new MechanicRegistry();
            var lease = registry.Register("test.owner", "test.mechanic", new Version(1, 0), MechanicEffects.Collision,
                delegate { reads++; return new MechanicState(true, false, false, MechanicSource.Controller, "Unavailable controller"); });
            Check(reads == 0, "Registration eagerly polled state");
            var report = registry.Inspect();
            Check(reads == 1 && report[0].State.Enabled && !report[0].State.Available && !report[0].State.Active, "Activation states conflated");
            Throws<InvalidOperationException>(() => registry.Register("test.other", "test.mechanic", new Version(2, 0), MechanicEffects.Collision, () => null));
            var broken = registry.Register("test.owner", "test.broken", new Version(1, 0), MechanicEffects.None, () => { throw new Exception("test failure"); });
            report = registry.Inspect();
            Check(report.Length == 2 && report[0].State == null && report[0].Error.Contains("test failure") && report[1].State != null, "Read failure hid unrelated mechanic");
            Throws<ArgumentException>(() => new MechanicState(false, true, true, MechanicSource.Setting, ""));
            broken.Dispose(); lease.Dispose();
            Check(registry.Inspect().Length == 0, "Mechanics leaked after release");
        }

        private static void Scopes()
        {
            var scope = new RuntimeScope();
            int releases = 0;
            bool fail = true;
            scope.Defer(() => releases++);
            scope.Defer(() => {
                Throws<ObjectDisposedException>(() => scope.Defer(() => { }));
                Throws<InvalidOperationException>(scope.Dispose);
                if (fail) throw new Exception("retry");
                releases++;
            });
            Throws<AggregateException>(scope.Dispose);
            Check(releases == 1, "Scope stopped at first failure");
            fail = false; scope.Dispose(); scope.Dispose();
            Check(releases == 2, "Scope retry repeated successful releases");
        }

        public static int Main()
        {
            try { Profiles(); NativeShape(); Mechanics(); Scopes(); Console.WriteLine("[OK] Profile isolation, native slope copies, on-demand mechanics and scope cleanup"); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
