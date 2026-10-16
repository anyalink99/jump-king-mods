using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using JumpKing;
using JumpKing.Workshop;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using JumpKing.PauseMenu.BT.Actions.Possessions;
using Microsoft.Xna.Framework;
using WardrobePlus;

internal static partial class WardrobeTests
{
    private static void LocalPackagesTests(string gameDir)
    {
        string directory=Path.Combine(output,"local-packages"),root=Path.Combine(directory,"12345");
        Directory.CreateDirectory(Path.Combine(root,"wardrobe"));
        File.Copy(Path.Combine(gameDir,"Content/king/base.xnb"),Path.Combine(root,"body.xnb"));
        File.WriteAllText(Path.Combine(root,Collection.FileName),"<SetSettings><enabled>false</enabled><Reskins><Reskin><skin>NULL</skin><name>body</name></Reskin></Reskins></SetSettings>");
        File.WriteAllText(Path.Combine(root,"wardrobe/skin.json"),WardrobePlus.Advanced.ManifestIO.Write(new WardrobePlus.Advanced.SkinManifest{id="local-test",name="Local test collection"}));
        var manager=(WorkshopManager)FormatterServices.GetUninitializedObject(typeof(WorkshopManager));
        manager.collections=new System.Collections.ObjectModel.Collection<Collection>();manager.reskins=new System.Collections.ObjectModel.Collection<Reskin>();
        Check(LocalPackages.Sync(manager,directory).Count==0,"Local collection registers with the native Workshop manager");
        var item=manager.collections.Single();LocalPackages.Sync(manager,directory);
        Check(Catalog.PackageId(item)=="local:12345","Numeric local folder names retain local saved IDs after native registration");
        Check(ReferenceEquals(item,manager.collections.Single()) && item.HasDetails && item.ID==0 && ((IUGC)item).Name=="Local test collection"
            && ((IIUGC)item).Author=="Local package","Native local entries have usable metadata without duplicate rows or a fabricated Steam identity");
        var factoryType=typeof(Game1).Assembly.GetType("JumpKing.PauseMenu.MenuFactory",true);
        var factory=FormatterServices.GetUninitializedObject(factoryType);
        var drawables=factoryType.GetField("m_drawables",Flags);drawables.SetValue(factory,Activator.CreateInstance(drawables.FieldType));
        var fonts=Game1.instance.contentManager.font;var oldStyle=fonts.StyleFont;fonts.StyleFont=fonts.MenuFont;
        var gui=Game1.instance.contentManager.gui;var oldUp=gui.ThumbsUp;var oldDown=gui.ThumbsDown;gui.ThumbsUp=gui.ThumbsDown=gui.Explore;
        var oldBack=gui.BackButton;gui.BackButton=gui.Explore;
        var oldTrue=gui.CheckBoxTrue;var oldFalse=gui.CheckBoxFalse;gui.CheckBoxTrue=gui.CheckBoxFalse=gui.Explore;
        try
        {
            var format=new GuiFormat{anchor_bounds=new Rectangle(0,0,480,360),anchor=new Vector2(.5f,.5f),element_margin=4};
            var page=(MenuSelector)factoryType.GetMethod("PaginatedList",Flags).MakeGenericMethod(typeof(Collection)).Invoke(factory,new object[]{manager.collections,format,format,format,null,null,0});
            var row=page.Children.OfType<DoubleTextInfoButton>().Single();var detail=row.Child as MenuSelector;
            Check(row.Texts.Any(x=>x.Text.Contains("Local test collection")) && detail!=null && detail.Children.OfType<ToggleCollection>().Count()==1,
                "The actual native Collections page opens the local entry with a collection toggle and native preview");
        }
        finally{fonts.StyleFont=oldStyle;gui.ThumbsUp=oldUp;gui.ThumbsDown=oldDown;gui.BackButton=oldBack;gui.CheckBoxTrue=oldTrue;gui.CheckBoxFalse=oldFalse;}
        manager.collections.Clear();LocalPackages.Sync(manager,directory);
        Check(manager.collections.Count==1,"A native Workshop refresh can repopulate local collections");
        var foreign=new Collection(root);manager.collections.Add(foreign);
        File.Move(Path.Combine(root,Collection.FileName),Path.Combine(root,"set_settings.saved"));LocalPackages.Sync(manager,directory);
        Check(manager.collections.Count==1 && ReferenceEquals(manager.collections[0],foreign),"Removing a local package retires only the owned entry, preserving external Workshop objects");
    }
}
