using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MegaMappingExpansion.Api;
using JumpKing;

namespace MegaMappingExpansion.Construction
{
    internal sealed partial class NativeRenderer
    {
        private readonly Dictionary<int,string> views=new Dictionary<int,string>();
        private RenderTarget2D gradeTarget;
        private void PrepareViews()
        {
            foreach(var component in scene.Components.Values)
            {
                if(component.Descriptor.Kind!="view")continue;int screen=(int)Number(component.Id,"screen");
                if(views.ContainsKey(screen))throw new InvalidDataException("Only one view is allowed for each screen, including the global screen 0");views.Add(screen,component.Id);
            }
            if(views.Count>0)gradeTarget=resources.Own(new RenderTarget2D(device,480,360,false,SurfaceFormat.Color,DepthFormat.None));
        }
        private string View(int screen){string id;return views.TryGetValue(screen,out id)||views.TryGetValue(0,out id)?id:null;}
        internal bool AnimateWhilePaused(int screen){string id=View(screen);return id!=null&&scene.Properties.Read(id,"animateWhilePaused").X!=0;}
        internal string Timer(int screen){string id=View(screen);return id==null?"inherit":Text(id,"timer");}
        internal string Intro(int screen){string id=View(screen);return id==null?"":Text(id,"intro");}
        internal JKRuntime.FrameStyle Style(int screen)
        {
            string id=View(screen);string mirror=id==null?"none":Text(id,"mirror");
            var effects=SpriteEffects.None;if(mirror=="horizontal"||mirror=="both")effects|=SpriteEffects.FlipHorizontally;if(mirror=="vertical"||mirror=="both")effects|=SpriteEffects.FlipVertically;
            return new JKRuntime.FrameStyle(Color.White,effects);
        }
        internal void Compose(RenderTarget2D frame,int screen)
        {
            FlushQuads();
            string id=View(screen);if(id==null)return;
            var ambient=Value(id,"ambient");var tint=Value(id,"tint");double amount=Number(id,"tintAmount"),exposure=Number(id,"exposure");
            double r=ambient.X*(1+(tint.X-1)*amount)*exposure,g=ambient.Y*(1+(tint.Y-1)*amount)*exposure,b=ambient.Z*(1+(tint.Z-1)*amount)*exposure;
            if(r==1&&g==1&&b==1)return;
            if(frame.Width!=480||frame.Height!=360)throw new InvalidOperationException("Construction composition requires a logical 480x360 screen target");
            var targets=device.GetRenderTargets();var viewport=device.Viewport;Game1.instance.EndBatch();
            try
            {
                device.SetRenderTarget(gradeTarget);device.Clear(Color.Transparent);
                Game1.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.Opaque,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);
                try{Game1.spriteBatch.Draw(frame,new Rectangle(0,0,480,360),Color.White);}finally{Game1.spriteBatch.End();}
                device.SetRenderTarget(frame);device.Clear(Color.Transparent);
                int count=Math.Max(1,(int)Math.Ceiling(Math.Max(r,Math.Max(g,b))));
                for(int pass=0;pass<count;pass++)
                {
                    Game1.spriteBatch.Begin(SpriteSortMode.Deferred,pass==0?BlendState.Opaque:emissionBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);
                    try{Game1.spriteBatch.Draw(gradeTarget,new Rectangle(0,0,480,360),new Color((float)Math.Max(0,Math.Min(1,r-pass)),(float)Math.Max(0,Math.Min(1,g-pass)),(float)Math.Max(0,Math.Min(1,b-pass)),1));}finally{Game1.spriteBatch.End();}
                }
            }
            finally{device.SetRenderTargets(targets);device.Viewport=viewport;Game1.instance.StartBatch();}
        }
    }
}
