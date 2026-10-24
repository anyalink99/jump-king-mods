using System;
using JKRuntime.Modules;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using BehaviorTree;

namespace OverlayPlus
{
    [RuntimeModule("overlay-plus","Overlay+")]
    public static class ModEntry
    {
        [BeforeLevelLoad] public static void BeforeLoad(){Controller.Initialize();}
        [OnWorldReady] public static void World(JKRuntime.RuntimeScope scope){Controller.PrepareWorld(scope);}
        [BeforeAttempt] public static void Attempt(JKRuntime.RuntimeScope scope){Controller.PrepareAttempt(scope);}
        [OnLevelStart] public static void Start(JKRuntime.ModuleContext context){Controller.Start(context);}
        [OnLevelEnd] public static void End(){Controller.End();}
        [OnLevelUnload] public static void Unload(){Controller.Unload();}
        [MainMenuItemSetting] public static JKRuntime.UI.SettingToggle Enable(object factory,GuiFormat format){return new JKRuntime.UI.SettingToggle(Controller.EnableSetting);}
        [PauseMenuItemSetting] public static JKRuntime.UI.SettingToggle PauseEnable(object factory,GuiFormat format){return Enable(factory,format);}
        [PauseMenuItemSetting] public static TextButton Edit(object factory,GuiFormat format){return new TextButton("Edit Overlay+",new EditAction());}
        public sealed class EditAction:IBTnode
        {
            protected override BTresult MyRun(TickData data){if(!Controller.Active)return BTresult.Failure;Controller.RequestEditor=true;return BTresult.Success;}
        }
    }
}
