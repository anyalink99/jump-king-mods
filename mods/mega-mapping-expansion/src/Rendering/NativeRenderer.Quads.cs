using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal struct DualVertex:IVertexType
    {
        internal Vector3 Position;
        internal Color Color;
        internal Vector2 UV,MaskUV;
        private static VertexDeclaration declaration;
        private static int readers;
        internal static IDisposable Acquire()
        {
            if(declaration==null)declaration=new VertexDeclaration(
            new VertexElement(0,VertexElementFormat.Vector3,VertexElementUsage.Position,0),new VertexElement(12,VertexElementFormat.Color,VertexElementUsage.Color,0),
            new VertexElement(16,VertexElementFormat.Vector2,VertexElementUsage.TextureCoordinate,0),new VertexElement(24,VertexElementFormat.Vector2,VertexElementUsage.TextureCoordinate,1));
            readers++;return new LayoutLease();
        }
        private sealed class LayoutLease:IDisposable
        {
            private bool disposed;
            public void Dispose(){if(disposed)return;disposed=true;if(--readers==0){declaration.Dispose();declaration=null;}}
        }
        public VertexDeclaration VertexDeclaration{get{if(declaration==null)throw new InvalidOperationException("Dual texture vertex layout wasn't prepared");return declaration;}}
    }
    internal sealed partial class NativeRenderer
    {
        private readonly VertexPositionColorTexture[] quad=new VertexPositionColorTexture[4];
        private readonly DualVertex[] dualQuad=new DualVertex[4];
        private static readonly short[] quadIndices={0,1,2,1,3,2};
        private const int QuadCapacity=4096;
        private readonly VertexPositionColorTexture[] quadBatch=new VertexPositionColorTexture[QuadCapacity*4];
        private readonly DualVertex[] maskedQuadBatch=new DualVertex[QuadCapacity*4];
        private readonly short[] quadBatchIndices=BuildQuadIndices();
        private Texture2D batchTexture,batchMask;
        private string batchMaterial;
        private bool batchPixelSnap;
        private int quadCount;
        internal int QuadDrawCalls{get;private set;}
        private static short[] BuildQuadIndices()
        {
            var result=new short[QuadCapacity*6];for(int i=0;i<QuadCapacity;i++)for(int n=0;n<6;n++)result[i*6+n]=(short)(i*4+quadIndices[n]);return result;
        }
        private readonly Dictionary<string,Texture2D> masks=new Dictionary<string,Texture2D>(StringComparer.Ordinal);
        private Texture2D PrepareMask(string id)
        {
            if(id=="")return null;Texture2D mask;if(masks.TryGetValue(id,out mask))return mask;
            mask=assets.PrepareAlpha(id);masks.Add(id,mask);return mask;
        }
        private void DrawQuad(Texture2D texture,Rectangle source,ComponentPose pose,SceneValue topLeft,SceneValue size,Color color,string material,Texture2D explicitMask)
        {
            Texture2D mask=explicitMask;
            if(mask==null&&material!=""){string id=Text(material,"mask");if(id!="")mask=masks[id];}
            bool screenMask=explicitMask!=null||(material!=""&&Text(material,"maskSpace")=="screen");
            var offset=material==""?SceneValue.Vector(0,0):Value(material,"uvOffset");var scale=material==""?SceneValue.Vector(1,1):Value(material,"uvScale");
            double left=source.X/(double)texture.Width+offset.X,right=left+source.Width/(double)texture.Width*scale.X,top=source.Y/(double)texture.Height+offset.Y,bottom=top+source.Height/(double)texture.Height*scale.Y;
            if(pose.PixelSnap){left=Math.Round(left*texture.Width)/texture.Width;right=Math.Round(right*texture.Width)/texture.Width;top=Math.Round(top*texture.Height)/texture.Height;bottom=Math.Round(bottom*texture.Height)/texture.Height;}
            for(int i=0;i<4;i++)
            {
                double u=i%2,v=i/2,x,y;
                if(pose.Rotation==0){x=pose.X+(topLeft.X+u*size.X)*pose.ScaleX;y=pose.Y+(topLeft.Y+v*size.Y)*pose.ScaleY;if(pose.PixelSnap){x=Math.Round(x);y=Math.Round(y);}}
                else{var point=pose.RenderPoint(SceneValue.Vector(topLeft.X+u*size.X,topLeft.Y+v*size.Y));x=point.X;y=point.Y;}
                var position=new Vector3((float)x,(float)y,0);var uv=new Vector2((float)(u==0?left:right),(float)(v==0?top:bottom));
                if(mask==null)quad[i]=new VertexPositionColorTexture(position,color,uv);
                else dualQuad[i]=new DualVertex{Position=position,Color=color,UV=uv,MaskUV=screenMask?new Vector2((float)x/mask.Width,(float)y/mask.Height):new Vector2((float)u,(float)v)};
            }
            if(quadCount==QuadCapacity||quadCount!=0&&(batchTexture!=texture||batchMask!=mask||batchMaterial!=material||batchPixelSnap!=pose.PixelSnap))FlushQuads();
            batchTexture=texture;batchMask=mask;batchMaterial=material;batchPixelSnap=pose.PixelSnap;
            if(mask==null)Array.Copy(quad,0,quadBatch,quadCount*4,4);else Array.Copy(dualQuad,0,maskedQuadBatch,quadCount*4,4);quadCount++;
        }
        private void FlushQuads()
        {
            if(quadCount==0)return;string material=batchMaterial;
            device.BlendState=Blend(material);device.DepthStencilState=DepthStencilState.None;device.RasterizerState=RasterizerState.CullNone;
            // DualTextureEffect doubles RGB for lightmaps; masks need the neutral half gain
            effect.DiffuseColor=Vector3.One;dualEffect.DiffuseColor=new Vector3(.5f);int count=material!=""&&Number(material,"emission")>0?2:1;
            for(int i=0;i<count;i++)
            {
                if(i==1){device.BlendState=emissionBlend;effect.DiffuseColor=new Vector3((float)Number(material,"emission"));dualEffect.DiffuseColor=effect.DiffuseColor*.5f;}
                if(batchMask==null)
                {effect.Texture=batchTexture;foreach(var pass in effect.CurrentTechnique.Passes){pass.Apply();device.SamplerStates[0]=Sampler(material,batchPixelSnap);device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,quadBatch,0,quadCount*4,quadBatchIndices,0,quadCount*2);QuadDrawCalls++;}}
                else
                {dualEffect.Texture=batchTexture;dualEffect.Texture2=batchMask;foreach(var pass in dualEffect.CurrentTechnique.Passes){pass.Apply();device.SamplerStates[0]=Sampler(material,batchPixelSnap);device.SamplerStates[1]=SamplerState.PointClamp;device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,maskedQuadBatch,0,quadCount*4,quadBatchIndices,0,quadCount*2);QuadDrawCalls++;}}
            }
            quadCount=0;
        }
    }
}
