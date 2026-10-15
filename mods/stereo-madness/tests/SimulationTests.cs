using System;
using StereoMadness;
internal static class SimulationTests
{
    private static int checks;
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    private static Simulation World(params LevelObject[] objects)
    { return new Simulation(new Course { EndX=100000, Objects=objects, Colors=new ColorTrigger[0] }); }
    private static void Near(double a, double b, string label) { Check(Math.Abs(a-b)<1e-7,label+": "+a+" != "+b); }
    public static void Main()
    {
        var m=World();
        for(int i=0;i<60;i++)m.Tick(false);
        Near(m.X,623.160216,"One-second classic speed"); Near(m.Y,30,"Ground support");
        m.Reset(); m.Substep(true); Near(m.VY,22.360064,"Takeoff impulse"); Near(m.Y,35.0310144,"First jump substep");
        for(int i=0;i<20;i++)m.Tick(true);
        Check(m.Jumps==1,"Held input does not count every tick");
        for(int i=0;i<60;i++)m.Tick(true);
        Check(m.Jumps>1,"Holding jumps again after landing");
        m=World(); m.Ship=true;m.Y=300;
        m.Substep(true); Near(m.VY,1.916398*.225*.5,"Ship lift while falling");
        int jumps=m.Jumps;
        for(int i=0;i<100;i++)m.Tick(true);
        Check(m.Jumps==jumps,"Ship thrust is not a native cube jump"); Near(m.VY,16,"Ship upward terminal speed");
        m=World(new LevelObject{Index=1,Kind="hazard",X=65,Y=30,Width=10,Height=20});
        for(int i=0;i<10&&!m.Dead;i++)m.Tick(false);
        Check(m.Dead,"Hazard contact kills"); double x=m.X;m.Tick(true);Near(m.X,x,"Death freezes simulation");
        m.Reset(); Check(!m.Dead&&!m.Ship&&m.Jumps==0&&m.X==0,"Restart clears model state");
        m=World(new LevelObject{Index=1,Kind="solid",X=50,Y=30,Width=60,Height=60});
        m.X=50;m.Y=90;m.VY=-4;m.Grounded=m.CanJump=false;m.Substep(false);
        Near(m.Y,90,"Top support snaps outer feet");Check(m.Grounded,"Top contact enables jump");
        m=World(new LevelObject{Index=1,Kind="solid",X=65,Y=30,Width=60,Height=60});
        for(int i=0;i<10&&!m.Dead;i++)m.Tick(false);
        Check(m.Dead,"Solid side crush kills");
        m=World(new LevelObject{Index=1,Kind="fly",X=0,Y=330,Width=90,Height=180},new LevelObject{Index=2,Kind="cube",X=800,Y=330,Width=90,Height=180});
        m.Y=330;m.Substep(false);Check(m.Ship&&m.Ceiling==600,"Fly portal opens ship corridor");
        m.X=800;m.Y=330;m.Substep(false);Check(!m.Ship,"Cube portal restores cube");
        for(int i=0;i<31;i++)m.Tick(false);Check(!m.Ceiling.HasValue&&m.Floor==0,"Cube exit releases corridor");
        m=World();m.Course.EndX=40;m.Tick(false);Check(m.Complete,"Finish threshold emits completion");
        m=World(new LevelObject{Index=11,Kind="fly",X=40,Y=330,Width=10,Height=180});
        m.Y=330; var preview=m.Preview(false);
        Check(preview.Ship && !m.Ship, "Presentation prediction cannot consume a live portal");
        m.Tick(false); Check(m.Ship,"Real tick can still enter the predicted portal");
        var saved=m.Capture(); var expected=saved.Preview(true); m.Tick(false); m.Restore(saved); m.Tick(true);
        Near(m.X,expected.X,"Restored ship X"); Near(m.Y,expected.Y,"Restored ship Y"); Near(m.VY,expected.VY,"Restored ship velocity");
        m.Reset(); m.Restore(saved); Check(m.Ship && m.Ceiling==saved.Ceiling,"Snapshot retains portal state independently of reset");
        m.Teleport(100,330,0); Check(m.Ship && m.Ceiling==600 && !m.Dead,"Native position restore recovers ship corridor");
        m=World(); preview=m.Preview(true);
        Check(m.Jumps==0 && m.Time==0 && m.X==0 && preview.Jumps==1,"Prediction never advances live jumps or time");
        m=World(new LevelObject{Index=12,Kind="hazard",X=35,Y=30,Width=10,Height=20});
        preview=m.Preview(false); Check(preview.Dead && !m.Dead,"Prediction detects hazards without killing the live player");
        Console.WriteLine("[OK] "+checks+" simulation checks");
    }
}
