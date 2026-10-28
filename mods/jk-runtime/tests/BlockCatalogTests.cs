using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using JKRuntime.Geometry;
using Microsoft.Xna.Framework;

namespace JKRuntime
{
    internal static class BlockCatalogTests
    {
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        public static int Main()
        {
            try
            {
                // measure first use before loading the JSON reference or touching registration
                var timer = System.Diagnostics.Stopwatch.StartNew();
                string date = BlockCatalog.AuditDate;
                Console.WriteLine("[TIME] Cold block catalogue: {0:F3} ms", timer.Elapsed.TotalMilliseconds);
                BlockCatalog.CatalogData expected, actual;
                var assembly = typeof(BlockCatalog).Assembly;
                using (var stream = assembly.GetManifestResourceStream("JKRuntime.BlockCatalog.json"))
                using (var reader = new StreamReader(stream))
                    expected = new JavaScriptSerializer().Deserialize<BlockCatalog.CatalogData>(reader.ReadToEnd());
                using (var stream = assembly.GetManifestResourceStream("JKRuntime.BlockCatalog.bin"))
                    actual = BlockCatalog.ReadCatalog(stream);
                Check(date == expected.auditDate && actual.auditDate == expected.auditDate
                    && actual.factories.Length == expected.factories.Length
                    && actual.reservations.Length == expected.reservations.Length, "Packed catalogue header differs");
                for (int i = 0; i < expected.factories.Length; i++)
                {
                    var left = expected.factories[i]; var right = actual.factories[i];
                    Check(left.owner == right.owner && left.factory == right.factory
                        && left.rgb_ranges.Length == right.rgb_ranges.Length, "Packed factory differs");
                    for (int j = 0; j < left.rgb_ranges.Length; j++)
                        Check(left.rgb_ranges[j].SequenceEqual(right.rgb_ranges[j]), "Packed range differs");
                }
                for (int i = 0; i < expected.reservations.Length; i++)
                {
                    var left = expected.reservations[i]; var right = actual.reservations[i];
                    Check(left.owner == right.owner && left.name == right.name
                        && left.rgb.SequenceEqual(right.rgb), "Packed reservation differs");
                }
                foreach (var factory in expected.factories)
                    foreach (var range in factory.rgb_ranges)
                        for (int blue = Math.Max(0, range[2] - 1); blue <= Math.Min(255, range[3] + 1); blue++)
                        {
                            var color = new Color(range[0], range[1], blue);
                            var owners = expected.factories.Where(f => f.rgb_ranges.Any(r => r[0] == color.R
                                && r[1] == color.G && r[2] <= color.B && r[3] >= color.B)).Select(f => f.owner)
                                .Concat(expected.reservations.Where(r => r.rgb.SequenceEqual(new[] { (int)color.R, color.G, color.B })).Select(r => r.owner))
                                .Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
                            Check(BlockCatalog.FindOwners(color).SequenceEqual(owners), "Packed lookup changed a range boundary");
                        }
                foreach (var reservation in expected.reservations)
                    Check(BlockCatalog.FindOwners(new Color(reservation.rgb[0], reservation.rgb[1], reservation.rgb[2])).Contains(reservation.owner),
                        "Packed lookup lost a reservation");
                var blocks = expected.reservations.Where(r => r.owner == "powers")
                    .Select(r => new BlockDeclaration("test.colour", new Color(r.rgb[0], r.rgb[1], r.rgb[2]), BlockVariant.Solid, true)).ToArray();
                var before = BlockCatalog.Inspect();
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    using (BlockCatalog.Register("powers", blocks))
                    {
                        Check(BlockCatalog.Inspect().Length == before.Length + blocks.Length, "Live colours missing");
                        bool rejected = false;
                        try { using (BlockCatalog.Register("powers", blocks)) { } }
                        catch (InvalidOperationException) { rejected = true; }
                        Check(rejected, "Duplicate live claims must still fail");
                    }
                    Check(before.SequenceEqual(BlockCatalog.Inspect()), "Released claims leaked into the next attempt");
                }
                Console.WriteLine("[OK] Packed/source catalogue parity, range boundaries, reservations and attempt ownership");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
