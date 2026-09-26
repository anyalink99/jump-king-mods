using System;
using System.Diagnostics;
using JKRuntime.Simulation;
using JumpKing.Level;
using JumpKing.Player;

namespace MegaGameplayExpansion
{
    // Runs only on the game thread, in cooperative slices. No live player/world
    // state is advanced. Immutable seed allows snapshots to restart pending work.
    internal sealed class FlightJob
    {
        internal const int MaxSlices = 30;
        internal const double MaxWorkMilliseconds = 60;
        internal sealed class Seed
        {
            internal readonly BodyComp Launch;
            internal readonly FlightWorld World;
            internal Seed(BodyComp source, FlightWorld world)
            { World = world; Launch = NativeFlight.CreateShadow(source, world); }
        }
        private readonly FlightWorld world;
        private readonly BodyComp body;
        private readonly FlightProfile profile;
        private readonly int firstTick, nativeTick, landingCheck, camera;
        internal BodyComp Landing { get; private set; }
        internal int Ticks { get; private set; }
        internal string Failure { get; private set; }
        internal int Slices { get; private set; }
        internal double WorkMilliseconds { get; private set; }
        internal double MaxSliceMilliseconds { get; private set; }
        internal bool Done { get { return Landing != null || Failure != null; } }
        internal FlightJob(Seed seed)
        {
            world = seed.World.Fork(); body = NativeFlight.CreateShadow(seed.Launch, world);
            profile = FlightProfile.Create();
            if (profile != null) {
                firstTick=profile.Add("first-tick-continuation"); nativeTick=profile.Add("native-tick-entry");
                landingCheck=profile.Add("landing-check"); camera=profile.Add("camera");
            }
        }
        internal void Step(int maxTicks = 128, double milliseconds = 2)
        {
            if (Done) return;
            if (maxTicks <= 0 || milliseconds <= 0 || double.IsNaN(milliseconds) || double.IsInfinity(milliseconds))
                throw new ArgumentOutOfRangeException("Forecast slice must have a positive finite budget");
            var clock = Stopwatch.StartNew();
            if (profile != null) profile.Enter();
            try
            {
                if (++Slices > MaxSlices) throw new InvalidOperationException("Flight forecast exceeded 30 cooperative slices");
                for (int i = 0; i < maxTicks; i++)
                {
                    CheckBudget(clock);
                    if (profile != null) profile.Begin(Ticks == 0 ? firstTick : nativeTick);
                    if (Ticks == 0) {
                        NativeFlightSimulation.AdvanceShadowFromX(body);
                        if (profile != null) { profile.End(); profile.Attach(body); }
                    }
                    else NativeFlightSimulation.AdvanceShadow(body, 1f / 60f);
                    if (profile != null) profile.Begin(landingCheck);
                    Ticks++;
                    CheckBudget(clock);
                    // Sand is a control surface in vanilla, although it never
                    // sets IsOnGround. Do not simulate sinking through it forever.
                    if (body.LastVelocity.Y >= 0 && (body.IsOnGround || body.IsOnBlock(typeof(SandBlock))))
                    { Landing = body; return; }
                    if (profile != null) profile.Begin(camera);
                    world.AdvanceCamera(body);
                    if (profile != null) profile.End();
                    if (Ticks >= 7200) throw new InvalidOperationException("No landing within 7200 native ticks");
                    if (clock.Elapsed.TotalMilliseconds >= milliseconds) return;
                }
            }
            catch (Exception error) { Failure = error.Message; }
            finally {
                if (profile != null) profile.Leave();
                double elapsed=clock.Elapsed.TotalMilliseconds;
                WorkMilliseconds += elapsed; MaxSliceMilliseconds = Math.Max(MaxSliceMilliseconds,elapsed);
                if (profile != null && Done) profile.Report(Ticks,Slices,WorkMilliseconds,Failure);
            }
        }
        private void CheckBudget(Stopwatch clock)
        {
            if (WorkMilliseconds + clock.Elapsed.TotalMilliseconds > MaxWorkMilliseconds)
                throw new InvalidOperationException("Flight forecast exceeded 60 ms of active work");
            if (!Finite(body.Position.X) || !Finite(body.Position.Y) || !Finite(body.Velocity.X) || !Finite(body.Velocity.Y)
                || Math.Abs(body.Position.X) > 10000000 || Math.Abs(body.Position.Y) > 10000000)
                throw new InvalidOperationException("Invalid native flight coordinates or velocity");
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
