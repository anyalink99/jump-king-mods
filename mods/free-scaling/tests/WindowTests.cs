using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Windows.Forms;
using FreeScaling;
using JKRuntime;
using JKRuntime.Settings;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

internal static class WindowTests
{
    private static bool StartMods() { ModEntry.Initialize();return false; }
    private static bool Skip() { return false; }
    private static void Check(bool value,string message) { if(!value)throw new Exception(message); }
    internal static void Run(string root)
    {
        var instance=typeof(Game1).GetField("_instance",OwnedPatches.Members);
        var previous=instance.GetValue(null);var previousGraphics=Game1.graphics;
        var oldSettings=Settings.File;
        var path=Path.Combine(root,"window-preferences.xml");
        // initialize MonoGame's actual window without entering Steam or loading a level
        var game=(Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
        var construct=new DynamicMethod("WindowFixture",typeof(void),new[]{typeof(Game1)},typeof(WindowTests),true);
        var il=construct.GetILGenerator();il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Call,typeof(Game).GetConstructor(Type.EmptyTypes));il.Emit(OpCodes.Ret);
        ((Action<Game1>)construct.CreateDelegate(typeof(Action<Game1>)))(game);
        instance.SetValue(null,game);
        using(var hooks=new OwnedPatches("graphics-plus.window-fixture"))try {
            hooks.Add(typeof(JumpGame).GetConstructor(Type.EmptyTypes),prefix:typeof(WindowTests).GetMethod("StartMods",OwnedPatches.Members));
            hooks.Add(typeof(Game1).GetMethod("UnloadContent",OwnedPatches.Members),prefix:typeof(WindowTests).GetMethod("Skip",OwnedPatches.Members));
            Game1.graphics=new GraphicsDeviceManager(game){PreferredBackBufferWidth=960,PreferredBackBufferHeight=720,GraphicsProfile=GraphicsProfile.HiDef};
            ((IGraphicsDeviceManager)Game1.graphics).CreateDevice();
            var form=(Form)Control.FromHandle(game.Window.Handle);form.ShowInTaskbar=false;
            var original=form.ClientSize;var minimum=form.MinimumSize;var location=form.Location;
            game.Window.AllowUserResizing=false;
            using(var writer=File.CreateText(path))new System.Xml.Serialization.XmlSerializer(typeof(Preferences)).Serialize(writer,new Preferences());
            Settings.File=new SettingsFile<Preferences>(path,()=>new Preferences(),Preferences.Validate);
            // production loads mods from new JumpGame(), inside this exact native call
            typeof(Game1).GetMethod("ContentInit",OwnedPatches.Members).Invoke(game,null);
            ModEntry.EnableSetting.Set(true);
            Check(ModEntry.Enabled && game.Window.AllowUserResizing && FramePresentation.Enabled,"First Graphics toggle must enable the actual resizable window after native startup");
            form.ClientSize=new System.Drawing.Size(1117,811);
            typeof(Form).GetMethod("OnResizeEnd",OwnedPatches.Members).Invoke(form,new object[]{EventArgs.Empty});
            Check(form.ClientSize.Width==1117 && form.ClientSize.Height==811,"Arbitrary client dimensions reach the native window");
            Check(Settings.Current.Width==1117 && Settings.Current.Height==811,"Native client resize saves the custom dimensions");
            var area=Screen.FromControl(form).WorkingArea;
            var moved=WindowSession.FitPosition(new System.Drawing.Point(area.Left+47,area.Top+53),form.Size,area);
            form.Location=moved;
            typeof(Form).GetMethod("OnResizeEnd",OwnedPatches.Members).Invoke(form,new object[]{EventArgs.Empty});
            Check(Settings.Current.HasPosition && Settings.Current.X==moved.X && Settings.Current.Y==moved.Y,"Dragging the window saves its position without a separate option");
            moved=WindowSession.FitPosition(new System.Drawing.Point(area.Left+73,area.Top+89),form.Size,area);
            form.Location=moved;
            ModEntry.EnableSetting.Set(false);
            Check(!ModEntry.Enabled && !game.Window.AllowUserResizing && !FramePresentation.Enabled && form.ClientSize==original,
                "Disabling restores the native window size and releases sampling");
            Check(form.MinimumSize==minimum && form.Location==location,"Disabling restores window constraints and position");
            Check(Settings.Current.X==moved.X && Settings.Current.Y==moved.Y,"A final move without ResizeEnd is saved before native restoration");
            ModEntry.EnableSetting.Set(true);
            Check(form.ClientSize.Width==1117 && form.ClientSize.Height==811,"Re-enabling restores the saved free size");
            Check(form.Location==moved,"Re-enabling restores the saved position too");
            foreach(var state in new[]{FormWindowState.Minimized,FormWindowState.Maximized}) {
                form.WindowState=state;
                typeof(Form).GetMethod("OnResizeEnd",OwnedPatches.Members).Invoke(form,new object[]{EventArgs.Empty});
                Check(Settings.Current.X==moved.X && Settings.Current.Y==moved.Y && Settings.Current.Width==1117,"Minimize and maximize preserve the remembered normal placement");
            }
            form.WindowState=FormWindowState.Normal;
            form.Location=moved;
            typeof(ModEntry).GetMethod("Exiting",OwnedPatches.Members).Invoke(null,new object[]{game,EventArgs.Empty});
            Settings.File=new SettingsFile<Preferences>(path,()=>new Preferences(),Preferences.Validate);
            typeof(Game1).GetMethod("ContentInit",OwnedPatches.Members).Invoke(game,null);
            Check(ModEntry.Enabled && game.Window.AllowUserResizing && form.ClientSize.Width==1117,"Saved free scaling restores during native startup, without waiting for another content initialization");
            Check(form.Location==moved,"Native startup restores the last normal position from disk");
            foreach(var method in typeof(ModEntry).GetMethods())foreach(var attribute in method.GetCustomAttributes(false))
                Check(attribute.GetType().Name!="MainMenuItemSettingAttribute" && attribute.GetType().Name!="PauseMenuItemSettingAttribute","Graphics+ must not publish mod-menu settings");
            Console.WriteLine("[OK] Actual MonoGame window: native startup, first toggle, arbitrary resize, persistence, restoration and no mod settings");
        } finally {
            typeof(ModEntry).GetMethod("Exiting",OwnedPatches.Members).Invoke(null,new object[]{game,EventArgs.Empty});
            game.Dispose();instance.SetValue(null,previous);Game1.graphics=previousGraphics;Settings.File=oldSettings;
        }
    }
}
