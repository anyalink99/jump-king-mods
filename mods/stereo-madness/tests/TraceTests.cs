using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using StereoMadness;
internal static class TraceTests
{
    public static void Main(string[] args)
    {
        var serializer = new JavaScriptSerializer();
        dynamic input = serializer.DeserializeObject(File.ReadAllText(args[0]));
        var objects = new List<LevelObject>();
        foreach (var o in input["objects"]) objects.Add(new LevelObject { Index=objects.Count,Kind=o["type"],X=Convert.ToDouble(o["x"]),Y=Convert.ToDouble(o["y"]),Width=Convert.ToDouble(o["w"]),Height=Convert.ToDouble(o["h"]) });
        var m = new Simulation(new Course { Objects=objects.ToArray(),Colors=new ColorTrigger[0],EndX=100000 });
        m.X=Convert.ToDouble(input["x"]);m.Y=Convert.ToDouble(input["y"]);m.Ship=input["ship"];
        m.CanJump=m.Grounded=!m.Ship;if(m.Ship)m.Ceiling=600;
        dynamic expected=serializer.DeserializeObject(File.ReadAllText(args[1]));int tick=0;
        foreach(bool held in input["held"]) {
            if(m.Dead)break;m.Tick(held);
            var row=expected[tick];
            if(Math.Abs(m.X-Convert.ToDouble(row[0]))>1e-7||Math.Abs(m.Y-Convert.ToDouble(row[1]))>1e-7||Math.Abs(m.VY-Convert.ToDouble(row[2]))>1e-7||m.Grounded!=(bool)row[3]||m.Dead!=(bool)row[4])
                throw new Exception("Reference mismatch at tick "+tick+": "+m.X+","+m.Y+","+m.VY);
            tick++;
        }
        if(tick!=expected.Length)throw new Exception("Trace length mismatch");
        Console.WriteLine("[OK] "+Path.GetFileName(args[0])+": "+tick+" frames match reference physics/collisions");
    }
}
