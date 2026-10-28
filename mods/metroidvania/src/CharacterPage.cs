using System;
using JKRuntime;
using JKRuntime.UI;
using JKRuntime.Presentation;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Metroidvania
{
    internal sealed class CharacterPage : ScopedUiPage
    {
        private readonly UiPageStack pages;
        private readonly UiFrame frame=new UiFrame(new Rectangle(12,12,456,336));
        private readonly UiPageCommand[] commands;
        private int tab,selected;
        private Sprite[] king;
        private string message="";
        private static readonly string[] Tabs={"Character","Inventory","Equipment","Camp"};
        internal CharacterPage(UiPageStack stack)
        {
            pages=stack;commands=new[]{new UiPageCommand(UiAction.Confirm,"Select",Activate),new UiPageCommand(UiAction.Secondary,"Next tab",()=>SelectTab((tab+1)%4)),new UiPageCommand(UiAction.Cancel,"Back",()=>WantsClose=true)};
        }
        protected override void OpenPage(RuntimeScope resources)
        {resources.Own(EquipmentArt.Acquire());king=KingLayers(Game1.instance.contentManager.playerSprites.idle);ModEntry.Store.Save();resources.Defer(delegate{ModEntry.Store.Save();king=null;});}
        private static Sprite[] KingLayers(Sprite sprite)
        {
            var result=new System.Collections.Generic.List<Sprite>();
            foreach(var layer in PlayerAppearance.Layers(sprite))if(layer.texture!=null)result.Add(layer);else if(!ReferenceEquals(layer,sprite))result.AddRange(KingLayers(layer));return result.ToArray();
        }
        private int Count {get{return tab==0?8:tab==1?ModEntry.Store.Current.Inventory.Length:tab==2?3:5;}}
        private void SelectTab(int next){tab=next;selected=0;message="";UiSounds.Play(UiSound.Move);}
        public override void Update(UiInput input,float delta)
        {
            foreach(var command in commands)if(command.Handle(input))return;
            if(input.Left||input.Right){SelectTab((tab+(input.Left?3:1))%4);return;}
            if(input.Up||input.Down){selected=(selected+(input.Up?Count-1:1))%Count;UiSounds.Play(UiSound.Move);message="";}
        }
        private void Activate()
        {
            bool changed=false;var store=ModEntry.Store;var session=ModEntry.Session;
            if(tab==0){changed=store.Edit(p=>p.Train(selected/2,selected%2==0?1:-1));message=changed?"":"Cannot increase this attribute.";}
            else if(tab==1){
                var stack=store.Current.Inventory[Math.Min(selected,store.Current.Inventory.Length-1)];var item=Catalog.Find(stack.Id);
                if(item.Slot==GearSlot.None){
                    if(item.Heal>0&&session!=null&&session.Battle.Player.Health<session.Battle.Player.MaximumHealth&&session.Battle.Player.Action!=CombatAction.Down&&store.Edit(p=>p.Consume(stack.Id)))changed=session.Battle.Heal(item.Heal);
                    message=changed?"":"Cannot heal now.";
                }else if(session==null||session.Battle.Player.Action==CombatAction.Idle){changed=store.Edit(p=>p.Equip(item.Id));message=changed?"":"Cannot equip this item.";}
                else message="Busy.";
            }
            else if(tab==2){
                var slot=(GearSlot)(selected+1);string id=store.Current.Equipped(slot);
                if(id!=""&&(session==null||session.Battle.Player.Action==CombatAction.Idle))changed=store.Edit(p=>p.Equip(id));
                message=changed?"":"Slot empty or busy.";
            }
            else if(selected==0){var next=ModEntry.Preferences.Copy();next.Enabled=!next.Enabled;try{ModEntry.SavePreferences(next);message="Mode changes apply on the next attempt.";}catch(Exception e){message=e.Message;}}
            else if(selected==1){changed=session!=null&&session.SpawnTraining();message=session==null?"No active character.":ModEntry.Status;}
            else if(selected==2){changed=session!=null&&session.Rest();message=changed?"":"Cannot rest here.";}
            else if(selected==3)pages.Push(new UiBindingsPage("Metroidvania binds",null,"metroidvania.character","metroidvania.light","metroidvania.heavy","metroidvania.guard"){ShowList=true});
            else {changed=store.Save();message=changed?"Character saved.":store.Error;}
            if(changed){store.Save();if(session!=null)session.RefreshStats();}
            selected=Math.Max(0,Math.Min(selected,Count-1));
        }
        public override void Draw()
        {
            frame.Draw();var layout=new UiPageLayout(frame.Bounds,commands,1);
            UiTheme.TextLine("METROIDVANIA",layout.Title.Location.ToVector2(),UiTheme.Gold,false);
            for(int i=0;i<4;i++){int index=i;var bounds=new Rectangle(28+i*106,62,100,20);UiTheme.Tab(Tabs[i],bounds,tab==i);UiPointer.Region(bounds,null,()=>SelectTab(index));}
            var left=new Rectangle(28,92,210,192);var right=new Rectangle(250,92,202,192);
            if(tab==0)DrawCharacter(left,right);else if(tab==1)DrawInventory(left,right);else if(tab==2)DrawEquipment(left,right);else DrawCamp(left,right);
            string status=ModEntry.Store.Error!=""?"Save failed.":message;
            UiTheme.TextLine(UiTheme.FitText(status,layout.Status.Width,true),layout.Status.Location.ToVector2(),UiTheme.Muted,true);layout.DrawCommands();
        }
        private void Row(string label,Rectangle bounds,int index,int icon=-1)
        {
            if(index==selected)UiTheme.Panel(bounds,new Color(20,29,35),UiTheme.Gold);
            int inset=icon<0?6:30;if(icon>=0)EquipmentArt.Icon(icon,new Rectangle(bounds.X+3,bounds.Y+1,22,22));
            UiTheme.TextLine(UiTheme.FitText(label,bounds.Width-inset-6,true),new Vector2(bounds.X+inset,bounds.Y+5),UiTheme.Text,true);
            UiPointer.Region(bounds,()=>{selected=index;},()=>{selected=index;Activate();UiSounds.Play(UiSound.Confirm);});
        }
        private void DrawCharacter(Rectangle left,Rectangle right)
        {
            var p=ModEntry.Store.Current;
            for(int i=0;i<8;i++){int axis=i/2,sign=i%2==0?1:-1;int value=p.Attributes[axis]*sign;Row((sign>0?Catalog.Positive[axis]:Catalog.Negative[axis])+"  "+Math.Max(0,value),new Rectangle(left.X,left.Y+i*23,left.Width,21),i);}
            var stats=CombatStats.From(p);Label("LEVEL "+p.Level,right.X,right.Y,UiTheme.Gold);Label("XP "+p.Experience+" / "+p.NextExperience,right.X,right.Y+24);Label("POINTS "+p.Points,right.X,right.Y+44);
            Label("Health    "+stats.Health,right.X,right.Y+78);Label("Damage    "+stats.Damage,right.X,right.Y+98);Label("Reach     "+stats.Reach,right.X,right.Y+118);Label("Armor     "+stats.Armor,right.X,right.Y+138);Label("Guard     "+stats.Guard,right.X,right.Y+158);
        }
        private void DrawInventory(Rectangle left,Rectangle right)
        {
            var p=ModEntry.Store.Current;var items=p.Inventory;
            for(int i=0;i<items.Length;i++){var item=Catalog.Find(items[i].Id);string marker=item.Slot!=GearSlot.None&&p.Equipped(item.Slot)==item.Id?"* ":"";Row(marker+item.Name+(items[i].Count>1?" x"+items[i].Count:""),new Rectangle(left.X,left.Y+i*25,left.Width,23),i,Catalog.Index(item.Id));}
            if(items.Length==0)return;var focus=Catalog.Find(items[Math.Min(selected,items.Length-1)].Id);
            Label(focus.Name,right.X,right.Y,UiTheme.Gold);
            EquipmentArt.Icon(Catalog.Index(focus.Id),new Rectangle(right.X+65,right.Y+33,64,64));
            if(focus.Damage>0){Label("DAMAGE "+focus.Damage,right.X,right.Y+125);Label("REACH "+focus.Reach,right.X,right.Y+147);}
            if(focus.Armor>0)Label("PROTECTION "+focus.Armor,right.X,right.Y+125);
            if(focus.Heal>0)Label("HEALING "+focus.Heal,right.X,right.Y+125);
        }
        private void DrawEquipment(Rectangle left,Rectangle right)
        {
            var p=ModEntry.Store.Current;string[] names={"Weapon","Shield","Body"};
            for(int i=0;i<3;i++){var item=Catalog.Find(p.Equipped((GearSlot)(i+1)));Row(names[i]+": "+(item==null?"Empty":item.Name),new Rectangle(left.X,left.Y+i*42,left.Width,35),i);}
            var anchor=new Vector2(right.X+right.Width/2,right.Y+138);float scale=2;
            if(king!=null)foreach(var sprite in king)Game1.spriteBatch.Draw(sprite.texture,anchor-sprite.source.Size.ToVector2()*sprite.center*scale,sprite.source,Color.White,0,Vector2.Zero,scale,SpriteEffects.None,0);
            var weapon=Catalog.Find(p.Weapon);var shield=Catalog.Find(p.Shield);var body=Catalog.Find(p.Body);
            EquipmentArt.Draw(anchor,1,new[]{(byte)(weapon==null?0:weapon.VisualId),(byte)(shield==null?0:shield.VisualId),(byte)CombatAction.Idle,(byte)0,(byte)(body==null?0:body.VisualId),(byte)0},Color.White,scale);
        }
        private void DrawCamp(Rectangle left,Rectangle right)
        {
            string[] labels={"Mode: "+(ModEntry.Preferences.Enabled?"ON":"OFF"),"Spawn "+ContentCatalog.Enemy(ContentCatalog.DefaultEnemy).Name,"Rest","Binds","Save character"};
            for(int i=0;i<labels.Length;i++)Row(labels[i],new Rectangle(left.X,left.Y+i*34,left.Width,30),i);
            Label("LEVEL "+ModEntry.Store.Current.Level,right.X,right.Y,UiTheme.Gold);
            Label("VICTORIES "+ModEntry.Store.Current.Victories,right.X,right.Y+24);
            if(ModEntry.Session!=null)Label("HP "+ModEntry.Session.Battle.Player.Health+" / "+ModEntry.Session.Battle.Player.MaximumHealth,right.X,right.Y+48);
        }
        private static void Label(string text,int x,int y){Label(text,x,y,UiTheme.Text);}
        private static void Label(string text,int x,int y,Color color){UiTheme.TextLine(text,new Vector2(x,y),color,true);}
    }
}
