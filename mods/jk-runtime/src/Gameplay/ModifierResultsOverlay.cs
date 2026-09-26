using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using EntityComponent;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.Util.Tags;
using Microsoft.Xna.Framework;

namespace JKRuntime.Gameplay
{
    internal sealed class ModifierResultsOverlay : Entity, IForeground
    {
        internal const string Warning = "Player Behaviour Modifier(s) Detected";
        private static readonly FieldInfo Screen = typeof(JumpGame).GetField("m_stats_screen", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo Lines = typeof(StatsScreen).GetField("m_lines", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly Stopwatch timer = new Stopwatch();
        private string[] nativeLines, rows;
        internal static void ValidateContract()
        {
            if (Screen == null || Screen.FieldType != typeof(StatsScreen) || Lines == null || Lines.FieldType != typeof(string[]))
                throw new InvalidOperationException("Required native StatsScreen attribution contract unavailable");
        }
        protected override void Update(float delta) { ModifierRegistrationObserver.Poll(); RunModifiers.Observe(); }
        internal static bool ShouldDisplay(string[] lines) { return lines != null && lines.Contains(Warning); }
        internal static int StartY(int lineCount, int stride)
        { int below = 130 + lineCount * 20; return below + 2 * stride <= 350 ? below : 20; }
        internal static int Capacity(int startY, int stride)
        { return Math.Max(1, ((startY == 20 ? 110 : 350) - startY) / stride - 1); }
        internal static string[] Wrap(IEnumerable<string> names, Func<string, float> measure, float width)
        {
            var result = new List<string>();
            foreach (string name in names)
            {
                string row = "";
                // Font-safe, bounded-width output even for long/untrusted names.
                foreach (char raw in name ?? "Unknown source")
                {
                    char c = raw >= 32 && raw <= 126 ? raw : '?';
                    if (row.Length != 0 && measure(row + c) > width) { result.Add(row); row = ""; }
                    row += c;
                }
                if (row.Length != 0) result.Add(row);
            }
            return result.ToArray();
        }
        public void ForegroundDraw()
        {
            var game = JumpGame.instance;
            var screen = game == null ? null : Screen.GetValue(game) as StatsScreen;
            if (screen == null || !screen.IsRunning()) { timer.Reset(); return; }
            var lines = Lines.GetValue(screen) as string[];
            if (!ShouldDisplay(lines)) return;
            var font = Game1.instance.contentManager.font.MenuFontSmall;
            if (!ReferenceEquals(lines, nativeLines))
            {
                nativeLines = lines;
                rows = Wrap(RunModifiers.GetResultRows(), s => font.MeasureString(s).X, 444);
                timer.Restart();
            }
            if (!timer.IsRunning) timer.Start();
            // Native stats begin at y=130 with a 20px stride. Use only the free
            // lower region; paginate without consuming the native exit input.
            int stride = Math.Max(12, font.LineSpacing);
            int y = StartY(lines.Length, stride);
            int capacity = Capacity(y, stride);
            int pages = Math.Max(1, (rows.Length + capacity - 1) / capacity);
            int page = (int)(timer.Elapsed.TotalSeconds / 4) % pages;
            string header = "Flag sources" + (pages > 1 ? " (" + (page + 1) + "/" + pages + ")" : "") + ":";
            Draw(header, y, Color.Gold);
            for (int i = 0; i < capacity && page * capacity + i < rows.Length; i++)
                Draw(rows[page * capacity + i], y + (i + 1) * stride, Color.White);
        }
        private static void Draw(string text, int y, Color color)
        {
            var font = Game1.instance.contentManager.font.MenuFontSmall;
            Game1.spriteBatch.DrawString(font, text, new Vector2(240 - font.MeasureString(text).X / 2, y), color);
        }
    }
}
