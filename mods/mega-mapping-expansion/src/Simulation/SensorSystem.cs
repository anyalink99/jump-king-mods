using System;
using System.Collections.Generic;
using System.Linq;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal struct SceneBox
    {
        internal double X,Y,Width,Height;
        internal bool Overlaps(SceneBox other){return X<other.X+other.Width&&X+Width>other.X&&Y<other.Y+other.Height&&Y+Height>other.Y;}
    }
    internal sealed class ActorObservation
    {
        internal string Id;
        internal int Screen;
        internal SceneBox Bounds;
        internal SceneValue Velocity;
    }
    internal sealed class SurfaceObservation
    {
        internal string Id,Type;
        internal string[] Tags=new string[0];
        internal int Screen;
        internal SceneValue Position,Normal,Velocity;
        internal ObservationQuality PositionQuality,NormalQuality,VelocityQuality;
        internal bool Impact;
    }
    internal sealed class SensorMembership
    {
        internal double Dwell;
        internal bool Inside;
        internal SurfaceObservation Contact;
        internal string Actor;
    }
    internal sealed partial class SensorSystem
    {
        private readonly SceneProgram scene;
        private readonly TransformSystem transforms;
        private readonly CompiledComponent[] sensors;
        private readonly Dictionary<string,Dictionary<string,SensorMembership>> memberships=new Dictionary<string,Dictionary<string,SensorMembership>>(StringComparer.Ordinal);
        private readonly Dictionary<string,SceneValue[]> polygons=new Dictionary<string,SceneValue[]>(StringComparer.Ordinal);
        private readonly HashSet<string> initialized=new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> faulted=new HashSet<string>(StringComparer.Ordinal);
        internal SensorSystem(SceneProgram program,TransformSystem poses)
        {
            scene=program;transforms=poses;sensors=program.Components.Values.Where(c=>c.Descriptor.Kind=="sensor").ToArray();
            if(sensors.Length>256)throw new System.IO.InvalidDataException("Scene supports at most 256 sensors");
            foreach(var sensor in sensors)
            {
                memberships.Add(sensor.Id,new Dictionary<string,SensorMembership>(StringComparer.Ordinal));
                if(Text(sensor.Id,"shape")=="polygon")polygons.Add(sensor.Id,GeometryMath.Points(Text(sensor.Id,"outline")));
            }
        }
        internal static void Declare(SignalHub hub,string id)
        {
            hub.Declare(new SignalDescriptor(id+".touching",SceneValueKind.Boolean,observation:true),SceneValue.Boolean(false));
            hub.Declare(new SignalDescriptor(id+".position",SceneValueKind.Vector2,"px",true),SceneValue.Vector(0,0));
            hub.Declare(new SignalDescriptor(id+".normal",SceneValueKind.Vector2,"ratio",true),SceneValue.Vector(0,0));
            hub.Declare(new SignalDescriptor(id+".velocity",SceneValueKind.Vector2,"px/s",true),SceneValue.Vector(0,0));
            hub.Declare(new SignalDescriptor(id+".speed",SceneValueKind.Number,"px/s",true),SceneValue.Number(0));
        }
        private string Text(string id,string property){return scene.Properties.Read(id,property).Text;}
        private double Number(string id,string property){return scene.Properties.Read(id,property).X;}
        private static bool Selected(string selection,SurfaceObservation contact)
        {
            if(selection=="")return true;
            foreach(string token in selection.Split(';'))
                if(token==contact.Id||token=="type:"+contact.Type||(token.StartsWith("tag:",StringComparison.Ordinal)&&contact.Tags.Contains(token.Substring(4))))return true;
            return false;
        }
        internal void Observe(ActorObservation actor,IEnumerable<SurfaceObservation> surfaces,string phase,double delta)
        {
            foreach(var sensor in sensors)
            {
                if(faulted.Contains(sensor.Id))continue;
                try{ObserveSensor(sensor,actor,surfaces,phase,delta);}
                catch(Exception error){faulted.Add(sensor.Id);memberships[sensor.Id].Clear();scene.ReportError(sensor.Id,error);}
            }
        }
        private void ObserveSensor(CompiledComponent sensor,ActorObservation actor,IEnumerable<SurfaceObservation> surfaces,string phase,double delta)
        {
                string id=sensor.Id;if(Text(id,"phase")!=phase||(Text(id,"actor")!=actor.Id&&Text(id,"actor")!="*"))return;
                var previous=memberships[id];var found=new Dictionary<string,SurfaceObservation>(StringComparer.Ordinal);
                bool first=initialized.Add(id+"/"+actor.Id);
                if(initialized.Count>4096)throw new InvalidOperationException("Sensor actor budget exceeded");
                var pose=transforms.Resolve(Text(id,"transform"));
                if(Number(id,"enabled")!=0&&pose.Visible&&(pose.Screen==0||pose.Screen==actor.Screen))
                {
                    if(Text(id,"shape")=="surface")
                    {
                        foreach(var surface in surfaces)
                        {
                            double speed=Math.Sqrt(surface.Velocity.X*surface.Velocity.X+surface.Velocity.Y*surface.Velocity.Y);
                            if(surface.Screen==actor.Screen&&Selected(Text(id,"selection"),surface)&&speed>=Number(id,"minimumSpeed"))found[actor.Id+"|"+surface.Id]=surface;
                        }
                    }
                    else if(Contains(sensor,pose,actor.Bounds,previous.ContainsKey(actor.Id+"|region")&&previous[actor.Id+"|region"].Inside))
                        found.Add(actor.Id+"|region",new SurfaceObservation{Id=actor.Id,Type="actor",Screen=actor.Screen,Position=SceneValue.Vector(actor.Bounds.X+actor.Bounds.Width/2,actor.Bounds.Y+actor.Bounds.Height/2),
                            Normal=SceneValue.Vector(0,0),Velocity=actor.Velocity,PositionQuality=ObservationQuality.Sampled,NormalQuality=ObservationQuality.Unavailable,VelocityQuality=ObservationQuality.Sampled});
                }
                foreach(var contact in found.OrderBy(p=>p.Key,StringComparer.Ordinal))
                {
                    if(previous.Count>=256&&!previous.ContainsKey(contact.Key))throw new InvalidOperationException("Sensor membership exceeds 256 contacts");
                    SensorMembership state;if(!previous.TryGetValue(contact.Key,out state))previous.Add(contact.Key,state=new SensorMembership{Actor=actor.Id});state.Contact=contact.Value;
                    state.Dwell+=delta;
                    Publish(id,contact.Value);
                    if(!state.Inside&&state.Dwell+1e-9>=Number(id,"dwell")){state.Inside=true;if(!first||Text(id,"initial")!="baseline")Emit(id,"enter",actor.Id,contact.Value);}
                    if(state.Inside&&contact.Value.Impact)Emit(id,"impact",actor.Id,contact.Value);
                }
                foreach(string gone in previous.Where(p=>p.Value.Actor==actor.Id&&!found.ContainsKey(p.Key)).Select(p=>p.Key).ToArray())
                {var state=previous[gone];if(state.Inside)Emit(id,"exit",actor.Id,state.Contact);previous.Remove(gone);}
                scene.Signals.Publish(id+".touching",SceneValue.Boolean(previous.Values.Any(s=>s.Inside)),ObservationQuality.Sampled);
        }
        private bool Contains(CompiledComponent sensor,ComponentPose pose,SceneBox actor,bool wasInside)
        {
            double angle=-pose.Rotation*Math.PI/180;double pad=wasInside?Number(sensor.Id,"hysteresis"):0;
            var size=scene.Properties.Read(sensor.Id,"size");
            var corners=new SceneValue[4];int[] order={0,1,3,2};
            for(int i=0;i<4;i++)
            {
                double x=actor.X+(order[i]%2)*actor.Width-pose.X,y=actor.Y+(order[i]/2)*actor.Height-pose.Y;
                double sx=Math.Abs(pose.ScaleX)<.001?.001:pose.ScaleX,sy=Math.Abs(pose.ScaleY)<.001?.001:pose.ScaleY;
                corners[i]=SceneValue.Vector((x*Math.Cos(angle)-y*Math.Sin(angle))/sx,(x*Math.Sin(angle)+y*Math.Cos(angle))/sy);
            }
            string shape=Text(sensor.Id,"shape");
            if(shape=="disc")
            {
                double rx=Math.Abs(size.X)/2+pad,ry=Math.Abs(size.Y)/2+pad;if(rx<=0||ry<=0)return false;
                var normalized=Array.ConvertAll(corners,p=>SceneValue.Vector(p.X/rx,p.Y/ry));
                if(GeometryMath.Contains(normalized,0,0))return true;
                for(int i=0;i<4;i++)if(GeometryMath.Distance(SceneValue.Vector(0,0),normalized[i],normalized[(i+1)%4])<=1)return true;return false;
            }
            if(shape=="polygon")
            {
                var polygon=polygons[sensor.Id];
                return GeometryMath.Overlaps(polygon,corners,pad);
            }
            return GeometryMath.Overlaps(new[]{SceneValue.Vector(-size.X/2,-size.Y/2),SceneValue.Vector(size.X/2,-size.Y/2),SceneValue.Vector(size.X/2,size.Y/2),SceneValue.Vector(-size.X/2,size.Y/2)},corners,pad);
        }
        private void Publish(string id,SurfaceObservation value)
        {
            scene.Signals.Publish(id+".position",value.Position,value.PositionQuality);scene.Signals.Publish(id+".normal",value.Normal,value.NormalQuality);
            scene.Signals.Publish(id+".velocity",value.Velocity,value.VelocityQuality);scene.Signals.Publish(id+".speed",SceneValue.Number(Math.Sqrt(value.Velocity.X*value.Velocity.X+value.Velocity.Y*value.Velocity.Y)),value.VelocityQuality);
        }
        private void Emit(string id,string kind,string actor,SurfaceObservation contact)
        {
            scene.Signals.Emit(id+"."+kind,contact.Id,actor,contact.Screen,scene.GameplayTime,
                new SignalSample("position",contact.Position,contact.PositionQuality,0),new SignalSample("normal",contact.Normal,contact.NormalQuality,0),new SignalSample("velocity",contact.Velocity,contact.VelocityQuality,0));
        }
        internal void Clear(){foreach(var state in memberships.Values)state.Clear();initialized.Clear();faulted.Clear();}
    }
}
