using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace JKRuntime.Presentation
{
    /// <summary>A scripted view. Coordinates are canonical world pixels; Screen is zero-based.</summary>
    public struct CameraShot
    {
        public Vector2 Center {get;private set;}
        public int Screen {get;private set;}
        public bool Smooth {get;private set;}
        public double Duration {get;private set;}
        public double ReturnDuration {get;private set;}
        public long Id {get;internal set;}
        public CameraShot(Vector2 center,int screen,bool smooth,double duration,double returnDuration):this()
        {
            new CameraTarget(center,Vector2.Zero,screen,false).Validate();
            if(double.IsNaN(duration)||double.IsInfinity(duration)||duration<0||duration>60||double.IsNaN(returnDuration)||double.IsInfinity(returnDuration)||returnDuration<0||returnDuration>60)
                throw new ArgumentOutOfRangeException("duration","Camera durations must be finite seconds in 0..60");
            Center=center;Screen=screen;Smooth=smooth;Duration=duration;ReturnDuration=returnDuration;
        }
    }
    /// <summary>Scoped map views. Exclusive CameraTargets sessions take precedence over map requests.</summary>
    public static class CameraShots
    {
        private static readonly List<Lease> requests=new List<Lease>();
        private static long serial;
        private static Func<bool> smoothReady;
        public static bool SmoothAvailable {get{RuntimeApi.Kernel.CheckThread();return smoothReady!=null&&smoothReady();}}
        public static IDisposable RegisterSmoothProvider(Func<bool> ready)
        {
            RuntimeApi.Kernel.CheckThread();if(ready==null||smoothReady!=null)throw new InvalidOperationException("A smooth shot provider is already registered or invalid");
            smoothReady=ready;return new ActionLease(delegate{RuntimeApi.Kernel.CheckThread();if(smoothReady==ready)smoothReady=null;});
        }
        public static Lease Acquire(string owner,int priority,RuntimeScope scope)
        {
            RuntimeApi.Kernel.CheckThread();owner=ModuleDefinition.ValidId(owner);
            if(scope==null)throw new ArgumentNullException("scope");if(requests.Count>=64)throw new InvalidOperationException("Camera shot request budget exceeded");
            var lease=new Lease(owner,priority);try{scope.Own(lease);}catch{lease.Dispose();throw;}requests.Add(lease);return lease;
        }
        public static bool TryRead(out CameraShot shot)
        {
            RuntimeApi.Kernel.CheckThread();CameraTarget target;
            Lease selected=null;
            if(!CameraTargets.TryRead(out target))foreach(var request in requests)
                if(request.Active&&(selected==null||request.Priority>selected.Priority||request.Priority==selected.Priority&&request.Order>selected.Order))selected=request;
            shot=selected==null?default(CameraShot):selected.Shot;return selected!=null;
        }
        public sealed class Lease : IDisposable
        {
            public string Owner {get;private set;}
            internal readonly int Priority;
            internal long Order;
            internal bool Active;
            internal CameraShot Shot;
            private bool disposed;
            private IDisposable registration;
            internal Lease(string owner,int priority)
            {
                Owner=owner;Priority=priority;
                registration=RuntimeResources.Track(owner,"camera-shot",new ActionLease(delegate{disposed=true;Active=false;requests.Remove(this);}));
            }
            public void Publish(CameraShot shot)
            {
                RuntimeApi.Kernel.CheckThread();if(disposed)throw new ObjectDisposedException("CameraShots.Lease");
                // repeated property synchronization mustn't steal equal-priority ownership
                if(!Active){Order=++serial;Active=true;}shot.Id=Order;Shot=shot;
            }
            public bool Selected {get{CameraShot shot;return !disposed&&Active&&TryRead(out shot)&&shot.Id==Order;}}
            public void Clear(){RuntimeApi.Kernel.CheckThread();if(disposed)throw new ObjectDisposedException("CameraShots.Lease");Active=false;}
            public void Dispose(){RuntimeApi.Kernel.CheckThread();if(registration==null)return;registration.Dispose();registration=null;}
        }
    }
}
