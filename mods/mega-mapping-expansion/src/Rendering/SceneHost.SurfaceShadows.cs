using System;
using System.Collections.Generic;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion
{
    internal sealed partial class SceneHost
    {
        private readonly Dictionary<ShadowSurfaceData,PuddleGeometry> shadowReceivers=new Dictionary<ShadowSurfaceData,PuddleGeometry>();
        private int shadowScanlines;
        private Vector2 shadowFoot, shadowSpriteOrigin;
        private Rectangle shadowSource;
        internal void DrawSurfaceShadows(PlayerEntity player)
        {
            shadowScanlines=0;
            if(previewMode!="scene" || scene.ShadowSurfaces.Length==0) return;
            bool activeReceiver=false;
            foreach(var receiver in scene.ShadowSurfaces)if(receiver.Screen==Camera.CurrentScreenIndex1){activeReceiver=true;break;}
            if(!activeReceiver)return;
            Sprite sprite; Texture2D texture; Rectangle source; SpriteEffects flip;
            if(!TryPlayerSprite(player,out sprite,out texture,out source,out flip))return;
            Vector2 foot=Camera.TransformVector2(player.m_body.Position+new Vector2(9,26));
            Vector2 topLeft=foot-source.Size.ToVector2()*sprite.center;
            shadowFoot=foot; shadowSpriteOrigin=topLeft; shadowSource=source;
            Texture2D silhouette=Silhouette(texture);
            foreach(var surface in scene.ShadowSurfaces)
            {
                if(surface.Screen!=Camera.CurrentScreenIndex1 || hiddenIds.Contains(surface.Id))continue;
                float lift=surface.PlaneY-foot.Y;
                if(lift< -2 || lift>surface.MaxHeight)continue;
                PuddleGeometry geometry = shadowReceivers[surface];
                float opacity=surface.Opacity*(1-.45f*MathHelper.Clamp(lift/surface.MaxHeight,0,1));
                Color tint=MultiplyAlpha(prepared.Color(surface.Color),opacity);
                foreach(Rectangle span in geometry.Spans)
                {
                    float height=GroundProjection.Height(span.Y+.5f,surface.PlaneY,surface.ScaleY);
                    int row=(int)Math.Floor(surface.PlaneY-height-topLeft.Y);
                    if(row<0 || row>=source.Height)continue;
                    int projectedLeft=(int)Math.Round(GroundProjection.X(topLeft.X,height,surface.ShearX));
                    int left=Math.Max(span.X,projectedLeft),right=Math.Min(span.Right,projectedLeft+source.Width);
                    if(right<=left)continue;
                    shadowScanlines++;
                    bool flipped=(flip&SpriteEffects.FlipHorizontally)!=0;
                    int sourceX=flipped ? source.Right-(right-projectedLeft) : source.X+left-projectedLeft;
                    Rectangle sample=new Rectangle(sourceX,source.Y+row,right-left,1);
                    Game1.spriteBatch.Draw(silhouette,new Rectangle(left,span.Y,right-left,1),sample,tint,0,Vector2.Zero,
                        flipped?SpriteEffects.FlipHorizontally:SpriteEffects.None,0);
                }
            }
        }
    }
}
