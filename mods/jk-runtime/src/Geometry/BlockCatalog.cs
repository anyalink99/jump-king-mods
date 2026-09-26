using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Microsoft.Xna.Framework;

namespace JKRuntime.Geometry
{
    public enum BlockVariant { Solid, Zone, Screen }
    /// <summary>Colour identity and behavior are separate. A Screen rule may be solid (Ball King) or metadata only (Mega).</summary>
    public sealed class BlockDeclaration
    {
        public string Mechanic { get; private set; }
        public Color Color { get; private set; }
        public BlockVariant Variant { get; private set; }
        public bool Solid { get; private set; }
        public BlockDeclaration(string mechanic, Color color, BlockVariant variant, bool solid)
        {
            Mechanic = ModuleDefinition.ValidId(mechanic);
            if (color.A != 255 || !Enum.IsDefined(typeof(BlockVariant), variant)) throw new ArgumentException("Opaque collision colour and known variant required");
            Color = color; Variant = variant; Solid = solid;
        }
    }
    /// <summary>Reviewed offline catalogue plus explicit live reservations. Queries never execute foreign factory code.</summary>
    public static class BlockCatalog
    {
        public sealed class CatalogData { public Factory[] factories { get; set; } public Reservation[] reservations { get; set; } public string auditDate { get; set; } }
        public sealed class Factory { public string owner { get; set; } public string factory { get; set; } public int[][] rgb_ranges { get; set; } }
        public sealed class Reservation { public string owner { get; set; } public string name { get; set; } public int[] rgb { get; set; } }
        private sealed class Entry { internal string Owner; internal BlockDeclaration[] Blocks; }
        private static readonly List<Entry> entries = new List<Entry>();
        private static CatalogData data;
        private static CatalogData Data
        {
            get
            {
                RuntimeApi.Kernel.CheckThread();
                if (data == null) using (var stream = typeof(BlockCatalog).Assembly.GetManifestResourceStream("JKRuntime.BlockCatalog.json"))
                {
                    if (stream == null) throw new InvalidOperationException("Bundled block catalogue missing");
                    using (var reader = new StreamReader(stream)) data = new JavaScriptSerializer().Deserialize<CatalogData>(reader.ReadToEnd());
                }
                return data;
            }
        }
        public static string AuditDate { get { return Data.auditDate; } }
        /// <summary>Query the reviewed catalogue and live reservations. An empty result is not a guarantee about unreviewed Workshop mods.</summary>
        public static string[] FindOwners(Color color)
        {
            if (color.A != 255) throw new ArgumentException("Transparent pixels are not free allocations");
            var owners = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var factory in Data.factories)
                if (factory.rgb_ranges.Any(r => color.R == r[0] && color.G == r[1] && color.B >= r[2] && color.B <= r[3])) owners.Add(factory.owner);
            foreach (var reservation in Data.reservations)
                if (color.R == reservation.rgb[0] && color.G == reservation.rgb[1] && color.B == reservation.rgb[2]) owners.Add(reservation.owner);
            foreach (var entry in entries) if (entry.Blocks.Any(b => b.Color == color)) owners.Add(entry.Owner);
            return owners.ToArray();
        }
        /// <summary>Reserve exact saved-map colours. Existing catalogue owners may identify their Workshop owner; colours are never remapped.</summary>
        public static IDisposable Register(string owner, IEnumerable<BlockDeclaration> declarations, string catalogOwner = null)
        {
            RuntimeApi.Kernel.CheckThread(); ModuleDefinition.ValidId(owner);
            if (declarations == null) throw new ArgumentNullException("declarations");
            var blocks = declarations.ToArray();
            if (blocks.Length == 0 || blocks.Length > 4096 || blocks.Any(b => b == null) || blocks.Select(b => b.Color).Distinct().Count() != blocks.Length)
                throw new ArgumentException("Invalid block declarations");
            foreach (var block in blocks)
            {
                string[] conflicts = FindOwners(block.Color).Where(id => id != owner && id != catalogOwner).ToArray();
                if (conflicts.Length != 0 || entries.Any(e => e.Blocks.Any(b => b.Color == block.Color)))
                    throw new InvalidOperationException("Block colour already reserved: " + block.Color + " " + string.Join(", ", conflicts));
            }
            var value = new Entry { Owner = owner, Blocks = blocks }; entries.Add(value);
            return RuntimeResources.Track(owner, "block-colours", new ActionLease(delegate { RuntimeApi.Kernel.CheckThread(); entries.Remove(value); }));
        }
        public static string[] Inspect()
        {
            RuntimeApi.Kernel.CheckThread();
            return entries.SelectMany(e => e.Blocks.Select(b => e.Owner + ":" + b.Mechanic + " " + b.Variant + " solid=" + b.Solid + " " + b.Color))
                .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }
    }
}
