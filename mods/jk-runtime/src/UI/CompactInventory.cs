using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviorTree;
using JumpKing;
using JumpKing.Controller;
using JumpKing.MiscEntities.WorldItems;
using JumpKing.MiscEntities.WorldItems.Inventory;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using JumpKing.SaveThread.SaveComponents;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JKRuntime.UI
{
    internal sealed class CompactInventoryOption : SettingToggle
    {
        private static readonly Settings.Setting<bool> Setting = new Settings.Setting<bool>(
            "jk-runtime.compact-inventory", "Compact Inventory",
            delegate { SettingsStore.EnsureLoaded(); return SettingsStore.Current.UseCompactInventory; },
            delegate(bool value) {
                SettingsStore.EnsureLoaded();bool previous=SettingsStore.Current.UseCompactInventory;
                SettingsStore.Current.UseCompactInventory=value;
                try { SettingsStore.Save(); }
                catch { SettingsStore.Current.UseCompactInventory=previous;throw; }
                CompactInventory.RestoreBounds();
            });
        internal CompactInventoryOption() : base(Setting) { }
    }

    // Adapt presentation/input on the original selector. Native ownership gates,
    // item actions, inspection and foreign decorators keep their exact tree nodes.
    internal static class CompactInventory
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private const int Columns=3;
        private static int textWidth,contentY,backY;
        private static GuiFormat format;
        private static readonly int[] widths=new int[Columns],offsets=new int[Columns];
        private static readonly FieldInfo NativeItem=typeof(InventoryButton).GetField("m_item",Flags);
        private static readonly Func<Items,int,string> NativeLabel=(Func<Items,int,string>)Delegate.CreateDelegate(typeof(Func<Items,int,string>),
            typeof(Game1).Assembly.GetType("JumpKing.MiscEntities.WorldItems.Inventory.ItemNameUtil",true).GetMethod("GetItemLabel",Flags));
        private static readonly PropertyInfo Selected=typeof(MenuSelector).GetProperty("Index",Flags);
        private static MenuSelector menu;
        private static IMenuItem[] rows;
        private static Entry[] entries;
        private static readonly List<int>[] columns={new List<int>(),new List<int>(),new List<int>()};
        private static readonly int[] first=new int[Columns],last=new int[Columns];
        private static SpriteFont font;
        private static int back=-1,lastColumn,contentHeight;
        private static Rectangle bounds;
        private static GuiFrame frame;
        private static bool supported;
        private static readonly Action<int> Scroll=ScrollRows;
        private static readonly Action BackHover=SelectBack;
        private static readonly Func<string,int> MeasureText=MeasureLine;
        private static int MeasureLine(string text){return (int)Math.Ceiling(font.MeasureString(text).X);}

        private sealed class Entry
        {
            internal IMenuItem Row;
            internal UiInventoryItemDefinition Mod;
            internal Items? Native;
            internal string Name;
            internal string[] Lines;
            internal int Column,Slot,Count=-1,Height,Width;
            internal Color Color { get { return Mod==null?Color.White:Mod.Color; } }
            internal bool Equipment { get { return Mod!=null?Mod.IsEquipment:Native.HasValue&&Category(Native.Value)!=2; } }
            internal Rectangle Hit;
            internal Action Hover;
            internal bool Equipped {
                get {
                    try { return Mod!=null ? Mod.IsEquipment&&Mod.IsEquipped!=null&&Mod.IsEquipped()
                        : Native.HasValue&&Equipment&&ItemEquipOptions.IsItemEnabled(Native.Value); }
                    catch { return false; }
                }
            }
            internal void Measure(SpriteFont value)
            {
                int count=1;string name=Name;
                try { count=Mod!=null?Math.Max(0,Mod.GetCount()):Native.HasValue?InventoryManager.GetItemCount(Native.Value):1; }
                catch { count=0; }
                if(Lines!=null&&Count==count)return;
                Count=count;
                if(Mod!=null)name=count>1?count+" "+Mod.PluralName:Mod.Name;
                else if(Native.HasValue)name=NativeLabel(Native.Value,count);
                Lines=UiTheme.WrapLines(name,textWidth,MeasureText);
                // Bound pathological third-party names, retaining a visible ellipsis.
                if(Lines.Length>12){Array.Resize(ref Lines,12);Lines[11]="...";}
                var measured=value.MeasureString(string.Join("\n",Lines));
                Height=(int)measured.Y;Width=(int)measured.X;
            }
        }

        internal static int Category(Items item)
        {
            switch(item){
                case Items.GiantBoots:case Items.SnakeRing:return 0;
                case Items.Crown:case Items.CrownNBP:case Items.CrownOwl:case Items.CapeOwl:
                case Items.Cap:case Items.GnomeHat:case Items.Shoes:case Items.YellowShoes:case Items.Tunic:return 1;
                default:return 2;
            }
        }
        internal static void Attach(MenuSelector value)
        { if(!ReferenceEquals(menu,value))Detach();menu=value;format=VanillaMenuAdapter.GetFormat(menu);rows=null; }
        internal static void Detach(){RestoreBounds();menu=null;rows=null;entries=null;font=null;frame=null;foreach(var column in columns)column.Clear();}
        internal static void RestoreBounds()
        {
            if(menu!=null&&rows!=null)VanillaMenuAdapter.SetRenderedBounds(menu,VanillaMenuAdapter.GetFormat(menu).CalculateBounds(rows));
        }
        internal static bool Handles(MenuSelector value)
        {return ReferenceEquals(menu,value)&&SettingsStore.Current!=null&&SettingsStore.Current.UseCompactInventory;}
        internal static bool IsAdapted(MenuSelector value){return Handles(value)&&supported&&rows!=null;}

        private static bool Prepare(IMenuItem[] items)
        {
            if(!ReferenceEquals(rows,items)){
                rows=items;entries=new Entry[items.Length];back=-1;supported=true;
                foreach(var column in columns)column.Clear();
                for(int i=0;i<items.Length;i++){
                    IBTnode node=items[i] as IBTnode;
                    for(int depth=0;depth<8&&!(node is InventoryButton)&&!(node is RegisteredInventoryButton)&&!(node is TextButton)&&!(node is IconButton);depth++){
                        var decorator=node as IBTdecorator;var menuDecorator=node as IBTMenuDecorator;
                        if(decorator!=null)node=decorator.Child;else if(menuDecorator!=null)node=menuDecorator.Child;else break;
                    }
                    var registered=node as RegisteredInventoryButton;var native=node as InventoryButton;var text=node as TextButton;
                    if(node is UnSelectable){supported=false;continue;}
                    var icon=node as IconButton;
                    if(icon!=null){if(icon.Child is MenuSelectorBack)back=i;else supported=false;continue;}
                    var entry=new Entry{Row=items[i],Mod=registered==null?null:registered.Definition,Column=0};
                    if(native!=null){entry.Native=(Items)NativeItem.GetValue(native);entry.Column=Category(entry.Native.Value);}
                    else if(registered==null){if(text==null){supported=false;continue;}entry.Name=text.Text;}
                    entry.Slot=columns[entry.Column].Count;columns[entry.Column].Add(i);
                    int index=i;entry.Hover=delegate{Selected.SetValue(menu,index,null);lastColumn=entry.Column;};entries[i]=entry;
                }
                Array.Clear(first,0,Columns);
            }
            if(!supported)return false;
            var current=Game1.instance.contentManager.font.MenuFontSmall;
            bool fontChanged=!ReferenceEquals(current,font);font=current;
            var available=format.anchor_bounds;
            available.X+=format.margin.left;available.Y+=format.margin.top;
            available.Width-=format.margin.width;available.Height-=format.margin.height;
            int gap=format.padding.right/2;
            int marker=Game1.instance.contentManager.gui.CheckBoxTrue.source.Width+2;
            int limit=Math.Max(1,(Math.Min(456,available.Width)-format.padding.right-gap*(Columns-1))/Columns-format.padding.left-marker);
            bool widthChanged=limit!=textWidth;textWidth=limit;
            int tallest=0,totalWidth=0;
            for(int c=0;c<Columns;c++){
                int height=0,width=0;
                foreach(int i in columns[c]){
                    if(fontChanged||widthChanged)entries[i].Lines=null;
                    entries[i].Measure(font);
                    height+=entries[i].Height+format.element_margin;
                    width=Math.Max(width,entries[i].Width+(entries[i].Equipment?marker:0));
                }
                if(columns[c].Count>0){height-=format.element_margin;if(totalWidth>0)totalWidth+=gap;}
                offsets[c]=totalWidth;widths[c]=columns[c].Count==0?0:format.padding.left+width;
                totalWidth+=widths[c];tallest=Math.Max(tallest,height);
            }
            int backHeight=back<0?0:rows[back].GetSize().Y;
            int footer=back<0?0:format.element_margin+backHeight;
            contentHeight=Math.Min(Math.Max(1,Math.Min(336,available.Height)-format.padding.height-footer),tallest);
            int heightTotal=format.padding.height+contentHeight+footer;
            totalWidth=Math.Max(totalWidth,back<0?0:format.padding.left+rows[back].GetSize().X)+format.padding.right;
            int x=available.X+(int)((available.Width-totalWidth)*format.anchor.X);
            int y=available.Y+(int)((available.Height-heightTotal)*format.anchor.Y);
            bounds=new Rectangle(Math.Max(12,Math.Min(468-totalWidth,x)),Math.Max(12,Math.Min(348-heightTotal,y)),totalWidth,heightTotal);
            contentY=bounds.Y+format.padding.top;
            backY=contentY+contentHeight+format.element_margin;
            return true;
        }

        internal static bool Navigate(MenuSelector value,IMenuItem[] items,ref int index,BTresult child,PadState state)
        {
            if(!Handles(value)||child==BTresult.Running)return false;
            UiAction action=UiInputRouter.Resolve(state,false);
            if(action!=UiAction.Up&&action!=UiAction.Down&&action!=UiAction.Left&&action!=UiAction.Right)return false;
            if(!Prepare(items))return false;
            index=Move(index,action);return true;
        }
        private static int Move(int index,UiAction action)
        {
            if(index<0||index>=entries.Length)return index;
            Entry entry=entries[index];
            if(entry==null){
                if(action==UiAction.Up||action==UiAction.Down){for(int d=0;d<Columns;d++){var col=columns[(lastColumn+d)%Columns];if(col.Count>0)return action==UiAction.Up?col[col.Count-1]:col[0];}}
                return index;
            }
            lastColumn=entry.Column;
            if(action==UiAction.Up||action==UiAction.Down){
                int next=entry.Slot+(action==UiAction.Up?-1:1);var column=columns[entry.Column];
                return next<0||next>=column.Count?(back>=0?back:index):column[next];
            }
            int direction=action==UiAction.Left?-1:1;
            for(int c=entry.Column+direction;c>=0&&c<Columns;c+=direction){if(columns[c].Count>0){lastColumn=c;return columns[c][Math.Min(entry.Slot,columns[c].Count-1)];}}
            return index;
        }
        private static void ScrollRows(int delta)
        {
            int index=(int)Selected.GetValue(menu,null);
            for(int n=0;n<Math.Abs(delta);n++)index=Move(index,delta>0?UiAction.Up:UiAction.Down);
            Selected.SetValue(menu,index,null);
        }

        internal static bool Draw(MenuSelector value,IMenuItem[] items,int selected,bool childActive)
        {
            if(!Handles(value)||!Prepare(items))return false;
            VanillaMenuAdapter.SetRenderedBounds(menu,bounds);
            if(frame==null)frame=new GuiFrame(bounds);else frame.SetBounds(bounds);
            frame.Draw();
            UiPointer.BeginSurface(menu,!childActive);
            for(int c=0;c<Columns;c++){
                var column=columns[c];if(column.Count==0)continue;
                first[c]=Math.Min(first[c],column.Count-1);
                var selection=selected>=0&&selected<entries.Length?entries[selected]:null;
                if(selection!=null&&selection.Column==c){
                    if(selection.Slot<first[c])first[c]=selection.Slot;
                    int size=0;for(int n=first[c];n<=selection.Slot;n++)size+=entries[column[n]].Height+format.element_margin;
                    while(size-format.element_margin>contentHeight&&first[c]<selection.Slot){size-=entries[column[first[c]]].Height+format.element_margin;first[c]++;}
                }
                int y=contentY;last[c]=first[c];
                for(int n=first[c];n<column.Count;n++){
                    int i=column[n];Entry entry=entries[i];if(y+entry.Height>contentY+contentHeight)break;
                    int x=bounds.X+offsets[c];bool active=i==selected;
                    entry.Hit=new Rectangle(x,y,widths[c],entry.Height);
                    if(active)Game1.spriteBatch.Draw(Game1.instance.contentManager.gui.Cursor,new Vector2(x,y),Color.White);
                    for(int line=0;line<entry.Lines.Length;line++)UiTheme.DrawText(font,entry.Lines[line],new Vector2(x+format.padding.left+(active?5:0),y+line*font.LineSpacing),entry.Color);
                    if(entry.Equipped)Game1.instance.contentManager.gui.CheckBoxTrue.Draw(new Vector2(x+format.padding.left+(active?5:0)+entry.Width+2,y+font.LineSpacing/2));
                    UiPointer.ActionRegion(entry.Hit,UiAction.Confirm,entry.Hover);
                    y+=entry.Height+format.element_margin;last[c]=n+1;
                }
            }
            if(back>=0){
                int x=bounds.X,y=backY;
                rows[back].Draw(x+format.padding.left+(selected==back?5:0),y,selected==back);
                if(selected==back)Game1.spriteBatch.Draw(Game1.instance.contentManager.gui.Cursor,new Vector2(x,y),Color.White);
                UiPointer.ActionRegion(new Rectangle(x,y,Math.Max(widths[0],format.padding.left+rows[back].GetSize().X),rows[back].GetSize().Y),UiAction.Confirm,BackHover);
            }
            UiPointer.ScrollRegion(bounds,Scroll);return true;
        }
        private static void SelectBack(){Selected.SetValue(menu,back,null);}
    }
}
