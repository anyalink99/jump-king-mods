using System;
using System.IO;
using System.Reflection;
using JumpKing;
using JumpKing.Level;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using SmoothCamera;

internal static partial class CameraTests
{
    private sealed class MappingConstructionFixture : IDisposable, JKRuntime.ISceneCompositor, JKRuntime.ISceneFrameCapture
    {
        private readonly IDisposable pool,host,registration;
        private readonly JKRuntime.ISceneCompositor compositor;
        internal bool Requested;
        internal string CapturePath;
        public bool Active {get{return compositor.Active;}}
        public JKRuntime.FrameStyle Style {get{return compositor.Style;}}
        public bool CaptureRequested {get{return Requested;}}
        public void Compose(RenderTarget2D frame){compositor.Compose(frame);}
        public void Capture(RenderTarget2D frame){Save(frame,CapturePath);Requested=false;}
        internal MappingConstructionFixture(Assembly mapping,string root,bool mirror)
        {
            string xml=@"<MappingSettings version='1'><Assets><Asset id='marker' path='marker.png'/></Assets><Components>
<Component id='lower-pose' kind='transform'><Value name='position' value='100,100'/></Component>
<Component id='upper-pose' kind='transform'><Value name='position' value='100,300'/><Value name='screen' value='2'/></Component>
<Component id='lower' kind='sprite'><Value name='transform' value='lower-pose'/><Value name='asset' value='marker'/></Component>
<Component id='upper' kind='sprite'><Value name='transform' value='upper-pose'/><Value name='asset' value='marker'/></Component>
<Component id='source' kind='capture'><Value name='selection' value='lower'/></Component>
<Component id='reflection-material' kind='material'><Value name='capture' value='source'/></Component>
<Component id='plane' kind='transform'><Value name='position' value='0,120'/></Component>
<Component id='reflection' kind='receiver'><Value name='transform' value='plane'/><Value name='material' value='reflection-material'/><Value name='mode' value='reflection'/><Value name='opacity' value='1'/><Value name='projection' value='0,1'/><Value name='outline' value='80,0;120,0;120,40;80,40'/></Component>
<Component id='grade' kind='view'><Value name='ambient' value='#808080'/><Value name='mirror' value='"+(mirror?"both":"none")+@"'/></Component>
</Components></MappingSettings>";
            File.WriteAllText(Path.Combine(root,"mapping_settings.xml"),xml);
            var registry=mapping.GetType("MegaMappingExpansion.Construction.PrimitiveRegistry").GetMethod("Create",Flags).Invoke(null,null);
            var compilerType=mapping.GetType("MegaMappingExpansion.Construction.RecipeCompiler");
            var compiler=Activator.CreateInstance(compilerType,Flags,null,new[]{registry},null);
            var document=compilerType.GetMethod("Read",Flags).Invoke(compiler,new object[]{root});
            pool=(IDisposable)Activator.CreateInstance(mapping.GetType("MegaMappingExpansion.Construction.NativeAssetPool"),true);
            var type=mapping.GetType("MegaMappingExpansion.Construction.NativeScene");
            host=(IDisposable)Activator.CreateInstance(type,Flags,null,new object[]{document,root,pool},null);
            // use the real prepared scene and rendering hooks, without starting a player update loop
            type.GetProperty("Current",Flags).SetValue(null,host,null);
            var renderer=type.GetField("Renderer",Flags).GetValue(host);renderer.GetType().GetMethod("Tick",Flags).Invoke(renderer,null);
            compositor=(JKRuntime.ISceneCompositor)host;registration=JKRuntime.FrameComposition.RegisterScene(this);
        }
        public void Dispose(){registration.Dispose();host.Dispose();pool.Dispose();}
    }
    private static void MappingSceneFixture(Assembly mapping, Type entry, GraphicsDevice device, RenderTarget2D target, JumpGame jump, string output)
    {
        string root=Path.Combine(output,"mapping-scene-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        using(var marker=new System.Drawing.Bitmap(12,12))
        {using(var painter=System.Drawing.Graphics.FromImage(marker))painter.Clear(System.Drawing.Color.Magenta);marker.Save(Path.Combine(root,"marker.png"));}
        using(var scene=new MappingConstructionFixture(mapping,root,false))
        {
            RenderFrame(device,target,jump);var pixels=Read(target);
            Check(!Renderer.Blocked&&pixels[100*480+10].R>55&&pixels[100*480+10].R<85&&pixels[250*480+10].B>55&&pixels[250*480+10].B<85,
                "Construction view grading processes both screen targets before camera composition");
            Check(pixels[120*480+100].R>100&&pixels[280*480+100].R>100,"Construction sprites remain visible on both sides of the moving seam");
            Check(pixels[316*480+100].R>100,"A receiver reflects its selected source from the correct logical screen");
            Check(pixels[10*480+10]==Color.Magenta&&pixels[25*480+310]==Color.Cyan,"Per-screen grading excludes stationary HUD and pause UI");
            Check(!JKRuntime.FrameComposition.InScreenPass&&Camera.CurrentScreen==0,"Composition restores borrowed screen context");
            Save(target,Path.Combine(output,"mapping-camera-scene.png"));
            Hooks.Uninstall();Hooks.Install();Renderer.InvalidateCompatibility();RenderFrame(device,target,jump);var reverse=Read(target);
            Check(reverse[120*480+100]==pixels[120*480+100]&&reverse[316*480+100]==pixels[316*480+100],"Both hook orders retain construction sprites and reflections");
            using(var outer=new JKRuntime.FrameComposition.ScreenPass(target))
            {
                using(var inner=new JKRuntime.FrameComposition.ScreenPass(target))
                {bool rejected=false;try{outer.Dispose();}catch(InvalidOperationException){rejected=true;}Check(rejected&&JKRuntime.FrameComposition.InScreenPass,"Nested screen passes reject out-of-order release");}
                try{using(var inner=new JKRuntime.FrameComposition.ScreenPass(target))throw new InvalidOperationException("fixture");}catch(InvalidOperationException){}
                Check(JKRuntime.FrameComposition.InScreenPass,"A throwing nested pass restores its outer scope");
            }
            Check(!JKRuntime.FrameComposition.InScreenPass,"Borrowed screen contexts release after faults");
            var settings=mapping.GetType("MegaMappingExpansion.MappingSettings");var store=settings.GetField("store",Flags).GetValue(null);
            var enabled=store.GetType().GetMethod("Set",Flags);enabled.Invoke(store,new object[]{false});RenderFrame(device,target,jump);
            Check(Read(target)[100*480+10]==Color.DarkRed,"Disabling construction restores ungraded smooth scenery");
            enabled.Invoke(store,new object[]{true});Settings.Current.Smooth=false;RenderFrame(device,target,jump);
            Check(Read(target)[250*480+10].B>55&&Read(target)[250*480+10].B<85,"Construction grading also works without the camera compositor");
            Settings.Current.Smooth=true;
        }
        using(var scene=new MappingConstructionFixture(mapping,root,true))
        using(var final=new RenderTarget2D(device,480,360))
        {
            scene.Requested=true;scene.CapturePath=Path.Combine(root,"captured.png");
            Renderer.BeforeGameDraw();device.SetRenderTarget(target);Game1.instance.StartBatch();jump.Draw();Game1.instance.EndBatch();
            device.SetRenderTarget(final);device.Clear(Color.Yellow);typeof(Game1).GetMethod("DrawRenderTarget",Flags).Invoke(Game1.instance,null);device.SetRenderTarget(null);
            Check(Read(final)[349*480+469]==Color.Magenta,"One final presentation mirrors the assembled world and HUD exactly once");
            Save(final,Path.Combine(output,"mapping-camera-mirror.png"));
            Check(File.Exists(scene.CapturePath)&&!scene.Requested,"The shared capture contract consumes the assembled view once");
            using(var capture=new System.Drawing.Bitmap(scene.CapturePath))
            {var hud=capture.GetPixel(10,10);Check(capture.Width==480&&capture.Height==360&&hud.R==255&&hud.B==255,"Captured logical output retains stationary UI before final mirror");}
        }
    }

    private static void MappingToggleFixture(GraphicsDevice device, RenderTarget2D target, JumpGame jump, string output, string gameDir)
    {
        // the previous fixture ends during the animated return from Focus
        // test compositor toggles from settled automatic framing
        CameraControls.Gestures.Reset(); Renderer.View.Reset();
        var mapping = Assembly.LoadFrom(mappingImplementation);
        MappingPortalFixture(mapping);
        var hooks = (IDisposable)Activator.CreateInstance(mapping.GetType("MegaMappingExpansion.Construction.SceneHooks", true), true);
        var settings = mapping.GetType("MegaMappingExpansion.MappingSettings", true);
        var storeType = mapping.GetType("MegaMappingExpansion.MappingSettingsStore", true);
        var storeField = settings.GetField("store", Flags);
        object oldStore = storeField.GetValue(null);
        string path = Path.Combine(output, "mapping-toggle-" + Guid.NewGuid().ToString("N") + ".xml");
        var store = Activator.CreateInstance(storeType, Flags, null, new object[] { path }, null);
        storeField.SetValue(null, store);
        var entry = mapping.GetType("MegaMappingExpansion.ModEntry", true);
        var toggle = (JKRuntime.UI.SettingToggle)entry.GetMethod("MainMenuEnabled").Invoke(null, new object[] { null, new JumpKing.PauseMenu.GuiFormat() });
        var change = typeof(JKRuntime.UI.SettingToggle).GetMethod("OnToggle", Flags);
        Renderer.InvalidateCompatibility();
        try
        {
            RenderFrame(device, target, jump);
            Check(!Renderer.Blocked && Read(target)[100 * 480 + 10] == Color.DarkRed, "Enabled Mapping without an active scene preserves smooth camera");
            toggle.OverrideToggle(false); change.Invoke(toggle, null);
            RenderFrame(device, target, jump);
            Check(!Renderer.Blocked && Read(target)[100 * 480 + 10] == Color.DarkRed, "Actual Mapping checkbox immediately releases Smooth Camera while hooks remain installed");
            Check(HarmonyLib.Harmony.GetPatchInfo(typeof(LevelScreen).GetMethod("DrawForeground")).Owners.Contains("mega-mapping-expansion.construction"), "Mapping save/lifecycle hooks are retained while disabled");
            var reloaded = Activator.CreateInstance(storeType, Flags, null, new object[] { path }, null);
            Check(!(bool)storeType.GetProperty("Enabled", Flags).GetValue(reloaded, null), "Mapping checkbox persists off");
            using (var content = new ContentManager(Game1.instance.Services, gameDir))
            {
                Game1.instance.contentManager.font.MenuFont = content.Load<SpriteFont>("Content/font/sf_litter_lover2_bold");
                var sheet = content.Load<Texture2D>("Content/gui/checkbox");
                var uncheckedSprite = Sprite.CreateSprite(sheet, new Rectangle(0, 0, sheet.Width / 2, sheet.Height));
                var checkedSprite = Sprite.CreateSprite(sheet, new Rectangle(sheet.Width / 2, 0, sheet.Width / 2, sheet.Height));
                uncheckedSprite.center = checkedSprite.center = new Vector2(0, .5f);
                Game1.instance.contentManager.gui.CheckBoxFalse = uncheckedSprite;
                Game1.instance.contentManager.gui.CheckBoxTrue = checkedSprite;
                var cameraToggle = new CameraOption();
                var horizontalToggle = ModEntry.MainHorizontal(null, new JumpKing.PauseMenu.GuiFormat());
                cameraToggle.GetSize(); horizontalToggle.GetSize(); toggle.GetSize();
                device.SetRenderTarget(target); device.Clear(new Color(15, 18, 24)); Game1.instance.StartBatch();
                cameraToggle.Draw(24, 110, false); horizontalToggle.Draw(24, 145, false); toggle.Draw(24, 180, false);
                Game1.instance.EndBatch(); device.SetRenderTarget(null);
                Save(target, Path.Combine(output, "native-checkboxes.png"));
                Check(cameraToggle.toggle && !toggle.toggle, "Both settings render native checkboxes with their current values");
                Check(!horizontalToggle.toggle, "Horizontal setting is a separate unchecked native checkbox");
            }
            toggle.OverrideToggle(true); change.Invoke(toggle, null);
            RenderFrame(device, target, jump);
            Check(!Renderer.Blocked, "Re-enabling scene-free Mapping keeps camera cooperation");
            MappingSceneFixture(mapping, entry, device, target, jump, output);
        }
        finally
        {
            hooks.Dispose();
            Renderer.InvalidateCompatibility();
            storeField.SetValue(null, oldStore);
        }
    }
}
