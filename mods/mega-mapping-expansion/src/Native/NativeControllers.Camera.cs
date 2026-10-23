using System;
using System.Collections.Generic;
using JKRuntime;
using JKRuntime.Presentation;
using JumpKing.Level;
using Microsoft.Xna.Framework;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal sealed partial class NativeControllers
    {
        private readonly Dictionary<string,CameraShots.Lease> cameraLeases=new Dictionary<string,CameraShots.Lease>();
        private string[] cameras=new string[0];
        private readonly HashSet<string> cameraFaults=new HashSet<string>(StringComparer.Ordinal);
        private void PrepareCamera()
        {
            cameras=Ids("camera");if(cameras.Length==0)return;
            foreach(string id in cameras)if(Number(id,"screen")>LevelManager.TotalScreens)throw new System.IO.InvalidDataException(id+": camera screen is outside the map");
            NativeScreenView.Prepare(resources);
        }
        private void ActivateCamera(RuntimeScope scope)
        {scope.Defer(()=>cameraLeases.Clear());foreach(string id in cameras)cameraLeases.Add(id,CameraShots.Acquire("mega-mapping-expansion.camera",(int)Number(id,"priority"),scope));}
        private void TickCamera(double delta,bool enabled,bool paused,bool ending)
        {
            foreach(var pair in cameraLeases)
            {
                string id=pair.Key;var lease=pair.Value;
                if(!enabled||ending||!Flag(id,"active")){lease.Clear();Publish(id,"selected",SceneValue.Boolean(false));continue;}
                bool smooth=Text(id,"transition")=="smooth";
                if(smooth&&!CameraShots.SmoothAvailable&&Text(id,"fallback")=="error")
                {lease.Clear();if(cameraFaults.Add(id))scene.ReportError(id,new InvalidOperationException("Smooth Camera is unavailable; camera request declined"));continue;}
                cameraFaults.Remove(id);
                int screen=(int)Number(id,"screen")-1;var point=scene.Properties.Read(id,"position");
                lease.Publish(new CameraShot(new Vector2((float)point.X,(float)point.Y-screen*360),screen,smooth,Number(id,"duration"),Number(id,"returnDuration")));
            }
            foreach(var pair in cameraLeases)Publish(pair.Key,"selected",SceneValue.Boolean(pair.Value.Selected));
        }
        private void StopCamera(){foreach(var lease in cameraLeases.Values)lease.Clear();}
    }
}
