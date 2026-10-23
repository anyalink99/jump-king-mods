using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion.Construction
{
    internal sealed partial class NativeRenderer
    {
        private sealed class CaptureFrame
        {
            internal RenderTarget2D Target;
            internal RenderTarget2D Alpha;
            internal int Screen;
            internal long Frame=-1;
        }
        private readonly Dictionary<string,CaptureFrame> captures=new Dictionary<string,CaptureFrame>(StringComparer.Ordinal);
        private long renderFrame;
        private BlendState alphaOnly;
        private RenderTarget2D borrowedFrame;
        private bool SaveBorrowedFrame(RenderTargetBinding[] targets)
        {
            if(targets.Length!=1)return false;
            var target=targets[0].RenderTarget as RenderTarget2D;
            if(target==null||target.RenderTargetUsage==RenderTargetUsage.PreserveContents)return false;
            if(target.Width!=480||target.Height!=360)throw new InvalidOperationException("Borrowed scene targets must have logical screen dimensions");
            device.SetRenderTarget(borrowedFrame);device.Clear(Color.Transparent);
            Game1.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.Opaque,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);
            try{Game1.spriteBatch.Draw(target,new Rectangle(0,0,480,360),Color.White);}finally{Game1.spriteBatch.End();}
            return true;
        }
        private void RestoreBorrowedFrame(bool saved)
        {
            if(!saved)return;
            Game1.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.Opaque,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);
            try{Game1.spriteBatch.Draw(borrowedFrame,new Rectangle(0,0,480,360),Color.White);}finally{Game1.spriteBatch.End();}
        }
        internal Action<string,int> DrawNativeSelection;
        private static int CaptureStageIndex(string stage){return stage=="background"?0:stage=="world"?2:4;}
        private static int LayerIndex(string layer){return layer=="background"?0:layer=="world"?1:layer=="surface"?3:layer=="foreground"?4:layer=="composite"?5:6;}
        private void PrepareCaptures()
        {
            borrowedFrame=resources.Own(new RenderTarget2D(device,480,360,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents));
            alphaOnly=resources.Own(new BlendState{ColorSourceBlend=Microsoft.Xna.Framework.Graphics.Blend.Zero,ColorDestinationBlend=Microsoft.Xna.Framework.Graphics.Blend.SourceAlpha,AlphaSourceBlend=Microsoft.Xna.Framework.Graphics.Blend.Zero,AlphaDestinationBlend=Microsoft.Xna.Framework.Graphics.Blend.SourceAlpha});
            foreach(var component in scene.Components.Values.Where(c=>c.Descriptor.Kind=="capture"))
            {
                if(captures.Count>=32)throw new InvalidDataException("Scene supports at most 32 capture targets");
                captures.Add(component.Id,new CaptureFrame{Target=resources.Own(new RenderTarget2D(device,482,362,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents)),Alpha=resources.Own(new RenderTarget2D(device,482,362,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents))});
            }
            foreach(var component in scene.Components.Values.Where(c=>c.Descriptor.Fields.Any(f=>f.Name=="material")&&c.Descriptor.Fields.Any(f=>f.Name=="layer")))
            {
                string material=Text(component.Id,"material");if(material=="")continue;string capture=Text(material,"capture");if(capture=="")continue;
                if(LayerIndex(Text(component.Id,"layer"))<=CaptureStageIndex(Text(capture,"stage")))throw new InvalidDataException(component.Id+": capture stage must precede the component's render layer");
            }
            foreach(var capture in captures)
                foreach(var component in scene.Components.Values.Where(c=>c.Descriptor.Fields.Any(f=>f.Name=="layer")))
                    if(Selected(component,Text(capture.Key,"selection"))&&LayerIndex(Text(component.Id,"layer"))>CaptureStageIndex(Text(capture.Key,"stage")))throw new InvalidDataException(capture.Key+": selection includes a component from a later render stage: "+component.Id);
        }
        private bool Selected(CompiledComponent component,string selection)
        {
            foreach(string token in selection.Split(';'))if(token==component.Id||(token.StartsWith("tag:",StringComparison.Ordinal)&&component.Tags.Contains(token.Substring(4))))return true;
            return false;
        }
        internal void CaptureStage(string stage,RenderTarget2D frame,int screen)
        {
            if(disposed||captures.Count==0)return;FlushQuads();var targets=device.GetRenderTargets();var viewport=device.Viewport;bool saved=false;
            Game1.instance.EndBatch();
            try
            {
                saved=SaveBorrowedFrame(targets);
                foreach(var entry in captures)
                {
                    string id=entry.Key;if(Text(id,"stage")!=stage||Number(id,"enabled")==0)continue;
                    var capture=entry.Value;device.SetRenderTarget(capture.Target);device.Clear(Color.Transparent);device.Viewport=new Viewport(1,1,480,360);string selection=Text(id,"selection");
                    if(selection=="")
                    {
                        Game1.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);
                        try{Game1.spriteBatch.Draw(saved?borrowedFrame:frame,new Rectangle(0,0,480,360),Color.White);}finally{Game1.spriteBatch.End();}
                    }
                    else
                    {
                        foreach(string layer in new[]{"background","world","surface","foreground","composite"})
                            foreach(var component in Commands(layer,screen))if(Selected(component,selection))DrawComponent(component,screen);
                        FlushQuads();
                        if(DrawNativeSelection!=null)foreach(string token in selection.Split(';'))if(token.StartsWith("native.",StringComparison.Ordinal)||token.StartsWith("actor:",StringComparison.Ordinal))DrawNativeSelection(token,screen);
                    }
                    device.SetRenderTarget(capture.Alpha);device.Clear(Color.White);
                    Game1.spriteBatch.Begin(SpriteSortMode.Deferred,alphaOnly,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);
                    try{Game1.spriteBatch.Draw(capture.Target,new Rectangle(0,0,482,362),Color.White);}finally{Game1.spriteBatch.End();}
                    capture.Screen=screen;capture.Frame=renderFrame;
                }
            }
            finally{device.SetRenderTargets(targets);device.Viewport=viewport;RestoreBorrowedFrame(saved);Game1.instance.StartBatch();}
        }
        private Texture2D MaterialTexture(string material,string asset)
        {
            if(material!="")
            {
                string id=Text(material,"capture");CaptureFrame capture;
                if(id!="")return captures.TryGetValue(id,out capture)&&capture.Frame==renderFrame?(Text(material,"channel")=="alpha"?capture.Alpha:capture.Target):null;
                if(asset!=""&&Text(material,"channel")=="alpha")return masks[asset];
            }
            return Texture(asset);
        }
        private Vector2 MaterialUV(string material,Vector2 uv)
        {
            if(material!=""&&Text(material,"capture")!="")return new Vector2((1+uv.X*480)/482f,(1+uv.Y*360)/362f);
            return uv;
        }
        private bool CaptureReady(string material,int screen)
        {
            if(material=="")return true;string id=Text(material,"capture");if(id=="")return true;
            CaptureFrame capture;return captures.TryGetValue(id,out capture)&&capture.Frame==renderFrame&&capture.Screen==screen;
        }
        private void DrawComponent(CompiledComponent component,int screen)
        {
            Action<int> command;if(drawing.TryGetValue(component.Id,out command))command(screen);
        }
    }
}
