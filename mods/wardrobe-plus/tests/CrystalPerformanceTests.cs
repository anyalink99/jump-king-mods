using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WardrobePlus;

internal static partial class WardrobeTests
{
    private static void CrystalPerformanceTests(GraphicsDevice device, SpriteBatch batch, RenderTarget2D target)
    {
        if (!CrystalRenderer.ExperimentalEnabled || Environment.GetEnvironmentVariable("WARDROBE_BENCHMARK") != "1") return;
        using (var prepared = PreparedAppearance.Build(new Outfit { Material = MaterialKind.Diamond }, Controller.Catalog, false))
        using (var background = new Texture2D(device, 480, 360))
        {
            var body = (CrystalSprite)NativeAppearance.Frames(prepared.Base.regular)[0];
            var scene = new Color[480 * 360];
            for (int i = 0; i < scene.Length; i++) scene[i] = new Color(i % 255, (i / 480) % 255, (i / 7) % 255);
            background.SetData(scene);
            var sample = new Color[1]; var report = new List<string> { "GPU-completed frame time; identical 1px readback fence in all cases. Includes frame setup and fence overhead.",
                "Native body covered pixels: " + body.Pixels.Length, "copies,mode,median_ms,p95_ms,p99_ms,max_ms,gc0" };
            IDisposable baseline = null; Sprite oldBody = null; MethodInfo oldRelease = null;
            string baselinePath = Environment.GetEnvironmentVariable("WARDROBE_BASELINE_PACKAGE");
            if (!string.IsNullOrEmpty(baselinePath))
            {
                var shell = Assembly.LoadFile(Path.GetFullPath(baselinePath)); Assembly implementation;
                using (var stream = shell.GetManifestResourceStream("JKRuntime.Module"))
                using (var reader = new BinaryReader(stream)) implementation = Assembly.Load(reader.ReadBytes((int)stream.Length));
                var outfit = Activator.CreateInstance(implementation.GetType("WardrobePlus.Outfit"));
                outfit.GetType().GetField("Material").SetValue(outfit, Enum.Parse(implementation.GetType("WardrobePlus.MaterialKind"), "Diamond"));
                var catalog = Activator.CreateInstance(implementation.GetType("WardrobePlus.Catalog"), true);
                var type = implementation.GetType("WardrobePlus.PreparedAppearance");
                baseline = (IDisposable)type.GetMethod("Build", Flags).Invoke(null, new object[] { outfit, catalog, false, true, null });
                var sprites = (JumpKing.JKMemory.KingSprites)type.GetField("Base", Flags).GetValue(baseline);
                oldBody = NativeAppearance.Frames(sprites.regular)[0];
                oldRelease = implementation.GetType("WardrobePlus.CrystalRenderer").GetMethod("Release", Flags);
                report.Insert(1, "Baseline package " + shell.GetName().Version + ": " + Path.GetFullPath(baselinePath));
            }
            try {
            foreach (int copies in new[] { 1, 4 })
            {
                var modes = oldBody == null ? new[] { "static", "diamond" } : new[] { "static", "legacy", "diamond" };
                var samples = modes.ToDictionary(mode => mode, mode => new List<double>());
                var collections = modes.ToDictionary(mode => mode, mode => 0);
                // Alternate order over several rounds to reduce warm-up, power-state
                // and scheduling bias. All versions share one device and fixture.
                for (int round = 0; round < 3; round++)
                for (int index = 0; index < modes.Length; index++)
                {
                    string mode = modes[(index + round) % modes.Length];
                    int gc = GC.CollectionCount(0); CrystalRenderer.Suspended = mode == "static";
                    Sprite image = mode == "legacy" ? oldBody : body;
                    for (int frame = 0; frame < 250; frame++)
                    {
                        var watch = Stopwatch.StartNew();
                        device.SetRenderTarget(target); device.Clear(Color.Black);
                        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
                        batch.Draw(background, new Vector2(-(frame % 8), 0), Color.White);
                        for (int copy = 0; copy < copies; copy++) image.Draw(new Vector2(110 + copy * 60 + frame % 7, 220));
                        batch.End(); device.SetRenderTarget(null);
                        target.GetData(0, new Rectangle(0,0,1,1), sample, 0, 1);
                        watch.Stop(); if (frame >= 50) samples[mode].Add(watch.Elapsed.TotalMilliseconds);
                    }
                    collections[mode] += GC.CollectionCount(0) - gc;
                }
                foreach (string mode in modes)
                {
                    var durations = samples[mode];
                    durations.Sort();
                    report.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2:F4},{3:F4},{4:F4},{5:F4},{6}", copies, mode,
                        durations[300], durations[569], durations[593], durations[599], collections[mode]));
                }
            }
            } finally { CrystalRenderer.Suspended = false; if (baseline != null) baseline.Dispose(); if (oldRelease != null) oldRelease.Invoke(null, null); }
            File.WriteAllLines(Path.Combine(output, "crystal-performance.csv"), report);
            foreach (var line in report) Console.WriteLine("BENCH: " + line);
        }
    }
}
