using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Linq;
using MegaMappingExpansion;

namespace WorldsmithExtension
{
    internal sealed class PlatformsWindow:Window
    {
        private readonly string root;
        private readonly int screens;
        private readonly TextBox xml;
        private readonly TabControl editorTabs;
        private XElement editing;
        private readonly TextBlock status;
        private readonly ListBox objects;
        private readonly StackPanel properties;
        private readonly Canvas canvas;
        private readonly ComboBox screen;
        private readonly Slider time;
        private readonly DispatcherTimer timer=new DispatcherTimer();
        private readonly Dictionary<string,TextBox> fields=new Dictionary<string,TextBox>();
        private readonly Dictionary<string,BitmapSource> pictures=new Dictionary<string,BitmapSource>();
        private readonly Dictionary<int,BitmapSource> collisionPictures=new Dictionary<int,BitmapSource>();
        private MovingPlatformPlan preview;
        private XDocument document;
        private bool changing;
        internal PlatformsWindow(string projectRoot)
        {
            root=projectRoot;if(string.IsNullOrEmpty(root)||!File.Exists(System.IO.Path.Combine(root,"level_settings.xml")))throw new InvalidOperationException("Open an editable level project first");
            int inferred=Collision.SourceScreens(root);screens=Layout.Read(root,inferred==0?169:inferred).Screens;
            Title="Moving platforms";Width=1200;Height=800;MinWidth=1020;MinHeight=680;EditorUI.Theme(this);
            var body=new DockPanel{Margin=new Thickness(12),Background=new SolidColorBrush(Color.FromRgb(29,33,41))};Content=body;
            System.Windows.Documents.TextElement.SetFontFamily(body,new FontFamily("Segoe UI"));System.Windows.Documents.TextElement.SetFontSize(body,13);System.Windows.Documents.TextElement.SetForeground(body,Brushes.Gainsboro);
            var toolbar=new WrapPanel();DockPanel.SetDock(toolbar,Dock.Top);body.Children.Add(toolbar);
            toolbar.Children.Add(EditorUI.Button("Validate / preview",()=>Guard(Refresh)));
            toolbar.Children.Add(EditorUI.Button("Save",()=>Guard(()=>{Refresh();PlatformAuthoring.Save(root,xml.Text,screens);status.Text="Saved moving-platforms.xml. The previous file is kept as .worldsmith-backup. Restart the test map after changes.";})));
            toolbar.Children.Add(EditorUI.Button("Add lift",()=>Guard(AddLift)));
            toolbar.Children.Add(EditorUI.Button("Add color",()=>Guard(()=>Add("Paint",new XAttribute("color","#D060A0"),new XAttribute("profile",FirstProfile())))));
            toolbar.Children.Add(EditorUI.Button("Add part",()=>Guard(()=>{ApplyFields();var e=Selected();if(e==null||e.Name!="Platform")throw new InvalidOperationException("Select a Platform first");e.Add(new XElement("Part",new XAttribute("rect","168,280,16,8")));Sync(e);})));
            toolbar.Children.Add(EditorUI.Button("Add waypoint",()=>Guard(()=>{ApplyFields();var e=Selected();if(e==null||e.Name!="Motion")throw new InvalidOperationException("Select a Motion first");if(e.Attribute("axis")!=null){var motion=MovingPlatformPlan.Parse(document.ToString(),screens).Motions[(string)e.Attribute("id")];e.Attribute("axis").Remove();if(e.Attribute("distance")!=null)e.Attribute("distance").Remove();foreach(var p in motion.Points)e.Add(new XElement("Point",new XAttribute("x",p.Position.X),new XAttribute("y",p.Position.Y)));}e.Add(new XElement("Point",new XAttribute("x",96),new XAttribute("y",-96)));Sync(e);})));
            toolbar.Children.Add(EditorUI.Button("Remove",()=>Guard(()=>{var e=Selected();if(e==null)return;e.Remove();Sync(null);})));
            status=new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Gainsboro,FontFamily=new FontFamily("Segoe UI"),FontSize=13,Margin=new Thickness(0,8,0,0)};DockPanel.SetDock(status,Dock.Bottom);body.Children.Add(status);
            var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(210)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(280)});grid.ColumnDefinitions.Add(new ColumnDefinition());body.Children.Add(grid);
            objects=new ListBox{Margin=new Thickness(0,8,8,0),FontFamily=new FontFamily("Segoe UI"),FontSize=13,Background=new SolidColorBrush(Color.FromRgb(23,27,34)),Foreground=Brushes.Gainsboro,Style=new Style(typeof(ListBox))};objects.SelectionChanged+=(s,e)=>{if(!changing)Guard(Select);};grid.Children.Add(objects);
            var tabs=editorTabs=new TabControl{Margin=new Thickness(0,8,8,0),Background=new SolidColorBrush(Color.FromRgb(29,33,41)),Foreground=Brushes.Gainsboro,TabStripPlacement=Dock.Top,Style=new Style(typeof(TabControl))};Grid.SetColumn(tabs,1);grid.Children.Add(tabs);
            properties=new StackPanel{Margin=new Thickness(8)};tabs.Items.Add(new TabItem{Header="Properties",FontFamily=new FontFamily("Segoe UI"),FontSize=13,Style=new Style(typeof(TabItem)),Content=new ScrollViewer{Content=properties,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}});
            xml=new TextBox{AcceptsReturn=true,AcceptsTab=true,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,FontFamily=new FontFamily("Consolas"),FontSize=13,Background=new SolidColorBrush(Color.FromRgb(23,27,34)),Foreground=Brushes.Gainsboro};tabs.Items.Add(new TabItem{Header="XML",FontFamily=new FontFamily("Segoe UI"),FontSize=13,Style=new Style(typeof(TabItem)),Content=xml});
            tabs.SelectionChanged+=(s,e)=>{if(e.Source==tabs&&tabs.SelectedIndex==1)Guard(ApplyFields);};
            var right=new DockPanel{Margin=new Thickness(0,8,0,0)};Grid.SetColumn(right,2);grid.Children.Add(right);
            var playback=new WrapPanel();DockPanel.SetDock(playback,Dock.Top);right.Children.Add(playback);
            playback.Children.Add(new TextBlock{Text="Screen ",Foreground=Brushes.Gainsboro,FontFamily=new FontFamily("Segoe UI"),FontSize=13,VerticalAlignment=VerticalAlignment.Center});screen=new ComboBox{Width=70,ItemsSource=Enumerable.Range(1,screens).ToArray(),SelectedIndex=0};screen.SelectionChanged+=(s,e)=>DrawPreview();playback.Children.Add(screen);
            playback.Children.Add(EditorUI.Button("Play / pause",()=>{if(preview==null)Guard(Refresh);timer.IsEnabled=!timer.IsEnabled;}));
            playback.Children.Add(EditorUI.Button("Reset time",()=>{timer.Stop();time.Value=0;}));
            time=new Slider{Minimum=0,Maximum=120,Width=160,Height=24,Margin=new Thickness(6),Style=new Style(typeof(Slider)),SmallChange=1.0/60,ToolTip="Preview time in seconds; uses the game's route evaluator"};time.ValueChanged+=(s,e)=>DrawPreview();playback.Children.Add(time);
            canvas=new Canvas{Width=480,Height=360,Background=new SolidColorBrush(Color.FromRgb(26,31,40)),ClipToBounds=true};RenderOptions.SetBitmapScalingMode(canvas,BitmapScalingMode.NearestNeighbor);right.Children.Add(new Viewbox{Child=canvas,Stretch=Stretch.Uniform});
            timer.Interval=TimeSpan.FromSeconds(1.0/30);timer.Tick+=(s,e)=>{time.Value=time.Value>=time.Maximum?0:time.Value+1.0/30;};Closed+=(s,e)=>timer.Stop();
            string file=System.IO.Path.Combine(root,MovingPlatformPlan.PathName);xml.Text=File.Exists(file)?File.ReadAllText(file):PlatformAuthoring.Example;
            Guard(Refresh);
        }
        private void Guard(Action action){try{action();}catch(Exception e){timer.Stop();status.Text=e.GetBaseException().Message;}}
        private XElement Selected(){return (objects.SelectedItem as ListBoxItem)==null?null:((ListBoxItem)objects.SelectedItem).Tag as XElement;}
        private string FirstProfile(){return document.Root.Elements("Profile").Select(e=>(string)e.Attribute("id")).FirstOrDefault()??"lift";}
        private void ApplyFields()
        {
            if(document==null)return;var e=editing;if(e==null)return;
            foreach(var field in fields){if(field.Value.Text=="")e.SetAttributeValue(field.Key,null);else e.SetAttributeValue(field.Key,field.Value.Text);}
            xml.Text=document.ToString();
        }
        private void Select()
        {
            fields.Clear();properties.Children.Clear();var e=Selected();editing=e;if(e==null)return;
            properties.Children.Add(new TextBlock{Text=e.Name.LocalName,Foreground=Brushes.Gainsboro,FontFamily=new FontFamily("Segoe UI"),FontSize=13,FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,8)});
            string names=e.Name=="Motion"?"id mode speed phase playing axis distance wait onBlocked blockedWait":e.Name=="Profile"?"id motion collision texture fit columns rows frames fps label momentum momentumAxes":e.Name=="Platform"?"id profile screen rect group label":e.Name=="Paint"?"color range profile":e.Name=="Point"?"x y wait":"rect";
            foreach(string name in names.Split(' '))
            {
                properties.Children.Add(new TextBlock{Text=Label(name),Foreground=Brushes.Gainsboro,FontFamily=new FontFamily("Segoe UI"),FontSize=13,Margin=new Thickness(0,6,0,2)});
                var field=new TextBox{Text=(string)e.Attribute(name)??"",ToolTip=Hint(name),FontFamily=new FontFamily("Segoe UI"),FontSize=13,Background=new SolidColorBrush(Color.FromRgb(23,27,34)),Foreground=Brushes.Gainsboro};properties.Children.Add(field);fields.Add(name,field);
                field.LostKeyboardFocus+=(s,a)=>Guard(()=>{ApplyFields();preview=null;});
                if(name=="texture")properties.Children.Add(EditorUI.Button("Choose image…",()=>Guard(()=>{
                    var dialog=new Microsoft.Win32.OpenFileDialog{Filter="Images|*.png;*.bmp;*.xnb",InitialDirectory=root};if(dialog.ShowDialog(this)!=true)return;
                    Files.Relative(root,dialog.FileName);field.Text=Files.Relative(root,dialog.FileName).Replace('\\','/');ApplyFields();
                })));
            }
            properties.Children.Add(EditorUI.Button("Apply and preview",()=>Guard(Refresh)));
            properties.Children.Add(new TextBlock{Text="Blank optional fields use defaults. A rect is x,y,width,height in game pixels. Points are offsets from the painted/XML start.",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Gainsboro,FontFamily=new FontFamily("Segoe UI"),FontSize=13,Margin=new Thickness(0,12,0,0)});
        }
        private static string Label(string name)
        {return name=="momentum"?"Jump momentum (0–4)":name=="onBlocked"?"When blocked":name=="blockedWait"?"Wait after obstruction (seconds)":name=="speed"?"Speed (pixels / second)":name=="wait"?"Wait (seconds)":name=="phase"?"Initial phase (seconds)":name=="distance"?"Travel distance (pixels)":name=="fps"?"Animation (frames / second)":name;}
        private static string Hint(string name)
        {return name=="momentum"?"0 adds no velocity; 1 inherits the deck's velocity once on jump; maximum 4":name=="momentumAxes"?"both, horizontal, vertical":name=="onBlocked"?"wait retries, reverse returns along the route, stop needs a start command":name=="label"?"Optional caption under the deck; a Platform overrides its Profile":name=="mode"?"pingPong, loop, once":name=="collision"?"solid, oneWay":name=="fit"?"tile, stretch, native":name=="axis"?"horizontal, vertical; remove to use Point children":name=="texture"?"Map-relative image path, blank for the packaged default PNG, none for invisible collision":name=="group"?"A shared group advances one motion for every member":name=="color"||name=="range"?"Exact opaque #RRGGBB; native markers and duplicate colors are rejected":name=="playing"?"true or false":"";}
        private void Refresh()
        {
            // XML edits take precedence when its tab is active
            if(editorTabs.SelectedIndex==0)ApplyFields();
            var candidate=PlatformAuthoring.Validate(root,xml.Text,screens);var parsed=XDocument.Parse(xml.Text);
            preview=candidate;document=parsed;pictures.Clear();collisionPictures.Clear();Sync(null,false);DrawPreview();status.Text="Valid: "+candidate.Platforms.Count+" platforms. Preview shows movement; use Test in-game to check player contacts. "+time.Value.ToString("0.00",CultureInfo.InvariantCulture)+" s";
        }
        private void Sync(XElement selected,bool write=true)
        {
            if(write){xml.Text=document.ToString();preview=null;}
            changing=true;objects.Items.Clear();
            foreach(var e in document.Root.Elements())
            {
                var item=new ListBoxItem{Content=e.Name+" "+((string)e.Attribute("id")??(string)e.Attribute("color")??""),Tag=e,FontFamily=new FontFamily("Segoe UI"),FontSize=13,Style=new Style(typeof(ListBoxItem))};objects.Items.Add(item);if(e==selected)objects.SelectedItem=item;
                foreach(var child in e.Elements()){var sub=new ListBoxItem{Content="  "+child.Name+" "+((string)child.Attribute("rect")??((string)child.Attribute("x")+","+(string)child.Attribute("y"))),Tag=child,FontFamily=new FontFamily("Segoe UI"),FontSize=13,Style=new Style(typeof(ListBoxItem))};objects.Items.Add(sub);if(child==selected)objects.SelectedItem=sub;}
            }
            if(objects.SelectedIndex<0&&objects.Items.Count>0)objects.SelectedIndex=0;
            changing=false;Select();
        }
        private void Add(string name,params XAttribute[] attributes){ApplyFields();var e=new XElement(name,attributes);document.Root.Add(e);Sync(e);}
        private void AddLift()
        {
            ApplyFields();string id="lift";int i=1;while(document.Root.Elements().Any(e=>(string)e.Attribute("id")==id))id="lift"+(++i);
            document.Root.Add(new XElement("Motion",new XAttribute("id",id),new XAttribute("axis","vertical"),new XAttribute("distance",-96),new XAttribute("speed",40),new XAttribute("wait",0.5)));
            document.Root.Add(new XElement("Profile",new XAttribute("id",id),new XAttribute("motion",id)));
            var platform=new XElement("Platform",new XAttribute("id",id+"-platform"),new XAttribute("profile",id),new XAttribute("screen",screen.SelectedItem??1),new XAttribute("rect","120,280,48,8"));document.Root.Add(platform);Sync(platform);
        }
        private void DrawPreview()
        {
            if(canvas==null||preview==null||screen.SelectedItem==null)return;canvas.Children.Clear();int index=(int)screen.SelectedItem;
            var background=CollisionPicture(index);if(background!=null)canvas.Children.Add(new Image{Source=background,Width=480,Height=360,Opacity=.4,IsHitTestVisible=false});
            foreach(var p in preview.Platforms)
            {
                var profile=preview.Profiles[p.Profile];var motion=new PlatformMotionPlayer(preview.Motions[profile.Motion]);motion.Advance(time.Value,false);var position=motion.Position;
                var first=p.Parts[0];var route=new Polyline{Stroke=Brushes.Goldenrod,StrokeThickness=1,StrokeDashArray=new DoubleCollection{3,2},IsHitTestVisible=false};
                foreach(var point in motion.Definition.Points)route.Points.Add(new Point(first.X+first.Width/2+point.Position.X,first.Y+(index-p.Screen)*360+point.Position.Y));if(motion.Definition.Mode=="loop")route.Points.Add(route.Points[0]);canvas.Children.Add(route);
                foreach(var part in p.Parts)
                {
                    double y=part.Y+(index-p.Screen)*360+position.Y;if(y+part.Height<0||y>360)continue;
                    var rectangle=new Rectangle{Width=part.Width,Height=part.Height,Fill=Brush(profile,motion.State.Time),Stroke=Brushes.LightCyan,StrokeThickness=profile.Collision=="oneWay"?2:1,ToolTip=p.Id+" / "+p.Profile};Canvas.SetLeft(rectangle,part.X+position.X);Canvas.SetTop(rectangle,y);canvas.Children.Add(rectangle);
                    string id=p.Id;rectangle.MouseLeftButtonDown+=(s,e)=>Guard(()=>{var target=document.Root.Elements("Platform").FirstOrDefault(n=>(string)n.Attribute("id")==id)??document.Root.Elements("Paint").FirstOrDefault(n=>(string)n.Attribute("profile")==p.Profile);if(target!=null)Sync(target,false);});
                }
                string label=p.Label!=""?p.Label:profile.Label;
                if(label!="")
                {
                    var caption=new TextBlock{Text=label,FontFamily=new FontFamily("Segoe UI"),FontSize=9,Foreground=Brushes.Gainsboro,Background=new SolidColorBrush(Color.FromArgb(190,15,20,30)),IsHitTestVisible=false};
                    Canvas.SetLeft(caption,first.X+position.X);Canvas.SetTop(caption,p.Parts.Max(b=>b.Y+b.Height)+(index-p.Screen)*360+position.Y+3);canvas.Children.Add(caption);
                }
            }
        }
        private BitmapSource CollisionPicture(int index)
        {
            BitmapSource result;if(collisionPictures.TryGetValue(index,out result))return result;
            var layout=Layout.Read(root,screens);string path=System.IO.Path.Combine(root,layout.Pixel?"level+.png":layout.Source=="atlas"?"level.png":"visual_level.png");if(!File.Exists(path))return null;
            var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.UriSource=new Uri(System.IO.Path.GetFullPath(path));image.EndInit();image.Freeze();
            int width=layout.Pixel?layout.CellWidth:60,height=layout.Pixel?layout.CellHeight:45,rows=layout.Source=="atlas"?image.PixelHeight/height:screens;
            int x=layout.Source=="atlas"?(index-1)/rows*width:0,y=layout.Source=="atlas"?(index-1)%rows*height:(screens-index)*height;
            result=new CroppedBitmap(image,new Int32Rect(x,y,width,height));result.Freeze();collisionPictures.Add(index,result);return result;
        }
        private Brush Brush(PlatformProfile profile,double seconds)
        {
            if(profile.Texture=="none")return Brushes.Transparent;if(profile.Texture.EndsWith(".xnb",StringComparison.OrdinalIgnoreCase))return Brushes.SteelBlue;
            bool fallback=profile.Texture=="";string path=fallback?System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(PlatformsWindow).Assembly.Location),"assets/platforms/default.png"):System.IO.Path.GetFullPath(System.IO.Path.Combine(root,profile.Texture));
            BitmapSource image;if(!pictures.TryGetValue(path,out image))
            {var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.UriSource=new Uri(path);bitmap.EndInit();bitmap.Freeze();pictures.Add(path,bitmap);image=bitmap;}
            int columns=fallback?1:profile.Columns,rows=fallback?1:profile.Rows,frame=fallback?0:(int)(seconds*profile.Fps)%profile.Frames,width=image.PixelWidth/columns,height=image.PixelHeight/rows;
            var crop=new CroppedBitmap(image,new Int32Rect(frame%columns*width,frame/columns*height,width,height));
            return profile.Fit=="tile"?new ImageBrush(crop){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,width,height),Stretch=Stretch.Fill}:new ImageBrush(crop){Stretch=profile.Fit=="native"?Stretch.None:Stretch.Fill,AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top};
        }
    }
}
