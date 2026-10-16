using System;
using System.IO;

namespace MegaMappingExpansion.Construction
{
    internal sealed class SpringChain
    {
        private readonly double[] height,velocity,acceleration;
        internal SpringChain(int count){height=new double[count];velocity=new double[count];acceleration=new double[count];}
        internal int Count{get{return height.Length;}}
        internal void Impulse(double position,double force,double radius)
        {
            if(!MegaMappingExpansion.Api.SceneValue.Finite(position)||!MegaMappingExpansion.Api.SceneValue.Finite(force)||!MegaMappingExpansion.Api.SceneValue.Finite(radius)||radius<0)throw new ArgumentException("Spring impulse needs finite position, force and radius");
            double center=Math.Max(0,Math.Min(1,position))*(Count-1),width=Math.Max(1,radius*Count);
            for(int i=Math.Max(0,(int)Math.Floor(center-width));i<=Math.Min(Count-1,(int)Math.Ceiling(center+width));i++)
            {
                double distance=Math.Abs(i-center)/width;if(distance<1)velocity[i]=Math.Max(-10000,Math.Min(10000,velocity[i]+force*(.5+.5*Math.Cos(distance*Math.PI))));
            }
        }
        internal void Tick(double delta,double stiffness,double propagation,double damping,double limit)
        {
            int steps=(int)Math.Ceiling(delta*240);if(steps==0)return;double dt=delta/steps,drag=Math.Exp(-damping*dt);
            for(int step=0;step<steps;step++)
            {
                for(int i=0;i<Count;i++)acceleration[i]=-stiffness*height[i]+propagation*(height[i==0?i:i-1]+height[i==Count-1?i:i+1]-2*height[i]);
                for(int i=0;i<Count;i++)
                {
                    velocity[i]=(velocity[i]+acceleration[i]*dt)*drag;double next=height[i]+velocity[i]*dt;
                    height[i]=Math.Max(-limit,Math.Min(limit,next));if(height[i]!=next)velocity[i]=0;
                }
            }
        }
        internal double Sample(double position)
        {double sample=Math.Max(0,Math.Min(1,position))*(Count-1);int low=(int)sample,high=Math.Min(Count-1,low+1);return height[low]+(height[high]-height[low])*(sample-low);}
        internal double[] Capture()
        {var data=new double[Count*2];Array.Copy(height,data,Count);Array.Copy(velocity,0,data,Count,Count);return data;}
        internal void Validate(double[] data)
        {if(data==null||data.Length!=Count*2)throw new InvalidDataException("Spring field state layout differs");foreach(double value in data)if(!MegaMappingExpansion.Api.SceneValue.Finite(value)||Math.Abs(value)>1000000)throw new InvalidDataException("Invalid spring field state");}
        internal void Restore(double[] data){Validate(data);Array.Copy(data,height,Count);Array.Copy(data,Count,velocity,0,Count);}
    }
}
