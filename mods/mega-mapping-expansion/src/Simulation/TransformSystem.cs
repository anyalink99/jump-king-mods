using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal struct ComponentPose
    {
        internal double X, Y, ScaleX, ScaleY, Rotation;
        internal int Screen;
        internal bool Visible, PixelSnap;
        internal static ComponentPose Identity { get { return new ComponentPose { ScaleX=1, ScaleY=1, Screen=1, Visible=true,PixelSnap=true }; } }
        internal SceneValue Point(SceneValue point)
        {
            if(Rotation==0)return SceneValue.Vector(X+point.X*ScaleX,Y+point.Y*ScaleY);
            double a=Rotation*Math.PI/180, x=point.X*ScaleX, y=point.Y*ScaleY;
            return SceneValue.Vector(X+x*Math.Cos(a)-y*Math.Sin(a),Y+x*Math.Sin(a)+y*Math.Cos(a));
        }
        internal SceneValue RenderPoint(SceneValue point)
        {var value=Point(point);return PixelSnap?SceneValue.Vector(Math.Round(value.X),Math.Round(value.Y)):value;}
    }
    internal sealed class TransformSystem
    {
        private readonly SceneProgram scene;
        private readonly Dictionary<string, ComponentPose> poses = new Dictionary<string, ComponentPose>(StringComparer.Ordinal);
        internal TransformSystem(SceneProgram program) { scene=program; }
        internal void Refresh() { poses.Clear(); }
        internal ComponentPose Resolve(string id)
        {
            if(string.IsNullOrEmpty(id))return ComponentPose.Identity;
            ComponentPose pose;if(poses.TryGetValue(id,out pose))return pose;
            var position=scene.Properties.Read(id,"position");var scale=scene.Properties.Read(id,"scale");
            pose=new ComponentPose { X=position.X,Y=position.Y,ScaleX=scale.X,ScaleY=scale.Y,Rotation=scene.Properties.Read(id,"rotation").X,
                Screen=(int)scene.Properties.Read(id,"screen").X,Visible=scene.Properties.Read(id,"visible").X!=0,PixelSnap=scene.Properties.Read(id,"pixelSnap").X!=0 };
            string parent=scene.Properties.Read(id,"parent").Text;
            if(parent!="")
            {
                var ancestor=Resolve(parent);var point=ancestor.Point(position);pose.X=point.X;pose.Y=point.Y;
                pose.ScaleX*=ancestor.ScaleX;pose.ScaleY*=ancestor.ScaleY;pose.Rotation+=ancestor.Rotation;pose.Screen=ancestor.Screen;pose.Visible&=ancestor.Visible;
            }
            if(pose.PixelSnap){pose.X=Math.Round(pose.X);pose.Y=Math.Round(pose.Y);}
            poses.Add(id,pose);return pose;
        }
    }
}
