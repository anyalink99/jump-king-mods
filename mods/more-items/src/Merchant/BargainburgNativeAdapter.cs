using System;
using System.Collections;
using System.Reflection;
using EntityComponent;
using EntityComponent.BT;
using JumpKing;
using JumpKing.JKMemory;
using JumpKing.JKMemory.KingSpriteLayers;
using JumpKing.MiscEntities.Merchant;
using JumpKing.MiscEntities.WorldItems;
using JumpKing.MiscEntities.WorldItems.Inventory;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using JKRuntime.UI;

namespace MoreItems
{
    internal static class BargainburgNativeAdapter
    {
        internal static void DrawItemIcon(Items item, Rectangle destination)
        {
            if (item == Items.Shoes || item == Items.SnakeRing)
            {
                DrawKingItemIcon(item, destination);
                return;
            }
            Sprite sprite = GetSprite(item);
            if (sprite != null && sprite.texture != null)
            {
                Game1.spriteBatch.Draw(sprite.texture, destination, sprite.source, Color.White);
                return;
            }
            Texture2D pixel = Game1.instance.contentManager.Pixel.texture;
            Game1.spriteBatch.Draw(
                pixel,
                new Rectangle(destination.X + 3, destination.Y + 3, destination.Width - 6, destination.Height - 6),
                Color.Gray);
        }

        internal static void GrantVanillaItem(Items item)
        {
            InventoryManager.AddItemOnce(item);
            EnableSkin(item);
            RemoveWorldItems(item);
            MarkSold(item);
        }

        private static void DrawKingItemIcon(Items item, Rectangle destination)
        {
            Sprite baseSprite = GetBaseKingSprite();
            Sprite itemSprite = GetSkinSprite(item);
            if (baseSprite != null && baseSprite.texture != null)
                Game1.spriteBatch.Draw(baseSprite.texture, destination, baseSprite.source, Color.White);
            if (itemSprite != null && itemSprite.texture != null)
                Game1.spriteBatch.Draw(itemSprite.texture, destination, itemSprite.source, Color.White);
        }

        private static Sprite GetBaseKingSprite()
        {
            Sprite layered = Game1.instance.contentManager.playerSprites.idle;
            PropertyInfo property = layered.GetType().GetProperty("Sprites", BindingFlags.Instance | BindingFlags.Public);
            IList sprites = property == null ? null : property.GetValue(layered, null) as IList;
            return sprites != null && sprites.Count > 0 ? sprites[0] as Sprite : layered;
        }

        private static Sprite GetSkinSprite(Items item)
        {
            Type manager = typeof(Game1).Assembly.GetType("JumpKing.Player.Skins.SkinManager");
            FieldInfo field = manager == null
                ? null
                : manager.GetField("m_king_sprites", BindingFlags.Static | BindingFlags.Public);
            IDictionary sprites = field == null ? null : field.GetValue(null) as IDictionary;
            KingSprites layer = sprites == null ? null : sprites[item] as KingSprites;
            return layer == null ? null : layer.regular.GetSprite(Regular.SpriteKey.idle);
        }

        private static Sprite GetSprite(Items item)
        {
            switch (item)
            {
                case Items.GoldRing: return Game1.instance.contentManager.ravenSprites.GoldRing;
                case Items.Ruby: return Game1.instance.contentManager.ravenSprites.Ruby;
                case Items.Silver: return Game1.instance.contentManager.props.SilverCoin;
                case Items.GhostFragment: return Game1.instance.contentManager.props.BabeGhostWorldItem;
                case Items.Shroom: return Game1.instance.contentManager.props.ShroomWorldItem;
                case Items.GiantBoots: return Game1.instance.contentManager.props.GiantBootsWorldItem;
                case Items.YellowShoes: return Game1.instance.contentManager.props.YellowShoesWorldItem;
                case Items.Tunic: return Game1.instance.contentManager.props.TunicWorldItem;
                default: return null;
            }
        }

        private static void RemoveWorldItems(Items item)
        {
            Type type = typeof(Game1).Assembly.GetType(
                "JumpKing.MiscEntities.WorldItems.Entities.WorldItemEntityDisplay");
            MethodInfo method = type == null
                ? null
                : type.GetMethod("RemoveAllItemsOfType", BindingFlags.Public | BindingFlags.Static);
            if (method != null) method.Invoke(null, new object[] { item });
        }

        private static void EnableSkin(Items item)
        {
            Type type = typeof(Game1).Assembly.GetType("JumpKing.Player.Skins.SkinManager");
            MethodInfo isSkin = type == null
                ? null
                : type.GetMethod("IsSkin", BindingFlags.Public | BindingFlags.Static);
            MethodInfo enable = type == null
                ? null
                : type.GetMethod("EnableSkin", BindingFlags.Public | BindingFlags.Static);
            if (isSkin != null && enable != null && (bool)isSkin.Invoke(null, new object[] { item }))
                enable.Invoke(null, new object[] { item });
        }

        private static void MarkSold(Items item)
        {
            object component = FindMerchantComponent(item);
            object state = GetState(component);
            if (component == null || state == null) return;
            FieldInfo sold = state.GetType().GetField(
                "sold_shoes",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (sold != null) sold.SetValue(state, true);
            MethodInfo save = component.GetType().GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic);
            if (save != null) save.Invoke(component, null);
        }

        private static object FindMerchantComponent(Items item)
        {
            foreach (Entity entity in EntityManager.instance.Entities)
            {
                if (entity.GetType().FullName != "JumpKing.MiscEntities.Merchant.MerchantEntity") continue;
                FieldInfo settingsField = entity.GetType().GetField(
                    "m_settings",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (settingsField == null) continue;
                MerchantSettings settings = (MerchantSettings)settingsField.GetValue(entity);
                if (settings.sale_item != item) continue;
                foreach (Component component in entity.GetComponents())
                    if (component.GetType().FullName == "JumpKing.MiscEntities.Merchant.MerchantComp") return component;
            }
            return null;
        }

        private static object GetState(object component)
        {
            if (component == null) return null;
            MethodInfo method = component.GetType().GetMethod("GetState", BindingFlags.Instance | BindingFlags.Public);
            return method == null ? null : method.Invoke(component, null);
        }
    }

    internal sealed class BargainburgMerchantGuard : Entity
    {
        private readonly Rectangle trigger;
        private BehaviorTreeComp merchantTree;

        internal BargainburgMerchantGuard(Rectangle triggerRectangle)
        {
            trigger = triggerRectangle;
        }

        internal bool IsAvailable()
        {
            PlayerEntity player = Find<PlayerEntity>();
            return Game1.instance.contentManager.level == null
                && player != null
                && Camera.CurrentScreenIndex1 == 15
                && trigger.Intersects(player.m_body.GetHitbox());
        }

        protected override void Update(float delta)
        {
            if (merchantTree == null) merchantTree = FindMerchantTree();
            if (merchantTree != null) merchantTree.Enabled = !IsAvailable();
        }

        protected override void OnDestroy()
        {
            if (merchantTree != null) merchantTree.Enabled = true;
        }

        private static BehaviorTreeComp FindMerchantTree()
        {
            foreach (Entity entity in EntityManager.instance.Entities)
            {
                if (entity.GetType().FullName != "JumpKing.MiscEntities.Merchant.MerchantEntity") continue;
                FieldInfo field = entity.GetType().GetField(
                    "m_settings",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null) continue;
                MerchantSettings settings = (MerchantSettings)field.GetValue(entity);
                if (!string.Equals(settings.settings.name, "merchant", StringComparison.Ordinal)) continue;
                foreach (Component component in entity.GetComponents())
                {
                    BehaviorTreeComp tree = component as BehaviorTreeComp;
                    if (tree != null) return tree;
                }
            }
            return null;
        }
    }
}
