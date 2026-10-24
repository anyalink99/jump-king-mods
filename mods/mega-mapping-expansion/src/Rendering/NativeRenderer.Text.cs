using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MegaMappingExpansion.Api;
using JumpKing;

namespace MegaMappingExpansion.Construction
{
    internal sealed partial class NativeRenderer
    {
        private sealed class TextLayout
        {
            internal string Value;
            internal double Width,Spacing;
            internal string[] Lines;
            internal float[] Lengths;
            internal int[] Starts;
        }
        private readonly Dictionary<string,TextLayout> textLayouts=new Dictionary<string,TextLayout>(StringComparer.Ordinal);
        private sealed class NativeText
        {
            internal RenderTarget2D Target;
            internal TextLayout Layout;
            internal string Align;
            internal int Visible=-1;
        }
        private readonly Dictionary<string,NativeText> nativeText=new Dictionary<string,NativeText>(StringComparer.Ordinal);
        private void PrepareNativeText(string id)
        {
            if(nativeText.Count>=64)throw new System.IO.InvalidDataException("Scene supports at most 64 native text canvases; use PNG font atlases for larger scenes");
            nativeText.Add(id,new NativeText{Target=resources.Own(new RenderTarget2D(device,480,360,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents))});
        }
        private void RenderNativeText(NativeText prepared,TextLayout layout,string align,int visible,SpriteFont font)
        {
            if(prepared.Layout==layout&&prepared.Align==align&&prepared.Visible==visible)return;
            FlushQuads();var targets=device.GetRenderTargets();var viewport=device.Viewport;bool saved=false;
            try
            {
                saved=SaveBorrowedFrame(targets);device.SetRenderTarget(prepared.Target);device.Clear(Color.Transparent);double height=layout.Lines.Length*font.LineSpacing;
                Game1.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);
                try{for(int i=0;i<layout.Lines.Length;i++)
                {
                    double left=align=="center"?-layout.Lengths[i]/2:align=="right"?-layout.Lengths[i]:0;
                    string shown=layout.Lines[i].Substring(0,Math.Max(0,Math.Min(layout.Lines[i].Length,visible-layout.Starts[i])));
                    Game1.spriteBatch.DrawString(font,shown,new Vector2((float)Math.Round(240+left),(float)Math.Round(180+i*font.LineSpacing-height/2)),Color.White);
                }}finally{Game1.spriteBatch.End();}
                prepared.Layout=layout;prepared.Align=align;prepared.Visible=visible;
            }
            finally{device.SetRenderTargets(targets);device.Viewport=viewport;RestoreBorrowedFrame(saved);}
        }
        private TextLayout LayoutText(string id,Func<string,float> measure)
        {
            string text=Text(id,"value");double width=Number(id,"width"),spacing=Number(id,"spacing");TextLayout layout;
            if(textLayouts.TryGetValue(id,out layout)&&layout.Value==text&&layout.Width==width&&layout.Spacing==spacing)return layout;
            var lines=new List<string>();var starts=new List<int>{0};var line=new StringBuilder();int offset=0;
            foreach(char c in text.Replace("\r", ""))
            {
                if(c=='\n'){lines.Add(line.ToString());line.Length=0;starts.Add(++offset);continue;}
                if(line.Length!=0&&measure(line.ToString()+c)>width){lines.Add(line.ToString());line.Length=0;starts.Add(offset);}
                line.Append(c);offset++;
            }
            lines.Add(line.ToString());
            layout=new TextLayout{Value=text,Width=width,Spacing=spacing,Lines=lines.ToArray(),Starts=starts.ToArray(),Lengths=Array.ConvertAll(lines.ToArray(),s=>measure(s))};textLayouts[id]=layout;return layout;
        }
        private void DrawText(CompiledComponent component,ComponentPose pose,string material)
        {
            string id=component.Id,fontId=Text(id,"font"),align=Text(id,"align");double spacing=Number(id,"spacing");
            int visible=(int)Number(id,"visibleCharacters");
            if(NativeNarrativeText.IsFont(fontId))
            {
                var font=NativeNarrativeText.Font(fontId);var layout=LayoutText(id,s=>font.MeasureString(s).X);var prepared=nativeText[id];RenderNativeText(prepared,layout,align,visible,font);
                DrawQuad(prepared.Target,new Rectangle(0,0,480,360),pose,SceneValue.Vector(-240,-180),SceneValue.Vector(480,360),Tint(material),material,null);
                return;
            }
            var atlas=assets.Bitmaps[fontId];string characters=Text(id,"characters");var png=LayoutText(id,s=>(float)Math.Max(0,s.Length*(atlas.FrameWidth+spacing)-spacing));double total=png.Lines.Length*atlas.FrameHeight;
            for(int line=0;line<png.Lines.Length;line++)
            {
                double left=align=="center"?-png.Lengths[line]/2:align=="right"?-png.Lengths[line]:0;
                for(int cell=0;cell<png.Lines[line].Length;cell++)
                {
                    if(png.Starts[line]+cell>=visible)break;
                    int frame=characters.IndexOf(png.Lines[line][cell]);if(frame<0)continue;
                    DrawQuad(MaterialTexture(material,fontId),atlas.Frame(frame),pose,SceneValue.Vector(left+cell*(atlas.FrameWidth+spacing),line*atlas.FrameHeight-total/2),SceneValue.Vector(atlas.FrameWidth,atlas.FrameHeight),Tint(material),material,null);
                }
            }
        }
    }
}
