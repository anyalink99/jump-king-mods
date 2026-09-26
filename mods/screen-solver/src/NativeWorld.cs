using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JumpKing;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace ScreenSolver
{
    // Screens and colliders are copied once, on Solve. Mutable query state is
    // private to each branch tick. No native Camera/LevelManager setters.
    internal sealed class NativeWorld : ICollisionQuery
    {
        internal const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private delegate bool CheckNative(int index, LevelScreen[] screens, int total, Rectangle box, out Rectangle overlap, out AggregateCollisionInfo info);
        private static readonly CheckNative Check = (CheckNative)Delegate.CreateDelegate(typeof(CheckNative),
            typeof(LevelManager).GetMethod("CheckCollisionInternal", Flags));
        internal readonly LevelScreen[] Screens;
        internal int Screen;
        internal NativeWorld(LevelScreen[] screens, int screen) { Screens = screens; Screen = screen; }
        internal static LevelScreen[] Capture(PassiveAdapters passive = null)
        {
            passive = passive ?? new PassiveAdapters();
            var originals = JKRuntime.Geometry.NativeWorldGeometry.ReadScreens();
            return originals.Select((s, index) => new LevelScreen(index,
                JKRuntime.Geometry.NativeWorldGeometry.ReadBlocks(s)
                    .Where(b => !passive.IsAudioBlock(b.GetType())).Select(b => CloneBlock(b, passive)).ToArray(),
                new LevelScreen.Graphics(), s.WindEndabled, s.teleport.Select(t => new TeleportLink(t.GetIndex1())).ToArray(),
                s.WindIntensity, s.WindDirection)).ToArray();
        }
        internal static IBlock CloneBlock(IBlock block, PassiveAdapters passive = null)
        {
            passive = passive ?? new PassiveAdapters();
            Type t = block.GetType();
            if (t == typeof(SlopeBlock) || passive.IsFixedSlope(t))
            {
                // Copy actual collision lines, including FixMySlopes corrections.
                // A constructor would both undo those corrections and run the
                // foreign registration postfix against the live static list.
                return JKRuntime.Geometry.NativeWorldGeometry.CopySlopeCollision((SlopeBlock)block);
            }
            if (new[] { typeof(BoxBlock), typeof(IceBlock), typeof(SnowBlock), typeof(SandBlock), typeof(WaterBlock), typeof(NoWindBlock), typeof(QuarkBlock) }.Contains(t))
                return (IBlock)Activator.CreateInstance(t, block.GetRect());
            throw new NotSupportedException("No simulation geometry adapter: " + t.FullName);
        }
        internal int[] Targets()
        { return ExitTargets().Select(t => t.Screen).Distinct().ToArray(); }
        internal SearchTarget[] ExitTargets()
        {
            var result = new List<SearchTarget>();
            if (Screen + 1 < Screens.Length) result.Add(new SearchTarget(Screen, Screen + 1, ExitDirection.Up));
            var screen = Screens[Screen];
            if (screen.CanTeleport)
            {
                for (int side = 0; side < 2; side++)
                {
                    var link = screen.IsTwoTeleports ? screen.teleport[side] : screen.teleport.First(t => t.IsEnabled);
                    int destination = link.GetIndex0();
                    if (destination < 0 || destination >= Screens.Length) throw new NotSupportedException("Invalid teleport target");
                    if (destination != Screen) result.Add(new SearchTarget(Screen, destination, side == 0 ? ExitDirection.Left : ExitDirection.Right));
                }
            }
            if (Screen > 0) result.Add(new SearchTarget(Screen, Screen - 1, ExitDirection.Down));
            return result.ToArray();
        }
        internal void AdvanceCamera(BodyComp body)
        {
            if (body.IsOnBlock(typeof(SandBlock))) return;
            int target = -(int)Math.Floor(body.GetHitbox().Center.Y / 360f); float vy = body.Velocity.Y;
            if (target == Screen || vy == 0 || Math.Sign(target - Screen) != Math.Sign(-vy) || (vy < 0 && Math.Abs(vy) < 3)) return;
            Screen = Math.Max(0, Math.Min(Screens.Length - 1, target));
        }
        public AdvCollisionInfo GetCollisionInfo(Rectangle hitbox)
        {
            AggregateCollisionInfo result = null;
            for (int i = Math.Max(0, Screen - 1); i <= Math.Min(Screens.Length - 1, Screen + 1); i++)
            {
                var info = Screens[i].GetCollisionInfo(hitbox);
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
        { return Check(Screen, Screens, Screens.Length, box, out overlap, out info); }
        public bool CheckCollision(Rectangle box, out Rectangle overlap, out AdvCollisionInfo info)
        { AggregateCollisionInfo aggregate; bool hit = CheckCollision(box, out overlap, out aggregate); info = aggregate; return hit; }
        public bool IsInWater(Rectangle box)
        {
            for (int i = Math.Max(0, Screen - 1); i <= Math.Min(Screens.Length - 1, Screen + 1); i++)
                if (Screens[i].GetCollisionInfo(box).Water) return true;
            return false;
        }
    }
}
