using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace SmoothCamera
{
    internal static class PracticeWorld
    {
        internal static readonly string[] Names = { "Side passages", "Vertical climb", "One-way exit", "Self loop", "Long fall" };
        internal static LevelScreen[] Create(int scenario)
        {
            var screens = new LevelScreen[4];
            for (int row = 0; row < screens.Length; row++)
            {
                int y = -row * 360;
                var blocks = new System.Collections.Generic.List<IBlock>();
                if (row == 0 || row == 2 && scenario != 4 && scenario != 1) blocks.Add(new BoxBlock(new Rectangle(0, y + 336, 480, 24)));
                if (scenario == 1)
                {
                    for (int step = 1; step < 9; step++)
                    {
                        int top = 336 - step * 160;
                        if (top >= y && top < y + 360) blocks.Add(new BoxBlock(new Rectangle(80, top, 320, 12)));
                    }
                }
                else if (scenario != 4)
                {
                    blocks.Add(new BoxBlock(new Rectangle(60, y + 248, 112, 12)));
                    blocks.Add(new BoxBlock(new Rectangle(260, y + 160, 112, 12)));
                    blocks.Add(new BoxBlock(new Rectangle(140, y + 64, 96, 12)));
                }
                int target = row == 0 ? (scenario == 3 ? 1 : 3) : row == 2 && scenario != 2 ? 1 : 0;
                var links = target == 0 ? new TeleportLink[0] : new[] { new TeleportLink(target) };
                screens[row] = new LevelScreen(row, blocks.ToArray(), new LevelScreen.Graphics(), false, links, 0, null);
            }
            return screens;
        }
        internal static Vector2 Start(int scenario)
        { return scenario == 4 ? new Vector2(220, -1050) : scenario == 1 ? new Vector2(220, 310) : new Vector2(120, 310); }
    }
}
