using System;
using System.Globalization;

namespace MegaMappingExpansion.Api
{
    public enum SceneValueKind { Number, Integer, Boolean, Vector2, Color, Text }
    public enum ObservationQuality { Unavailable, Estimated, Sampled, Exact }
    public enum SceneClock { Gameplay, Presentation }
    public enum SceneStateScope { Transient, Attempt, Save }

    /// <summary>A detached typed value. No game or graphics assembly is needed.</summary>
    public struct SceneValue : IEquatable<SceneValue>
    {
        private readonly SceneValueKind kind;
        private readonly double x, y, z, w;
        private readonly string text;
        public SceneValueKind Kind { get { return kind; } }
        public double X { get { return x; } }
        public double Y { get { return y; } }
        public double Z { get { return z; } }
        public double W { get { return w; } }
        public string Text { get { return text ?? ""; } }
        private SceneValue(SceneValueKind type, double a, double b, double c, double d, string value)
        {
            if (!Finite(a) || !Finite(b) || !Finite(c) || !Finite(d)) throw new ArgumentException("Scene values must be finite");
            kind = type; x = a; y = b; z = c; w = d; text = value;
        }
        public static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        public static SceneValue Number(double value) { return new SceneValue(SceneValueKind.Number, value, 0, 0, 0, null); }
        public static SceneValue Integer(int value) { return new SceneValue(SceneValueKind.Integer, value, 0, 0, 0, null); }
        public static SceneValue Boolean(bool value) { return new SceneValue(SceneValueKind.Boolean, value ? 1 : 0, 0, 0, 0, null); }
        public static SceneValue Vector(double a, double b) { return new SceneValue(SceneValueKind.Vector2, a, b, 0, 0, null); }
        public static SceneValue Color(double r, double g, double b, double a)
        {
            if (r < 0 || r > 1 || g < 0 || g > 1 || b < 0 || b > 1 || a < 0 || a > 1) throw new ArgumentException("Color channels must be 0..1");
            return new SceneValue(SceneValueKind.Color, r, g, b, a, null);
        }
        public static SceneValue String(string value)
        { if (value == null || value.Length > 4096) throw new ArgumentException("Scene text must contain at most 4096 characters"); return new SceneValue(SceneValueKind.Text, 0, 0, 0, 0, value); }
        public bool Equals(SceneValue other)
        { return kind == other.kind && x == other.x && y == other.y && z == other.z && w == other.w && Text == other.Text; }
        public override bool Equals(object other) { return other is SceneValue && Equals((SceneValue)other); }
        public override int GetHashCode() { return kind.GetHashCode() ^ x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode() ^ w.GetHashCode() ^ Text.GetHashCode(); }
        public override string ToString()
        {
            if (kind == SceneValueKind.Text) return Text;
            if (kind == SceneValueKind.Boolean) return x == 0 ? "false" : "true";
            string result = x.ToString("R", CultureInfo.InvariantCulture);
            if (kind == SceneValueKind.Vector2 || kind == SceneValueKind.Color) result += "," + y.ToString("R", CultureInfo.InvariantCulture);
            if (kind == SceneValueKind.Color) result += "," + z.ToString("R", CultureInfo.InvariantCulture) + "," + w.ToString("R", CultureInfo.InvariantCulture);
            return result;
        }
        public static SceneValue Parse(SceneValueKind kind, string source)
        {
            if (kind == SceneValueKind.Text) return String(source);
            if (kind == SceneValueKind.Boolean) return Boolean(System.Xml.XmlConvert.ToBoolean(source));
            if (kind == SceneValueKind.Integer) return Integer(int.Parse(source, NumberStyles.Integer, CultureInfo.InvariantCulture));
            if (kind == SceneValueKind.Color && source.StartsWith("#", StringComparison.Ordinal))
            {
                if (source.Length != 7 && source.Length != 9) throw new FormatException("Colors use #RRGGBB or #RRGGBBAA");
                uint rgba = uint.Parse(source.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                if (source.Length == 7) rgba = (rgba << 8) | 255;
                return Color((rgba >> 24) / 255.0, ((rgba >> 16) & 255) / 255.0, ((rgba >> 8) & 255) / 255.0, (rgba & 255) / 255.0);
            }
            string[] parts = source.Split(','); int count = kind == SceneValueKind.Color ? 4 : kind == SceneValueKind.Vector2 ? 2 : 1;
            if (parts.Length != count) throw new FormatException("Expected " + count + " numeric channels for " + kind);
            var values = new double[count]; for (int i = 0; i < count; i++) values[i] = double.Parse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture);
            if (kind == SceneValueKind.Color) return Color(values[0], values[1], values[2], values[3]);
            return kind == SceneValueKind.Vector2 ? Vector(values[0], values[1]) : Number(values[0]);
        }
    }

    public sealed class SignalSample
    {
        public string Id { get; private set; }
        public SceneValue Value { get; private set; }
        public ObservationQuality Quality { get; private set; }
        public long Sequence { get; private set; }
        public SignalSample(string id, SceneValue value, ObservationQuality quality, long sequence)
        { Id = id; Value = value; Quality = quality; Sequence = sequence; }
    }

    public sealed class SceneEventSample
    {
        public string Id { get; private set; }
        public string Source { get; private set; }
        public string Actor { get; private set; }
        public int Screen { get; private set; }
        public long Sequence { get; private set; }
        public double Time { get; private set; }
        private readonly SignalSample[] payload;
        public SignalSample[] Payload { get { return (SignalSample[])payload.Clone(); } }
        public SceneEventSample(string id, string source, string actor, int screen, long sequence, double time, SignalSample[] values)
        { Id = id; Source = source; Actor = actor; Screen = screen; Sequence = sequence; Time = time; payload = values == null ? new SignalSample[0] : (SignalSample[])values.Clone(); }
    }

    /// <summary>Read-only scene observations. subscriptions end with their scene generation.</summary>
    public interface IMappingSignals
    {
        bool Available { get; }
        long Generation { get; }
        SignalSample Read(string id);
        IDisposable Subscribe(string id, Action<SignalSample> observer);
        IDisposable SubscribeEvent(string id, Action<SceneEventSample> observer);
    }
}
