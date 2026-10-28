using JumpKing.Level;
using System;
using System.Linq;
using Microsoft.Xna.Framework;
namespace Powers { internal static partial class Tests {
        private static void NoWalkOffWarp(WalkInputFixture input)
        {
            var previousScreens=NoWalkOff.MapPixels.NoWalkOff.Screens.ToArray();
            bool previousGlobal=NoWalkOff.Settings.Current.NoWalkOff;
            NoWalkOff.Settings.Current.NoWalkOff=false;
            NoWalkOff.MapPixels.NoWalkOff.Screens.Clear(); NoWalkOff.MapPixels.NoWalkOff.Screens.Add(0);
            bool added = MapPixels.Warp.Screens.Add(0);
            try
            {
                WalkScene(new NoWalkOff.NoWalkOffSurfaceBlock(new Rectangle(80, 280, 80, 16)),
                    new BoxBlock(new Rectangle(0, 340, 480, 20)),
                    new NoWalkOff.NoWalkOffZoneBlock(new Rectangle(80, 220, 80, 60)));
                var player = NoWalkPlayer(); player.m_body.Position = new Vector2(100, 254);
                using (var warp = new WarpController(player))
                using (var edge = new NoWalkOff.NoWalkOffController(player))
                {
                    // settle first, a new test body has Vy=0
                    // that's different from the steady native grounded state after gravity
                    player.m_body.Velocity.Y = 1;
                    WalkTicks(player, input, 1, 100);
                    Require(player.m_body.Enabled && player.m_body.IsOnGround && player.m_body.Position.X == 159,
                        "Warp ran before the protected walk-off");
                    WalkTicks(player, input, 0, 3);
                    for (int i = 0; i < 10 && player.m_body.Enabled; i++) WalkTicks(player, input, 1, 1);
                    Require(!player.m_body.Enabled, "Permitted walk-off did not start Warp");
                    for (int i = 0; i < 40 && !player.m_body.Enabled; i++) WalkTicks(player, input, 1, 1);
                    Require(player.m_body.Enabled && player.m_body.IsOnGround && player.m_body.Position.Y == 314,
                        "Combined Warp did not land on lower platform");
                    WalkTicks(player, input, 1, 250);
                    Require(player.m_body.IsOnGround, "Combined controller lost ground after landing");
                }
            }
            finally {
                if (added) MapPixels.Warp.Screens.Remove(0);
                NoWalkOff.Settings.Current.NoWalkOff=previousGlobal;
                NoWalkOff.MapPixels.NoWalkOff.Screens.Clear();
                foreach(int screen in previousScreens) NoWalkOff.MapPixels.NoWalkOff.Screens.Add(screen);
            }
            Console.WriteLine("[OK] No Walk Off: Warp walk-off/landing integration");
        }
} }
