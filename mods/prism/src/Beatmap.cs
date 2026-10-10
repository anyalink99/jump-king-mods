using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework;

namespace Prism
{
    internal sealed class Note
    {
        internal double Time, End;
        internal Vector2 Position, Direction;
        internal float Distance;
        internal int Kind;
        internal Vector2[] Path;
    }
    internal sealed class Timing
    {
        internal double Time, Beat;
        internal bool Kiai;
    }
    internal sealed class Beatmap
    {
        internal Note[] Notes;
        internal Timing[] Timing;
        private static double Number(string s)
        {
            double v = double.Parse(s, CultureInfo.InvariantCulture);
            if (double.IsNaN(v) || double.IsInfinity(v)) throw new InvalidDataException("Non-finite beatmap value");
            return v;
        }
        internal static Beatmap Load(string path) { return Parse(File.ReadAllLines(path)); }
        internal static Beatmap Parse(string[] lines)
        {
            var notes = new List<Note>(); var timing = new List<Timing>();
            string section = ""; double slider = 1.4, beat = 500, velocity = 1;
            var rawTiming = new List<string[]>();
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("//")) continue;
                if (line[0] == '[') { section = line; continue; }
                if (section == "[Difficulty]" && line.StartsWith("SliderMultiplier:")) slider = Number(line.Split(':')[1]);
                if (section == "[TimingPoints]") rawTiming.Add(line.Split(','));
                if (section != "[HitObjects]") continue;
                string[] f = line.Split(','); if (f.Length < 5) throw new InvalidDataException("Incomplete hit object");
                var n = new Note { Time = Number(f[2]) / 1000, Position = new Vector2((float)Number(f[0]), (float)Number(f[1])), Kind = int.Parse(f[3], CultureInfo.InvariantCulture) };
                n.End = n.Time; n.Path = new[] { n.Position };
                if ((n.Kind & 8) != 0) n.End = Number(f[5]) / 1000;
                if ((n.Kind & 2) != 0)
                {
                    if (f.Length < 8 || slider <= 0) throw new InvalidDataException("Invalid slider");
                    beat = 500; velocity = 1;
                    foreach (var t in rawTiming)
                    {
                        if (Number(t[0]) > n.Time * 1000) break;
                        double b = Number(t[1]);
                        if (b > 0) { beat = b; velocity = 1; } else if (b < 0) velocity = Math.Max(.1, Math.Min(10, -100 / b));
                    }
                    n.End += Number(f[7]) * Number(f[6]) * beat / (100 * slider * velocity) / 1000;
                    var points = new List<Vector2>(); points.Add(n.Position);
                    foreach (var p in f[5].Split('|'))
                    {
                        var xy = p.Split(':'); if (xy.Length == 2) points.Add(new Vector2((float)Number(xy[0]), (float)Number(xy[1])));
                    }
                    n.Path = points.ToArray();
                }
                if (n.Time < 0 || n.End < n.Time || n.End - n.Time > 120) throw new InvalidDataException("Invalid object time");
                if (notes.Count != 0)
                {
                    if (n.Time < notes[notes.Count - 1].Time) throw new InvalidDataException("Hit objects must be sorted");
                    Vector2 d = n.Position - notes[notes.Count - 1].Position;
                    n.Distance = d.Length() / 640f; n.Direction = d.LengthSquared() > 0 ? Vector2.Normalize(d) : Vector2.UnitX;
                }
                notes.Add(n);
            }
            beat = 500;
            foreach (var t in rawTiming)
            {
                if (t.Length < 2) throw new InvalidDataException("Incomplete timing point");
                if (Number(t[1]) > 0) beat = Number(t[1]);
                timing.Add(new Timing { Time = Number(t[0]) / 1000, Beat = beat / 1000, Kiai = t.Length > 7 && (int.Parse(t[7], CultureInfo.InvariantCulture) & 1) != 0 });
            }
            if (notes.Count == 0 || timing.Count == 0) throw new InvalidDataException("Beatmap needs timing and hit objects");
            return new Beatmap { Notes = notes.ToArray(), Timing = timing.ToArray() };
        }
        internal int LastNote(double time)
        {
            int lo = 0, hi = Notes.Length;
            while (lo < hi) { int mid = (lo + hi) / 2; if (Notes[mid].Time <= time) lo = mid + 1; else hi = mid; }
            return lo - 1;
        }
        internal float Pulse(double time)
        {
            int n = LastNote(time); if (n < 0) return 0;
            return (float)Math.Exp(-(time - Notes[n].Time) * 9) * (.55f + Math.Min(.45f, Notes[n].Distance));
        }
        internal double BeatAt(double time)
        {
            Timing t = Timing[0]; foreach (var next in Timing) { if (next.Time > time) break; t = next; }
            return (time - t.Time) / t.Beat;
        }
    }
}
