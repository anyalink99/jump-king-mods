using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using God;
using JKRuntime.Geometry;
using JumpKing;
using JumpKing.Level;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

internal static partial class GodTests
{
    private static void ProfileLargeWorld(IServiceProvider services, string output)
    {
        string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../palace-of-tangled-ways/UPLOAD_TO_WORKSHOP"));
        if (!File.Exists(Path.Combine(root, "level.xnb"))) return;
        var camera = typeof(Camera).GetField("_current_screen", Flags);
        var count = typeof(LevelManager).GetField("_total_screens", Flags);
        var oldCamera = camera.GetValue(null); var oldCount = count.GetValue(null); var oldScreens = ScreenEdit.Screens.GetValue(null);
        using (var content = new ContentManager(services, root))
        {
            var level = JumpKing.Level.Sampler.LevelTexture.FromTexture(content.Load<Texture2D>("level"));
            var loader = typeof(LevelManager).GetMethod("LoadBlocksInterval", Flags);
            var screens = new LevelScreen[300];
            const int focus = 60;
            for (int i = 0; i < screens.Length; i++)
            {
                object[] args = { level, new JumpKing.Workshop.Level(root), i, false, null, 0f, null };
                var blocks = (IBlock[])loader.Invoke(null, args);
                var graphics = i == focus ? new LevelScreen.Graphics {
                    background = content.Load<Texture2D>("screens/midground/" + (i + 1)),
                    foreground = File.Exists(Path.Combine(root, "screens/foreground/fg" + (i + 1) + ".xnb"))
                        ? content.Load<Texture2D>("screens/foreground/fg" + (i + 1)) : null } : new LevelScreen.Graphics();
                screens[i] = new LevelScreen(i, blocks, graphics, false, (TeleportLink[])args[4], 0, null);
            }
            var links = new int[300, 2];
            string settings = File.Exists(Path.Combine(root, "mapping_settings.xml")) ? "mapping_settings.xml" : "props/mega-mapping-expansion/map.xml";
            foreach (string file in new[] { settings, "screens/mega-mapping-expansion/transitions.xml" })
            {
                if (!File.Exists(Path.Combine(root, file))) continue;
                var layout = new XmlDocument(); layout.Load(Path.Combine(root, file));
                foreach (XmlElement link in layout.SelectNodes("/MappingSettings/MapLayout/SideLink | /MapLayout/SideLink | /Transitions/SideLink"))
                    links[int.Parse(link.GetAttribute("screen")) - 1, link.GetAttribute("side") == "left" ? 0 : 1] = int.Parse(link.GetAttribute("target"));
            }
            for (int i = 0; i < screens.Length; i++)
                if (links[i, 0] != 0 || links[i, 1] != 0) JKRuntime.World.NativeSideLinks.SetPair(screens[i].teleport, links[i, 0], links[i, 1]);
            ScreenEdit.Screens.SetValue(null, screens); count.SetValue(null, screens.Length); camera.SetValue(null, focus); MapTopology.Invalidate();
            Storage.Initialize(Path.Combine(output, "palace-performance-storage"));
            Entry.World = new World(); Entry.Editor = new Editor();
            try
            {
                Entry.Editor.Begin(focus);
                var reports = new List<string>();
                var next = (int[])Entry.World.Get(focus).Cells.Clone();
                foreach (bool drawing in new[] { false, true })
                {
                    var times = new List<double>();
                    for (int frame = 0; frame < 44; frame++)
                    {
                        MapTopology.Invalidate();
                        var watch = System.Diagnostics.Stopwatch.StartNew();
                        if (drawing)
                        {
                            next[200 * 480 + 200] = frame % 2 == 0 ? 1 : 0;
                            Entry.World.Get(focus).Apply(next, false);
                        }
                        Entry.Editor.Update(new KeyboardState(), Pointer(240, 200, false)); Entry.Editor.Draw();
                        if (frame >= 4) times.Add(watch.Elapsed.TotalMilliseconds);
                    }
                    times.Sort();
                    string report = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "Palace: {0} screens, {1} blocks, screen 61, drawing={2}; God update+draw mean={3:F3} ms p95={4:F3} ms",
                        screens.Length, screens.Sum(s => ((IBlock[])ScreenEdit.Blocks.GetValue(s)).Length), drawing, times.Average(), times[(int)(times.Count * .95)]);
                    reports.Add(report); Console.WriteLine("[PERF] " + report);
                }
                var edit = Entry.World.Get(focus);
                edit.Apply(edit.Baseline, false);
                Entry.Editor.Begin(focus);
                Entry.Status = "";
                Entry.Editor.Seed(new KeyboardState(), Pointer(200, 150, false));
                Entry.Editor.Update(new KeyboardState(Keys.D2), Pointer(200, 150, true));
                var dragTimes = new List<double>();
                for (int frame = 0; frame < 44; frame++)
                {
                    MapTopology.Invalidate();
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    Entry.Editor.Update(new KeyboardState(), Pointer(240 + frame % 2 * 8, 200, true));
                    Entry.Editor.Draw();
                    if (frame >= 4) dragTimes.Add(watch.Elapsed.TotalMilliseconds);
                }
                Check(string.IsNullOrEmpty(Entry.Status) && !edit.Cells.SequenceEqual(edit.Baseline), "Palace drag updates live terrain without an error");
                dragTimes.Sort();
                string dragReport = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Palace: live rectangle drag; God update+draw mean={0:F3} ms p95={1:F3} ms",
                    dragTimes.Average(), dragTimes[(int)(dragTimes.Count * .95)]);
                reports.Add(dragReport); Console.WriteLine("[PERF] " + dragReport);
                Entry.Editor.Cancel();
                File.WriteAllLines(Path.Combine(output, "palace-performance.txt"), reports);
            }
            finally
            {
                Entry.Editor.Dispose(); Entry.Editor = null; Entry.World.Dispose(); Entry.World = null;
                camera.SetValue(null, oldCamera); count.SetValue(null, oldCount); ScreenEdit.Screens.SetValue(null, oldScreens); MapTopology.Invalidate();
            }
        }
    }
}
