using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Controls;
using HarmonyLib;

public static class NativeContracts
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    [STAThread]
    public static int Main(string[] args)
    {
        string host = args[0], bundle = args[1], root = Path.Combine(Path.GetTempPath(), "WorldsmithNativeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            string name = new AssemblyName(e.Name).Name + ".dll";
            foreach (string folder in new[]{bundle, host, Path.Combine(bundle, "mapping")})
            {
                string path = Path.Combine(folder, name);
                if (File.Exists(path))
                    return Assembly.LoadFrom(path);
            }

            return null;
        };
        try
        {
            var engine = Assembly.LoadFrom(Path.Combine(bundle, "WorldsmithExtension.Engine.dll"));
            engine.GetType("WorldsmithExtension.Engine").GetMethod("Run").Invoke(null, new object[]{host, bundle, new[]{"--check"}});
            var native = Assembly.LoadFrom(Path.Combine(host, "JKWorldsmith.exe"));
            var dashboardType = native.GetType("JKWorldsmith.ViewModels.DashboardViewModel");
            var dashboard = FormatterServices.GetUninitializedObject(dashboardType);
            var posts = new System.Collections.ObjectModel.ObservableCollection<System.ServiceModel.Syndication.SyndicationItem>();
            dashboardType.GetProperty("Posts").SetValue(dashboard, posts, null);
            var newsList = new ListView();
            System.Windows.Data.BindingOperations.SetBinding(newsList, ItemsControl.ItemsSourceProperty,
                new System.Windows.Data.Binding("Posts") { Source = dashboard });
            var article = new System.ServiceModel.Syndication.SyndicationItem { Title = new System.ServiceModel.Syndication.TextSyndicationContent("News after binding") };
            engine.GetType("WorldsmithExtension.NewsFeed").GetMethod("Populate", All).Invoke(null,
                new object[] { posts, new[] { article } });
            if (!Object.ReferenceEquals(newsList.ItemsSource, posts) || newsList.Items.Count != 1 || !Object.ReferenceEquals(newsList.Items[0], article))
                throw new Exception("Dashboard binding missed asynchronous news collection changes");
            System.Windows.Data.BindingOperations.ClearBinding(newsList, ItemsControl.ItemsSourceProperty);
            var toolkit = Assembly.LoadFrom(Path.Combine(host, "CommunityToolkit.Mvvm.dll"));
            var commandType = toolkit.GetType("CommunityToolkit.Mvvm.Input.RelayCommand`1").MakeGenericType(typeof(object));
            object commandValue = null, commandArgument = new object ();
            object command = Activator.CreateInstance(commandType, new object[]{new Action<object>(value => commandValue = value)});
            engine.GetType("WorldsmithExtension.Engine").GetMethod("ExecuteCommand", All).Invoke(null, new object[]{command, commandArgument});
            if (!Object.ReferenceEquals(commandValue, commandArgument))
                throw new Exception("Native generic command did not receive its project argument");
            var type = native.GetType("JKWorldsmith.ViewModels.Level.HitboxViewModel");
            object vm = FormatterServices.GetUninitializedObject(type);
            string strip = Path.Combine(root, "visual_level.png");
            using (var b = new Bitmap(60, 512 * 45))
            {
                b.SetPixel(0, 0, Color.Magenta);
                b.SetPixel(0, 511 * 45, Color.Blue);
                b.Save(strip);
            }

            Directory.CreateDirectory(Path.Combine(root, "props", "mega-mapping-expansion"));
            File.WriteAllText(Path.Combine(root, "props", "mega-mapping-expansion", "map.xml"), "<MapLayout version=\"1\" screens=\"512\" atlasSide=\"23\"/>");
            type.GetField("folder", All).SetValue(vm, root);
            type.GetMethod("InitializeHitbox", All).Invoke(vm, null);
            var frames = (Array)type.GetProperty("HitboxFrames").GetValue(vm, null);
            if (frames.Length != 512)
                throw new Exception("Native preview truncated");
            type.GetMethod("GenerateInGameLevelHitbox", All).Invoke(vm, null);
            using (var atlas = new Bitmap(Path.Combine(root, "level.png")))
            {
                if (atlas.Width != 1380 || atlas.Height != 1035 || atlas.GetPixel((511 / 23) * 60, (511 % 23) * 45).ToArgb() != Color.Magenta.ToArgb() || atlas.GetPixel(0, 0).ToArgb() != Color.Blue.ToArgb())
                    throw new Exception("Native atlas patch corrupted screen order");
            }

            type.GetField("frameNumber", All).SetValue(vm, 511);
            if (!(bool)type.GetProperty("CanFrameNumberUp", All).GetValue(vm, null))
                throw new Exception("Navigation still capped at 255");
            type.GetField("frameNumber", All).SetValue(vm, 512);
            if ((bool)type.GetProperty("CanFrameNumberUp", All).GetValue(vm, null))
                throw new Exception("Navigation exceeded authored count");
            // Atlas timestamps can't silently replace the configured strip source
            using (var atlas = new Bitmap(1380, 1035))
            {
                atlas.SetPixel(0, 0, Color.Red);
                atlas.Save(Path.Combine(root, "level.png"));
            }

            File.SetLastWriteTimeUtc(Path.Combine(root, "level.png"), DateTime.UtcNow.AddMinutes(1));
            type.GetMethod("InitializeHitbox", All).Invoke(vm, null);
            var image = (Bitmap)type.GetProperty("GdiImage").GetValue(vm, null);
            if (image.GetPixel(0, 511 * 45).ToArgb() != Color.Blue.ToArgb())
                throw new Exception("Timestamp changed source authority");
            image.Dispose();
            using (var fixture = new Bitmap(60, 512 * 45))
            {
                for (int y = 44; y < 92; y++)
                    for (int x = 0; x < 60; x++)
                        fixture.SetPixel(x, y, ((x + y) % 3 == 0) ? Color.Red : ((x + y) % 3 == 1) ? Color.Black : Color.Transparent);
                type.GetField("gdiImage", All).SetValue(vm, fixture);
                var method = type.GetMethod("GetPreview");
                var area = new Rectangle(0, 45, 60, 45);
                using (var fast = (Bitmap)method.Invoke(vm, new object[]{area}))
                {
                    var harmony = new Harmony("worldsmith.extension");
                    harmony.Unpatch(method, HarmonyPatchType.Prefix, "worldsmith.extension");
                    try
                    {
                        using (var stock = (Bitmap)method.Invoke(vm, new object[]{area}))
                            for (int y = 0; y < 360; y++)
                                for (int x = 0; x < 480; x++)
                                    if (stock.GetPixel(x, y).ToArgb() != fast.GetPixel(x, y).ToArgb())
                                        throw new Exception("Optimized slope preview differs at " + x + "," + y);
                    }
                    finally
                    {
                        harmony.Patch(method, new HarmonyMethod(engine.GetType("WorldsmithExtension.CollisionPatches").GetMethod("Preview", All)));
                    }
                }
            }

            var watch = Stopwatch.StartNew();
            bool ready = (bool)native.GetType("JKWorldsmith.Models.FileWatcher.ProperFileWatcher").GetMethod("WaitForFile").Invoke(null, new object[]{new FileInfo(Path.Combine(root, "missing.png")), 10});
            if (ready || watch.ElapsedMilliseconds > 250)
                throw new Exception("Missing-file wait regressed");
            string delayed = Path.Combine(root, "external-save.xml");
            var locked = new FileStream(delayed, FileMode.Create, FileAccess.Write, FileShare.None);
            var writer = System.Threading.Tasks.Task.Run(() => { System.Threading.Thread.Sleep(850); locked.WriteByte(42); locked.Dispose(); });
            bool available = (bool)native.GetType("JKWorldsmith.Models.FileWatcher.ProperFileWatcher").GetMethod("WaitForFile").Invoke(null, new object[] { new FileInfo(delayed), 10 });
            writer.Wait();
            if (!available) throw new Exception("External saves held open for more than half a second are dropped");
            var builder = engine.GetType("WorldsmithExtension.Builder");
            string buildSource = Path.Combine(root, "build-source"), buildOutput = Path.Combine(root, "build-stage", "content");
            string[] emptyFolders = {"props/textures/old_man/lines", "props/textures/old_man/merchant", "resources/empty/nested"};
            foreach (string folder in emptyFolders)
                Directory.CreateDirectory(Path.Combine(buildSource, folder));
            Directory.CreateDirectory(Path.Combine(buildSource, "bin", "ignored-empty"));
            File.WriteAllText(Path.Combine(buildSource, "resource.xml"), "<Resource/>");
            string controller = Path.Combine("jk-runtime", "modules", "map-controller", "MapController.jkmod");
            string dependency = Path.Combine("jk-runtime", "modules", "map-controller", "MapDependency.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(buildSource, controller)));
            byte[] controllerBytes = { 77, 90, 1, 2, 3 }, dependencyBytes = { 77, 90, 4, 5, 6 };
            File.WriteAllBytes(Path.Combine(buildSource, controller), controllerBytes);
            File.WriteAllBytes(Path.Combine(buildSource, dependency), dependencyBytes);
            string wallSource = Path.Combine(buildSource, "props", "hidden_walls", "hidden_wall1.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(wallSource));
            string wallXml = "<RaymanCollection><walls><RaymanData><texture_name>first-screen</texture_name><use_screen_coords>true</use_screen_coords><Position><X>-17</X><Y>283</Y></Position><hitboxes><Rectangle><X>9</X><Y>16</Y><Width>45</Width><Height>29</Height></Rectangle></hitboxes></RaymanData></walls></RaymanCollection>";
            File.WriteAllText(wallSource, wallXml);
            var wallCollection = native.GetType("JKWorldsmith.Shared.Structs.RaymanCollection");
            object wallData;
            using (var reader = new StringReader(wallXml))
                wallData = new System.Xml.Serialization.XmlSerializer(wallCollection).Deserialize(reader);
            var walls = (System.Collections.IList)wallCollection.GetMethod("GetObjectsFromList").Invoke(wallData, null);
            if ((int)walls[0].GetType().GetProperty("X").GetValue(walls[0], null) != -17 || (int)walls[0].GetType().GetProperty("Y").GetValue(walls[0], null) != 283 || !(bool)walls[0].GetType().GetProperty("UseScreenCoords").GetValue(walls[0], null))
                throw new Exception("Loading a first-screen hidden wall changed its coordinates");
            builder.GetMethod("Build", All).Invoke(null, new object[]{buildSource, buildOutput, new Action<string>(text => {})});
            if (File.ReadAllText(Path.Combine(buildOutput, "props", "hidden_walls", "hidden_wall1.xml")) != wallXml)
                throw new Exception("Building changed hidden wall placement XML");
            foreach (string module in new[] { controller, dependency })
                if (!System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(Path.Combine(buildSource, module)), File.ReadAllBytes(Path.Combine(buildOutput, module))))
                    throw new Exception("Building changed a bundled map module or dependency");
            string packedSource = Path.Combine(root, "packed-source"), packedOutput = Path.Combine(root, "packed-stage", "content");
            Directory.CreateDirectory(packedSource);
            File.WriteAllText(Path.Combine(packedSource, "original.xnb"), "compiled bytes must not be recompiled");
            File.WriteAllText(Path.Combine(packedSource, "original.png"), "unused source");
            engine.GetType("WorldsmithExtension.BuildJob").GetMethod("Run", All).Invoke(null, new object[]{packedSource, "Level", packedOutput, new Action<string>(text => {})});
            if (File.ReadAllText(Path.Combine(packedOutput, "original.xnb")) != "compiled bytes must not be recompiled")
                throw new Exception("Testing a compiled package did not preserve its compiled content");

            string mixedSource = Path.Combine(root, "editable-mixed"), mixedOutput = Path.Combine(root, "editable-mixed-build", "content");
            Directory.CreateDirectory(mixedSource);
            using (var sourceImage = new Bitmap(3, 3))
            {
                sourceImage.SetPixel(0, 0, Color.Blue);
                sourceImage.Save(Path.Combine(mixedSource, "original.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            File.WriteAllText(Path.Combine(mixedSource, "original.xnb"), "stale compiled data");
            engine.GetType("WorldsmithExtension.BuildJob").GetMethod("Run", All).Invoke(null, new object[] { mixedSource, "Skin", mixedOutput, new Action<string>(Console.WriteLine) });
            if (File.ReadAllText(Path.Combine(mixedOutput, "original.xnb")) == "stale compiled data") throw new Exception("Common build plan retained stale compiled output for editable source");
            foreach (string copy in new[]{Path.Combine(root, "build-stage", "input"), buildOutput})
            {
                foreach (string folder in emptyFolders)
                    if (!Directory.Exists(Path.Combine(copy, folder)))
                        throw new Exception("Build lost an empty resource directory: " + folder);
                if (Directory.Exists(Path.Combine(copy, "bin")))
                    throw new Exception("Build included excluded directories");
            }
            if (File.ReadAllText(Path.Combine(buildOutput, "resource.xml")) != "<Resource/>")
                throw new Exception("Build lost resource content");
            engine.GetType("WorldsmithExtension.NativeCompiler").GetMethod("Configure", All).Invoke(null, new object[]{root, Path.Combine(root, "output")});
            bool rejected = false;
            try
            {
                native.GetType("JKWorldsmith.Models.XNBConverters").GetMethod("ToTexture2D").Invoke(null, new object[]{Path.Combine(root, "missing.png"), Path.Combine(root, "output", "missing.xnb")});
            }
            catch (TargetInvocationException)
            {
                rejected = true;
            }

            if (!rejected)
                throw new Exception("Missing texture was silently accepted");
            string props = Path.Combine(root, "props"), propFile = Path.Combine(props, "prop7.xml");
            string source = "<PropCollection><screen>7</screen><Props><PropData><type>unresolved-first</type><Position><X>12</X><Y>34</Y></Position><flipped>true</flipped></PropData><PropData><type>unresolved-second</type><Position><X>56</X><Y>78</Y></Position><flipped>false</flipped></PropData></Props></PropCollection>";
            File.WriteAllText(propFile, source);
            var propsType = native.GetType("JKWorldsmith.Models.PageModels.Screen.PropsModel");
            object propsModel = FormatterServices.GetUninitializedObject(propsType);
            propsType.GetProperty("Folder").SetValue(propsModel, props, null);
            object settings = Activator.CreateInstance(native.GetType("JKWorldsmith.Models.PageModels.Screen.BindingSettings"));
            var loaded = (System.Collections.IList)propsType.GetMethod("GetProps", All).Invoke(propsModel, new[]{(object)new DirectoryInfo(props), settings});
            if (File.ReadAllText(propFile) != source)
                throw new Exception("Opening a project rewrote prop source");
            var placements = (System.Collections.IList)loaded[0].GetType().GetProperty("List").GetValue(loaded[0], null);
            if (placements.Count != 2)
                throw new Exception("Unresolved props were removed");
            placements[0].GetType().GetProperty("X").SetValue(placements[0], 99.0, null);
            string saved = File.ReadAllText(propFile);
            if (!saved.Contains("unresolved-second") || !saved.Contains("<screen>7</screen>") || !saved.Contains("<X>99</X>"))
                throw new Exception("Editing one prop lost another unresolved prop or screen number");
            string hiddenProps = Path.Combine(root, "props", "hidden wall props");
            Directory.CreateDirectory(hiddenProps);
            string hiddenPropFile = Path.Combine(hiddenProps, "prop1.xml");
            string hiddenPropXml = source.Replace("<screen>7</screen>", "<screen>1</screen>").Replace("<X>12</X>", "<X>-17</X>").Replace("<Y>34</Y>", "<Y>283</Y>");
            File.WriteAllText(hiddenPropFile, hiddenPropXml);
            var hidden = (System.Collections.IList)propsType.GetMethod("GetProps", All).Invoke(propsModel, new[]{(object)new DirectoryInfo(hiddenProps), settings});
            var hiddenPlacements = (System.Collections.IList)hidden[0].GetType().GetProperty("List").GetValue(hidden[0], null);
            if ((double)hiddenPlacements[0].GetType().GetProperty("X").GetValue(hiddenPlacements[0], null) != -17 || (double)hiddenPlacements[0].GetType().GetProperty("Y").GetValue(hiddenPlacements[0], null) != 283 || File.ReadAllText(hiddenPropFile) != hiddenPropXml)
                throw new Exception("Loading first-screen hidden props changed their coordinates or XML");
            Directory.CreateDirectory(Path.Combine(props, "textures"));
            File.WriteAllText(Path.Combine(props, "textures", "prop_settings.xml"), "<broken");
            rejected = false;
            try
            {
                propsType.GetMethod("GetPropSettings", All).Invoke(propsModel, null);
            }
            catch (TargetInvocationException)
            {
                rejected = true;
            }

            if (!rejected)
                throw new Exception("Malformed settings silently became an empty list");
            rejected = false;
            try
            {
                placements[0].GetType().GetProperty("X").SetValue(placements[0], 100.0, null);
            }
            catch (TargetInvocationException)
            {
                rejected = true;
            }

            if (!rejected || File.ReadAllText(propFile) != saved)
                throw new Exception("Failed load allowed stale prop state to overwrite files");
            // exercise layout measurement without showing a window or starting Steam
            var parent = new System.Windows.Controls.Grid();
            parent.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition{Width = new System.Windows.GridLength(40)});
            parent.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());
            var page = new System.Windows.Controls.Grid();
            var footer = new System.Windows.Controls.Border{Height = 70};
            System.Windows.Controls.Grid.SetColumn(page, 1);
            parent.Children.Add(page);
            engine.GetType("WorldsmithExtension.EditorUI").GetMethod("ReserveFooter", All).Invoke(null, new object[]{page, footer});
            parent.Measure(new System.Windows.Size(800, 600));
            parent.Arrange(new System.Windows.Rect(0, 0, 800, 600));
            if (page.ActualHeight != 530 || footer.ActualHeight != 70 || page.ActualWidth != 760)
                throw new Exception("Toolbar failed to reserve page space");
            var spriteType = native.GetType("JKWorldsmith.Controls.SpriteImage");
            object sprite = Activator.CreateInstance(spriteType);
            var images = new System.Collections.Generic.List<System.Windows.Media.ImageSource>();
            for (int i = 0; i < 3; i++)
                images.Add(System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, new byte[]{(byte)i, 0, 0, 255}, 4));
            spriteType.GetProperty("Frames").SetValue(sprite, images, null);
            spriteType.GetProperty("TimerInterval").SetValue(sprite, 0f, null);
            var advance = engine.GetType("WorldsmithExtension.SpritePlayback").GetMethod("Advance", All);
            watch.Restart();
            advance.Invoke(null, new[]{sprite, (object)0.5f});
            if (watch.ElapsedMilliseconds > 250 || (int)spriteType.GetProperty("Index").GetValue(sprite, null) != 0)
                throw new Exception("Zero animation interval must freeze safely");
            spriteType.GetProperty("TimerInterval").SetValue(sprite, 0.1f, null);
            advance.Invoke(null, new[]{sprite, (object)0.25f});
            if ((int)spriteType.GetProperty("Index").GetValue(sprite, null) != 2)
                throw new Exception("Animation failed to skip to the correct frame");
            spriteType.GetProperty("Index").SetValue(sprite, 0, null);
            spriteType.GetField("m_timer", All).SetValue(sprite, 0d);
            spriteType.GetProperty("Floats").SetValue(sprite, new System.Collections.Generic.List<float>{0.1f, 0.2f, 0.3f}, null);
            advance.Invoke(null, new[]{sprite, (object)0.35f});
            if ((int)spriteType.GetProperty("Index").GetValue(sprite, null) != 2 || (int)spriteType.GetProperty("FloatIndex").GetValue(sprite, null) != 2)
                throw new Exception("Variable animation advanced its timing twice");
            var delta = native.GetType("JKWorldsmith.Models.DeltaManager").GetField("OnUpdate", All);
            int subscriptions = ((Delegate)delta.GetValue(null)).GetInvocationList().Length;
            spriteType.GetMethod("StartAnimation").Invoke(sprite, null);
            spriteType.GetMethod("StartAnimation").Invoke(sprite, null);
            if (((Delegate)delta.GetValue(null)).GetInvocationList().Length != subscriptions + 1)
                throw new Exception("Duplicate animation subscription");
            spriteType.GetMethod("StopAnimation").Invoke(sprite, null);
            // binding changes during load must never save wardrobe defaults
            var loading = engine.GetType("WorldsmithExtension.LoadState");
            loading.GetField("Busy", All).SetValue(null, true);
            try
            {
                object wardrobe = FormatterServices.GetUninitializedObject(native.GetType("JKWorldsmith.ViewModels.Level.WardrobeViewModel"));
                foreach (string save in new[]{"SaveSkinSettings", "SaveCosmeticSettings", "SaveSetSettings"})
                    wardrobe.GetType().GetMethod(save, All).Invoke(wardrobe, new object[]{null, null});
            }
            finally
            {
                loading.GetField("Busy", All).SetValue(null, false);
            }

            var safety = engine.GetType("WorldsmithExtension.FileSafety");
            string kept = Path.Combine(root, "kept.png");
            File.WriteAllText(kept, "previous image");
            rejected = false;
            try
            {
                safety.GetMethod("Copy", All).Invoke(null, new object[]{Path.Combine(root, "missing-replacement.png"), kept});
            }
            catch (TargetInvocationException)
            {
                rejected = true;
            }

            if (!rejected || File.ReadAllText(kept) != "previous image")
                throw new Exception("Failed replacement removed the original");
            string incoming = Path.Combine(root, "incoming.png");
            File.WriteAllText(incoming, "replacement image");
            native.GetType("JKWorldsmith.Extensions.IOWrappers").GetMethod("OverridePreviousFile").Invoke(null, new object[]{kept, incoming});
            if (File.ReadAllText(kept) != "replacement image" || File.ReadAllText(kept + ".worldsmith-backup") != "previous image")
                throw new Exception("Native replacement was not atomic with a backup");
            string removed = (string)safety.GetMethod("Archive", All).Invoke(null, new object[]{root, kept});
            if (File.Exists(kept) || File.ReadAllText(removed) != "replacement image")
                throw new Exception("Deleted asset was not recoverable");
            rejected = false;
            try
            {
                safety.GetMethod("Archive", All).Invoke(null, new object[]{root, Path.Combine(root, "..", "outside.png")});
            }
            catch (TargetInvocationException)
            {
                rejected = true;
            }

            if (!rejected)
                throw new Exception("Deletion escaped the source folder");
            var projectType = native.GetType("JKWorldsmith.Models.Projects.CurrentProjectManager");
            object current = FormatterServices.GetUninitializedObject(projectType);
            projectType.BaseType.GetField("directory", All).SetValue(current, root);
            object[] compiled = {Path.Combine(root, "textures", "sprite.png"), null};
            projectType.GetMethod("GetCompiledFromRelative", All).Invoke(current, compiled);
            if ((string)compiled[1] != Path.Combine(root, "bin", "textures", "sprite.xnb"))
                throw new Exception("Compiled asset path mapping failed");
            compiled = new object[]{Path.Combine(root, "..", "outside.png"), null};
            rejected = false;
            try
            {
                projectType.GetMethod("GetCompiledFromRelative", All).Invoke(current, compiled);
            }
            catch (TargetInvocationException)
            {
                rejected = true;
            }

            if (!rejected)
                throw new Exception("Compiled asset path escaped the project");
            string mod = Path.Combine(root, "mod-package");
            Directory.CreateDirectory(mod);
            File.Copy(typeof(NativeContracts).Assembly.Location, Path.Combine(mod, "Example.dll"));
            object[] detect = {mod, null};
            object category = native.GetType("JKWorldsmith.Shared.Workshop.Workshop").GetMethod("IsValidType").Invoke(null, detect);
            if (category == null || category.ToString() != "Mod")
                throw new Exception("Compiled mods are not recognized as projects");
            object adaptedCategory = engine.GetType("WorldsmithExtension.NativeProjects").GetMethod("Category", All).Invoke(null, new object[] { mod });
            if (adaptedCategory.ToString() != "Mod") throw new Exception("Project adapter changed the detected category");
            string archivedProject = Path.Combine(root, "archive-fixture");
            Directory.CreateDirectory(archivedProject);
            File.WriteAllText(Path.Combine(archivedProject, "source.txt"), "retained");
            string archiveLocation = (string)engine.GetType("WorldsmithExtension.ProjectArchive").GetMethod("Move", All).Invoke(null, new object[]{archivedProject});
            if (Directory.Exists(archivedProject) || File.ReadAllText(Path.Combine(archiveLocation, "source.txt")) != "retained")
                throw new Exception("Project archive lost its source files");
            var updateUi = engine.GetType("WorldsmithExtension.UpdateUI");
            updateUi.GetField("started", All).SetValue(null, true);
            var home = new StackPanel();
            var openRow = new Grid();
            var openButton = new System.Windows.Controls.Button();
            openRow.Children.Add(openButton);
            home.Children.Add(openRow);
            updateUi.GetMethod("Attach", All).Invoke(null, new object[]{openButton});
            if (openRow.Children.Count != 1 || home.Children.Count != 2 || !(home.Children[1] is WrapPanel))
                throw new Exception("Update controls overlap the native project buttons");
            var workshopUi = engine.GetType("WorldsmithExtension.WorkshopUI");
            var workshopPage = new Page();
            var nativeContent = new Grid();
            var workshopItems = new ListView();
            nativeContent.Children.Add(workshopItems);
            workshopPage.Content = nativeContent;
            workshopUi.GetMethod("Wrap", All).Invoke(null, new object[] { workshopPage, workshopItems });
            var workshopLayout = workshopPage.Content as Grid;
            if (workshopLayout == null || workshopLayout.RowDefinitions.Count != 3 || Grid.GetRow(nativeContent) != 1 || !workshopLayout.Children.Contains(nativeContent))
                throw new Exception("Workshop actions did not reserve space around the native item list");
            workshopUi.GetMethod("Wrap", All).Invoke(null, new object[] { workshopPage, workshopItems });
            if (!Object.ReferenceEquals(workshopPage.Content, workshopLayout)) throw new Exception("Workshop actions were attached twice");
            var nativeDetails = native.GetType("JKWorldsmith.Models.Steamworks.SteamUGCDetails");
            var detailsConstructor = nativeDetails.GetConstructors()[0];
            var detailsStruct = Activator.CreateInstance(detailsConstructor.GetParameters()[0].ParameterType);
            foreach (var field in detailsStruct.GetType().GetFields(All))
                if (field.FieldType == typeof(byte[]))
                {
                    var marshal = (System.Runtime.InteropServices.MarshalAsAttribute)Attribute.GetCustomAttribute(field, typeof(System.Runtime.InteropServices.MarshalAsAttribute));
                    field.SetValue(detailsStruct, new byte[marshal.SizeConst]);
                }
            // fill the marshalled buffers like Steam does
            // this SDK's string setters only replace a local buffer variable, not the struct
            Action<string, string> setSteamText = (field, value) =>
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value);
                Buffer.BlockCopy(bytes, 0, (byte[])detailsStruct.GetType().GetField(field, All).GetValue(detailsStruct), 0, bytes.Length);
            };
            setSteamText("m_rgchTitle_", "Workshop title");
            setSteamText("m_rgchDescription_", "Original description");
            setSteamText("m_rgchTags_", "Mod,Interface");
            detailsStruct.GetType().GetField("m_ulSteamIDOwner").SetValue(detailsStruct, (ulong)123);
            var publishedIdField = detailsStruct.GetType().GetField("m_nPublishedFileId");
            publishedIdField.SetValue(detailsStruct, Activator.CreateInstance(publishedIdField.FieldType, new object[] { (ulong)42 }));
            var metadata = engine.GetType("WorldsmithExtension.NativeWorkshop").GetMethod("FromNative", All).Invoke(null, new object[] { detailsConstructor.Invoke(new[] { detailsStruct }) });
            if ((string)metadata.GetType().GetField("Title", All).GetValue(metadata) != "Workshop title" || (string)metadata.GetType().GetField("Description", All).GetValue(metadata) != "Original description" || (string)metadata.GetType().GetField("Category", All).GetValue(metadata) != "Mod")
                throw new Exception("Native Workshop details lost their title description or type: " + metadata.GetType().GetField("Title", All).GetValue(metadata) + " / " + metadata.GetType().GetField("Description", All).GetValue(metadata) + " / " + metadata.GetType().GetField("Category", All).GetValue(metadata));
            if ((ulong)metadata.GetType().GetField("Id", All).GetValue(metadata) != 42 || (ulong)metadata.GetType().GetField("Owner", All).GetValue(metadata) != 123)
                throw new Exception("Native Workshop identity or ownership changed during conversion");
            var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            var dispatchFrame = new System.Windows.Threading.DispatcherFrame();
            var releaseWorker = new System.Threading.ManualResetEventSlim(false);
            int uiThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            bool workerWasBackground = false, completionWasUI = false, uiResponded = false;
            var schedule = (Action<Action, Action<Exception>>)engine.GetType("WorldsmithExtension.BackgroundWork").GetMethod("On", All).Invoke(null, new object[] { dispatcher });
            Exception backgroundError = null;
            schedule(() =>
            {
                workerWasBackground = System.Threading.Thread.CurrentThread.ManagedThreadId != uiThread;
                if (!releaseWorker.Wait(TimeSpan.FromSeconds(5))) throw new Exception("UI did not respond while background work was pending");
            }, error =>
            {
                backgroundError = error;
                completionWasUI = System.Threading.Thread.CurrentThread.ManagedThreadId == uiThread;
                dispatchFrame.Continue = false;
            });
            dispatcher.BeginInvoke(new Action(() => { uiResponded = true; releaseWorker.Set(); }));
            System.Windows.Threading.Dispatcher.PushFrame(dispatchFrame);
            releaseWorker.Dispose();
            if (backgroundError != null || !workerWasBackground || !completionWasUI || !uiResponded)
                throw new Exception("Background workflow blocked the dispatcher or completed on the wrong thread", backgroundError);
            var gameTest = engine.GetType("WorldsmithExtension.ModInstallation");
            string fakeSteam = Path.Combine(root, "steamapps"), fakeGame = Path.Combine(fakeSteam, "common", "Jump King");
            Directory.CreateDirectory(fakeGame);
            string installed = Path.Combine(fakeSteam, "workshop", "content", "1061090", "123");
            Directory.CreateDirectory(installed);
            File.WriteAllText(Path.Combine(installed, "Example.dll"), "old dll");
            string picked = (string)gameTest.GetMethod("SelectTarget", All).Invoke(null, new object[]{fakeGame, mod});
            if (picked != installed)
                throw new Exception("Test install ignored an existing Workshop copy");
            File.WriteAllText(Path.Combine(installed, "settings.json"), "user settings");
            File.WriteAllText(Path.Combine(mod, "settings.json"), "default settings");
            string rollback = Path.Combine(root, "install-backup");
            gameTest.GetMethod("Install", All).Invoke(null, new object[]{mod, installed, rollback, new Action<string>(text =>
            {
            }), null});
            if (File.ReadAllText(Path.Combine(installed, "settings.json")) != "user settings" || File.ReadAllText(Path.Combine(rollback, "Example.dll")) != "old dll")
                throw new Exception("Test install lost settings or rollback files");
            byte[] beforeInstall = File.ReadAllBytes(Path.Combine(installed, "Example.dll"));
            File.WriteAllText(Path.Combine(mod, "Example.dll"), "new dll");
            File.WriteAllText(Path.Combine(mod, "z.txt"), "fail here");
            rejected = false;
            try
            {
                gameTest.GetMethod("Install", All).Invoke(null, new object[]{mod, installed, Path.Combine(root, "failed-install-backup"), new Action<string>(text =>
                {
                    if (text.Contains("z.txt"))
                        throw new IOException("simulated failure");
                }), null});
            }
            catch (TargetInvocationException)
            {
                rejected = true;
            }

            if (!rejected || !System.Linq.Enumerable.SequenceEqual(beforeInstall, File.ReadAllBytes(Path.Combine(installed, "Example.dll"))))
                throw new Exception("Failed test install did not roll back replaced DLLs");
            Typography.Run(engine, native);
            ProjectLoading.Run(engine, native, root);
            ProjectHistory.Run(engine, native, root);
            ScrollingLoading.Run(native, root);
            RightGrowing.Run(engine, native, root);
            Console.WriteLine("[OK] Installed methods: 512-screen loading, conversion, navigation, source authority, missing-file wait and compiler errors. No UI or Steam session started.");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }
}
