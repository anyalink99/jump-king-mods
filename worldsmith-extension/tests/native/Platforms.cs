using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class Platforms
{
    private const BindingFlags All=BindingFlags.Static|BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    internal static void Run(Assembly engine,string fixtures)
    {
        string root=Path.Combine(fixtures,"platform-editor");Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root,"level_settings.xml"),"<LevelSettings/>");
        File.WriteAllText(Path.Combine(root,"mapping_settings.xml"),"<MappingSettings version='1'><MapLayout version='2' source='atlas' screens='1' columns='1' rows='1' cellWidth='60' cellHeight='45'/></MappingSettings>");
        using(var image=new System.Drawing.Bitmap(60,45))
        {using(var g=System.Drawing.Graphics.FromImage(image)){g.Clear(System.Drawing.Color.Transparent);g.FillRectangle(System.Drawing.Brushes.Black,0,41,60,4);}image.Save(Path.Combine(root,"level+.png"));}
        var type=engine.GetType("WorldsmithExtension.PlatformsWindow");var window=(Window)Activator.CreateInstance(type,All,null,new object[]{root},null);
        try
        {
            var status=(TextBlock)type.GetField("status",All).GetValue(window);if(!status.Text.StartsWith("Valid:"))throw new Exception("Platform editor initialization failed: "+status.Text);
            var canvas=(Canvas)type.GetField("canvas",All).GetValue(window);if(canvas.Children.Count<3)throw new Exception("Platform preview omitted collision, route or platform");
            var deck=canvas.Children.OfType<System.Windows.Shapes.Rectangle>().FirstOrDefault();var deckBrush=deck==null?null:deck.Fill as ImageBrush;
            var deckImage=deckBrush==null?null:deckBrush.ImageSource as BitmapSource;
            if(deckImage==null||deckImage.PixelWidth!=16||deckImage.PixelHeight!=8)throw new Exception("The default platform preview didn't load its packaged PNG");
            var content=(FrameworkElement)window.Content;content.Measure(new Size(1176,728));content.Arrange(new Rect(0,0,1176,728));content.UpdateLayout();
            var frame=new RenderTargetBitmap(1176,728,96,96,PixelFormats.Pbgra32);frame.Render(content);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(frame));using(var file=File.Create(Path.Combine(root,"editor.png")))png.Save(file);
            var list=(ListBox)type.GetField("objects",All).GetValue(window);list.SelectedIndex=0;
            var fields=(System.Collections.IDictionary)type.GetField("fields",All).GetValue(window);((TextBox)fields["speed"]).Text="80";
            type.GetMethod("Refresh",All).Invoke(window,null);if(!status.Text.StartsWith("Valid:"))throw new Exception("Platform form did not validate");
            var xml=(TextBox)type.GetField("xml",All).GetValue(window);if(!xml.Text.Contains("speed=\"80\""))throw new Exception("Form edits did not reach the source XML");
            var tabs=(TabControl)type.GetField("editorTabs",All).GetValue(window);tabs.SelectedIndex=1;xml.Text=xml.Text.Replace("speed=\"80\"","speed=\"25\"");type.GetMethod("Refresh",All).Invoke(window,null);
            if(!xml.Text.Contains("speed=\"25\""))throw new Exception("Form state overwrote a direct XML edit");
            tabs.SelectedIndex=0;list.SelectedIndex=0;fields=(System.Collections.IDictionary)type.GetField("fields",All).GetValue(window);
            ((TextBox)fields["onBlocked"]).Text="reverse";((TextBox)fields["blockedWait"]).Text="0.4";type.GetMethod("Refresh",All).Invoke(window,null);
            list.SelectedItem=list.Items.Cast<ListBoxItem>().First(i=>i.Content.ToString().StartsWith("Profile "));fields=(System.Collections.IDictionary)type.GetField("fields",All).GetValue(window);
            ((TextBox)fields["momentum"]).Text="1";((TextBox)fields["momentumAxes"]).Text="vertical";((TextBox)fields["label"]).Text="MOMENTUM x1";type.GetMethod("Refresh",All).Invoke(window,null);
            if(!status.Text.StartsWith("Valid:")||!xml.Text.Contains("onBlocked=\"reverse\"")||!xml.Text.Contains("momentumAxes=\"vertical\"")||!canvas.Children.OfType<TextBlock>().Any(t=>t.Text=="MOMENTUM x1"))throw new Exception("Momentum, obstruction or caption forms did not survive validation and preview");
            Console.WriteLine("[OK] Moving-platform editor: form/XML round trip, collision overlay, route preview and WPF render.");
        }
        finally{window.Close();}
    }
}
