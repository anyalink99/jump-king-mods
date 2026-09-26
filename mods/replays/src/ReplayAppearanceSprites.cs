using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using JumpKing;
using JumpKing.JKMemory;
using JumpKing.JKMemory.KingSpriteLayers;
using JumpKing.MiscEntities.WorldItems;

namespace Replays
{
    internal static class ReplayAppearanceSprites
    {
        private static readonly FieldInfo ItemSprites =
            typeof(Game1).Assembly.GetType(
                "JumpKing.Player.Skins.SkinManager",
                true).GetField(
                "m_king_sprites",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly Type LayeredSpriteType =
            typeof(Game1).Assembly.GetType(
                "JumpKing.XnaWrappers.LayeredSprite",
                true);
        private static readonly ConstructorInfo LayeredSpriteConstructor =
            LayeredSpriteType.GetConstructor(new[]
            {
                typeof(Sprite),
                typeof(Sprite[])
            });
        private static readonly PropertyInfo LayeredSprites =
            LayeredSpriteType.GetProperty(
                "Sprites",
                BindingFlags.Instance | BindingFlags.Public);
        private static readonly Dictionary<AppearanceKey, Sprite> Cache =
            new Dictionary<AppearanceKey, Sprite>();
        private static Sprite[] cachedActiveIdleLayers = new Sprite[0];

        internal static void ValidateContract()
        {
            if (ItemSprites == null
                || LayeredSpriteConstructor == null
                || LayeredSprites == null)
            {
                throw new InvalidOperationException(
                    "Jump King equipped-sprite contract is unavailable");
            }
        }

        internal static Sprite Resolve(ReplayPose pose, int[] itemIds)
        {
            ValidateContract();
            RefreshCacheIfNeeded();
            AppearanceKey key = new AppearanceKey(pose, itemIds);
            Sprite result;
            if (Cache.TryGetValue(key, out result)) return result;
            IList activeLayers = Layers(
                ReplayGhostRenderer.ResolveActiveSprite(pose));
            Sprite baseSprite = activeLayers.Count == 0
                ? null
                : activeLayers[0] as Sprite;
            if (baseSprite == null)
                throw new InvalidOperationException(
                    "Jump King base sprite is unavailable");
            List<Sprite> equipment = OrderedEquipmentSprites(pose, itemIds);
            result = equipment.Count == 0
                ? baseSprite
                : LayeredSpriteConstructor.Invoke(
                    new object[] { baseSprite, equipment.ToArray() }) as Sprite;
            Cache[key] = result;
            return result;
        }

        internal static Sprite BaseLayer(Sprite sprite)
        {
            ValidateContract();
            IList layers = Layers(sprite);
            return layers.Count == 0 ? null : layers[0] as Sprite;
        }

        private static void RefreshCacheIfNeeded()
        {
            Sprite current = ReplayGhostRenderer.ResolveActiveSprite(
                ReplayPose.Idle);
            IList layers = Layers(current);
            if (SameLayers(cachedActiveIdleLayers, layers)) return;
            cachedActiveIdleLayers = new Sprite[layers.Count];
            for (int index = 0; index < layers.Count; index++)
                cachedActiveIdleLayers[index] = layers[index] as Sprite;
            Cache.Clear();
        }

        private static IList Layers(Sprite sprite)
        {
            if (sprite == null || !LayeredSpriteType.IsInstanceOfType(sprite))
                return new Sprite[] { sprite };
            IList layers = LayeredSprites.GetValue(sprite, null) as IList;
            return layers != null && layers.Count > 0
                ? layers
                : new Sprite[] { sprite };
        }

        private static bool SameLayers(Sprite[] cached, IList current)
        {
            if (cached == null || current == null
                || cached.Length != current.Count)
            {
                return false;
            }
            for (int index = 0; index < cached.Length; index++)
                if (!ReferenceEquals(cached[index], current[index])) return false;
            return true;
        }

        private static List<Sprite> OrderedEquipmentSprites(
            ReplayPose pose,
            int[] itemIds)
        {
            List<Sprite> result = new List<Sprite>();
            IDictionary sprites = ItemSprites.GetValue(null) as IDictionary;
            if (sprites == null)
                throw new InvalidOperationException(
                    "Jump King equipped sprites are unavailable");
            foreach (int itemId in itemIds ?? new int[0])
            {
                Items item = (Items)itemId;
                KingSprites skin = sprites[item] as KingSprites;
                if (skin == null)
                {
                    throw new InvalidOperationException(
                        "Equipped replay item has no sprite: " + item);
                }
                result.Add(skin.regular.GetSprite(SpriteKey(pose)));
            }
            return result;
        }

        private static Regular.SpriteKey SpriteKey(ReplayPose pose)
        {
            switch (pose)
            {
                case ReplayPose.WalkOne: return Regular.SpriteKey.walk_one;
                case ReplayPose.WalkSmear: return Regular.SpriteKey.walk_smear;
                case ReplayPose.WalkTwo: return Regular.SpriteKey.walk_two;
                case ReplayPose.Charge: return Regular.SpriteKey.jump_charge;
                case ReplayPose.JumpUp: return Regular.SpriteKey.jump_up;
                case ReplayPose.JumpFall: return Regular.SpriteKey.jump_fall;
                case ReplayPose.Bounce: return Regular.SpriteKey.jump_bounce;
                case ReplayPose.Splat: return Regular.SpriteKey.splat;
                case ReplayPose.LookUp: return Regular.SpriteKey.look_up;
                case ReplayPose.StretchOne: return Regular.SpriteKey.stretch_one;
                case ReplayPose.StretchSmear: return Regular.SpriteKey.stretch_smear;
                case ReplayPose.StretchTwo: return Regular.SpriteKey.stretch_two;
                default: return Regular.SpriteKey.idle;
            }
        }

        private struct AppearanceKey : IEquatable<AppearanceKey>
        {
            private readonly ReplayPose pose;
            private readonly int[] items;

            internal AppearanceKey(ReplayPose value, int[] equippedItems)
            {
                pose = value;
                items = equippedItems ?? new int[0];
            }

            public bool Equals(AppearanceKey other)
            {
                return pose == other.pose
                    && ReplayAppearanceTrack.Same(items, other.items);
            }

            public override bool Equals(object value)
            {
                return value is AppearanceKey
                    && Equals((AppearanceKey)value);
            }

            public override int GetHashCode()
            {
                int hash = (int)pose + 17;
                foreach (int item in items) hash = hash * 31 + item;
                return hash;
            }
        }
    }
}
