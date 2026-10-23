using System;
using System.Collections.Generic;
using System.IO;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal sealed partial class FieldSystem
    {
        private readonly SceneProgram scene;
        private readonly Dictionary<string,SceneValue[]> curves=new Dictionary<string,SceneValue[]>(StringComparer.Ordinal);
        private readonly Dictionary<string,SpringChain> springs=new Dictionary<string,SpringChain>(StringComparer.Ordinal);
        private sealed class FieldSampler
        {
            internal string Function;
            internal PropertySlot Origin,Amplitude,Frequency,Phase,Decay,Seed,Direction,Length;
            internal SceneValue[] Curve;
            internal SpringChain Spring;
        }
        private sealed class DeformationStep
        {
            internal FieldSampler Field;
            internal PropertySlot Pin,Extent,Strength,Falloff;
            internal string Axis;
            internal bool World;
        }
        private readonly Dictionary<string,FieldSampler> samplers=new Dictionary<string,FieldSampler>(StringComparer.Ordinal);
        private readonly Dictionary<string,DeformationStep[]> chains=new Dictionary<string,DeformationStep[]>(StringComparer.Ordinal);
        private PropertySlot Slot(string id,string property){return scene.Properties.Require(id+"/"+property);}
        internal FieldSystem(SceneProgram program)
        {
            scene=program;
            int springCells=0;
            foreach(var component in program.Components.Values)
            {
                if(component.Descriptor.Kind=="field"&&program.Properties.Read(component.Id,"function").Text=="curve")curves.Add(component.Id,GeometryMath.Points(program.Properties.Read(component.Id,"curve").Text));
                if(component.Descriptor.Kind=="field"&&program.Properties.Read(component.Id,"function").Text=="spring")
                {
                    int count=(int)program.Properties.Read(component.Id,"segments").X;springCells+=count;if(springCells>4096)throw new InvalidDataException("Scene spring fields exceed 4096 cells");springs.Add(component.Id,new SpringChain(count));
                }
                if(component.Descriptor.Kind=="deformation")
                {
                    var visited=new HashSet<string>(StringComparer.Ordinal);string id=component.Id;
                    while(id!="")
                    {
                        CompiledComponent current;if(!program.Components.TryGetValue(id,out current)||current.Descriptor.Kind!="deformation"||!visited.Add(id)||visited.Count>16)throw new InvalidDataException("Invalid deformation chain: "+component.Id);
                        string field=program.Properties.Read(id,"field").Text;
                        if(field==""||program.Components[field].Descriptor.Kind!="field")throw new InvalidDataException(id+": deformation requires a field");
                        id=program.Properties.Read(id,"next").Text;
                    }
                }
            }
            foreach(var component in program.Components.Values)if(component.Descriptor.Kind=="field")
            {
                var field=new FieldSampler{Function=Slot(component.Id,"function").Value.Text,Origin=Slot(component.Id,"origin"),Amplitude=Slot(component.Id,"amplitude"),Frequency=Slot(component.Id,"frequency"),Phase=Slot(component.Id,"phase"),Decay=Slot(component.Id,"decay"),Seed=Slot(component.Id,"seed"),Direction=Slot(component.Id,"direction"),Length=Slot(component.Id,"length")};
                curves.TryGetValue(component.Id,out field.Curve);springs.TryGetValue(component.Id,out field.Spring);samplers.Add(component.Id,field);
            }
            foreach(var component in program.Components.Values)if(component.Descriptor.Kind=="deformation")
            {
                var steps=new List<DeformationStep>();string id=component.Id;
                while(id!="")
                {
                    steps.Add(new DeformationStep{Field=samplers[Slot(id,"field").Value.Text],Pin=Slot(id,"pin"),Extent=Slot(id,"extent"),Strength=Slot(id,"strength"),Falloff=Slot(id,"falloff"),Axis=Slot(id,"axis").Value.Text,World=Slot(id,"space").Value.Text=="world"});id=Slot(id,"next").Value.Text;
                }
                chains.Add(component.Id,steps.ToArray());
            }
        }
        internal SceneValue Sample(string id,SceneValue point)
        {return Sample(samplers[id],point);}
        private static SceneValue Sample(FieldSampler field,SceneValue point)
        {
            var origin=field.Origin.Value;var amplitude=field.Amplitude.Value;var frequency=field.Frequency.Value;
            double phase=field.Phase.Value.X,x=point.X-origin.X,y=point.Y-origin.Y;
            double p=x*frequency.X+y*frequency.Y+phase,decay=Math.Exp(-Math.Abs(y)*field.Decay.Value.X);string function=field.Function;
            SceneValue value;
            if(function=="spring"){var axis=field.Direction.Value;double length=Math.Sqrt(axis.X*axis.X+axis.Y*axis.Y);double position=length<1e-10?0:(x*axis.X+y*axis.Y)/length/field.Length.Value.X;double height=field.Spring.Sample(position);value=SceneValue.Vector(height,height);}
            else if(function=="curve")value=GeometryMath.Curve(field.Curve,p-Math.Floor(p));
            else if(function=="noise")
            {
                double seed=field.Seed.Value.X;value=SceneValue.Vector(Noise(p,seed),Noise(p+17.13,seed+31));
            }
            else value=SceneValue.Vector(Math.Sin(p*Math.PI*2),Math.Cos((x*frequency.X-y*frequency.Y+phase)*Math.PI*2));
            return SceneValue.Vector(value.X*amplitude.X*decay,value.Y*amplitude.Y*decay);
        }
        private static double Noise(double p,double seed)
        {
            double cell=Math.Floor(p),t=p-cell;t=t*t*(3-2*t);
            double a=NoiseHash(cell,seed);return a+(NoiseHash(cell+1,seed)-a)*t;
        }
        private static double NoiseHash(double value,double seed){double n=Math.Sin(value*127.1+seed*311.7)*43758.5453123;return(n-Math.Floor(n))*2-1;}
        internal SceneValue Deform(string id,SceneValue local,ComponentPose pose)
        {
            foreach(var step in chains[id])
            {
                var pin=step.Pin.Value;var extent=step.Extent.Value;double weight=Math.Min(1,Math.Abs(local.Y-pin.Y)/Math.Max(.001,Math.Abs(extent.Y)));
                weight=Math.Pow(weight,step.Falloff.Value.X)*step.Strength.Value.X;
                var point=step.World?pose.Point(local):local;var offset=Sample(step.Field,point);string axis=step.Axis;
                local=SceneValue.Vector(local.X+(axis=="y"?0:offset.X*weight),local.Y+(axis=="x"?0:offset.Y*weight));
            }
            return local;
        }
    }
}
