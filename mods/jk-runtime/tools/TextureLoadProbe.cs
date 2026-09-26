using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

// Offline read-only probe. It does not launch the game, load mods or touch saves.
// Scans texture directories rather than claiming to reproduce all LoadAssets work.
internal static class TextureLoadProbe
{
    private sealed class Service : IGraphicsDeviceService
    {
        public GraphicsDevice GraphicsDevice { get; private set; }
        internal Service(GraphicsDevice device) { GraphicsDevice=device; }
        public event EventHandler<EventArgs> DeviceCreated { add { } remove { } }
        public event EventHandler<EventArgs> DeviceDisposing { add { } remove { } }
        public event EventHandler<EventArgs> DeviceReset { add { } remove { } }
        public event EventHandler<EventArgs> DeviceResetting { add { } remove { } }
    }
    private sealed class Row { internal string Name; internal double Ms; internal long Bytes; }
    [STAThread]
    private static int Main(string[] args)
    {
        Type diagnostics=null;
        try
        {
            if(args.Length>=4 && args[0]=="--runtime")
            {
                Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(args[1]),"0Harmony.dll"));
                diagnostics=Assembly.LoadFrom(args[1]).GetType("JKRuntime.PerformanceDiagnostics",true);
                diagnostics.GetField("OutputDirectory",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,args[2]);
                Directory.CreateDirectory(args[2]);
                diagnostics.GetMethod("Configure",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{true});
                args=args.Skip(3).ToArray();
            }
            using(var window=new Form { ShowInTaskbar=false })
            using(var device=new GraphicsDevice(GraphicsAdapter.DefaultAdapter,GraphicsProfile.HiDef,
                new PresentationParameters { DeviceWindowHandle=window.Handle,BackBufferWidth=480,BackBufferHeight=360 }))
            {
                var services=new GameServiceContainer(); services.AddService(typeof(IGraphicsDeviceService),new Service(device));
                foreach(var root in args)
                using(var content=new ContentManager(services))
                {
                    var rows=new List<Row>();
                    Console.WriteLine("Root: "+root);
                    Console.WriteLine("Texture folders only; installed MonoGame loader; no game/mod hooks; file cache uncontrolled.");
                    foreach(var folder in new[]{"screens","props/textures"})
                    {
                        var path=Path.Combine(root,folder); if(!Directory.Exists(path)) continue;
                        int count=0; long bytes=0; var total=Stopwatch.StartNew();
                        foreach(var file in Directory.GetFiles(path,"*.xnb",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal))
                        {
                            var timer=Stopwatch.StartNew(); var texture=content.Load<Texture2D>(file.Substring(0,file.Length-4)); timer.Stop();
                            long size=(long)texture.Width*texture.Height*4;
                            rows.Add(new Row {Name=file.Substring(root.Length).TrimStart('\\','/'),Ms=timer.Elapsed.TotalMilliseconds,Bytes=size});
                            bytes+=size; count++;
                        }
                        total.Stop();
                        Console.WriteLine(folder+": "+count+" textures, "+(bytes/1048576.0).ToString("F1")+" MiB RGBA base-level estimate, "+total.Elapsed.TotalMilliseconds.ToString("F1")+" ms");
                    }
                    foreach(var row in rows.OrderByDescending(r=>r.Ms).Take(15))
                        Console.WriteLine(row.Ms.ToString("F2")+" ms\t"+(row.Bytes/1048576.0).ToString("F2")+" MiB\t"+row.Name);
                }
            }
            return 0;
        }
        catch(Exception error) { Console.Error.WriteLine(error); return 1; }
        finally
        {
            if(diagnostics!=null)
            {
                diagnostics.GetMethod("Configure",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{false});
                diagnostics.GetMethod("Flush",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            }
        }
    }
}
