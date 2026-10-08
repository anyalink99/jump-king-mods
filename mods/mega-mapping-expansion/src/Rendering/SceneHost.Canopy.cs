using System;
using System.Collections.Generic;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion
{
    internal sealed partial class SceneHost
    {
        private sealed class CanopyMesh
        {
            internal readonly VertexPositionColorTexture[] Vertices;
            internal readonly short[] Indices;
            internal readonly int Columns, Rows;
            internal readonly Rectangle Bounds;
            internal readonly CanopyWind.Binding[] Bindings;
            internal readonly CanopyWind.Pose Pose = new CanopyWind.Pose();
            internal CanopyMesh(Texture2D texture, Rectangle source, Vector2 root, float treeHeight)
            {
                int width = source.Width, height = source.Height;
                Color[] pixels = new Color[width * height]; texture.GetData(0, source, pixels, 0, pixels.Length);
                int left = width, right = -1, top = height, bottom = -1;
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    if (pixels[y * width + x].A > 0)
                    { left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
                Bounds = right < left ? new Rectangle(0, 0, 1, 1) : new Rectangle(
                    Math.Max(0, left - 1), Math.Max(0, top - 1), Math.Min(width, right + 2) - Math.Max(0, left - 1),
                    Math.Min(height, bottom + 2) - Math.Max(0, top - 1));
                width = Bounds.Width; height = Bounds.Height;
                int cell = Math.Max(8, (Math.Max(width, height) + 127) / 128);
                Columns = (width + cell - 1) / cell; Rows = (height + cell - 1) / cell;
                Vertices = new VertexPositionColorTexture[(Columns + 1) * (Rows + 1)];
                Bindings = new CanopyWind.Binding[Vertices.Length];
                for (int y = 0; y <= Rows; y++) for (int x = 0; x <= Columns; x++)
                    Bindings[y * (Columns + 1) + x] = CanopyWind.Bind(
                        new Vector2(Bounds.X + x / (float)Columns * width, Bounds.Y + y / (float)Rows * height), root, treeHeight);
                Indices = new short[Columns * Rows * 6];
                int n = 0;
                for (int y = 0; y < Rows; y++) for (int x = 0; x < Columns; x++)
                {
                    short a = (short)(y * (Columns + 1) + x), b = (short)(a + 1);
                    short c = (short)(a + Columns + 1), d = (short)(c + 1);
                    Indices[n++] = a; Indices[n++] = b; Indices[n++] = c;
                    Indices[n++] = b; Indices[n++] = d; Indices[n++] = c;
                }
            }
        }
        private readonly Dictionary<string, CanopyMesh> canopyMeshes = new Dictionary<string, CanopyMesh>();
        private BasicEffect canopyEffect;

        private void DrawCanopy(Texture2D texture, Rectangle source, Vector2 position, Vector2 origin,
            Vector2 scale, float rotation, Color tint, SpriteEffects flip, PropData node, float age, bool emission = false)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            CanopyMesh mesh;
            if (!canopyMeshes.TryGetValue(node.Id, out mesh))
            { mesh = new CanopyMesh(texture, source, origin, node.Cloth ? node.ClothHeight : node.WindHeight); canopyMeshes.Add(node.Id, mesh); }
            float phase = StableHash(node.Asset ?? node.Texture) % 1024 / 1024f * MathHelper.TwoPi;
            mesh.Pose.Update(age, node.WindStrength, node.Duration, phase);
            float sine = (float)Math.Sin(rotation), cosine = (float)Math.Cos(rotation);
            float sx = scale.X * ((flip & SpriteEffects.FlipHorizontally) != 0 ? -1f : 1f);
            float sy = scale.Y * ((flip & SpriteEffects.FlipVertically) != 0 ? -1f : 1f);
            for (int y = 0; y <= mesh.Rows; y++) for (int x = 0; x <= mesh.Columns; x++)
            {
                float u = x / (float)mesh.Columns, v = y / (float)mesh.Rows;
                Vector2 point=new Vector2(mesh.Bounds.X+u*mesh.Bounds.Width,mesh.Bounds.Y+v*mesh.Bounds.Height);
                Vector2 local = node.Cloth ? ClothMotion.Bend(point,origin,node.ClothHeight,node.ClothStrength,age,node.Duration)-origin
                    : CanopyWind.Evaluate(mesh.Bindings[y * (mesh.Columns + 1) + x], mesh.Pose) - origin;
                local.X *= sx; local.Y *= sy;
                local = position + new Vector2(local.X * cosine - local.Y * sine, local.X * sine + local.Y * cosine);
                mesh.Vertices[y * (mesh.Columns + 1) + x] = new VertexPositionColorTexture(
                    new Vector3(local, 0f), tint,
                    new Vector2((source.X + mesh.Bounds.X + u * mesh.Bounds.Width) / texture.Width,
                        (source.Y + mesh.Bounds.Y + v * mesh.Bounds.Height) / texture.Height));
            }
            GraphicsDevice device = Game1.instance.GraphicsDevice;
            if (canopyEffect == null) canopyEffect = new BasicEffect(device) { TextureEnabled = true, VertexColorEnabled = true };
            Game1.instance.EndBatch();
            try
            {
                canopyEffect.Texture = texture;
                canopyEffect.Projection = Matrix.CreateOrthographicOffCenter(0, device.Viewport.Width, device.Viewport.Height, 0, 0, 1);
                device.BlendState = emission ? lightAdditive : BlendState.AlphaBlend;
                device.DepthStencilState = DepthStencilState.None;
                device.RasterizerState = RasterizerState.CullNone;
                foreach (EffectPass pass in canopyEffect.CurrentTechnique.Passes)
                {
                    pass.Apply(); device.SamplerStates[0] = string.Equals(node.Sampling,"linear",StringComparison.OrdinalIgnoreCase)?SamplerState.LinearClamp:SamplerState.PointClamp;
                    device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, mesh.Vertices, 0,
                        mesh.Vertices.Length, mesh.Indices, 0, mesh.Indices.Length / 3);
                }
            }
            finally { Game1.instance.StartBatch(); }
            canopyProfileTicks += System.Diagnostics.Stopwatch.GetTimestamp() - started;
        }
    }
}
