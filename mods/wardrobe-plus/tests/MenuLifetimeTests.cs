using System;
using System.Collections;
using System.Linq;
using BehaviorTree;
using JKRuntime.UI;
using JumpKing;
using JumpKing.Controller;
using JumpKing.MiscEntities.WorldItems;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using LanguageJK;
using Microsoft.Xna.Framework;
using TimerCallback;
using WardrobePlus;

internal static partial class WardrobeTests
{
    private static void MenuLifetimeTests()
    {
        var callbacks = new CallbackManager();
        var callbackField = typeof(Game1).GetField("_callback_manager",Flags);
        var oldCallbacks = callbackField.GetValue(null); callbackField.SetValue(null,callbacks);
        var menuControllerField = typeof(ControllerManager).GetField("_menu_controller",Flags);
        var oldMenuController = menuControllerField.GetValue(ControllerManager.instance);
        menuControllerField.SetValue(ControllerManager.instance,new MenuController(ControllerManager.instance));
        var format = new GuiFormat { anchor_bounds = new Rectangle(0,0,480,360), anchor = new Vector2(.5f,.5f), element_margin = 4 };
        var menu = new MenuSelector(format);
        menu.AddChild(new LinkButton(language.WORKSHOP_BROWSE,"https://steamcommunity.com/app/1061090/workshop/"));
        menu.AddChild(new TextButton("Back",new MenuSelectorBack(menu))); menu.Initialize(false);
        var factory = new MenuFixture(); factory.AddDrawable(menu);
        Menus.Sync();
        typeof(UIApi).Assembly.GetType("JKRuntime.UI.WorkshopMenuIntegration").GetMethod("Apply",Flags).Invoke(null,new object[] { factory });
        var button = menu.Children.OfType<TextButton>().Single(b => b.Text == "Wardrobe+");
        var host = button.Child;
        var page = (WardrobePage)host.GetType().GetField("page",Flags).GetValue(host);
        // Attach the live factory to Runtime's real deferred menu refresh path.
        // Direct page tests alone cannot detect replacement of its parent button.
        var integration = typeof(UIApi).Assembly.GetType("JKRuntime.UI.ModMenuIntegration");
        var registration = Activator.CreateInstance(integration.GetNestedType("FactoryRegistration",Flags),true);
        registration.GetType().GetField("Factory",Flags).SetValue(registration,new WeakReference(factory));
        registration.GetType().GetField("Format",Flags).SetValue(registration,format);
        var factories = (IList)integration.GetField("Factories",Flags).GetValue(null); factories.Add(registration);
        var saved = Controller.Snapshot(); int tick = 1;
        try
        {
            host.Run(new TickData(.016f,tick++)); host.Run(new TickData(.016f,tick++));
            typeof(WardrobePage).GetMethod("Sources",Flags).Invoke(page,new object[] { (int)Items.Cap });
            var state = PageState(page);
            for (int i = 0; i < 2; i++)
            {
                bool wasEquipped = NativeAppearance.Worn().Contains((int)Items.Cap);
                SelectRow(page,row => RowLabel(row) == (wasEquipped ? "Unequip" : "Equip"));
                callbacks.Update(.016f); callbacks.Update(.016f);
                Check(NativeAppearance.Worn().Contains((int)Items.Cap) != wasEquipped,"Equip action changes native equipment through the page");
                Check(menu.Children.Contains(button) && ReferenceEquals(button.Child,host) && !page.WantsClose && ReferenceEquals(PageState(page),state),
                    "Equip/Unequip retains the open Workshop page after deferred menu callbacks");
                Check(host.Run(new TickData(.016f,tick++)) == BTresult.Running,"The same embedded wardrobe host continues running after equipment changes");
            }
            SelectRow(page,row => RowLabel(row).StartsWith("Material:"));
            SelectRow(page,row => RowLabel(row) == "Gold"); callbacks.Update(.016f); callbacks.Update(.016f);
            Check(menu.Children.Contains(button) && !page.WantsClose,"Material edits also retain the current Workshop page");
            Controller.Request(data => data.Animate = !data.Animate,false,"Preview preference saved"); Controller.Pump();
            callbacks.Update(.016f); callbacks.Update(.016f);
            Check(menu.Children.Contains(button),"Saving preferences does not replace the active Workshop button");
            Menus.Sync(); callbacks.Update(.016f); callbacks.Update(.016f);
            Check(menu.Children.Contains(button),"Repeated menu synchronization preserves an existing registration");
        }
        finally
        {
            factories.Remove(registration); page.OnClose(); Controller.LoadPreset(saved); Controller.Pump();
            callbackField.SetValue(null,oldCallbacks); menuControllerField.SetValue(ControllerManager.instance,oldMenuController);
        }
    }
}
