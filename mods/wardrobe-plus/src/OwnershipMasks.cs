using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using JumpKing.MiscEntities.WorldItems;

namespace WardrobePlus
{
    internal sealed class OwnershipMask
    {
        internal readonly bool[] Visible;
        internal readonly bool[] Occluded;

        internal OwnershipMask(bool[] visible, bool[] occluded)
        { Visible = visible; Occluded = occluded; }
    }

    // ownership belongs to the native slot and pose, never to a collection ID
    // resolve the selected artwork first, use these immutable masks before fitting
    internal static class OwnershipMasks
    {
        public sealed class Document
        {
            public int schema;
            public List<Frame> frames;
            public List<Item> items;
        }
        public sealed class Item { public string item; public List<Frame> frames; }
        public sealed class Frame
        {
            public string group;
            public int key, width, height;
            public List<int[]> rows, occluded, foreign;
        }

        private static readonly string[] Groups = { "Regular", "Babe", "BabeCouple", "Ending1Misc",
            "NBPKing", "NBPBabe", "OwlKing", "OwlBabe", "OwlGargoyle", "OwlBird" };
        private static readonly Dictionary<int, Dictionary<string, OwnershipMask>> ItemsBySlot = Load();
        private static readonly Dictionary<int, Dictionary<string, OwnershipMask>> EmbeddedBySlot = LoadEmbedded();

        internal static IEnumerable<int> EmbeddedItems { get { return EmbeddedBySlot.Keys; } }

        private static Dictionary<int, Dictionary<string, OwnershipMask>> LoadEmbedded()
        {
            var result = new Dictionary<int, Dictionary<string, OwnershipMask>>();
            foreach (var item in Read("embedded-masks.json", 1).items)
            {
                var frames = Frames(item.frames);
                foreach (var frame in item.frames)
                {
                    var key = Key(frame.group, frame.key, frame.width, frame.height);
                    var body = ItemsBySlot[NativeAppearance.BaseItem][key];
                    for (int i = 0; i < body.Visible.Length; i++)
                        if (body.Visible[i] && frames[key].Visible[i])
                            throw new InvalidDataException("Embedded item overlaps body ownership");
                }
                result.Add((int)Enum.Parse(typeof(Items), item.item), frames);
            }
            return result;
        }

        private static Dictionary<int, Dictionary<string, OwnershipMask>> Load()
        {
            var result = new Dictionary<int, Dictionary<string, OwnershipMask>>();
            var body = Read("king-mask.json", 2);
            result.Add(NativeAppearance.BaseItem, Frames(body.frames));
            foreach (var item in Read("item-masks.json", 1).items)
                result.Add((int)Enum.Parse(typeof(Items), item.item), Frames(item.frames));
            return result;
        }

        private static Document Read(string name, int schema)
        {
            using (var stream = typeof(OwnershipMasks).Assembly.GetManifestResourceStream("WardrobePlus." + name))
            using (var reader = new StreamReader(stream))
            {
                var document = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }.Deserialize<Document>(reader.ReadToEnd());
                if (document.schema != schema) throw new InvalidDataException("Unsupported ownership mask: " + name);
                return document;
            }
        }

        private static Dictionary<string, OwnershipMask> Frames(IEnumerable<Frame> source)
        {
            var result = new Dictionary<string, OwnershipMask>();
            foreach (var frame in source)
            {
                if (Array.IndexOf(Groups, frame.group) < 0 || frame.width < 1 || frame.height < 1
                    || frame.width > 512 || frame.height > 512)
                    throw new InvalidDataException("Invalid ownership frame");
                var visible = Channel(frame, frame.rows);
                var occluded = Channel(frame, frame.occluded);
                var foreign = Channel(frame, frame.foreign);
                for (int i = 0; i < visible.Length; i++)
                    if ((visible[i] && (occluded[i] || foreign[i])) || (occluded[i] && foreign[i]))
                        throw new InvalidDataException("Overlapping ownership classes");
                result.Add(Key(frame.group, frame.key, frame.width, frame.height), new OwnershipMask(visible, occluded));
            }
            return result;
        }

        private static bool[] Channel(Frame frame, IEnumerable<int[]> rows)
        {
            var result = new bool[frame.width * frame.height];
            foreach (var row in rows)
            {
                if (row.Length != 3 || row[0] < 0 || row[0] >= frame.height || row[1] < 0
                    || row[2] < row[1] || row[2] >= frame.width)
                    throw new InvalidDataException("Invalid ownership scanline");
                for (int x = row[1]; x <= row[2]; x++) result[row[0] * frame.width + x] = true;
            }
            return result;
        }

        private static string Key(string group, int frame, int width, int height)
        { return group + "/" + frame + "/" + width + "/" + height; }

        internal static OwnershipMask For(int item, int group, int frame, int width, int height)
        { return Find(ItemsBySlot, item, group, frame, width, height); }

        internal static OwnershipMask Embedded(int item, int group, int frame, int width, int height)
        { return Find(EmbeddedBySlot, item, group, frame, width, height); }

        private static OwnershipMask Find(Dictionary<int, Dictionary<string, OwnershipMask>> source,
            int item, int group, int frame, int width, int height)
        {
            Dictionary<string, OwnershipMask> frames;
            OwnershipMask result;
            return group >= 0 && group < Groups.Length && source.TryGetValue(item, out frames)
                && frames.TryGetValue(Key(Groups[group], frame, width, height), out result) ? result : null;
        }
    }
}
