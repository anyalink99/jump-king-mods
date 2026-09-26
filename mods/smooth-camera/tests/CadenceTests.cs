using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using HarmonyLib;
using JumpKing;
using Microsoft.Xna.Framework;
using SmoothCamera;

internal static partial class CameraTests
{
    private static readonly List<GameTime> updateTimes = new List<GameTime>();
    private static int presentedFrames;
    private static bool disableRefreshDuringUpdate;
    private static bool ActiveWithoutPlatform(Game game) { return true; }
    private static IEnumerable<CodeInstruction> HeadlessTick(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(typeof(Game).GetProperty("IsActive").GetGetMethod()))
            { instruction.opcode = OpCodes.Call; instruction.operand = typeof(CameraTests).GetMethod("ActiveWithoutPlatform", Flags); }
            yield return instruction;
        }
    }
    private static bool RecordUpdate(GameTime gameTime)
    {
        updateTimes.Add(new GameTime(gameTime.TotalGameTime, gameTime.ElapsedGameTime));
        if (disableRefreshDuringUpdate) { disableRefreshDuringUpdate = false; Settings.Current.HighRefresh = false; }
        return false;
    }
    private static bool RecordDraw() { presentedFrames++; return false; }

    private static void CadenceTests()
    {
        var fixture = new Harmony("smooth-camera.clock-fixture");
        var game = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
        GC.SuppressFinalize(game);
        var instance = typeof(Game1).GetField("_instance", Flags);
        object previous = instance.GetValue(null);
        bool previousRefresh = Settings.Current.HighRefresh;
        var accumulator = typeof(Game).GetField("_accumulatedElapsedTime", Flags);
        var clock = new GameTime();
        const long nativeTicks = 170000;
        long renderTicks = PresentationClock.RenderInterval.Ticks;
        typeof(Game).GetField("_targetElapsedTime", Flags).SetValue(game, TimeSpan.FromTicks(nativeTicks));
        typeof(Game).GetField("_gameTime", Flags).SetValue(game, clock);
        typeof(Game).GetField("_gameTimer", Flags).SetValue(game, new Stopwatch());
        typeof(Game).GetField("_maxElapsedTime", Flags).SetValue(game, TimeSpan.FromSeconds(.5));
        game.IsFixedTimeStep = true;
        fixture.Patch(typeof(Game).GetMethod("DoUpdate", Flags), prefix: new HarmonyMethod(typeof(CameraTests).GetMethod("RecordUpdate", Flags)) { priority = Priority.Last });
        fixture.Patch(typeof(Game).GetMethod("DoDraw", Flags), prefix: new HarmonyMethod(typeof(CameraTests).GetMethod("RecordDraw", Flags)));
        fixture.Patch(typeof(Game).GetMethod("Tick"), transpiler: new HarmonyMethod(typeof(CameraTests).GetMethod("HeadlessTick", Flags)));
        try
        {
            instance.SetValue(null, game); Settings.Current.Smooth = true; Settings.Current.HighRefresh = true;
            Renderer.Stop();
            updateTimes.Clear(); presentedFrames = 0;
            for (int i = 0; i < 5; i++)
            {
                accumulator.SetValue(game, TimeSpan.FromTicks(nativeTicks)); game.Tick();
            }
            Check(!PresentationClock.Active && updateTimes.Count == 5 && presentedFrames == 5,
                "Prepared hooks retain native update and presentation count before player handoff");
            Check(updateTimes.All(t => t.ElapsedGameTime.Ticks == nativeTicks),
                "Prepared hooks preserve native intro simulation delta");
            clock.TotalGameTime = TimeSpan.Zero;
            Renderer.Start();
            updateTimes.Clear(); presentedFrames = 0;
            // Execute the installed framework's actual patched scheduler, with a
            // stopped stopwatch and deterministic accumulated elapsed intervals.
            const int frames = 2400;
            for (int i = 0; i < frames; i++)
            {
                accumulator.SetValue(game, PresentationClock.RenderInterval);
                game.Tick();
            }
            Check(presentedFrames == frames, "Native scheduler presents every high refresh frame");
            Check(updateTimes.Count == frames * renderTicks / nativeTicks, "High refresh preserves original update count");
            for (int i = 0; i < updateTimes.Count; i++)
            {
                Check(updateTimes[i].ElapsedGameTime.Ticks == nativeTicks, "Original simulation delta reaches native update");
                Check(updateTimes[i].TotalGameTime.Ticks == (i + 1L) * nativeTicks, "Simulation clock advances without drift");
            }
            Check(game.TargetElapsedTime.Ticks == nativeTicks, "Public physics interval is unchanged");
            long remainder = frames * renderTicks % nativeTicks;
            int before = updateTimes.Count;
            Settings.Current.Smooth = false;
            accumulator.SetValue(game, TimeSpan.FromTicks(nativeTicks - remainder));
            Settings.ApplyPresentationSetting();
            Check(!Hooks.Installed, "Unchecked camera removes its native patches");
            game.Tick();
            Check(!PresentationClock.Active && updateTimes.Count == before + 1, "Disabling restores native cadence and returns partial time");
            Check(updateTimes[before].TotalGameTime.Ticks == (before + 1L) * nativeTicks, "Toggle has no simulation time jump");
            Check(((TimeSpan)accumulator.GetValue(game)).Ticks == 0, "Toggle accounts for all elapsed time");
            Settings.Current.Smooth = true;
            Settings.ApplyPresentationSetting();
            Check(Hooks.Installed, "Enabling during a level restores camera hooks");
            accumulator.SetValue(game, TimeSpan.FromTicks(renderTicks * 10)); game.Tick();
            Check(presentedFrames == frames + 2, "Catch-up renders once for multiple presentation intervals");
            Check(updateTimes.Count == before + 1 + 10 * renderTicks / nativeTicks, "Catch-up retains native update count");
            for (int i = 0; i < 120; i++)
            { accumulator.SetValue(game, TimeSpan.FromTicks(renderTicks * 4)); game.Tick(); }
            Check(!clock.IsRunningSlowly && (int)typeof(Game).GetField("_updateFrameLag", Flags).GetValue(game) == 0,
                "60 Hz presentation does not falsely mark normal simulation as slow");
            // Switch only the presentation option, keeping all camera patches,
            // framing and gameplay controls alive. Use actual partial tick time.
            for (int round = 0; round < 3; round++)
            {
                int updatesBefore = updateTimes.Count, drawsBefore = presentedFrames;
                long lastNativeTime = updateTimes.Last().TotalGameTime.Ticks;
                long pending = clock.TotalGameTime.Ticks - lastNativeTime;
                float framing = Renderer.Motion.Translation;
                accumulator.SetValue(game, TimeSpan.FromTicks(nativeTicks - pending));
                Settings.Current.HighRefresh = false;
                Check(!Renderer.HighRefreshRequested && Hooks.Installed && Renderer.Running,
                    "240 Hz checkbox preserves smooth camera and its hooks");
                Near(Renderer.Motion.Translation, framing, "Refresh switch preserves camera framing");
                game.Tick();
                Check(!PresentationClock.Active, "240 Hz checkbox releases the scheduler at the next tick boundary");
                Check(updateTimes.Count == updatesBefore + 1 && presentedFrames == drawsBefore + 1,
                    "Unchecked 240 Hz presents exactly once for the restored native tick");
                Check(updateTimes.Last().TotalGameTime.Ticks == lastNativeTime + nativeTicks
                    && updateTimes.Last().ElapsedGameTime.Ticks == nativeTicks
                    && ((TimeSpan)accumulator.GetValue(game)).Ticks == 0,
                    "Refresh-only switch conserves partial time without extra simulation");
                for (int i = 0; i < 5; i++)
                { accumulator.SetValue(game, TimeSpan.FromTicks(nativeTicks)); game.Tick(); }
                Check(updateTimes.Count == updatesBefore + 6 && presentedFrames == drawsBefore + 6 && !PresentationClock.Active,
                    "Unchecked mode retains one native update per rendered frame");
                Settings.Current.HighRefresh = true;
                accumulator.SetValue(game, TimeSpan.FromTicks(renderTicks * 3)); game.Tick();
                Check(PresentationClock.Active && updateTimes.Count == updatesBefore + 6 && presentedFrames == drawsBefore + 7,
                    "Rechecking 240 Hz resumes intermediate frames without an early gameplay update");
                Check(game.TargetElapsedTime.Ticks == nativeTicks, "Refresh toggles never change public simulation interval");
            }
            int insideUpdates = updateTimes.Count;
            long insideTime = updateTimes.Last().TotalGameTime.Ticks;
            long insidePending = clock.TotalGameTime.Ticks - insideTime;
            disableRefreshDuringUpdate = true;
            accumulator.SetValue(game, TimeSpan.FromTicks(renderTicks * 10)); game.Tick();
            int catchup = (int)((insidePending + renderTicks * 10) / nativeTicks);
            Check(PresentationClock.Active && !Settings.Current.HighRefresh && updateTimes.Count == insideUpdates + catchup,
                "Changing the checkbox inside Update cannot switch cadence halfway through a catch-up tick");
            long afterPending = clock.TotalGameTime.Ticks - updateTimes.Last().TotalGameTime.Ticks;
            accumulator.SetValue(game, TimeSpan.FromTicks(nativeTicks - afterPending)); game.Tick();
            Check(!PresentationClock.Active && updateTimes.Count == insideUpdates + catchup + 1
                && updateTimes.Last().TotalGameTime.Ticks == insideTime + (catchup + 1L) * nativeTicks,
                "In-update checkbox changes return to native time without dropped or duplicate updates");
            // Independent clients share one accumulator and cannot break each other's native updates.
            Settings.Current.HighRefresh = true;
            int samples = 0, releaseAttempts = 0;
            bool failSample = false, failRelease = true;
            var companion = JKRuntime.PresentationScheduling.Register("fixture.input",
                delegate { return new JKRuntime.PresentationRequest(true, false); },
                delegate(float delta) { samples++; if (failSample) throw new InvalidOperationException("input fixture fault"); },
                delegate { releaseAttempts++; if (failRelease) throw new InvalidOperationException("release fixture fault"); });
            try
            {
                int combinedBefore = updateTimes.Count;
                for (int i = 0; i < 2040; i++)
                { accumulator.SetValue(game, TimeSpan.FromTicks(renderTicks)); game.Tick(); }
                Check(samples == 2040 && updateTimes.Count == combinedBefore + 2040 * renderTicks / nativeTicks,
                    "Input and camera clients preserve one native simulation cadence");
                failSample = true;
                accumulator.SetValue(game, TimeSpan.FromTicks(renderTicks * 8)); game.Tick();
                Check(!companion.Active && companion.LastError != null && PresentationClock.Active && releaseAttempts == 1,
                    "Faulting sampler and cleanup are isolated while camera and native updates continue");
                for (int i = 0; i < 8; i++)
                { accumulator.SetValue(game, TimeSpan.FromTicks(renderTicks)); game.Tick(); }
                Check(releaseAttempts == 1, "Failed cleanup is not repeated on every presentation frame");
                failRelease = false; companion.Dispose();
                Check(releaseAttempts == 2, "Failed callback cleanup remains explicitly retryable");
            }
            finally { failRelease = false; companion.Dispose(); }
            int recursiveReleases = 0;
            JKRuntime.PresentationScheduling.Client recursive = null;
            recursive = JKRuntime.PresentationScheduling.Register("fixture.recursive-release",
                delegate { return new JKRuntime.PresentationRequest(true, false); }, null,
                delegate { recursiveReleases++; recursive.Dispose(); });
            accumulator.SetValue(game, TimeSpan.FromTicks(renderTicks)); game.Tick();
            recursive.Dispose(); recursive.Dispose();
            Check(recursiveReleases == 1 && !recursive.Active && PresentationClock.Active,
                "A release callback may dispose its owner without recursion or disturbing the camera");
            bool requestFault = false;
            using (var invalid = JKRuntime.PresentationScheduling.Register("fixture.request",
                delegate { if (requestFault) throw new InvalidOperationException("request fixture fault"); return new JKRuntime.PresentationRequest(true, false); }))
            {
                accumulator.SetValue(game, TimeSpan.FromTicks(renderTicks)); game.Tick();
                requestFault = true;
                accumulator.SetValue(game, TimeSpan.FromTicks(renderTicks)); game.Tick();
                Check(!invalid.Active && invalid.LastError != null && PresentationClock.Active, "Request failure isolates one client at the boundary");
            }
            var cost = Stopwatch.StartNew();
            for (int i = 0; i < 240000; i++) JKRuntime.PresentationScheduling.BeginTick(game);
            cost.Stop();
            Console.WriteLine("[COST] Shared scheduler boundary, one active client: " + (cost.Elapsed.TotalMilliseconds / 240000).ToString("F6") + " ms/call");
            Renderer.Stop(); PresentationClock.Release();
            Check(!PresentationClock.Active && game.TargetElapsedTime.Ticks == nativeTicks, "Level end restores scheduler");
            ((Stopwatch)typeof(Game).GetField("_gameTimer", Flags).GetValue(game)).Stop();
        }
        finally
        {
            disableRefreshDuringUpdate = false;
            PresentationClock.Release(); Renderer.Stop(); Settings.Current.Smooth = true; Settings.Current.HighRefresh = previousRefresh;
            fixture.UnpatchAll(fixture.Id); instance.SetValue(null, previous);
        }
    }
}
