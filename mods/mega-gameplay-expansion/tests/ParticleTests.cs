using System;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal static partial class Tests
    {
        private static void ParticleCollisions()
        {
            var blocks=new IBlock[] {
                new BoxBlock(new Rectangle(5,-30,1,60)),
                new BoxBlock(new Rectangle(-30,5,60,1)),
                new BoxBlock(new Rectangle(-30,-6,60,1)),
                new BoxBlock(new Rectangle(-6,-30,1,60)),
                new WaterBlock(new Rectangle(-30,-30,60,60))
            };
            var space=new PixelCollision(Scene(blocks),new Rectangle(-40,-40,80,80));
            foreach(var direction in new[]{Vector2.UnitX,-Vector2.UnitX,Vector2.UnitY,-Vector2.UnitY,new Vector2(1,1),new Vector2(-1,-1)})
            {
                var path=new PixelPath(space,Vector2.Zero,direction*10);
                Require(path.Visible && path.At(0)==Vector2.Zero,"Free particle origin changed");
                for(int i=0;i<=100;i++) Require(!space.Blocked(path.At(i/100f)),"Particle penetrated a 1px wall/floor/ceiling");
                var outward=path.At(.5f); var inward=path.At(.5f);
                Require(outward==inward,"Path sampling must be stateless/reversible");
                Require(Vector2.Dot(path.At(1),direction)<Vector2.Dot(path.At(.5f),direction),"Blocked particle stopped instead of bouncing");
            }
            Require(!space.Blocked(Vector2.Zero),"Water must not repel pixels");
            var overlap=new PixelPath(space,new Vector2(5,0),new Vector2(-10,0));
            Require(overlap.Visible && !space.Blocked(new Vector2(5,0)+overlap.At(.001f)),"Sprite overlap was not ejected");
            var sealedSpace=new PixelCollision(Scene(new IBlock[]{new BoxBlock(new Rectangle(-30,-30,60,60))}),new Rectangle(-40,-40,80,80));
            Require(!new PixelPath(sealedSpace,Vector2.Zero,Vector2.UnitX*10).Visible,"Deeply occluded art must not scatter through terrain");
            // A negative-world-Y destination on another screen uses its own geometry,
            // not whichever screen happens to be visible when the plan is built.
            var upper=new LevelScreen(1,new IBlock[]{new BoxBlock(new Rectangle(5,-200,1,80)),new SlopeBlock(new Rectangle(-25,-180,20,20),SlopeType.TopRight)},new LevelScreen.Graphics(),false,new TeleportLink[0],0,null);
            var crossSpace=new PixelCollision(new[]{Scene(new IBlock[0])[0],upper},new Rectangle(-40,-210,80,100));
            var cross=new PixelPath(crossSpace,new Vector2(0,-170),new Vector2(12,0));
            for(int i=0;i<=100;i++) Require(!crossSpace.Blocked(new Vector2(0,-170)+cross.At(i/100f)),"Destination screen wall ignored");
            for(int y=-190;y<-150;y+=2) for(int x=-30;x<0;x+=2)
            {
                var origin=new Vector2(x,y); if(crossSpace.Blocked(origin)) continue;
                var path=new PixelPath(crossSpace,origin,new Vector2(6,10));
                for(int i=0;i<=100;i++) Require(!crossSpace.Blocked(origin+path.At(i/100f)),"Slope penetration");
            }
            float vertical=0; int count=0;
            for(int y=0;y<48;y++) for(int x=0;x<48;x++)
            {
                var sample=MatrixPixels.At(x,y,48,48,1,false);
                Require(sample.Offset.Length()<=12.001f,"Scatter is not compact");
                vertical+=sample.Offset.Y; count++;
            }
            Require(Math.Abs(vertical/count)<.2f,"Unwanted upward scatter bias");
            Console.WriteLine("[OK] RGB scatter: <=12px, no vertical bias; Jetpack-style bounces, 1px walls/floor/ceiling, slopes, water, overlap ejection, cross-screen paths");
        }
    }
}
