using System;
using System.Collections.Generic;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion.Construction
{
    internal sealed partial class NativeRenderer
    {
        internal void Prime()
        {
            // exercise the owned draw paths while loading, without clocks, actors or events
            var screens=new SortedSet<int>();
            foreach(var key in layers.Keys){int screen=int.Parse(key.Substring(key.LastIndexOf('/')+1));screens.Add(screen==0?1:screen);}
            if(screens.Count==0)return;
            var targets=device.GetRenderTargets();var viewport=device.Viewport;
            var blend=device.BlendState;var depth=device.DepthStencilState;var rasterizer=device.RasterizerState;
            var sampler0=device.SamplerStates[0];var sampler1=device.SamplerStates[1];bool saved=false;
            using(var target=new RenderTarget2D(device,480,360,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents))
            try
            {
                priming=true;
                saved=SaveBorrowedFrame(targets);
                foreach(int screen in screens)
                {
                    device.SetRenderTarget(target);device.Clear(Color.Transparent);Game1.instance.StartBatch();
                    try
                    {
                        foreach(string layer in new[]{"background","world","surface","foreground","composite","hud"})DrawLayer(layer,screen);
                        Compose(target,screen);
                    }
                    finally{Game1.instance.EndBatch();}
                }
                // finish the queued GPU work before the player receives control
                target.GetData(0,new Rectangle(0,0,1,1),new Color[1],0,1);
            }
            finally
            {
                priming=false;
                device.SetRenderTargets(targets);device.Viewport=viewport;RestoreBorrowedFrame(saved);
                device.BlendState=blend;device.DepthStencilState=depth;device.RasterizerState=rasterizer;
                device.SamplerStates[0]=sampler0;device.SamplerStates[1]=sampler1;
                surfaceFrame=-1;QuadDrawCalls=0;MeshVertexUpdates=0;
            }
        }
    }
}
