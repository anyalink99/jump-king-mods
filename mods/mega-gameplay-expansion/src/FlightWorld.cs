using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using JumpKing.API;
using JumpKing;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    // The simulated screen/collision queries never change Camera or LevelManager.
    internal sealed class FlightWorld : JKRuntime.Simulation.INativeFlightWorld, JKRuntime.Simulation.INativeFlightWind, JKRuntime.Simulation.INativeFlightTeleports
    {
        private delegate bool CheckNative(int index, LevelScreen[] screens, int total, Rectangle box, out Rectangle overlap, out AggregateCollisionInfo info);
        private static readonly CheckNative Check = (CheckNative)Delegate.CreateDelegate(typeof(CheckNative),
            typeof(LevelManager).GetMethod("CheckCollisionInternal", BindingFlags.Static | BindingFlags.NonPublic));
        private readonly LevelScreen[] screens;
        private readonly LevelScreen[] collisionScreens;
        private readonly Dictionary<int, FlightIndex> indices = new Dictionary<int, FlightIndex>();
        private long windTicks;
        private double windStep;
        private float windLegacy;
        public bool WindEnabled { get { return screens[Screen].WindEndabled; } }
        internal void SetWindClock(long ticks, double step, float legacy)
        { windTicks = ticks; windStep = step; windLegacy = legacy; }
        internal void CaptureWindClock()
        {
            foreach (var screen in screens) if (screen.WindEndabled)
            {
                var state = JKRuntime.State.GameClock.ReadCurrent();
                SetWindClock(state.Ticks, JumpKing.Game1.instance.TargetElapsedTime.TotalSeconds, state.Time);
                return;
            }
        }
        public float WindVelocity(int tick)
        {
            if (windStep <= 0) throw new InvalidOperationException("Wind clock unavailable for forecast");
            var screen = screens[Screen];
            return JKRuntime.Simulation.NativeWind.Velocity(windTicks + tick, windStep, windLegacy, screen.WindEndabled, screen.WindIntensity, screen.WindDirection);
        }
        private readonly HashSet<int> validated = new HashSet<int>();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly int milliseconds;
        private int queries;
        private static readonly Type EndingType = typeof(Camera).Assembly.GetType("JumpKing.GameManager.MultiEnding.EndingManager", true);
        private static readonly FieldInfo EndingInstance = EndingType.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo EndingScreens = EndingType.GetMethod("GetWinScreens0");
        public int Screen { get; private set; }
        public bool HasTeleport { get { return screens[Screen].CanTeleport; } }
        internal static LevelScreen[] InstalledScreens { get { return JKRuntime.Geometry.NativeWorldGeometry.ReadScreens(); } }
        internal FlightWorld(LevelScreen[] value, int screen, int timeBudget = 100)
        { screens = value; collisionScreens = (LevelScreen[])value.Clone(); Screen = screen; milliseconds = timeBudget; }
        internal FlightWorld Fork()
        {
            var copy = new FlightWorld(screens, Screen, 0);
            copy.SetWindClock(windTicks, windStep, windLegacy);
            return copy;
        }
        public void ValidateScreen()
        {
            Budget();
            if (Screen < 0 || Screen >= screens.Length) throw new InvalidOperationException("Flight left the level");
            for (int i = Math.Max(0, Screen - 1); i <= Math.Min(screens.Length - 1, Screen + 1); i++)
            {
                if (validated.Contains(i)) continue;
                LevelScreen screen = screens[i];
                var blocks = JKRuntime.Geometry.NativeWorldGeometry.ReadBlocks(screen);
                foreach (IBlock block in blocks)
                    if (!Supported(block.GetType())) throw new InvalidOperationException("Unsupported block on screen " + (i + 1) + ": " + block.GetType().FullName);
                var index = new FlightIndex(screen, blocks);
                indices.Add(i, index); collisionScreens[i] = index.Screen;
                validated.Add(i);
            }
        }
        private static bool Supported(Type type)
        {
            return type == typeof(BoxBlock) || type == typeof(SlopeBlock) || type == typeof(IceBlock)
                || type == typeof(SnowBlock) || type == typeof(SandBlock) || type == typeof(WaterBlock)
                || type == typeof(NoWindBlock) || type == typeof(QuarkBlock) || type == typeof(WarpSurfaceBlock)
                || type == typeof(WarpZoneBlock) || type == typeof(NoWalkOffSurfaceBlock)
                || type == typeof(NoWalkOffZoneBlock) || type == typeof(AirDashSurfaceBlock) || type == typeof(AirDashZoneBlock);
        }
        private void Budget()
        {
            if (++queries > 200000 || (milliseconds > 0 && clock.ElapsedMilliseconds > milliseconds))
                throw new InvalidOperationException("Flight calculation budget exceeded");
        }
        public void HandleTeleport(BehaviourContext context)
        {
            if (!HasTeleport) return;
            var body = context.BodyComp;
            var center = body.GetHitbox().Center;
            if (center.X >= 0 && center.X <= 480) return;
            var screen = screens[Screen];
            bool left = center.X < 0;
            TeleportLink link = null;
            if (!screen.IsTwoTeleports) link = Array.Find(screen.teleport, candidate => candidate != null && candidate.IsEnabled);
            else if (screen.teleport.Length > (left ? 0 : 1)) link = screen.teleport[left ? 0 : 1];
            int target = link == null ? -1 : link.GetIndex0();
            if (target < 0 || target >= screens.Length) throw new InvalidOperationException("Invalid screen teleport destination");

            // HandlePlayerTeleportBehaviour truncates body Y, computes its own
            // source index (not Camera.CurrentScreen), and forces the camera
            // from the shifted ORIGINAL hitbox centre. Keep all three details.
            int source = (int)(-(body.Position.Y - 360f) / 360f);
            int offset = checked(360 * (target - source));
            body.Position.X += left ? 480 : -480;
            body.Position.Y = (int)body.Position.Y - offset;
            center.Y = checked(center.Y - offset);
            Screen = Math.Max(0, Math.Min(screens.Length - 1, -(int)Math.Floor(center.Y / 360f)));
            var endings = EndingInstance.GetValue(null);
            if (endings != null) foreach (int ending in (int[])EndingScreens.Invoke(endings, null)) if (Screen == ending + 1) Screen--;
            context[JumpKing.BodyCompBehaviours.HandlePlayerTeleportBehaviour.TeleportedPlayerFlag] = new object();
            ValidateScreen();
        }
        public void AdvanceCamera(BodyComp body)
        {
            if (body.IsOnBlock(typeof(SandBlock))) return; // native CameraFollowComp freezes the screen in sand
            int target = -(int)Math.Floor(body.GetHitbox().Center.Y / 360f);
            float vy = body.Velocity.Y;
            if (target == Screen || vy == 0 || Math.Sign(target - Screen) != Math.Sign(-vy) || (vy < 0 && Math.Abs(vy) < 3)) return;
            Screen = Math.Max(0, Math.Min(screens.Length - 1, target));
        }
        public AdvCollisionInfo GetCollisionInfo(Rectangle hitbox)
        {
            Select(hitbox);
            AggregateCollisionInfo result = null;
            for (int i = Math.Max(0, Screen - 1); i <= Math.Min(screens.Length - 1, Screen + 1); i++)
            {
                AdvCollisionInfo info = collisionScreens[i].GetCollisionInfo(hitbox);
                if (result == null) { result = new AggregateCollisionInfo(info); continue; }
                result.AggregateCollidingBlocks(info.GetCollidedBlocks());
                if (i == Screen) result.AggregateWind = info.IsInWind();
                if (info.SlopeType != SlopeType.None) result.OverrideSlopeType = info.SlopeType;
                if (info.SlopeNormal != Vector2.Zero) result.OverrideSlopeNormal = info.SlopeNormal;
                result.AggregateIce |= info.Ice; result.AggregateSnow |= info.Snow;
                result.AggregateWater |= info.Water; result.AggregateSand |= info.Sand; result.AggregateQuark |= info.Quark;
            }
            return result;
        }
        public bool CheckCollision(Rectangle box, out Rectangle overlap, out AggregateCollisionInfo info)
        { Select(box); return Check(Screen, collisionScreens, screens.Length, box, out overlap, out info); }
        public bool CheckCollision(Rectangle box, out Rectangle overlap, out AdvCollisionInfo info)
        { AggregateCollisionInfo aggregate; bool hit = CheckCollision(box, out overlap, out aggregate); info = aggregate; return hit; }
        public bool IsInWater(Rectangle box)
        {
            Select(box);
            for (int i = Math.Max(0, Screen - 1); i <= Math.Min(screens.Length - 1, Screen + 1); i++)
            {
                Rectangle overlap; AdvCollisionInfo info; collisionScreens[i].TryCollision(box, out overlap, out info);
                if (info.Water) return true;
            }
            return false;
        }
        private void Select(Rectangle box)
        {
            Budget();
            if (!validated.Contains(Screen)) ValidateScreen();
            for (int i = Math.Max(0, Screen - 1); i <= Math.Min(screens.Length - 1, Screen + 1); i++) indices[i].Select(box);
        }
    }
}
