using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace JKRuntime.Particles
{
    /// <summary>reusable cosmetic state. A reference is valid only while its particle is alive</summary>
    public class ParticleState
    {
        public Vector2 Position, Velocity;
        public float Age, Life, Rotation;
        /// <summary>release borrowed resources in overrides, then call base.Reset(). never dispose shared textures</summary>
        public virtual void Reset() { Position=Velocity=Vector2.Zero; Age=Life=Rotation=0; }
    }

    /// <summary>fixed-capacity, stable-order particle pool. explicit updates only, no timer, draw hook or gameplay writes</summary>
    public sealed class ParticleSystem<T> : IReadOnlyList<T>, IDisposable where T:ParticleState,new()
    {
        private readonly T[] active, free;
        private readonly bool[] retired;
        private readonly Action<T,float> step;
        private int count, available;
        private bool updating, disposed;
        public int Count { get { return count; } }
        public int Capacity { get { return active.Length; } }
        public int PeakCount { get; private set; }
        public long DroppedCount { get; private set; }
        public T this[int index] { get { if(index<0||index>=count)throw new ArgumentOutOfRangeException("index");return active[index]; } }
        public ParticleSystem(int capacity,Action<T,float> update)
        {
            RuntimeApi.Kernel.CheckThread();
            if(capacity<1||capacity>65536)throw new ArgumentOutOfRangeException("capacity");
            if(update==null)throw new ArgumentNullException("update");
            active=new T[capacity];free=new T[capacity];retired=new bool[capacity];available=capacity;step=update;
            for(int i=0;i<capacity;i++)free[i]=new T();
        }
        private void Check()
        {RuntimeApi.Kernel.CheckThread();if(disposed)throw new ObjectDisposedException("ParticleSystem");if(updating)throw new InvalidOperationException("Do not mutate a particle system from its update callback");}
        /// <summary>returns a reset particle, or null at capacity. optional replacement retires the oldest particle</summary>
        public T Spawn(bool replaceOldest=false)
        {
            Check();if(count==Capacity){DroppedCount++;if(!replaceOldest)return null;RemoveAt(0);}
            T particle=free[--available];free[available]=null;active[count++]=particle;PeakCount=Math.Max(PeakCount,count);return particle;
        }
        public void RemoveAt(int index)
        {
            Check();T particle=this[index];particle.Reset();
            Array.Copy(active,index+1,active,index,count-index-1);active[--count]=null;free[available++]=particle;
        }
        /// <summary>age, retire and update in stable order without allocating or changing render state, Delta must be in [0, 0.1]</summary>
        public void Update(float delta)
        {
            Check();if(float.IsNaN(delta)||delta<0||delta>.1f)throw new ArgumentOutOfRangeException("delta");if(delta==0)return;
            updating=true;
            try
            {
                // retire before callbacks, expired particles never run their behaviour
                for(int i=0;i<count;i++){active[i].Age+=delta;retired[i]=active[i].Age>=active[i].Life;}
                // reset before changing ownership, a user reset that throws mustn't corrupt the pool
                for(int i=0;i<count;i++)if(retired[i])active[i].Reset();
                int retained=0;
                for(int i=0;i<count;i++)
                {
                    T p=active[i];
                    if(retired[i])free[available++]=p;else active[retained++]=p;
                }
                Array.Clear(active,retained,count-retained);count=retained;
                for(int i=count-1;i>=0;i--)step(active[i],delta);
            }
            finally {updating=false;}
        }
        public void Clear()
        {Check();for(int i=0;i<count;i++)active[i].Reset();for(int i=count-1;i>=0;i--){free[available++]=active[i];active[i]=null;}count=0;}
        public void Dispose(){if(disposed)return;Clear();disposed=true;Array.Clear(free,0,free.Length);}
        public IEnumerator<T> GetEnumerator(){for(int i=0;i<count;i++)yield return active[i];}
        IEnumerator IEnumerable.GetEnumerator(){return GetEnumerator();}
    }
}
