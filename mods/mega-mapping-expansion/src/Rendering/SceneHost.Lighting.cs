using System;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion
{
    internal sealed partial class SceneHost
    {
        private sealed class RimFrame
        {
            internal RimContour Contour;
            internal readonly Texture2D[] Directions = new Texture2D[8];
        }
        private readonly System.Collections.Generic.Dictionary<Texture2D,
            System.Collections.Generic.Dictionary<string, RimFrame>> rimFrames =
            new System.Collections.Generic.Dictionary<Texture2D, System.Collections.Generic.Dictionary<string, RimFrame>>();

        private void DrawOuterRim(Texture2D texture, Rectangle source, Vector2 position, Vector2 origin,
            Vector2 scale, float rotation, SpriteEffects effects, Color tint, float pixels, Vector2 direction)
        {
            int radius = Math.Max(1, (int)Math.Ceiling(pixels / Math.Max(.1f, Math.Min(Math.Abs(scale.X), Math.Abs(scale.Y)))));
            radius = Math.Min(16, radius);
            string key = source.ToString() + "/" + radius;
            System.Collections.Generic.Dictionary<string, RimFrame> frames;
            if (!rimFrames.TryGetValue(texture, out frames))
            { frames = new System.Collections.Generic.Dictionary<string, RimFrame>(); rimFrames.Add(texture, frames); }
            RimFrame frame;
            if (!frames.TryGetValue(key, out frame))
            {
                var data = new Color[source.Width * source.Height];
                texture.GetData(0, source, data, 0, data.Length);
                frame = new RimFrame { Contour = new RimContour(data, source.Width, source.Height, radius + 2) };
                frames.Add(key, frame);
            }
            Vector2 local = RotateVector(direction, -rotation);
            if ((effects & SpriteEffects.FlipHorizontally) != 0) local.X = -local.X;
            if ((effects & SpriteEffects.FlipVertically) != 0) local.Y = -local.Y;
            float sector = PositiveModulo((float)Math.Atan2(local.Y, local.X) / MathHelper.TwoPi * 8f, 8f);
            int first = (int)sector; float fraction = sector - first;
            for (int i = 0; i < 2; i++)
            {
                int index = (first + i) % 8;
                if (frame.Directions[index] == null)
                {
                    var mask = new Texture2D(Game1.instance.GraphicsDevice, frame.Contour.Width, frame.Contour.Height);
                    mask.SetData(frame.Contour.Direction(index * MathHelper.TwoPi / 8f, radius));
                    frame.Directions[index] = mask;
                }
                float weight = (i == 0 ? 1f - fraction : fraction) * Math.Min(1f, pixels);
                Game1.spriteBatch.Draw(frame.Directions[index], position, null, MultiplyAlpha(tint, weight), rotation,
                    origin + new Vector2(frame.Contour.Padding), scale, effects, 0f);
            }
        }

        private void DrawAssetRim(Texture2D texture, Rectangle source, Vector2 position, Vector2 origin,
            Vector2 scale, float rotation, SpriteEffects effects, Color color, float opacity, float pixels)
        {
            // Every nearby light contributes a rim from its actual direction.
            foreach (LightData light in scene.Lights ?? new LightData[0])
            {
                if (light.Screen != Camera.CurrentScreenIndex1) continue;
                Vector2 toward = new Vector2(light.X, light.Y) - position;
                float distance = toward.Length();
                if (distance < 0.01f || distance > light.Radius) continue;
                toward /= distance;
                float envelope = LightEnvelope.Rim(distance,light) * SceneVisibility(new Vector2(light.X, light.Y), position)
                    * (OccludesKing(light) ? LightVisibility.Sample(new Vector2(light.X, light.Y),
                        position, KingLightBlocker(), light.ShadowOpacity) : 1f);
                Color lightColor = prepared.Color(light.Color);
                Color tint = MultiplyAlpha(lightColor, opacity * LightGain(light) * envelope * SpotAt(light,position) * 0.95f);
                if(tint.A<2)continue;
                DrawOuterRim(texture, source, position, origin, scale, rotation, effects, tint, pixels, toward);
            }
        }

        private void DrawLight(LightData light)
        {
            if (!light.Enabled || !light.AttachmentVisible) return;
            DrawScatter(light);
            if (scene.Options.AdvancedLighting)
            {
                return;
            }
            Color color = prepared.Color(light.Color);
            float visibility = OccludesKing(light)
                ? LightVisibility.SourceVisibility(new Vector2(light.X, light.Y), KingLightBlocker()) : 1f;
            if (visibility <= 0f) return;
            float flicker = light.Flicker <= 0f ? 1f : 1f - light.Flicker * (0.5f + 0.5f * (float)Math.Sin(time * 13.7f + StableHash(light.Id)));
            int radius = (int)light.Radius;
            Game1.spriteBatch.Draw(radial, new Rectangle((int)light.X - radius, (int)light.Y - radius,
                radius * 2, radius * 2), MultiplyAlpha(color, LightGain(light) * flicker * visibility));
            if (OccludesKing(light)) DrawKingShadow(light);
        }

        private Rectangle KingLightBlocker()
        {
            PlayerEntity player = GameLoopPlayer();
            return player == null ? Rectangle.Empty : Camera.TransformRect(player.m_body.GetHitbox());
        }

        private void DrawKingShadow(LightData light)
        {
            PlayerEntity player = GameLoopPlayer();
            if (player == null) return;
            Rectangle box = Camera.TransformRect(player.m_body.GetHitbox());
            Vector2 lightPos = new Vector2(light.X, light.Y);
            Vector2 center = box.Center.ToVector2();
            Vector2 direction = center - lightPos;
            if (direction.LengthSquared() < 1f) return;
            direction.Normalize();
            Color baseShadow = prepared.Color("#000000");
            const int samples = 18;
            for (int i = samples - 1; i >= 0; i--)
            {
                float t = i / (float)(samples - 1);
                Vector2 shadowCenter = center + direction * (8f + t * light.Radius * 0.82f);
                float width = box.Width * (0.9f + t * 1.15f) + 7f;
                float height = box.Height * (0.42f + t * 0.35f) + 5f;
                float opacity = light.ShadowOpacity * (0.035f + (1f - t) * 0.075f);
                Rectangle destination = new Rectangle((int)(shadowCenter.X - width / 2f),
                    (int)(shadowCenter.Y - height / 2f), (int)width, (int)height);
                Game1.spriteBatch.Draw(radial, destination, MultiplyAlpha(baseShadow, opacity));
            }
        }

        internal void DrawPlayerRim(PlayerEntity player)
        {
            if (scene.Options.PlayerRimOpacity <= 0f || scene.Options.PlayerRimPixels<=0f || CurrentLook.PlayerRimScale<=0f || previewMode != "scene") return;
            Sprite sprite=null; Texture2D texture=null; Rectangle source=Rectangle.Empty; SpriteEffects flip=SpriteEffects.None;
            Vector2 anchor = Camera.TransformVector2(player.m_body.Position + new Vector2(9f, 26f));
            Vector2 position=Vector2.Zero;
            float radius = scene.Options.PlayerRimPixels;
            foreach (LightData light in scene.Lights ?? new LightData[0])
            {
                if (light.Screen != Camera.CurrentScreenIndex1) continue;
                Vector2 toward = new Vector2(light.X, light.Y) - anchor;
                float distance = toward.Length();
                if (distance < 1f || distance > light.Radius) continue;
                toward /= distance;
                float envelope = LightEnvelope.Rim(distance,light) * SceneVisibility(new Vector2(light.X, light.Y), anchor)
                    * (OccludesKing(light) ? LightVisibility.SourceVisibility(new Vector2(light.X, light.Y),
                        Camera.TransformRect(player.m_body.GetHitbox())) : 1f);
                Color warm = prepared.Color(light.Color);
                Color warmTint = MultiplyAlpha(warm,
                    scene.Options.PlayerRimOpacity * CurrentLook.PlayerRimScale * LightGain(light) * envelope * SpotAt(light,anchor) * 1.1f);
                if(warmTint.A<2)continue;
                if(texture==null)
                {
                    if(!TryPlayerSprite(player,out sprite,out texture,out source,out flip))return;
                    texture=Silhouette(texture);position=anchor-source.Size.ToVector2()*sprite.center;
                }
                Game1.spriteBatch.Draw(texture, position + toward * radius, source, warmTint,
                    0f, Vector2.Zero, 1f, flip, 0f);
            }
        }

        private Texture2D Silhouette(Texture2D original)
        {
            Texture2D mask;
            if (silhouetteTextures.TryGetValue(original, out mask)) return mask;
            Color[] data = new Color[original.Width * original.Height];
            original.GetData(data);
            for (int i = 0; i < data.Length; i++)
            { byte a = data[i].A; data[i] = new Color(a, a, a, a); }
            mask = new Texture2D(Game1.instance.GraphicsDevice, original.Width, original.Height);
            mask.SetData(data); silhouetteTextures.Add(original, mask);
            return mask;
        }

        private sealed class LightField
        {
            internal Vector2 Source;
            internal float Radius, Falloff;
            internal bool WasOccluding;
            internal LightData Light;
            internal Vector3 Color;
            internal float[] Base, Attenuated;
            internal Vector2[] Directions;
            internal float[] VisibleBase;
            internal float ConeOuter,ConeFeather;
            internal Rectangle Blocker;
            internal bool HasBlocker;
            internal Vector2 LastDirection;
            internal ShadowFootprint PreviousShadow,NextShadow;
            internal int BlockerRevision = -1;
        }
        private readonly System.Collections.Generic.Dictionary<LightData, LightField> lightFields =
            new System.Collections.Generic.Dictionary<LightData, LightField>();
        private LightField[] activeLightFields;
        private Vector3[] activeLightColors;
        private int lightFieldScreen = -1;

        private bool PrepareLightFields(int width, int height, int w, int h, Rectangle blocker)
        {
            bool changed=lightFieldScreen != Camera.CurrentScreenIndex1;
            if (lightFieldScreen != Camera.CurrentScreenIndex1)
            {
                lightFieldScreen = Camera.CurrentScreenIndex1;
                var active = new System.Collections.Generic.List<LightField>();
                foreach (LightData light in scene.Lights ?? new LightData[0])
                {
                    if (light.Screen != lightFieldScreen) continue;
                    LightField field;
                    if (!lightFields.TryGetValue(light, out field))
                    {
                        field = new LightField { Light = light,
                            Color = prepared.Color(light.Color).ToVector3(),
                            Base = new float[w * h], Attenuated = new float[w * h], Directions=light.ConeWidth>0?new Vector2[w*h]:null,
                            VisibleBase=new float[w*h],ConeOuter=(float)Math.Cos(light.ConeWidth*Math.PI/360) };
                        field.PreviousShadow=new ShadowFootprint(w,h);field.NextShadow=new ShadowFootprint(w,h);
                        field.ConeFeather=1/Math.Max(.000001f,(float)Math.Cos(light.ConeWidth*Math.PI/480)-field.ConeOuter);
                        // Geometry is filled below; initialize once, in the same path as movement.
                        lightFields.Add(light, field);
                    }
                    active.Add(field);
                }
                activeLightFields = active.ToArray(); activeLightColors = new Vector3[activeLightFields.Length];
            }
            for (int l = 0; l < activeLightFields.Length; l++)
            {
                LightField field = activeLightFields[l]; LightData light = field.Light;
                Vector2 nextSource = new Vector2(light.X, light.Y);
                bool geometryChanged = field.Source != nextSource || field.Radius != light.Radius || field.Falloff != light.Falloff || field.BlockerRevision != blockerRevision;
                if (geometryChanged)
                {
                    Array.Clear(field.Base, 0, field.Base.Length);
                    int left = Math.Max(0, (int)Math.Floor((light.X - light.Radius) * w / width));
                    int right = Math.Min(w - 1, (int)Math.Ceiling((light.X + light.Radius) * w / width));
                    int top = Math.Max(0, (int)Math.Floor((light.Y - light.Radius) * h / height));
                    int bottom = Math.Min(h - 1, (int)Math.Ceiling((light.Y + light.Radius) * h / height));
                    for (int y = top; y <= bottom; y++) for (int x = left; x <= right; x++)
                    {
                        int i = y * w + x;
                        Vector2 receiver = new Vector2((x + .5f) * width / w, (y + .5f) * height / h);
                        field.Base[i] = LightVisibility.Attenuation(Vector2.Distance(nextSource, receiver), light.Radius, light.Falloff) * SceneVisibility(nextSource, receiver);
                        if (field.Directions != null) { Vector2 sourceDirection = receiver - nextSource; if (sourceDirection.LengthSquared() > .01f) sourceDirection.Normalize(); field.Directions[i] = sourceDirection; }
                    }
                    field.Source = nextSource; field.Radius = light.Radius; field.Falloff = light.Falloff;
                    field.BlockerRevision = blockerRevision;
                }
                if (geometryChanged || (field.WasOccluding && !OccludesKing(light)))
                {
                    Array.Copy(field.Base, field.Attenuated, field.Base.Length);
                    Array.Copy(field.Base, field.VisibleBase, field.Base.Length);
                    field.HasBlocker = false; changed = true;
                }
                field.WasOccluding = OccludesKing(light);
                field.Color = prepared.Color(light.Color).ToVector3();
                float pulse = 1f - light.Flicker * (.5f + .5f * (float)Math.Sin(time * 13.7f + StableHash(light.Id)));
                Vector3 nextColor=field.Color * LightGain(light) * pulse;
                Vector2 source = new Vector2(light.X, light.Y);
                bool covered=OccludesKing(light) && LightVisibility.SourceVisibility(source,blocker)<=0;
                if(covered)nextColor=Vector3.Zero;
                changed |= nextColor!=activeLightColors[l];activeLightColors[l]=nextColor;
                if(covered)continue;
                if (light.ConeWidth<=0 && (!OccludesKing(light) || (field.HasBlocker && field.Blocker == blocker))) continue;
                Vector2 direction=SpotLight.Direction(light,time);
                bool refreshVisibility=OccludesKing(light) && (!field.HasBlocker||field.Blocker!=blocker);
                if(!refreshVisibility && field.HasBlocker && direction==field.LastDirection)continue;
                changed=true;
                if(refreshVisibility)
                {
                    field.NextShadow.Build(source,blocker,light.Radius,width/(float)w,height/(float)h);
                    for(int y=0;y<h;y++)
                    {
                        int left=Math.Min(field.PreviousShadow.Left[y],field.NextShadow.Left[y]);
                        int right=Math.Max(field.PreviousShadow.Right[y],field.NextShadow.Right[y]);
                        for(int x=left;x<=right;x++)
                        {
                            int i=y*w+x;if(field.Base[i]==0)continue;
                            Vector2 receiver=new Vector2((x+.5f)*width/w,(y+.5f)*height/h);
                            field.VisibleBase[i]=field.Base[i]*LightVisibility.Sample(source,receiver,blocker,light.ShadowOpacity);
                            if(field.Directions==null)field.Attenuated[i]=field.VisibleBase[i];
                        }
                    }
                    var old=field.PreviousShadow;field.PreviousShadow=field.NextShadow;field.NextShadow=old;
                }
                if(field.Directions!=null)
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                {
                    int i = y * w + x; if (field.Base[i] == 0f) continue;
                    field.Attenuated[i] = field.VisibleBase[i]
                        * (field.Directions==null?1:SpotLight.Angular(field.Directions[i],direction,field.ConeOuter,field.ConeFeather));
                }
                field.Blocker = blocker; field.HasBlocker = true;
                field.LastDirection=direction;
            }
            return changed;
        }

        private float LightGain(LightData light)
        { return light.Enabled && light.AttachmentVisible ? light.Intensity*LightEnvelope.Pulse(time,light.PulsePeriod,light.PulseAmount,light.PulsePhase) : 0f; }
        private static bool OccludesKing(LightData light) { return light.OccludeKing && !light.IgnoreAttachmentActor; }

        private float SceneVisibility(Vector2 source, Vector2 receiver)
        {
            float visible = 1;
            foreach (SceneAnchor anchor in work.LightBlockers.At(Camera.CurrentScreenIndex1))
                visible *= 1f - anchor.LightOpacity * (1f - LightVisibility.Sample(source, receiver, new Rectangle(anchor.X, anchor.Y, anchor.Width, anchor.Height), 1f));
            return visible;
        }

        private Vector3 previousAmbient;
        private bool hasRadiance;
        private void BuildLightMap(int width, int height)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            GraphicsDevice device = Game1.instance.GraphicsDevice;
            // Half-resolution irradiance is bilinearly reconstructed. Occlusion is
            // applied to each source before accumulation, never painted over the scene.
            int w = Math.Max(1, width / 2), h = Math.Max(1, height / 2);
            if (radianceTexture == null || radianceTexture.Width != w || radianceTexture.Height != h)
            {
                if (radianceTexture != null) radianceTexture.Dispose();
                radianceTexture = new Texture2D(device, w, h);
                radiancePixels = new Color[w * h];
                lightFields.Clear(); lightFieldScreen = -1;
                hasRadiance=false;
            }
            Color ambient = prepared.Color(string.IsNullOrEmpty(CurrentLook.AmbientLight) ? scene.Options.AmbientLight : CurrentLook.AmbientLight);
            float ambientIntensity=CurrentLook.AmbientIntensity<0 ? scene.Options.AmbientIntensity : CurrentLook.AmbientIntensity;
            Vector3 floor = ambient.ToVector3() * ambientIntensity * CurrentLook.AmbientScale;
            PlayerEntity player = GameLoopPlayer();
            Rectangle blocker = player == null ? Rectangle.Empty : Camera.TransformRect(player.m_body.GetHitbox());
            bool changed=PrepareLightFields(width, height, w, h, blocker) || !hasRadiance || floor!=previousAmbient;
            if(changed)
            {
            for (int i = 0; i < radiancePixels.Length; i++)
            {
                Vector3 radiance = floor;
                for (int l = 0; l < activeLightFields.Length; l++)
                    radiance += activeLightColors[l] * activeLightFields[l].Attenuated[i];
                radiancePixels[i] = new Color(Vector3.Clamp(radiance, Vector3.Zero, Vector3.One));
            }
            radianceTexture.SetData(radiancePixels);
            hasRadiance=true;previousAmbient=floor;
            }
            device.SetRenderTarget(lightTarget);
            device.Clear(Color.Black);
            Game1.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone);
            try { Game1.spriteBatch.Draw(radianceTexture, new Rectangle(0, 0, width, height), Color.White); }
            finally { Game1.spriteBatch.End(); }
            lightProfileTicks += System.Diagnostics.Stopwatch.GetTimestamp() - started;
        }
    }
}
