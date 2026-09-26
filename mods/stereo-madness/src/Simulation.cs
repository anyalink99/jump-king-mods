using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace StereoMadness
{
    public sealed class LevelObject
    {
        public int Id, Index, Layer;
        public string Kind, Frame;
        public double X, Y, Width, Height, Rotation, Scale = 1;
        public bool FlipX, FlipY, Additive, Audio;
        public int Tint = 0xffffff;
    }
    public sealed class ColorTrigger
    {
        public double X, Duration;
        public int Channel, R, G, B;
    }
    public sealed class Course
    {
        public sealed class Effect { public double X; public int Value; }
        public Effect[] Effects = new Effect[0];
        public LevelObject[] Objects;
        public ColorTrigger[] Colors;
        public double EndX;
        public static Course Load(string path)
        {
            var root = XDocument.Load(path).Root;
            if (root == null || root.Name != "StereoMadness" || (int)root.Attribute("version") != 1)
                throw new InvalidDataException("Unsupported Stereo Madness course");
            var objects = new List<LevelObject>();
            var colors = new List<ColorTrigger>();
            foreach (var n in root.Elements("Object")) {
                objects.Add(new LevelObject { Index = objects.Count, Id = (int)n.Attribute("id"), Kind = (string)n.Attribute("kind"),
                    Frame = (string)n.Attribute("frame") ?? "", X = Number(n,"x"), Y = Number(n,"y"),
                    Width = Number(n,"w"), Height = Number(n,"h"), Rotation = Number(n,"rotation"),
                    Scale = Number(n,"scale",1), FlipX = Number(n,"flipX") != 0, FlipY = Number(n,"flipY") != 0,
                    Tint = (int)Number(n,"tint",0xffffff), Layer = (int)Number(n,"layer",1),
                    Additive = Number(n,"additive") != 0, Audio = Number(n,"audio") != 0 });
            }
            foreach (var n in root.Elements("Color")) colors.Add(new ColorTrigger { X = Number(n,"x"),
                Duration = Number(n,"duration"), Channel = (int)Number(n,"channel"), R = (int)Number(n,"r"), G = (int)Number(n,"g"), B = (int)Number(n,"b") });
            colors.Sort(delegate(ColorTrigger a, ColorTrigger b) { return a.X.CompareTo(b.X); });
            double end = Number(root,"endX");
            if (objects.Count == 0 || end <= 0) throw new InvalidDataException("Empty course");
            var effects = new List<Effect>();
            foreach (var n in root.Elements("Effect")) effects.Add(new Effect { X = Number(n,"x"), Value = (int)Number(n,"value") });
            effects.Sort(delegate(Effect a, Effect b) { return a.X.CompareTo(b.X); });
            return new Course { Objects = objects.ToArray(), Colors = colors.ToArray(), EndX = end, Effects = effects.ToArray() };
        }
        private static double Number(XElement n, string key, double fallback = 0)
        {
            var attribute = n.Attribute(key);
            double value = attribute == null ? fallback : double.Parse(attribute.Value, CultureInfo.InvariantCulture);
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new InvalidDataException("Non-finite " + key);
            return value;
        }
    }

    // Reference uses doubled GD coordinates and 60 Hz frame units, subdivided at 240 Hz.
    // Rendering and native coordinates never round the collision state.
    public sealed class Simulation
    {
        public const double Speed = 11.540004, Gravity = 1.916398, Step = .225;
        public readonly Course Course;
        private readonly Dictionary<int,List<LevelObject>> buckets = new Dictionary<int,List<LevelObject>>();
        private HashSet<int> portals = new HashSet<int>();
        private LevelObject lastLand;
        private double lastOffset, rotationStart, rotationTime, cubeGroundDelay;
        private bool rotating;
        public double X, Y, VY, Rotation, CameraY, Floor, Time;
        public double? Ceiling;
        public bool Grounded, CanJump, Jumping, Ship, Dead, Complete;
        public int Jumps;
        public Simulation(Course course)
        {
            Course = course;
            foreach (var o in course.Objects) if (o.Kind == "solid" || o.Kind == "hazard" || o.Kind == "fly" || o.Kind == "cube") {
                int key = (int)Math.Floor(o.X / 400);
                List<LevelObject> items;
                if (!buckets.TryGetValue(key,out items)) buckets.Add(key, items = new List<LevelObject>());
                items.Add(o);
            }
            Reset();
        }
        public void Reset()
        {
            X = 0; Y = 30; VY = Rotation = CameraY = Floor = Time = 0; Ceiling = null;
            Grounded = CanJump = true; Jumping = Ship = Dead = Complete = rotating = false;
            Jumps = 0; portals.Clear(); lastLand = null; lastOffset = cubeGroundDelay = 0;
        }
        public bool Takeoff(bool held)
        {
            if (!held || Ship || !CanJump || Dead || Complete) return false;
            VY = 22.360064; CanJump = Grounded = false; Jumping = true; Jumps++;
            rotationStart = Rotation; rotationTime = 0; rotating = true;
            return true;
        }
        // Detached prediction: collision/portal state is private; immutable course buckets are shared.
        public Simulation Preview(bool held)
        {
            var copy = Capture();
            copy.Tick(held);
            return copy;
        }
        public Simulation Capture()
        {
            var copy = (Simulation)MemberwiseClone();
            copy.portals = new HashSet<int>(portals); return copy;
        }
        public void Restore(Simulation state)
        {
            if (state == null || !ReferenceEquals(Course, state.Course)) throw new InvalidOperationException("Snapshot belongs to another course");
            X=state.X; Y=state.Y; VY=state.VY; Rotation=state.Rotation; CameraY=state.CameraY;
            Floor=state.Floor; Time=state.Time; Ceiling=state.Ceiling; Grounded=state.Grounded;
            CanJump=state.CanJump; Jumping=state.Jumping; Ship=state.Ship; Dead=state.Dead; Complete=state.Complete;
            Jumps=state.Jumps; rotating=state.rotating; rotationStart=state.rotationStart; rotationTime=state.rotationTime;
            lastLand=state.lastLand; lastOffset=state.lastOffset; cubeGroundDelay=state.cubeGroundDelay;
            portals=new HashSet<int>(state.portals);
        }
        // Native position-only tools do not store controller internals. Recover
        // the course region from passed portals, using their native zero-velocity
        // restore semantics. Full Runtime snapshots retain exact airborne state.
        public void Teleport(double x, double y, double vy)
        {
            Reset(); X=x; Y=y; VY=vy;
            var ordered = new List<LevelObject>();
            foreach (var o in Course.Objects) if ((o.Kind=="fly"||o.Kind=="cube") && o.X < x-30) ordered.Add(o);
            ordered.Sort((a,b)=>a.X.CompareTo(b.X));
            foreach (var o in ordered) {
                portals.Add(o.Index); Ship=o.Kind=="fly";
                if (Ship) { Floor=Math.Max(0,Math.Floor((o.Y-300)/60)*60); Ceiling=Floor+600; }
                else { Floor=0; Ceiling=null; }
            }
            Grounded=CanJump=Y <= Floor+30;
            foreach (var o in Course.Objects) if (o.Kind=="solid" && Math.Abs(X-o.X)<o.Width/2+25 && Math.Abs(Y-(o.Y+o.Height/2+30))<.01)
                Grounded=CanJump=true;
            Jumping=!Ship && VY>3.832796;
            CameraY=Ship ? Math.Max(0,Floor+160) : Math.Max(0,Y-280);
            Time=Math.Max(0,(x+330)/(Speed*.9*60));
        }
        public void Tick(bool held)
        {
            if (Dead || Complete) return;
            double beforeY = Y;
            for (int i = 0; i < 4 && !Dead; i++) Substep(held);
            if (Dead) return;
            Time += 1.0/60;
            if (cubeGroundDelay > 0 && (cubeGroundDelay -= 1.0/60) <= 0) { Floor=0; Ceiling=null; }
            double target = CameraY;
            if (Ceiling.HasValue && Ship) target = Math.Max(0,Floor+160);
            else if (Y > CameraY+280) target = Y-280;
            else if (Y < CameraY+60) target = Y-60;
            CameraY += (Math.Max(0,target)-CameraY)/10;
            if (Ship) Rotation = Slerp(Rotation, Math.Atan2(-(Y-beforeY),10.3860036),.15);
            if (X >= Course.EndX-30) Complete = true;
        }
        public void Substep(bool held)
        {
            if (Dead || Complete) return;
            double lastY = Y;
            if (Ship) {
                double lift = held ? -1 : .8;
                if (!held && VY >= 3.832796) lift=1.2;
                double multiplier = held && VY < 3.832796 ? .5 : .4;
                VY = Math.Max(-12.8,Math.Min(16,VY-Gravity*Step*lift*multiplier));
                if (held) Grounded=false;
            } else if (!Takeoff(held)) {
                if (Jumping) { VY -= Gravity*Step; if(VY<3.832796) { Jumping=false; Grounded=false; } }
                else {
                    if(VY<3.832796) CanJump=false;
                    VY=Math.Max(-30,VY-Gravity*Step);
                    if(VY<-.25 && !rotating) { rotationStart=Rotation;rotationTime=0;rotating=true; }
                    if(VY < -4) Grounded=false;
                }
            }
            Y += VY*Step;
            Collide(lastY);
            // Official classic mode advances X after collision, including a fatal substep.
            X += Speed*.225;
            if (!Ship) {
                if(Grounded) Rotation=Slerp(Rotation,Math.Floor(Rotation/(Math.PI/2)+.5)*(Math.PI/2),.4725*Step);
                else if(rotating) { rotationTime+=1.0/240; Rotation=rotationStart+Math.PI*Math.Min(1,rotationTime/(.39/.9)); if(rotationTime>=.39/.9) rotating=false; }
            }
        }
        private void Land() { VY=0;Grounded=CanJump=true;Jumping=rotating=false; }
        private void Collide(double lastY)
        {
            double py=Y, topContact=0, bottomContact=0;
            int inset=Ship?12:20;
            bool landed=false;
            int section=(int)Math.Floor(X/400);
            for(int s=section-1;s<=section+1;s++) {
                List<LevelObject> items; if(!buckets.TryGetValue(s,out items))continue;
                foreach(var o in items) {
                    double left=o.X-o.Width/2,right=o.X+o.Width/2,top=o.Y-o.Height/2,bottom=o.Y+o.Height/2;
                    if(X+30<=left||X-30>=right||py+30<=top||py-30>=bottom)continue;
                    if(o.Kind=="hazard") { Dead=true;return; }
                    if(o.Kind=="fly"||o.Kind=="cube") {
                        if(portals.Add(o.Index)) {
                            bool fly=o.Kind=="fly";
                            if(Ship!=fly) {
                                Ship=fly;VY*=.5;Grounded=CanJump=Jumping=rotating=false;Rotation=0;
                                if(fly) { Floor=Math.Max(0,Math.Floor((o.Y-300)/60)*60);Ceiling=Floor+600;cubeGroundDelay=0; }
                                else cubeGroundDelay=.5;
                            }
                        }
                        continue;
                    }
                    double feet=py-30+inset,feetLast=lastY-30+inset,head=py+30-inset,headLast=lastY+30-inset;
                    bool onTop=(VY<=0||Grounded)&&(feet>=bottom||feetLast>=bottom);
                    if(X+9>left&&X-9<right&&py+9>top&&py-9<bottom&&!onTop) { Dead=true;return; }
                    if(X+25>left&&X-25<right) {
                        if(onTop) { Y=bottom+30;Land();landed=true;bottomContact=bottom;if(!Ship)Snap(o);continue; }
                        if((head<=top||headLast<=top)&&(VY>=0||Grounded)&&Ship) { Y=top-30;Land();topContact=top; }
                    }
                }
            }
            if(topContact!=0&&bottomContact!=0&&Math.Abs(topContact-bottomContact)<48) { Dead=true;return; }
            if(!landed&&Y<=Floor+30) { Y=Floor+30;Land(); }
            if(Ceiling.HasValue&&Y>=Ceiling.Value-30) { Y=Ceiling.Value-30;Land(); }
            if(Ship&&!landed&&Y>Floor+30&&topContact==0&&(!Ceiling.HasValue||Y<Ceiling.Value-30))Grounded=false;
        }
        private void Snap(LevelObject o)
        {
            if(lastLand!=null&&lastLand!=o) {
                double dx=o.X-lastLand.X,dy=o.Y-lastLand.Y;
                if((Math.Abs(dx-240)<=2&&Math.Abs(dy-60)<=2)||(Math.Abs(dx-300)<=2&&Math.Abs(dy+60)<=2)||(Math.Abs(dx-180)<=2&&Math.Abs(dy-120)<=2))
                    X+=Math.Max(-2,Math.Min(2,o.X+lastOffset-X));
            }
            lastLand=o;lastOffset=X-o.X;
        }
        private static double Slerp(double a,double b,double t)
        { double d=b-a;while(d>Math.PI)d-=2*Math.PI;while(d< -Math.PI)d+=2*Math.PI;return a+d*t; }
    }
}
