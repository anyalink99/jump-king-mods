using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Level.Sampler;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal static class MovingPlatformScan
    {
        internal static MovingPlatformPlan Plan;
        internal static readonly List<PlatformCell> Cells=new List<PlatformCell>();
        internal static readonly MovingPlatformFactory Factory=new MovingPlatformFactory();
        internal static void Begin(string root)
        {Plan=MovingPlatformPlan.Read(root,4096);Cells.Clear();}
        internal static string Hex(Color c){return "#"+c.R.ToString("X2")+c.G.ToString("X2")+c.B.ToString("X2");}
        internal static bool Owns(Color color){return color.A==255&&Plan!=null&&Plan.Owns(Hex(color));}
        internal static void Add(Color color,int screen,Rectangle world)
        {
            string code=Hex(color);var rect=new PlatformRect(world.X,world.Y+screen*360,world.Width,world.Height);
            var last=Cells.Count==0?null:Cells[Cells.Count-1];
            if(last!=null&&last.Screen==screen+1&&last.Color==code&&last.Rect.Y==rect.Y&&last.Rect.Height==rect.Height&&last.Rect.X+last.Rect.Width==rect.X)
            {var joined=last.Rect;joined.Width+=rect.Width;last.Rect=joined;return;}
            if(Cells.Count>=16384)throw new InvalidDataException("Too many platform marker runs; simplify the collision image");
            Cells.Add(new PlatformCell{Color=code,Screen=screen+1,Rect=rect});
        }
        internal static MovingPlatformPlan Prepare(string root,int screens)
        {
            var result=MovingPlatformPlan.Read(root,screens);result.AddPainted(Cells);
            PlatformImageScan.ValidateBounds(result,screens);
            return result;
        }
    }
    internal sealed class MovingPlatformFactory:IBlockFactory
    {
        public bool CanMakeBlock(Color blockCode,JumpKing.Workshop.Level level){return MovingPlatformScan.Owns(blockCode);}
        public bool IsSolidBlock(Color blockCode){return false;}
        public IBlock GetBlock(Color blockCode,Rectangle blockRect,JumpKing.Workshop.Level level,LevelTexture textureSrc,int currentScreen,int x,int y)
        {MovingPlatformScan.Add(blockCode,currentScreen,blockRect);return new PlatformMarker(blockRect);}
    }
    internal sealed class PlatformMarker:IBlock
    {
        private readonly Rectangle rect;
        internal PlatformMarker(Rectangle value){rect=value;}
        public Rectangle GetRect(){return rect;}
        public BlockCollisionType Intersects(Rectangle box,out Rectangle intersection){intersection=Rectangle.Empty;return BlockCollisionType.NoCollision;}
    }
}
