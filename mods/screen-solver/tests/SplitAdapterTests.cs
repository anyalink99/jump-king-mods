using System;
using System.Collections;
using System.Reflection;

namespace ScreenSolver
{
    internal static partial class Tests
    {
        private static void SplitAdapters()
        {
            foreach(string module in new[]{"Powers","NoWalkOff"})
            {
                var assembly=Fixture(module+".Module");
                var field=assembly.GetType(module+".Settings",true).GetField("current",NativeWorld.Flags);
                var saved=field.GetValue(null);
                var preferences=Activator.CreateInstance(assembly.GetType(module+".Preferences",true));
                string[] settings=module=="Powers"?new[]{"WarpJump","AirDash"}:new[]{"NoWalkOff"};
                string[] components=module=="Powers"?new[]{"WarpController","AirDashController","WarpVisual","AirDashVisual"}
                    :new[]{"NoWalkOffController+Phase","NoWalkOffController+CommitComponent"};
                foreach(string name in settings) preferences.GetType().GetProperty(name).SetValue(preferences,false,null);
                field.SetValue(null,preferences);
                try
                {
                    foreach(string name in components)
                    {
                        var value=Empty(assembly.GetType(module+"."+name,true));
                        Check(new PlayerAdapters().Ignore(value),"Inactive split component refused: "+name);
                        foreach(string setting in settings)
                        {
                            preferences.GetType().GetProperty(setting).SetValue(preferences,true,null);
                            bool rejected=false;
                            try { new PlayerAdapters().Ignore(value); } catch(NotSupportedException) { rejected=true; }
                            finally { preferences.GetType().GetProperty(setting).SetValue(preferences,false,null); }
                            Check(rejected,"Active split mechanic accepted: "+module+"/"+setting);
                        }
                        foreach(string marker in module=="Powers"?new[]{"Warp","AirDash"}:new[]{"NoWalkOff"})
                        {
                            var variant=assembly.GetType(module+".MapPixels",true).GetField(marker,NativeWorld.Flags).GetValue(null);
                            var screens=PlayerAdapters.Read(variant,"Screens");
                            var add=screens.GetType().GetMethod("Add"); var remove=screens.GetType().GetMethod("Remove");
                            bool inserted=(bool)add.Invoke(screens,new object[]{123}); bool rejected=false;
                            try { new PlayerAdapters().Ignore(value); } catch(NotSupportedException) { rejected=true; }
                            finally { if(inserted) remove.Invoke(screens,new object[]{123}); }
                            Check(rejected,"Active split screen accepted: "+module+"/"+marker);
                        }
                    }
                }
                finally { field.SetValue(null,saved); }
            }
            Console.WriteLine("[OK] Independent No Walk Off/Powers adapters: inactive components, global and screen activation");
        }
    }
}
