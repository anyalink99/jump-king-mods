using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BehaviorTree;
using JumpKing;
using JumpKing.Controller;
using JumpKing.MiscEntities.WorldItems;
using JumpKing.MiscEntities.WorldItems.Inventory;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using JumpKing.SaveThread.SaveComponents;
using JKRuntime;
using JKRuntime.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

internal static class InventoryGraphicsTests
{
    private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    private static readonly Dictionary<Items,int> Counts=new Dictionary<Items,int>();
    private static readonly HashSet<Items> Equipped=new HashSet<Items>();
    private static bool Count(Items __0,ref int __result){Counts.TryGetValue(__0,out __result);return false;}
    private static bool Worn(Items __0,ref bool __result){__result=Equipped.Contains(__0);return false;}
    private static bool Silence(){return false;}
    private sealed class Detail:IBTnode
    {
        internal int Runs;
        protected override BTresult MyRun(TickData data){Runs++;return BTresult.Running;}
    }
    internal static void Run(ContentManager content,Func<Action,string,Color[]> capture,Action<bool,string> check)
    {
        var type=typeof(UIApi).Assembly.GetType("JKRuntime.UI.CompactInventory",true);
        var attach=type.GetMethod("Attach",Flags);var detach=type.GetMethod("Detach",Flags);
        var settingsType=typeof(UIApi).Assembly.GetType("JKRuntime.UI.SettingsStore",true);
        settingsType.GetMethod("EnsureLoaded",Flags).Invoke(null,null);
        var settings=(UIApiSettings)settingsType.GetProperty("Current",Flags).GetValue(null,null);
        bool originalSetting=settings.UseCompactInventory;settings.UseCompactInventory=true;
        var select=typeof(MenuSelector).GetProperty("Index",Flags);
        var input=typeof(MenuController).GetField("_menu_state",Flags);
        var gui=Game1.instance.contentManager.gui;
        var oldTrue=gui.CheckBoxTrue;var oldBack=gui.BackButton;
        var audio=Game1.instance.contentManager.audio.menu;var oldCue=audio.CursorMove;
        var silentCue=(JumpKing.XnaWrappers.JKSound)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(JumpKing.XnaWrappers.JKSound));
        GC.SuppressFinalize(silentCue);audio.CursorMove=silentCue;
        var silentInstance=(Microsoft.Xna.Framework.Audio.SoundEffectInstance)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Microsoft.Xna.Framework.Audio.SoundEffectInstance));
        GC.SuppressFinalize(silentInstance);typeof(JumpKing.XnaWrappers.JKSound).GetField("m_instance",Flags).SetValue(silentCue,silentInstance);
        gui.BackButton=Sprite.CreateSprite(content.Load<Texture2D>("Content/gui/back"));
        var texture=content.Load<Texture2D>("Content/gui/checkbox");
        gui.CheckBoxTrue=Sprite.CreateSprite(texture,new Rectangle(texture.Width/2,0,texture.Width/2,texture.Height));
        gui.CheckBoxTrue.center=new Vector2(0,.5f);
        using(var patches=new OwnedPatches("jk-runtime.inventory-fixture")){
            // Audio routing has a separate native-asset fixture; this menu probe
            // also runs in the graphics-only tier without an initialized device.
            patches.Add(typeof(JumpKing.XnaWrappers.JKSound).GetMethod("Play",Type.EmptyTypes),prefix:typeof(InventoryGraphicsTests).GetMethod("Silence",Flags));
            patches.Add(typeof(Microsoft.Xna.Framework.Audio.SoundEffectInstance).GetMethod("Play",Type.EmptyTypes),prefix:typeof(InventoryGraphicsTests).GetMethod("Silence",Flags));
            patches.Add(typeof(InventoryManager).GetMethod("GetItemCount"),prefix:typeof(InventoryGraphicsTests).GetMethod("Count",Flags));
            patches.Add(typeof(ItemEquipOptions).GetMethod("IsItemEnabled"),prefix:typeof(InventoryGraphicsTests).GetMethod("Worn",Flags));
            try {
                var nativeFormat=(GuiFormat)typeof(Game1).Assembly.GetType("JumpKing.PauseMenu.PauseManager",true).GetField("GUI_FORMAT",Flags).GetValue(null);
                nativeFormat.element_margin=1;
                var inventory=new MenuSelector(nativeFormat);
                var detail=new Detail();bool hammerEquipped=true;
                var registered=typeof(UIApi).Assembly.GetType("JKRuntime.UI.RegisteredInventoryButton",true);
                foreach(string name in new[]{"Hammer","Jetpack","Rewinder"}){
                    var definition=UiInventoryItemDefinition.Equipment("fixture."+name.ToLowerInvariant(),name,"Description",name=="Hammer"?new Color(157,132,105):name=="Rewinder"?new Color(66,220,255):new Color(244,190,48),()=>1,
                        ()=>name=="Hammer"&&hammerEquipped,v=>{hammerEquipped=v;return true;});
                    var button=(IBTnode)Activator.CreateInstance(registered,Flags,null,new object[]{definition,detail},null);
                    ((IBTcomposite)inventory).AddChild(button);
                }
                var nativeItems=new[]{Items.GiantBoots,Items.SnakeRing,Items.Crown,Items.CrownNBP,Items.CrownOwl,Items.CapeOwl,Items.Cap,Items.GnomeHat,Items.Shoes,Items.YellowShoes,Items.Tunic,Items.GoldRing,Items.Silver,Items.Ruby,Items.GhostFragment,Items.Shroom,Items.BugNote};
                foreach(var item in nativeItems){
                    Counts[item]=item==Items.GoldRing?12:1;
                    var button=(InventoryButton)Activator.CreateInstance(typeof(InventoryButton),Flags,null,new object[]{item,new Detail()},null);
                    inventory.AddChild(EnableableMenuItem.CreateEnableableMenuItem(inventory,button));
                }
                Equipped.Add(Items.GiantBoots);Equipped.Add(Items.Crown);Equipped.Add(Items.Shoes);
                inventory.Initialize();var original=inventory.Children;
                typeof(IBTnode).GetField("m_last_result",Flags).SetValue(inventory,BTresult.Running);
                attach.Invoke(null,new object[]{inventory});
                var enabled=capture(inventory.Draw,"inventory-compact");
                var columns=(IList[])type.GetField("columns",Flags).GetValue(null);
                check(columns.Select(c=>c.Count).SequenceEqual(new[]{5,9,6}),"Inventory combines mod/cursed items and separates decorative and misc entries without changing their native nodes");
                check(inventory.GetBounds().Top>=12&&inventory.GetBounds().Bottom<=348,"Three-column inventory stays inside the viewport");
                check(original.SequenceEqual(inventory.Children),"Compact inventory preserves the original behavior tree");
                var measuredEntries=(Array)type.GetField("entries",Flags).GetValue(null);
                Func<int,Rectangle> cellBounds=i=>(Rectangle)measuredEntries.GetValue(i).GetType().GetField("Hit",Flags).GetValue(measuredEntries.GetValue(i));
                var firstCell=cellBounds(0);var secondCell=cellBounds(1);
                check(firstCell.X==inventory.GetBounds().X&&firstCell.Y-inventory.GetBounds().Y==nativeFormat.padding.top,
                    "Inventory uses the native cursor origin and top padding without extra insets");
                check(secondCell.Y-firstCell.Y==((IMenuItem)original[0]).GetSize().Y+nativeFormat.element_margin,
                    "Inventory row spacing exactly matches the native one-pixel item margin");
                int lastBottom=Enumerable.Range(0,20).Max(i=>cellBounds(i).Bottom);
                int backY=(int)type.GetField("backY",Flags).GetValue(null);
                check(backY-lastBottom==nativeFormat.element_margin&&inventory.GetBounds().Bottom-backY-((IMenuItem)original[20]).GetSize().Y==nativeFormat.padding.bottom,
                    "Back uses the original item gap and bottom padding without a reserved footer");
                var textAt=new Point(firstCell.X+nativeFormat.padding.left+5,firstCell.Y);
                var nativeText=capture(()=>((IMenuItem)original[0]).Draw(textAt.X,textAt.Y,true),null);
                var labelSize=Game1.instance.contentManager.font.MenuFontSmall.MeasureString("Hammer").ToPoint();
                check(Enumerable.Range(textAt.Y,labelSize.Y).All(y=>Enumerable.Range(textAt.X,labelSize.X).All(x=>nativeText[y*480+x]==enabled[y*480+x])),
                    "Mod item color, native text padding and selected shift match original pixels");
                check(enabled.Any(c=>c==new Color(66,220,255))&&enabled.Any(c=>c==new Color(244,190,48))&&enabled.Any(c=>c==new Color(157,132,105)),"Registered cyan, gold and brown item colors are preserved");
                hammerEquipped=false;Equipped.Clear();
                var unequipped=capture(inventory.Draw,"inventory-unequipped");
                check(!enabled.SequenceEqual(unequipped),"Equipped markers update immediately for native and registered items");
                hammerEquipped=true;Equipped.Add(Items.GiantBoots);Equipped.Add(Items.Crown);Equipped.Add(Items.Shoes);
                input.SetValue(ControllerManager.instance.MenuController,new PadState{right=true});inventory.Run(new TickData(.016f,1));
                check((int)select.GetValue(inventory,null)==5&&detail.Runs==0,"Right moves to decorative column without activating an item");
                input.SetValue(ControllerManager.instance.MenuController,new PadState{down=true});inventory.Run(new TickData(.016f,2));
                check((int)select.GetValue(inventory,null)==6,"Down stays within the current inventory column");
                input.SetValue(ControllerManager.instance.MenuController,new PadState{right=true});inventory.Run(new TickData(.016f,3));
                check((int)select.GetValue(inventory,null)==15,"Horizontal navigation preserves row across columns");
                select.SetValue(inventory,0,null);input.SetValue(ControllerManager.instance.MenuController,new PadState{confirm=true});inventory.Run(new TickData(.016f,4));
                check(detail.Runs==1,"Inventory confirmation enters the original item action exactly once");
                input.SetValue(ControllerManager.instance.MenuController,new PadState{right=true});inventory.Run(new TickData(.016f,5));
                check(detail.Runs==2&&(int)select.GetValue(inventory,null)==0,"An open item owns navigation instead of the inventory grid");
                typeof(MenuSelector).GetField("m_last_child_result",Flags).SetValue(inventory,BTresult.NULL);
                foreach(var node in inventory.Children)node.ResetResult();
                settings.UseCompactInventory=false;type.GetMethod("RestoreBounds",Flags).Invoke(null,null);
                capture(inventory.Draw,"inventory-original");
                check(!(bool)type.GetMethod("Handles",Flags).Invoke(null,new object[]{inventory}),"Disabling Compact Inventory restores native rendering and navigation");
                settings.UseCompactInventory=true;
                Profile(inventory,settings,type);
                // A long mod column must scroll independently without losing rows.
                for(int i=0;i<35;i++)inventory.AddChild(new TextButton("Mod item "+i,new Detail()));
                inventory.Initialize(false);select.SetValue(inventory,inventory.Children.Length-1,null);
                capture(inventory.Draw,"inventory-scrolled");
                var first=(int[])type.GetField("first",Flags).GetValue(null);
                var last=(int[])type.GetField("last",Flags).GetValue(null);
                check(first[0]>0&&first[1]==0&&last[0]==40,"Long mod column scrolls to selection independently of other categories");
                check(inventory.GetBounds().Bottom<=348,"Scrolled inventory keeps Back and all rows inside the frame");
                var begin=(Action)Delegate.CreateDelegate(typeof(Action),typeof(UiPointer).GetMethod("BeginDraw",Flags));
                var end=(Action)Delegate.CreateDelegate(typeof(Action),typeof(UiPointer).GetMethod("EndDraw",Flags));
                typeof(UiPointer).GetMethod("Reset",Flags).Invoke(null,null);
                capture(()=>{begin();inventory.Draw();end();},null);
                var entries=(Array)type.GetField("entries",Flags).GetValue(null);
                var hitEntry=entries.GetValue((int)columns[0][first[0]]);
                var hit=(Rectangle)hitEntry.GetType().GetField("Hit",Flags).GetValue(hitEntry);
                var point=new Point(hit.X+25,hit.Y+4);var poll=typeof(UiPointer).GetMethod("Poll",Flags);
                poll.Invoke(null,new object[]{point,true,true,false,false,0,false});
                poll.Invoke(null,new object[]{point,true,true,true,false,0,false});
                poll.Invoke(null,new object[]{point,true,true,false,false,0,false});
                point.X++;poll.Invoke(null,new object[]{point,true,true,false,false,0,false});
                poll.Invoke(null,new object[]{point,true,true,true,false,0,false});
                var delivered=(UiInput)typeof(UiPointer).GetMethod("Read",Flags).Invoke(null,new object[]{inventory,new UiInput()});
                check((int)select.GetValue(inventory,null)==(int)columns[0][first[0]]&&delivered.Action==UiAction.Confirm,
                    "Mouse selects and activates the actual visible row after column scrolling");
                typeof(UiPointer).GetMethod("Reset",Flags).Invoke(null,null);
                for(int i=5;i<=13;i++)((EnableableMenuItem)original[i]).Disable();
                select.SetValue(inventory,0,null);capture(inventory.Draw,null);
                input.SetValue(ControllerManager.instance.MenuController,new PadState{right=true});inventory.Run(new TickData(.016f,6));
                check((int)select.GetValue(inventory,null)==5&&columns[1].Count==0,"Ownership changes remove rows and horizontal navigation skips an empty category");
                inventory.AddChild(new TextInfo("Foreign custom row",Color.White));inventory.Initialize(false);
                capture(inventory.Draw,null);
                check(!(bool)type.GetMethod("IsAdapted",Flags).Invoke(null,new object[]{inventory}),"An unsupported foreign row safely restores the native list");
            } finally {detach.Invoke(null,null);settings.UseCompactInventory=originalSetting;gui.CheckBoxTrue=oldTrue;gui.BackButton=oldBack;audio.CursorMove=oldCue;Counts.Clear();Equipped.Clear();}
        }
    }
    private static void Profile(MenuSelector menu,UIApiSettings settings,Type adapter)
    {
        var device=Game1.instance.GraphicsDevice;var previousBatch=Game1.spriteBatch;
        var begin=(Action)Delegate.CreateDelegate(typeof(Action),typeof(UiPointer).GetMethod("BeginDraw",Flags));
        var end=(Action)Delegate.CreateDelegate(typeof(Action),typeof(UiPointer).GetMethod("EndDraw",Flags));
        var allocated=(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread"));
        using(var target=new RenderTarget2D(device,480,360))using(var batch=new SpriteBatch(device)){
            Game1.spriteBatch=batch;device.SetRenderTarget(target);
            foreach(bool compact in new[]{false,true}){
                settings.UseCompactInventory=compact;adapter.GetMethod("RestoreBounds",Flags).Invoke(null,null);
                Action draw=()=>{begin();batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);menu.Draw();batch.End();end();};
                for(int n=0;n<200;n++)draw();
                long memory=allocated();var watch=System.Diagnostics.Stopwatch.StartNew();
                for(int n=0;n<1000;n++)draw();watch.Stop();
                Console.WriteLine("PERF inventory "+(compact?"compact":"list")+": "+(watch.Elapsed.TotalMilliseconds/1000).ToString("F4")+" ms/draw; "+((allocated()-memory)/1000.0).ToString("F1")+" bytes/draw");
            }
            device.SetRenderTarget(null);Game1.spriteBatch=previousBatch;
        }
        typeof(UiPointer).GetMethod("Reset",Flags).Invoke(null,null);
    }
}
