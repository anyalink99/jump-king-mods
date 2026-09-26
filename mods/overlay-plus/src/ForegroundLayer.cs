using System;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace OverlayPlus
{
    // Keep native UI separate from the scene so the full-resolution HUD can sit
    // between them. Never rebind the scene's DiscardContents target in this frame.
    internal sealed class ForegroundLayer:IDisposable
    {
        private readonly RenderTarget2D target;
        private bool captured;
        internal ForegroundLayer(GraphicsDevice device){target=new RenderTarget2D(device,480,360);}
        internal void BeginFrame(){captured=false;}
        internal void Begin()
        {
            if(captured)return;
            var device=target.GraphicsDevice;var bindings=device.GetRenderTargets();
            var source=bindings.Length==1?bindings[0].RenderTarget as Texture2D:null;
            if(source==null||source.Width!=480||source.Height!=360)
                throw new InvalidOperationException("Overlay+: unexpected native UI render target");
            Game1.instance.EndBatch();
            try{device.SetRenderTarget(target);device.Clear(Color.Transparent);captured=true;}
            finally{Game1.instance.StartBatch();}
            // Game1.Draw ends this batch and unbinds the UI target normally. Its
            // original scene target is still intact for native/Smooth Camera presentation.
        }
        internal void Draw(SpriteBatch batch,Rectangle destination,float canvasScale)
        {
            if(!captured)return;
            batch.Draw(target,new Vector2(destination.X,destination.Y)/canvasScale,null,Color.White,0,Vector2.Zero,
                new Vector2(destination.Width/480f,destination.Height/360f)/canvasScale,SpriteEffects.None,0);
        }
        public void Dispose(){captured=false;target.Dispose();}
    }
}
