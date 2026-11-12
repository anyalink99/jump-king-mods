using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using System.Xml.Serialization;
using FreeScaling;
using JKRuntime;
using JKRuntime.Settings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color=Microsoft.Xna.Framework.Color;
using Menu=FreeScaling.Menu;

internal static class Tests
{
    private static void Check(bool value,string message) { if(!value)throw new Exception(message); }
    [STAThread]
    private static int Main(string[] args)
    {
        try {
            Assembly.LoadFrom(Path.Combine(args[0],"0Harmony.dll"));
            Core(args[0]);
            if(args[1]=="--graphics"){Graphics(args[0]);WindowTests.Run(args[0]);}
            Console.WriteLine("[OK] Graphics+: settings, native menu contracts, ownership and sampling");return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private static void Core(string root)
    {
        string path=Path.Combine(root,"preferences.xml");
        var original=new Preferences {Enabled=true,Width=1200,Height=901,Sampling=PixelSampling.Nearest,HasPosition=true,X=-1600,Y=35};
        using(var output=File.Create(path))new XmlSerializer(typeof(Preferences)).Serialize(output,original);
        var file=new SettingsFile<Preferences>(path,()=>new Preferences(),Preferences.Validate);
        Check(file.Value.Width==1200 && file.Value.Height==901 && file.Value.Enabled,"Fractional size round trip");
        Check(file.Value.HasPosition && file.Value.X==-1600 && file.Value.Y==35,"A left-hand monitor's negative coordinates survive a settings round trip");
        var next=file.Value.Copy();next.Enabled=false;file.Save(next);
        Check(file.Value.Width==1200 && file.Value.Sampling==PixelSampling.Nearest,"Disabling preserves custom size and filter");
        File.WriteAllText(path,"<Preferences><Schema>99</Schema></Preferences>");
        var invalid=new SettingsFile<Preferences>(path,()=>new Preferences(),Preferences.Validate);
        Check(!invalid.CanSave && File.ReadAllText(path).Contains("99"),"Future settings remain untouched");
        var bounded=WindowSession.Fit(new System.Drawing.Size(9000,8000),new System.Drawing.Size(1920,1040));
        Check(bounded.Width<=1920 && bounded.Height<=1040,"Oversized saved dimensions fit the working area");
        bounded=WindowSession.Fit(new System.Drawing.Size(1,1),new System.Drawing.Size(1920,1040));
        Check(bounded.Width==480 && bounded.Height==360,"Minimum logical size");
        var window=new System.Drawing.Size(1000,800);
        var position=WindowSession.FitPosition(new System.Drawing.Point(-1600,35),window,new System.Drawing.Rectangle(-1920,0,1920,1040));
        Check(position==new System.Drawing.Point(-1600,35),"Available secondary monitor position is preserved");
        position=WindowSession.FitPosition(new System.Drawing.Point(-1600,35),window,new System.Drawing.Rectangle(0,0,1920,1040));
        Check(position==new System.Drawing.Point(0,35),"Disconnected monitor restores inside an available work area");
        position=WindowSession.FitPosition(new System.Drawing.Point(1800,1000),window,new System.Drawing.Rectangle(0,40,1280,680));
        Check(position==new System.Drawing.Point(280,40),"Changed resolution and top taskbar leave the title bar reachable");
        using(var hooks=new OwnedPatches("free-scaling.tests"))Menu.Install(hooks);
        var state=typeof(ModEntry).GetField("session",OwnedPatches.Members);
        try {
            state.SetValue(null,FormatterServices.GetUninitializedObject(typeof(WindowSession)));
            object[] result={true};
            bool run=(bool)typeof(Menu).GetMethod("CanResize",OwnedPatches.Members).Invoke(null,result);
            Check(!run && !(bool)result[0],"Native integer selector is disabled while free sizing owns the window");
            object[] mode={BehaviorTree.BTresult.Success};
            run=(bool)typeof(Menu).GetMethod("ModeRun",OwnedPatches.Members).Invoke(null,mode);
            Check(!run && (BehaviorTree.BTresult)mode[0]==BehaviorTree.BTresult.Failure,"Native fullscreen selector can't bypass ownership");
        }finally{state.SetValue(null,null);}
        object[] unlocked={false};
        Check((bool)typeof(Menu).GetMethod("CanResize",OwnedPatches.Members).Invoke(null,unlocked),"Native selector resumes its own availability rule");
    }
    private static void Graphics(string root)
    {
        using(var window=new Form {ShowInTaskbar=false})
        using(var device=new GraphicsDevice(GraphicsAdapter.DefaultAdapter,GraphicsProfile.HiDef,
            new PresentationParameters {DeviceWindowHandle=window.Handle,BackBufferWidth=480,BackBufferHeight=360}))
        using(var batch=new SpriteBatch(device))
        using(var source=new Texture2D(device,480,360))
        {
            var pixels=new Color[480*360];
            for(int y=0;y<360;y++)for(int x=0;x<480;x++)
                pixels[y*480+x]=new Color((byte)((x%2)*255),(byte)((y%2)*255),(byte)(((x+y)%2)*255),(byte)255);
            source.SetData(pixels);
            using(var lease=FramePresentation.Acquire(device,PixelSampling.Sharp)) {
                bool refused=false;try{FramePresentation.Acquire(device,PixelSampling.Nearest);}catch(InvalidOperationException){refused=true;}
                Check(refused && FramePresentation.Sampling==PixelSampling.Sharp,"Second owner leaves the first filter intact");
                int[,] sizes={{480,360},{960,720},{1200,900},{1440,1080},{1137,853}};
                for(int test=0;test<sizes.GetLength(0);test++) {
                    int w=sizes[test,0],h=sizes[test,1];
                    using(var target=new RenderTarget2D(device,w+14,h+22)) {
                        device.SetRenderTarget(target);device.Clear(Color.Magenta);
                        FramePresentation.Draw(batch,source,new Vector2(7,11),new Vector2(w/480f,h/360f),Color.White,SpriteEffects.None,Matrix.Identity,null);
                        device.SetRenderTarget(null);var actual=new Color[target.Width*target.Height];target.GetData(actual);
                        int maximum=0;
                        for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
                            var expected=Area(pixels,x,y,w,h);var got=actual[(y+11)*target.Width+x+7];
                            int error=Math.Max(Math.Abs(expected.R-got.R),Math.Max(Math.Abs(expected.G-got.G),Math.Abs(expected.B-got.B)));
                            maximum=Math.Max(maximum,error);Check(error<=2 && got.A==255,"GPU area coverage mismatch");
                        }
                        Check(actual[0]==Color.Magenta,"Margins remain untouched");
                        Console.WriteLine("[GPU] "+w+"x"+h+": max channel error "+maximum);
                    }
                }
                foreach(PixelSampling mode in Enum.GetValues(typeof(PixelSampling))) {
                    lease.Mode=mode;
                    using(var target=new RenderTarget2D(device,32,32))using(var transparent=new Texture2D(device,2,1)) {
                        transparent.SetData(new[]{Color.Transparent,new Color(128,0,0,128)});
                        device.SetRenderTarget(target);device.Clear(Color.Blue);
                        FramePresentation.Draw(batch,transparent,new Vector2(5,5),new Vector2(10,10),Color.White,SpriteEffects.FlipHorizontally,Matrix.Identity,null);
                        device.SetRenderTarget(null);var data=new Color[1024];target.GetData(data);
                        Check(data[8*32+6].R>=120 && data[8*32+23].R==0,"Flip and premultiplied alpha survive filter changes");
                    }
                }
            }
            Check(!FramePresentation.Enabled && FramePresentation.Sampling==PixelSampling.Nearest,"Disposal restores native sampling");
            using(var lease=FramePresentation.Acquire(device,PixelSampling.Sharp))Check(FramePresentation.Enabled,"Sampling can be re-enabled");
        }
    }
    private static Color Area(Color[] source,int x,int y,int w,int h)
    {
        double x0=x*480.0/w,x1=(x+1)*480.0/w,y0=y*360.0/h,y1=(y+1)*360.0/h;
        double r=0,g=0,b=0,area=(x1-x0)*(y1-y0);
        for(int sy=(int)Math.Floor(y0);sy<Math.Ceiling(y1)&&sy<360;sy++)
            for(int sx=(int)Math.Floor(x0);sx<Math.Ceiling(x1)&&sx<480;sx++) {
                double weight=Math.Max(0,Math.Min(x1,sx+1)-Math.Max(x0,sx))*Math.Max(0,Math.Min(y1,sy+1)-Math.Max(y0,sy))/area;
                var value=source[sy*480+sx];r+=value.R*weight;g+=value.G*weight;b+=value.B*weight;
            }
        return new Color((byte)Math.Round(r),(byte)Math.Round(g),(byte)Math.Round(b),(byte)255);
    }
}
