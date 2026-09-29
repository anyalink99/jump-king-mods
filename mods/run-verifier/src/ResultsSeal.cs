using System;
using System.Linq;
using EntityComponent;
using JumpKing;
using JumpKing.Util.Tags;
using Microsoft.Xna.Framework;

namespace RunVerifier
{
    internal sealed class ResultsSeal : Entity, IForeground
    {
        private bool[,] pixels;
        private string id;
        public void ForegroundDraw()
        {
            var r=ModEntry.Last;if(r==null||!ModEntry.ResultsVisible)return;
            DrawRecord(r,ModEntry.Repository.GetStatus(r.id));
        }
        internal void DrawRecord(RunRecord r,string status)
        {
            if(id!=r.id) { ulong steam;ulong.TryParse(r.steamId,out steam);pixels=RoundSeal.Raster(SealCodec.Payload(r.id,steam,(ulong)Math.Round(r.time*1000),(uint)r.nativePeak));id=r.id; }
            var texture=Game1.instance.contentManager.Pixel.texture;
            int x=8,y=8;
            for(int cy=0;cy<RoundSeal.Size;cy++)for(int cx=0;cx<RoundSeal.Size;cx++)
                if((cx-61.5)*(cx-61.5)+(cy-61.5)*(cy-61.5)<62*62)
                    Game1.spriteBatch.Draw(texture,new Rectangle(x+cx,y+cy,1,1),pixels[cx,cy]?new Color(235,217,168):Color.Black);
        }
    }
}
