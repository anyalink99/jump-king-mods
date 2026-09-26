using System;
using System.Collections;
using System.Reflection;
using JumpKing;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using JumpKing.PauseMenu.BT.Actions;
using JumpKing.Util;
using Microsoft.Xna.Framework;

namespace MorphBallMod
{
    public abstract class BallToggleOption : IToggle, IMenuItem
    {
        private const int Padding = 2;
        private const int RestrictionGap = 12;
        private const string RestrictionText = "Restricted";
        private static readonly FieldInfo MenuItemsField =
            typeof(MenuSelector).GetField(
                "m_menu_items",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo UpdateBoundsMethod =
            typeof(MenuSelector).GetMethod(
                "UpdateBounds",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly string label;
        private readonly bool respectScreenRestriction;
        private readonly object menuFactory;
        private readonly FieldInfo drawablesField;
        private bool lastRestrictionActive;
        private Point labelSize;
        private Point restrictionSize;

        protected BallToggleOption(string optionLabel, bool initialValue)
            : this(optionLabel, initialValue, false)
        {
        }

        protected BallToggleOption(
            string optionLabel,
            bool initialValue,
            bool showScreenRestriction,
            object factory)
            : base(initialValue)
        {
            label = optionLabel;
            respectScreenRestriction = showScreenRestriction;
            menuFactory = factory;
            lastRestrictionActive = false;
            if (!showScreenRestriction)
            {
                return;
            }
            if (factory == null
                || MenuItemsField == null
                || UpdateBoundsMethod == null)
            {
                throw new InvalidOperationException(
                    "Jump King dynamic menu bounds contract is unavailable");
            }
            drawablesField = factory.GetType().GetField(
                "m_drawables",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (drawablesField == null)
            {
                throw new InvalidOperationException(
                    "Jump King menu drawable registry is unavailable");
            }
        }

        protected BallToggleOption(
            string optionLabel,
            bool initialValue,
            bool showScreenRestriction)
            : this(
                optionLabel,
                initialValue,
                showScreenRestriction,
                null)
        {
        }

        protected bool ScreenRestrictionActive
        {
            get
            {
                return respectScreenRestriction
                    && IsRestrictedOnCurrentScreen;
            }
        }

        protected virtual bool IsRestrictedOnCurrentScreen
        {
            get { return false; }
        }

        public override void Draw(int x, int y, bool selected)
        {
            bool restricted = ScreenRestrictionActive;
            if (restricted != lastRestrictionActive)
            {
                lastRestrictionActive = restricted;
                RefreshContainingMenuBounds();
            }
            TextHelper.DrawString(
                Game1.instance.contentManager.font.MenuFont,
                label,
                new Vector2(x, y),
                Color.White,
                Vector2.Zero);
            if (restricted)
            {
                TextHelper.DrawString(
                    Game1.instance.contentManager.font.MenuFont,
                    RestrictionText,
                    new Vector2(x + labelSize.X + RestrictionGap, y),
                    Color.Gray,
                    Vector2.Zero);
                return;
            }
            DrawCheckBox(
                new Vector2(x + labelSize.X + Padding, y + labelSize.Y / 2),
                toggle);
        }

        private void RefreshContainingMenuBounds()
        {
            IEnumerable drawables = drawablesField.GetValue(menuFactory)
                as IEnumerable;
            if (drawables == null)
            {
                throw new InvalidOperationException(
                    "Jump King menu drawable registry is invalid");
            }
            foreach (object drawable in drawables)
            {
                MenuSelector selector = drawable as MenuSelector;
                if (selector == null)
                {
                    continue;
                }
                IMenuItem[] items = MenuItemsField.GetValue(selector)
                    as IMenuItem[];
                if (items == null || Array.IndexOf(items, this) < 0)
                {
                    continue;
                }
                UpdateBoundsMethod.Invoke(selector, null);
                return;
            }
            throw new InvalidOperationException(
                "Ball King setting menu is not registered for drawing");
        }

        public override Point GetSize()
        {
            Vector2 measured = Game1.instance.contentManager.font.MenuFont
                .MeasureString(label);
            labelSize = new Point((int)measured.X, (int)measured.Y);
            Vector2 restriction =
                Game1.instance.contentManager.font.MenuFont.MeasureString(
                    RestrictionText);
            restrictionSize = new Point(
                (int)restriction.X,
                (int)restriction.Y);
            if (ScreenRestrictionActive)
            {
                return new Point(
                    labelSize.X + RestrictionGap + restrictionSize.X,
                    System.Math.Max(labelSize.Y, restrictionSize.Y));
            }
            Point checkbox = GetCheckBoxSize();
            return new Point(
                labelSize.X + Padding + checkbox.X,
                System.Math.Max(labelSize.Y, checkbox.Y));
        }
    }
}
