using System;
using System.Collections;
using System.Reflection;
using System.Linq;
using System.Runtime.CompilerServices;
using BehaviorTree;
using EntityComponent.BT;
using JumpKing;
using JumpKing.Controller;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using LanguageJK;

namespace JKRuntime.UI
{
    internal static class VanillaMenuAdapter
    {
        private const BindingFlags InstanceMembers =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo CompositeChildren = typeof(IBTcomposite).GetField(
            "m_children",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private sealed class PendingChildren { internal IBTnode[] Value; }
        private static readonly ConditionalWeakTable<MenuSelector, PendingChildren> Pending = new ConditionalWeakTable<MenuSelector, PendingChildren>();
        private static readonly FieldInfo VisibleItems = typeof(MenuSelector).GetField("m_menu_items", InstanceMembers);
        private static readonly FieldInfo StaticItems = typeof(MenuSelector).GetField("m_static_menu_items", InstanceMembers);
        private static readonly FieldInfo SelectedIndex = typeof(MenuSelector).GetField("_index", InstanceMembers);
        private static readonly FieldInfo ChildResult = typeof(MenuSelector).GetField("m_last_child_result", InstanceMembers);
        private static readonly FieldInfo Bounds = typeof(MenuSelector).GetField("m_bounds", InstanceMembers);
        private static readonly FieldInfo Frame = typeof(MenuSelector).GetField("m_gui_frame", InstanceMembers);
        private static readonly FieldInfo MenuFormat = typeof(MenuSelector).GetField(
            "m_format",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo BehaviourTreeRoot = typeof(BTmanager).GetField(
            "m_root_node",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Type PauseManagerType = typeof(Game1).Assembly.GetType(
            "JumpKing.PauseMenu.PauseManager",
            true);
        private static readonly FieldInfo PauseInstance = PauseManagerType.GetField(
            "instance",
            BindingFlags.Public | BindingFlags.Static);
        private static readonly FieldInfo PauseMainMenu = PauseManagerType.GetField(
            "m_main_menu",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo PauseFactory = PauseManagerType.GetField(
            "m_factory",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo SetPause = PauseManagerType.GetMethod(
            "SetPause",
            BindingFlags.Public | BindingFlags.Static);
        private static readonly MethodInfo ExitToMenu = PauseManagerType.GetMethod(
            "OnExitToMenu",
            BindingFlags.Instance | BindingFlags.Public);

        internal static IList GetDrawables(object factory)
        {
            if (factory == null) return null;
            PropertyInfo property = factory.GetType().GetProperty("Drawables", InstanceMembers);
            return property == null ? null : property.GetValue(factory, null) as IList;
        }

        internal static bool TryGetPauseContext(out object pause, out object factory)
        {
            pause = PauseInstance.GetValue(null);
            factory = pause == null ? null : PauseFactory.GetValue(pause);
            return pause != null && factory != null;
        }

        internal static bool TryGetPauseMainMenu(object factory, out MenuSelector menu)
        {
            menu = null;
            object pause = PauseInstance.GetValue(null);
            if (pause == null || !ReferenceEquals(PauseFactory.GetValue(pause), factory)) return false;
            menu = PauseMainMenu.GetValue(pause) as MenuSelector;
            return menu != null;
        }

        internal static GuiFormat GetFormat(MenuSelector menu)
        {
            return (GuiFormat)MenuFormat.GetValue(menu);
        }

        internal static void SetFormat(MenuSelector menu, GuiFormat format)
        {
            MenuFormat.SetValue(menu, format);
        }

        internal static void AddDrawable(object factory, object drawable)
        {
            MethodInfo method = factory.GetType().GetMethod("AddDrawable", InstanceMembers);
            if (method == null) throw new InvalidOperationException("Jump King drawable API is unavailable");
            method.Invoke(factory, new[] { drawable });
        }

        internal static IBTnode GetBehaviourTreeRoot(object entity)
        {
            EntityComponent.Entity value = entity as EntityComponent.Entity;
            if (value == null) return null;
            foreach (object raw in value.GetComponents())
            {
                BehaviorTreeComp component = raw as BehaviorTreeComp;
                if (component != null)
                    return BehaviourTreeRoot.GetValue(component.GetRaw()) as IBTnode;
            }
            return null;
        }

        internal static bool TryCreateSetting(
            object factory,
            GuiFormat format,
            MethodInfo method,
            out IBTSimpleMenuItem item)
        {
            item = null;
            MethodInfo create = factory.GetType().GetMethod("TryCreateModSetting", InstanceMembers);
            if (create == null) return false;
            object[] arguments = { format, method, null };
            bool created = (bool)create.Invoke(factory, arguments);
            item = arguments[2] as IBTSimpleMenuItem;
            return created && item != null;
        }

        internal static void SetChildren(MenuSelector menu, IBTnode[] children)
        {
            if (menu == null || children == null) throw new ArgumentNullException();
            var copy = (IBTnode[])children.Clone();
            if (copy.Any(child => !(child is IMenuItem))) throw new ArgumentException("Every menu child must be a menu item");
            bool applied = ApplyChildren(menu, copy);
            Pending.Remove(menu);
            if (!applied) Pending.Add(menu, new PendingChildren { Value = copy });
        }

        internal static void Refresh(MenuSelector menu)
        {
            SetChildren(menu, ChildrenForEdit(menu));
        }

        internal static IBTnode[] ChildrenForEdit(MenuSelector menu)
        {
            PendingChildren pending;
            return (IBTnode[])(Pending.TryGetValue(menu, out pending) ? pending.Value : menu.Children).Clone();
        }

        // Called at the native selector boundary. An open child may finish before
        // its row is removed; unrelated list edits must never replace that child.
        internal static void ApplyPending(MenuSelector menu)
        {
            PendingChildren pending;
            if (Pending.TryGetValue(menu, out pending) && ApplyChildren(menu, pending.Value)) Pending.Remove(menu);
        }

        internal static void RepairIdleSelection(IMenuItem[] items, int index, BTresult childResult)
        {
            if (childResult == BTresult.Running) return;
            if (childResult == BTresult.NULL) { ResetRunningRows(items); return; }
            if (items == null || index < 0 || index >= items.Length) return;
            var node = items[index] as IBTnode;
            // Native Reset resets the selector but not its decorators. An
            // interrupted TextButton/Next can otherwise resume without Confirm.
            if (node != null && node.last_result == BTresult.Running) node.ResetResult();
        }

        internal static void ResetRunningRows(IMenuItem[] items)
        {
            if (items == null) return;
            foreach (IMenuItem item in items)
            {
                var node = item as IBTnode;
                if (node != null && node.last_result == BTresult.Running) node.ResetResult();
            }
        }

        internal static void SetRenderedBounds(MenuSelector menu, Microsoft.Xna.Framework.Rectangle bounds)
        {
            Bounds.SetValue(menu, bounds);
            ((GuiFrame)Frame.GetValue(menu)).SetBounds(bounds);
        }

        private static bool ApplyChildren(MenuSelector menu, IBTnode[] children)
        {
            var oldVisible = (IMenuItem[])VisibleItems.GetValue(menu) ?? new IMenuItem[0];
            var oldStatic = (IMenuItem[])StaticItems.GetValue(menu) ?? new IMenuItem[0];
            int oldIndex = (int)SelectedIndex.GetValue(menu);
            IMenuItem selected = oldIndex >= 0 && oldIndex < oldVisible.Length ? oldVisible[oldIndex] : null;
            bool runningChild = menu.IsRunning() && (BTresult)ChildResult.GetValue(menu) == BTresult.Running;
            if (runningChild && selected != null && !children.Any(child => ReferenceEquals(child, selected))) return false;
            var all = children.Cast<IMenuItem>().ToArray();
            // Preserve native disabled rows; refreshing pins must not enable them.
            var visible = all.Where(item => !oldStatic.Any(old => ReferenceEquals(old,item)) || oldVisible.Any(old => ReferenceEquals(old,item))).ToArray();
            int index = Array.FindIndex(visible, item => ReferenceEquals(item, selected));
            bool retained = index >= 0;
            if (!retained)
            {
                index = Math.Max(0, Math.Min(oldIndex, visible.Length - 1));
                if (visible.Length != 0 && visible[index] is UnSelectable)
                { int candidate = Array.FindIndex(visible, item => !(item is UnSelectable)); if (candidate >= 0) index = candidate; }
            }
            // Prepare potentially throwing layout work before publishing anything.
            var bounds = GetFormat(menu).CalculateBounds(visible);
            var frame = new GuiFrame(bounds);
            CompositeChildren.SetValue(menu, children);
            StaticItems.SetValue(menu, all); VisibleItems.SetValue(menu, visible);
            Bounds.SetValue(menu, bounds); Frame.SetValue(menu, frame);
            SelectedIndex.SetValue(menu, index);
            if (!retained) ChildResult.SetValue(menu, BTresult.NULL);
            return true;
        }

        internal static void ClosePause()
        {
            SetPause.Invoke(null, new object[] { false });
        }

        internal static bool ContinueFromMainMenu(object factory)
        {
            MenuSelector menu = FindRootMainMenu(factory);
            if (menu == null) return false;
            menu.SetResult(BTresult.Success);
            ControllerManager.instance.MenuController.ConsumePadPresses();
            return true;
        }

        internal static MenuSelector FindRootMainMenu(object factory)
        {
            IList drawables = GetDrawables(factory);
            if (drawables == null) return null;
            foreach (object value in drawables)
            {
                MenuSelector menu = value as MenuSelector;
                if (menu == null || menu.AllowEscape) continue;
                foreach (IBTnode child in menu.Children)
                {
                    TextButton button = child as TextButton;
                    if (button != null
                        && string.Equals(
                            button.Text,
                            language.GAMETITLESCREEN_EXTRAS,
                            StringComparison.Ordinal))
                    {
                        return menu;
                    }
                }
            }
            return null;
        }

        internal static bool ReturnToMainMenu()
        {
            object pause = PauseInstance.GetValue(null);
            if (pause == null || ExitToMenu == null) return false;
            ExitToMenu.Invoke(pause, null);
            ControllerManager.instance.MenuController.ConsumePadPresses();
            return true;
        }
    }
}
