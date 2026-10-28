using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OverlayPlus;

internal static class PerformanceProbe
{
    internal static void Run(string game)
    {
        Console.WriteLine("Overlay+ isolated CPU measurements; these are not live game frame times.");
        Measure("input.GamePad.GetState",()=>GamePad.GetState(PlayerIndex.One),450,16);
        string root=Path.Combine(game,"Content/OverlayPlus");
        string runs=Path.Combine(root,"Runs");
        string path=Directory.Exists(runs)?Directory.GetFiles(runs,"*.xml").FirstOrDefault():null;
        var archive=path==null?new RunArchive():XmlData.Parse<RunArchive>(File.ReadAllText(path));
        Console.WriteLine("Read-only archive fixture: "+archive.Runs.Count+" runs");
        var run=archive.Runs.FirstOrDefault()??new Run();
        Measure("save.run-xml-clone",()=>XmlData.Clone(run),40,0);
        Measure("save.run-copy",()=>run.Copy(),40,0);
        Measure("save.archive-serialize.current",()=>XmlData.Serialize(archive),40,0);
        foreach(int count in new[]{100,1000}){
            var large=new RunArchive{Route=archive.Route};for(int i=0;i<count;i++){var copy=XmlData.Clone(run);copy.Id=Guid.NewGuid().ToString("N");large.Runs.Add(copy);}
            Measure("save.archive-serialize."+count,()=>XmlData.Serialize(large),12,0);
            Measure("save.archive-snapshot."+count,()=>large.Copy(),40,0);
        }
    }
    internal static void Measure(string name,Action action,int count,int spacing)
    {
        double[] times=new double[count];int collections=GC.CollectionCount(0);
        for(int i=0;i<count;i++){long start=Stopwatch.GetTimestamp();action();times[i]=(Stopwatch.GetTimestamp()-start)*1000d/Stopwatch.Frequency;if(spacing>0)Thread.Sleep(spacing);}
        double first=times[0],warmMax=times.Skip(1).Max();Array.Sort(times);Console.WriteLine("[PERF] "+name+": median="+times[count/2].ToString("F3")+" ms p95="+times[Math.Min(count-1,(int)(count*.95))].ToString("F3")+" ms max="+times[count-1].ToString("F3")+" ms first="+first.ToString("F3")+" ms warmMax="+warmMax.ToString("F3")+" ms Gen0="+(GC.CollectionCount(0)-collections));
    }
}
