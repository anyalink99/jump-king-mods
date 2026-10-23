using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JKRuntime.Gameplay;
using JumpKing;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal sealed class NativeContacts
    {
        private readonly MovementObserver movement;
        private static readonly FieldInfo Screens=typeof(LevelManager).GetField("m_screens",BindingFlags.Static|BindingFlags.NonPublic);
        private long lastX,lastY,lastMaterials;
        internal NativeContacts(MovementObserver observer){movement=observer;}
        internal static Rectangle ContactArea(IBlock block,Rectangle probe)
        {
            Rectangle area;if(block.Intersects(probe,out area)==BlockCollisionType.NoCollision)return Rectangle.Empty;
            if(area.Width<=0||area.Height<=0)area=Rectangle.Intersect(block.GetRect(),probe);return area.Width>0&&area.Height>0?area:Rectangle.Empty;
        }
        internal void ResetEvidence(){if(movement==null)return;lastX=movement.Read(MovementStage.XCollision).Sequence;lastY=movement.Read(MovementStage.YCollision).Sequence;lastMaterials=movement.Read(MovementStage.Materials).Sequence;}
        internal SurfaceObservation[] Read(PlayerEntity player)
        {
            var contacts=new Dictionary<string,SurfaceObservation>(StringComparer.Ordinal);var hitbox=player.m_body.GetHitbox();
            Query(contacts,hitbox,player.m_body.Velocity,false,SceneValue.Vector(0,0));
            if(movement!=null)
            {
                var x=movement.Read(MovementStage.XCollision);var y=movement.Read(MovementStage.YCollision);
                if(x.Sequence>lastX&&x.Completed&&x.Covered&&x.Contact.Path!=ContactPath.None)
                    Query(contacts,x.Contact.Probe,x.VelocityBefore,true,SceneValue.Vector(-x.Contact.Direction,0));
                if(y.Sequence>lastY&&y.Completed&&y.Covered&&y.Contact.Path!=ContactPath.None)
                    Query(contacts,y.Contact.Probe,y.VelocityBefore,true,SceneValue.Vector(0,-y.Contact.Direction));
                var materials=movement.Read(MovementStage.Materials);
                // a nonblocking provider can relocate the body before the next input pass
                if(materials.Sequence>lastMaterials&&materials.Completed&&materials.Covered&&materials.PositionBefore!=materials.PositionAfter)
                {
                    Query(contacts,materials.HitboxBefore,materials.VelocityBefore,false,SceneValue.Vector(0,0));
                    // native PreResolutionCollisionInfo combines the samples before both axes
                    if(x.Sequence>lastX&&x.Completed&&x.Covered)
                        Query(contacts,x.HitboxBefore,x.VelocityBefore,false,SceneValue.Vector(0,0));
                    if(y.Sequence>lastY&&y.Completed&&y.Covered)
                        Query(contacts,y.HitboxBefore,y.VelocityBefore,false,SceneValue.Vector(0,0));
                }
                lastX=x.Sequence;lastY=y.Sequence;lastMaterials=materials.Sequence;
            }
            return contacts.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>p.Value).ToArray();
        }
        private static void Query(Dictionary<string,SurfaceObservation> contacts,Rectangle probe,Vector2 velocity,bool impact,SceneValue normal)
        {
            foreach(var block in BlocksAt(probe))
            {
                Rectangle area;var collision=block.Intersects(probe,out area);if(collision==BlockCollisionType.NoCollision)continue;
                if(area.Width<=0||area.Height<=0)area=Rectangle.Intersect(probe,block.GetRect());if(area.Width<=0||area.Height<=0)continue;
                var original=SlopeClipping.SourceOf(block);var bounds=original.GetRect();string id=original.GetType().FullName+"@"+bounds.X+","+bounds.Y+","+bounds.Width+","+bounds.Height;
                int screen=Math.Max(1,1-(int)Math.Floor(bounds.Center.Y/360.0));
                var point=area.Center;double px=point.X,py=point.Y+(screen-1)*360;
                if(impact){if(normal.X<0)px=area.Left;else if(normal.X>0)px=area.Right-1;else if(normal.Y<0)py=area.Top+(screen-1)*360;else py=area.Bottom-1+(screen-1)*360;}
                bool physicalImpact=impact&&collision==BlockCollisionType.Collision_Blocking;
                var slopeNormal=SlopeClipping.NormalOf(block);var surfaceNormal=slopeNormal.HasValue?SceneValue.Vector(slopeNormal.Value.X,slopeNormal.Value.Y):normal;
                SurfaceObservation previous;if(contacts.TryGetValue(id,out previous)&&previous.Impact&&!physicalImpact)continue;
                contacts[id]=new SurfaceObservation{Id=id,Type=original.GetType().FullName,Screen=screen,Position=SceneValue.Vector(px,py),Normal=surfaceNormal,Velocity=SceneValue.Vector(velocity.X*60,velocity.Y*60),
                    PositionQuality=ObservationQuality.Estimated,NormalQuality=slopeNormal.HasValue?ObservationQuality.Sampled:impact?ObservationQuality.Estimated:ObservationQuality.Unavailable,VelocityQuality=ObservationQuality.Sampled,Impact=physicalImpact};
            }
        }
        private static IEnumerable<IBlock> BlocksAt(Rectangle probe)
        {
            // a far teleport has already moved the camera away from the source screen
            var screens=Screens==null?null:Screens.GetValue(null) as LevelScreen[];
            if(screens==null)yield break;
            int first=Math.Max(0,-(int)Math.Floor((probe.Bottom-1)/360.0));
            int last=Math.Min(screens.Length-1,-(int)Math.Floor(probe.Top/360.0));
            for(int i=first;i<=last;i++)
                foreach(var block in screens[i].GetCollisionInfo(probe).GetCollidedBlocks())yield return block;
        }
    }
}
