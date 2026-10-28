using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WorldsmithExtension
{
    internal static class CollisionTests
    {
        internal static void Run(string dir, Action<bool, string> Assert, Action<Action, string> Fails)
        {
            string ordinary = Path.Combine(dir, "ordinary-collision");
            Directory.CreateDirectory(ordinary);
            string atlasPath = Path.Combine(ordinary, "level.png"), stripPath = Path.Combine(ordinary, "visual_level.png");
            using (var atlas = new Bitmap(780, 585)) atlas.Save(atlasPath);
            using (var strip = new Bitmap(60, 45 * 10)) strip.Save(stripPath);
            DateTime stamp = DateTime.UtcNow.AddHours(-1);
            File.SetLastWriteTimeUtc(stripPath, stamp);
            File.SetLastWriteTimeUtc(atlasPath, stamp.AddMinutes(1));
            Assert(Layout.Read(ordinary, 169).Source == "atlas" && Collision.SourceScreens(ordinary) == 169, "ordinary collision uses the newer atlas and its screen count");
            string fingerprint = Files.Fingerprint(ordinary);
            File.SetLastWriteTimeUtc(stripPath, stamp.AddMinutes(2));
            Assert(Layout.Read(ordinary, 10).Source == "strip" && Collision.SourceScreens(ordinary) == 10, "ordinary collision uses the newer strip and its screen count");
            Assert(Files.Fingerprint(ordinary) != fingerprint, "changing the automatic source invalidates checked builds even without editing pixels");
            File.SetLastWriteTimeUtc(atlasPath, stamp.AddMinutes(2));
            Assert(Layout.Read(ordinary, 10).Source == "strip", "equal collision timestamps prefer the strip like regular Worldsmith");
            Files.Text(Path.Combine(ordinary, "worldsmith-extension.xml"), "<WorldsmithExtension collisionSource='atlas'/>");
            File.SetLastWriteTimeUtc(stripPath, stamp.AddMinutes(3));
            Assert(Layout.Read(ordinary, 169).Source == "atlas", "explicit source selection overrides ordinary timestamp selection");
            string inferred = Path.Combine(dir, "inferred-large-collision");
            Directory.CreateDirectory(inferred);
            using (var atlas = new Bitmap(780, 585)) atlas.Save(Path.Combine(inferred, "level.png"));
            using (var strip = new Bitmap(60, 512 * 45)) strip.Save(Path.Combine(inferred, "visual_level.png"));
            File.SetLastWriteTimeUtc(Path.Combine(inferred, "level.png"), stamp);
            Files.Text(Path.Combine(inferred, "transitions.xml"), "<Transitions version='1'><SideLink screen='1' side='left' target='512'/></Transitions>");
            Assert(Collision.SourceScreens(inferred) == 512 && Layout.Read(inferred, 512).Screens == 512 && Files.Fingerprint(inferred).Length > 0,
                "ordinary capacity is inferred before validating transitions above screen 169");
            foreach (int n in new[]{1, 169, 170, 255, 256, 512})
            {
                var layout = new Layout{Screens = n};
                layout.Validate();
                using (var strip = new Bitmap(60, 45 * n))
                {
                    for (int i = 0; i < n; i++)
                        strip.SetPixel(0, (n - i - 1) * 45, Color.FromArgb(127, i % 256, i / 256, 253));
                    strip.SetPixel(1, 0, Color.Magenta);
                    using (var atlas = Collision.Atlas(strip, layout))
                        using (var back = Collision.Strip(atlas, layout))
                        {
                            for (int i = 0; i < n; i++)
                                Assert(strip.GetPixel(0, (n - i - 1) * 45).ToArgb() == back.GetPixel(0, (n - i - 1) * 45).ToArgb(), "screen marker " + i);
                            Assert(back.GetPixel(1, 0).ToArgb() == Color.Magenta.ToArgb(), "magenta is data");
                            string xnb = Path.Combine(dir, "collision.xnb");
                            Collision.Xnb(atlas, xnb);
                            using (var r = new BinaryReader(File.OpenRead(xnb)))
                            {
                                Assert(new string (r.ReadChars(3)) == "XNB", "XNB magic");
                                r.ReadBytes(3);
                                Assert(r.ReadInt32() == r.BaseStream.Length, "XNB length");
                                Assert(r.ReadByte() == 1, "reader count");
                                r.ReadString();
                                r.ReadInt32();
                                r.ReadByte();
                                r.ReadByte();
                                Assert(r.ReadInt32() == 0, "RGBA format");
                                Assert(r.ReadInt32() == layout.Side * 60, "width");
                                Assert(r.ReadInt32() == layout.Side * 45, "height");
                                r.ReadInt32();
                                r.ReadInt32();
                                for (int y = 0; y < atlas.Height; y++)
                                    for (int x = 0; x < atlas.Width; x++)
                                    {
                                        var c = atlas.GetPixel(x, y);
                                        if (r.ReadByte() != c.R || r.ReadByte() != c.G || r.ReadByte() != c.B || r.ReadByte() != c.A)
                                            throw new Exception("Pixel corruption");
                                    }

                                Assert(r.BaseStream.Position == r.BaseStream.Length, "XNB payload");
                            }
                        }
                }
            }

            RightGrowing(dir, Assert, Fails);
            StructuredLayout(dir, Assert, Fails);
            PixelLayout(dir,Assert,Fails);
            LegacyCells(Assert,Fails);
        }

        static void StructuredLayout(string dir,Action<bool,string> Assert,Action<Action,string> Fails)
        {
            string root=Path.Combine(dir,"structured-layout");
            Directory.CreateDirectory(root);
            Files.Text(Path.Combine(root,"mapping_settings.xml"),"<MappingSettings version='1'><Components><Component id='view' kind='view'><Value name='timer' value='hide'/></Component></Components><MapLayout version='2' screens='10' source='native'/></MappingSettings>");
            Files.Text(Path.Combine(root,MegaMappingExpansion.MappingFiles.Transitions),"<Transitions version='1'><SideLink screen='2' side='right' target='7'/></Transitions>");
            var layout=Layout.Read(root,169);
            Assert(layout.Links.Count==1&&layout.Links[0].Item3==7,"structured settings load links from screens folder");
            layout.Links.Clear();layout.Links.Add(Tuple.Create(2,"left",9));layout.Save(root);
            var settings=XDocument.Load(Path.Combine(root,"mapping_settings.xml"));
            Assert((string)settings.Root.Element("Components").Element("Component").Element("Value").Attribute("value")=="hide","layout save preserves general scene settings");
            Assert(settings.Root.Element("MapLayout").Element("SideLink")==null&&!File.Exists(Path.Combine(root,"props/mega-mapping-expansion/map.xml")),"structured save doesn't generate a competing legacy layout");
            var reread=Layout.Read(root,169);
            Assert(reread.Links.Count==1&&reread.Links[0].Item2=="left"&&reread.Links[0].Item3==9,"editor link edits persist in transitions.xml");
            string output=Path.Combine(dir,"structured-layout-output");
            layout.SaveMetadata(output);
            Assert((string)XDocument.Load(Path.Combine(output,"mapping_settings.xml")).Root.Element("Components").Element("Component").Element("Value").Attribute("value")=="hide","native compilation preserves source settings before its copy pass");
            var recovered=Layout.Read(output,169);
            Assert(recovered.Links.Count==1&&recovered.Links[0].Item3==9,"compiled layout round-trips its separate transitions");
            string routesPath=Path.Combine(root,MegaMappingExpansion.MappingFiles.Transitions);
            Files.Text(routesPath,"<Transitions version='1' seed='123' saveId='test'><SideLink screen='2' side='left' target='9'/><Pool id='trials' screens='7,8,9' mode='shuffle'/><Exit screen='1' side='right' pool='trials'/></Transitions>");
            layout=Layout.Read(root,169);layout.Save(root);
            var routes=XDocument.Load(routesPath);
            Assert((string)routes.Root.Attribute("seed")=="123"&&routes.Root.Element("Pool")!=null&&routes.Root.Element("Exit")!=null,"layout edits preserve dynamic routes and seed attributes");
            string dynamicOutput=Path.Combine(dir,"dynamic-layout-output");layout.SaveMetadata(dynamicOutput);
            Assert(XDocument.Load(Path.Combine(dynamicOutput,MegaMappingExpansion.MappingFiles.Transitions)).Root.Element("Pool")!=null,"compiling into a fresh folder carries dynamic routes before the native copy pass");
            string original=File.ReadAllText(routesPath);layout.Links.Clear();layout.Screens=6;
            Fails(()=>layout.Save(root),"reducing content cannot silently discard or invalidate dynamic destinations");
            Assert(File.ReadAllText(routesPath)==original,"a failed layout edit preserves original transitions");
            var plan=TransitionAuthoring.Validate(root,original,10);
            Assert(TransitionAuthoring.Preview(plan,1,"trials",30)==TransitionAuthoring.Preview(plan,999,"trials",30),"fixed XML seed governs an isolated deterministic editor preview");
            Fails(()=>TransitionAuthoring.Save(root,original.Replace("7,8,9","7,7,9"),10),"editor refuses invalid pools before saving");
            Assert(File.ReadAllText(routesPath)==original,"invalid transition edits don't replace the source file");
        }

        static void LegacyCells(Action<bool,string> Assert,Action<Action,string> Fails)
        {
            using(var sample=new Bitmap(62,47))using(var sizes=new Bitmap(120,90))
            {
                sample.SetPixel(2,3,Color.Yellow);
                for(int offset=0;offset<64;offset++)
                {
                    sizes.SetPixel(61,47,Color.FromArgb(255,offset,3,5));
                    var cells=LegacyCollision.LegacyCells(sample,sizes,60,45);
                    int dx=offset%8,dy=offset/8;
                    Assert(cells.Count==1&&cells[0].Bounds==new Rectangle(8+dx,16+dy,Math.Min(3,8-dx),Math.Min(5,8-dy)),"legacy size offset and clipping "+offset);
                    Assert(cells[0].Colour.ToArgb()==Color.Yellow.ToArgb()&&cells[0].X==1&&cells[0].Y==2,"legacy material and sampler coordinate "+offset);
                }
                foreach(Color size in new[]{Color.FromArgb(255,64,8,8),Color.FromArgb(255,0,0,8),Color.FromArgb(255,0,8,0)})
                {sizes.SetPixel(61,47,size);Assert(LegacyCollision.LegacyCells(sample,sizes,60,45).Count==0,"legacy invalid size stays empty");}
                sizes.SetPixel(61,47,Color.FromArgb(255,0,255,255));
                Assert(LegacyCollision.LegacyCells(sample,sizes,60,45)[0].Bounds==new Rectangle(8,16,8,8),"legacy oversized dimensions clip to the original cell");
                foreach(Color empty in new[]{Color.Transparent,Color.Magenta})
                {sample.SetPixel(2,3,empty);Assert(LegacyCollision.LegacyCells(sample,sizes,60,45).Count==0,"legacy transparent and keyed cells stay empty");}
                Fails(()=>LegacyCollision.LegacyCells(sample,sizes,61,45),"legacy preview refuses a partial size cell");
                Fails(()=>LegacyCollision.LegacyCells(sample,sizes,-1,45),"legacy preview refuses a negative size origin");
                using(var wrong=new Bitmap(60,45))Fails(()=>LegacyCollision.LegacyCells(wrong,sizes,0,0),"legacy sampler needs its neighbour border");
            }
        }

        static void PixelLayout(string dir,Action<bool,string> Assert,Action<Action,string> Fails)
        {
            string root=Path.Combine(dir,"pixel-layout"),recovered=Path.Combine(dir,"pixel-layout-recovered");
            var layout=new Layout{Version=2,Screens=2,Columns=2,Rows=1,CellWidth=480,CellHeight=360,Source="atlas"};
            layout.Resolutions.Add(0,60);layout.Save(root);
            Files.Text(Path.Combine(root,"worldsmith-extension.xml"),"<WorldsmithExtension collisionSource='strip'/>");
            var parsed=Layout.Read(root,169);
            Assert(parsed.Pixel&&parsed.Source=="atlas"&&parsed.Resolutions[0]==60,"v2 metadata owns source and preserves per-screen resolution");
            Files.Text(Path.Combine(root,"worldsmith-extension.xml"),"<Broken>");
            Assert(Layout.Read(root,169).Source=="atlas"&&Collision.SourceScreens(root)==2,"v2 source inference ignores obsolete authoring source settings");
            Files.Text(Path.Combine(root,"worldsmith-extension.xml"),"<WorldsmithExtension collisionSource='strip'/>");
            using(var image=new Bitmap(960,360))
            {
                using(var graphics=Graphics.FromImage(image))graphics.FillRectangle(Brushes.Black,16,24,8,8);
                image.SetPixel(497,23,Color.Cyan);image.SetPixel(498,23,Color.Magenta);
                Collision.ValidatePixel(image,parsed);
                image.SetPixel(17,25,Color.Cyan);
                Fails(()=>Collision.ValidatePixel(image,parsed),"coarse screen cannot hide fine detail");
                image.SetPixel(17,25,Color.Black);image.SetPixel(499,23,Color.FromArgb(127,0,0,0));
                Fails(()=>Collision.ValidatePixel(image,parsed),"pixel collision refuses partial alpha");image.SetPixel(499,23,Color.Transparent);
                Collision.Png(image,Path.Combine(root,"level+.png"));Collision.Xnb(image,Path.Combine(root,"level+.xnb"));Collision.Scaffold(Path.Combine(root,"level.xnb"));
                Collision.ValidatePixelXnb(Path.Combine(root,"level+.xnb"),parsed);
                ProjectFormat.Recover(root,recovered,text=>{});
                var back=Layout.Read(recovered,169);
                Assert(back.Pixel&&back.Columns==2&&back.Resolutions[0]==60,"recovery keeps pixel metadata and mixed resolution");
                Assert(Files.Hash(Path.Combine(root,"level+.png"))==Files.Hash(Path.Combine(recovered,"level+.png")),"recovery preserves authoritative level+ source bytes");
                Assert(!File.Exists(Path.Combine(recovered,"visual_level.png")),"recovery doesn't replace pixel metadata with a stock strip layout");
            }
            using(var bad=new Bitmap(480,360))Fails(()=>Collision.ValidatePixel(bad,parsed),"PNG dimensions must match explicit grid");
            Fails(()=>new Layout{Version=2,Screens=4096,Columns=64,Rows=64,CellWidth=480,CellHeight=360,Source="atlas"}.Validate(),"oversized collision atlas fails before pixel allocation");
            var links=new Layout{Version=2,Screens=1024,Columns=32,Rows=32,CellWidth=60,CellHeight=45,Source="atlas"};
            for(int i=1;i<=512;i++)links.Links.Add(Tuple.Create(i,"left",1024));
            links.Validate();links.Links.Add(Tuple.Create(513,"left",1024));
            Fails(()=>links.Validate(),"editor refuses more side links than the runtime can load");
            var document=XDocument.Load(Layout.Metadata(root));document.Root.Element("MapLayout").Add(new XElement("Resolution",new XAttribute("screen",1),new XAttribute("width",60),new XAttribute("height",45)));document.Save(Layout.Metadata(root));
            Fails(()=>Layout.Read(root,169),"duplicate resolution declarations are rejected");
            string legacy=Path.Combine(dir,"legacy-level-plus");
            using(var image=new Bitmap(180,135))
            {Collision.Png(image,Path.Combine(legacy,"level+.png"));Collision.Png(image,Path.Combine(legacy,"sizes.png"));}
            var legacyLayout=new Layout{Screens=169};
            Assert(Collision.LegacyLayout(legacy,legacyLayout)&&legacyLayout.Screens==9&&legacyLayout.Side==3,"legacy level+ owns screen capacity instead of stock scaffold");
            legacyLayout.Screens=4;legacyLayout.Save(legacy);Collision.LegacyLayout(legacy,legacyLayout);
            Assert(legacyLayout.Screens==4&&legacyLayout.Side==3,"legacy authored count stays explicit while compiled side follows level+");
            legacyLayout.Screens=10;Fails(()=>Collision.LegacyLayout(legacy,legacyLayout,false),"legacy metadata cannot exceed level+ capacity");
        }

        static void RightGrowing(string dir, Action<bool, string> Assert, Action<Action, string> Fails)
        {
            foreach (int n in new[] { 169, 170, 200, 256, 512, 4096 })
            {
                var layout = new Layout { Screens = n, Source = "atlas" };
                layout.Validate();
                using (var source = new Bitmap(Math.Max(13, (n + 12) / 13) * 60, 585))
                {
                    for (int i = 0; i < n; i++)
                        source.SetPixel((i / 13) * 60, (i % 13) * 45, Color.FromArgb(127, i % 256, i / 256, 253));
                    source.SetPixel(((n - 1) / 13) * 60 + 59, ((n - 1) % 13) * 45 + 44, Color.Magenta);
                    using (var compiled = Collision.Compile(source, layout))
                    using (var preview = Collision.SourceStrip(source, layout))
                    using (var runtimeStrip = Collision.Strip(compiled, layout))
                    {
                        Assert(compiled.Width == layout.Side * 60 && compiled.Height == layout.Side * 45, "runtime dimensions " + n);
                        for (int i = 0; i < n; i++)
                        {
                            int expected = Color.FromArgb(127, i % 256, i / 256, 253).ToArgb();
                            Assert(preview.GetPixel(0, (n - i - 1) * 45).ToArgb() == expected, "preview screen order and alpha " + i);
                            Assert(runtimeStrip.GetPixel(0, (n - i - 1) * 45).ToArgb() == expected, "runtime screen order and alpha " + i);
                        }
                        Assert(runtimeStrip.GetPixel(59, 44).ToArgb() == Color.Magenta.ToArgb(), "last collision pixel survives repacking " + n);
                    }
                    if (n == 200)
                    {
                        string root = Path.Combine(dir, "right-growing"), recovered = Path.Combine(dir, "right-growing-recovered");
                        layout.Save(root);
                        Collision.Png(source, Path.Combine(root, "level.png"));
                        string before = Files.Hash(Path.Combine(root, "level.png"));
                        ProjectFormat.Recover(root, recovered, text => {});
                        Assert(Layout.Read(recovered, 1).Source == "atlas", "recovery retains ordinary atlas source");
                        Assert(Files.Hash(Path.Combine(recovered, "level.png")) == before, "recovery preserves editable atlas bytes");
                        Assert(Layout.Read(recovered, 1).Side == 15, "metadata retains compiled square dimensions");
                        using (var preview = Collision.Read(Path.Combine(recovered, "visual_level.png")))
                            Assert(preview.Height == 200 * 45 && preview.GetPixel(59, 44).ToArgb() == Color.Magenta.ToArgb(), "recovery reconstructs preview order");
                    }
                }
            }
            var large = new Layout { Screens = 200, Source = "atlas" };
            using (var tooSmall = new Bitmap(780, 585))
                Fails(() => { using (Collision.Compile(tooSmall, large)) {} }, "atlas must contain every authored screen");
            using (var wrongRows = new Bitmap(960, 629))
                Fails(() => { using (Collision.SourceStrip(wrongRows, large)) {} }, "partial screen rows are rejected");
            using (var tooSmall = new Bitmap(780, 540))
                Fails(() => { using (Collision.SourceStrip(tooSmall, large)) {} }, "atlas capacity must cover authored screens");
            foreach (var shape in new[] { new Size(3, 3), new Size(7, 5), new Size(12, 13) })
            {
                var compact = new Layout { Screens = shape.Width * shape.Height, Source = "atlas" };
                compact.Validate();
                using (var source = new Bitmap(shape.Width * 60, shape.Height * 45))
                {
                    for (int i = 0; i < compact.Screens; i++) source.SetPixel(i / shape.Height * 60, i % shape.Height * 45, Color.FromArgb(255, i, 71, 12));
                    using (var compiled = Collision.Compile(source, compact))
                        for (int i = 0; i < compact.Screens; i++)
                            Assert(compiled.GetPixel(i / compact.Side * 60, i % compact.Side * 45).ToArgb() == Color.FromArgb(255, i, 71, 12).ToArgb(),
                                "compact atlas preserves native screen order: " + shape + ", screen " + i);
                }
            }
            using (var partialColumn = new Bitmap(961, 585))
                Fails(() => { using (Collision.Compile(partialColumn, large)) {} }, "partial screen columns are rejected");
            var legacy = new Layout { Screens = 512, Source = "atlas" };
            legacy.Validate();
            using (var square = new Bitmap(1380, 1035))
            {
                square.SetPixel(1379, 1034, Color.Blue);
                using (var compiled = Collision.Compile(square, legacy))
                    Assert(compiled.GetPixel(1379, 1034).ToArgb() == Color.Blue.ToArgb(), "legacy square atlas retains unused cells");
            }
        }
    }
}
