using System;
using System.Collections;
using JumpKing;
using JumpKing.JKMemory;
using JumpKing.JKMemory.KingSpriteLayers;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MoreItems;

namespace JumpKingJetpack
{
    internal sealed class JetpackItem : IDisposable
    {
        private readonly Texture2D bodyTexture;
        private readonly Sprite[] bodySprites;
        private readonly IList[] playerLayers;
        private readonly JetpackFireTrail trail;
        private readonly JetpackFlameLayer flame;
        private readonly JetpackSound sound;
        private bool bodyVisible;
        private bool disposed;
        private readonly JKRuntime.RuntimeScope resources = new JKRuntime.RuntimeScope();

        internal JetpackItem(PlayerEntity player)
        {
            if (Game1.instance == null
                || Game1.instance.contentManager == null
                || Game1.instance.contentManager.playerSprites == null)
            {
                throw new InvalidOperationException(
                    "Jump King player sprites are unavailable");
            }

            JKContentManager.PlayerSprites playerSprites =
                Game1.instance.contentManager.playerSprites;
            try
            {
            bodyTexture = resources.Own(CreateTexture(JetpackAtlasData.BuildBodyAtlas()));
            KingSprites bodyLayer = new KingSprites(bodyTexture);
            Sprite[] targets = GetRegularSprites(playerSprites);
            bodySprites = GetRegularSprites(bodyLayer);
            playerLayers = new IList[targets.Length];
            resources.Defer(RemoveBodyLayer);
                for (int pose = 0; pose < targets.Length; pose++)
                {
                    playerLayers[pose] = SpriteLayerAccess.GetLayers(
                        targets[pose]);
                }
                trail = new JetpackFireTrail(player);
                resources.Defer(delegate { trail.SetEnabled(false); });
                flame = resources.Own(new JetpackFlameLayer(targets, trail));
                sound = MoreItems.ModEntry.PreparedJetpackSound;
                if (sound == null) sound = resources.Own(new JetpackSound());
                else resources.Defer(sound.Stop);
                ApplySettings();
            }
            catch (Exception error)
            {
                try { resources.Dispose(); }
                catch (Exception cleanup) { throw new AggregateException("Jetpack resources and rollback failed", error, cleanup); }
                throw;
            }
        }

        internal void SetThrustActive(bool active)
        {
            if (disposed)
            {
                return;
            }
            flame.SetActive(active);
            trail.Update(active);
            sound.Update(active);
        }

        internal void ApplySettings()
        {
            if (disposed)
            {
                return;
            }
            SettingsStore.EnsureLoaded();
            SetVisible(SettingsStore.Current.ShowJetpack);
            trail.SetEnabled(
                SettingsStore.Current.ShowJetpack
                && SettingsStore.Current.ShowJetpackTrail);
            sound.SetVolume(
                SettingsStore.Current.ShowJetpack
                    ? SettingsStore.Current.JetpackVolume
                    : 0f);
        }

        public void Dispose()
        {
            disposed = true;
            resources.Dispose();
        }

        private void SetVisible(bool visible)
        {
            flame.SetVisible(visible);
            if (visible == bodyVisible)
            {
                return;
            }
            if (visible)
            {
                for (int pose = 0; pose < playerLayers.Length; pose++)
                {
                    playerLayers[pose].Insert(1, bodySprites[pose]);
                }
            }
            else
            {
                RemoveBodyLayer();
            }
            bodyVisible = visible;
        }

        private void RemoveBodyLayer()
        {
            for (int pose = 0; pose < playerLayers.Length; pose++)
            {
                if (playerLayers[pose] != null && bodySprites[pose] != null)
                {
                    playerLayers[pose].Remove(bodySprites[pose]);
                }
            }
            bodyVisible = false;
        }

        private static Sprite[] GetRegularSprites(
            JKContentManager.PlayerSprites sprites)
        {
            return new[]
            {
                sprites.idle,
                sprites.walk_one,
                sprites.walk_smear,
                sprites.walk_two,
                sprites.jump_charge,
                sprites.jump_up,
                sprites.jump_fall,
                sprites.jump_bounce,
                sprites.splat,
                sprites.stretch_one,
                sprites.stretch_smear,
                sprites.stretch_two,
                sprites.look_up,
            };
        }

        private static Sprite[] GetRegularSprites(KingSprites sprites)
        {
            return new[]
            {
                Wrap(sprites.regular.GetSprite(Regular.SpriteKey.idle)),
                Wrap(sprites.regular.GetSprite(Regular.SpriteKey.walk_one)),
                Wrap(sprites.regular.GetSprite(Regular.SpriteKey.walk_smear)),
                Wrap(sprites.regular.GetSprite(Regular.SpriteKey.walk_two)),
                Wrap(sprites.regular.GetSprite(Regular.SpriteKey.jump_charge)),
                Wrap(sprites.regular.GetSprite(Regular.SpriteKey.jump_up)),
                Wrap(sprites.regular.GetSprite(Regular.SpriteKey.jump_fall)),
                Wrap(sprites.regular.GetSprite(Regular.SpriteKey.jump_bounce)),
                Wrap(sprites.regular.GetSprite(Regular.SpriteKey.splat)),
                Wrap(sprites.regular.GetSprite(Regular.SpriteKey.stretch_one)),
                Wrap(sprites.regular.GetSprite(Regular.SpriteKey.stretch_smear)),
                Wrap(sprites.regular.GetSprite(Regular.SpriteKey.stretch_two)),
                Wrap(sprites.regular.GetSprite(Regular.SpriteKey.look_up)),
            };
        }

        private static Sprite Wrap(Sprite sprite)
        {
            return new JetpackBodySprite(sprite);
        }

        private static Texture2D CreateTexture(char[] atlas)
        {
            Texture2D texture = new Texture2D(
                Game1.instance.GraphicsDevice,
                JetpackAtlasData.Width,
                JetpackAtlasData.Height,
                false,
                SurfaceFormat.Color);
            Color[] pixels = new Color[atlas.Length];
            for (int index = 0; index < atlas.Length; index++)
            {
                pixels[index] = JetpackArt.PixelColor(atlas[index]);
            }
            texture.SetData(pixels);
            return texture;
        }

        private sealed class JetpackBodySprite : Sprite
        {
            private readonly Sprite bodySprite;

            internal JetpackBodySprite(Sprite bodySprite)
            {
                this.bodySprite = bodySprite;
            }

            public override void Draw(
                Vector2 position,
                SpriteEffects effects = SpriteEffects.None)
            {
                bodySprite.Draw(position, effects);
            }

            public override void Draw(
                float x,
                float y,
                SpriteEffects effects = SpriteEffects.None)
            {
                bodySprite.Draw(x, y, effects);
            }

            public override void Draw(
                Point position,
                SpriteEffects effects = SpriteEffects.None)
            {
                bodySprite.Draw(position, effects);
            }

            public override void Draw(
                Rectangle destination,
                SpriteEffects effects = SpriteEffects.None)
            {
                bodySprite.Draw(destination, effects);
            }
        }
    }
}
