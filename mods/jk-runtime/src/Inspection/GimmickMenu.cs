using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JumpKing.Mods;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT.Actions;
namespace JKRuntime.Inspection
{
    internal static class GimmickMenu
    {
        internal static void DiscoverSettings(object factory, GuiFormat format, bool pause)
        {
            var weak = new WeakReference(factory);
            var resolve = typeof(JKRuntime.PackageHost).GetMethod("MenuImplementation", Gimmicks.Members);
            foreach (ModAssembly mod in ModLoader.Instance.LoadedMods)
            {
                var methods = (pause ? mod.PauseMenuItemSettingMethods : mod.MainMenuItemSettingMethods).ToArray();
                var implementations = new Dictionary<MethodInfo, MethodInfo>();
                foreach (var method in methods)
                    try { implementations[method] = resolve == null ? method : (MethodInfo)resolve.Invoke(null, new object[] { mod.Assembly, method }); } catch { }
                foreach (var method in methods)
                {
                    try
                    {
                        MethodInfo implementation; if (!implementations.TryGetValue(method, out implementation)) continue;
                        if (implementation == null || implementation.DeclaringType.Assembly == typeof(Gimmicks).Assembly
                            || !typeof(IToggle).IsAssignableFrom(implementation.ReturnType)) continue;
                        MethodInfo menuMethod = method;
                        Func<IToggle> create = () => {
                            if (!weak.IsAlive) throw new InvalidOperationException("Reopen the current mod menu to refresh settings");
                            var option = menuMethod.Invoke(null, new object[] { weak.Target, format }) as IToggle;
                            if (option == null) throw new InvalidOperationException("This setting is unavailable in the current context");
                            return option;
                        };
                        create();
                        string identity = implementation.ReturnType.FullName;
                        if (implementations.Values.Count(m => m != null && m.DeclaringType == implementation.DeclaringType && m.ReturnType == implementation.ReturnType) > 1)
                            identity += ":" + implementation.Name;
                        string id = "setting:" + Gimmicks.TypeId(implementation.DeclaringType) + ":" + identity;
                        Gimmicks.Add(new GimmickEntry { Id = id,
                            Label = Gimmicks.Human(implementation.ReturnType.Name), Owner = mod.ModName, Kind = "Setting",
                            Detail = "Uses the mod's own toggle callback and persistence. This enables its setting, which may still require its original input.",
                            Read = () => create().toggle,
                            Write = value => {
                                IToggle option = create();
                                if (!(bool)option.GetType().GetMethod("CanChange", Gimmicks.Members).Invoke(option, null)) throw new InvalidOperationException("The original mod currently restricts this setting");
                                option.OverrideToggle(value); option.GetType().GetMethod("OnToggle", Gimmicks.Members).Invoke(option, null);
                            } });
                    }
                    catch (Exception error) { Gimmicks.Status = "Setting discovery: " + error.GetBaseException().Message; }
                }
            }
        }
    }
}
