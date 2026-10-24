using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MegaMappingExpansion.Api;
using MegaMappingExpansion.Construction;

namespace MegaMappingExpansion
{
    internal static class AuthoringLessons
    {
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Color Background = new Color(20, 27, 40);
        private sealed class DeviceService : IGraphicsDeviceService
        {
            public GraphicsDevice GraphicsDevice { get; set; }
            public event EventHandler<EventArgs> DeviceCreated { add { } remove { } }
            public event EventHandler<EventArgs> DeviceDisposing { add { } remove { } }
            public event EventHandler<EventArgs> DeviceReset { add { } remove { } }
            public event EventHandler<EventArgs> DeviceResetting { add { } remove { } }
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception(message); Console.WriteLine("PASS: " + message); }
        private static int Main(string[] args)
        {
            try { Run(Path.GetFullPath(args[0]), args[1], Path.GetFullPath(args[2])); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        private static Color[] Draw(Game1 game, GraphicsDevice device, RenderTarget2D target, NativeRenderer renderer, string path)
        {
            device.SetRenderTarget(target);
            device.Clear(Background);
            renderer.Tick();
            game.StartBatch();
            foreach (string layer in new[] { "background", "world", "surface", "foreground", "composite", "hud" })
                renderer.DrawLayer(layer, 1);
            game.EndBatch();
            var pixels = new Color[480 * 360];
            target.GetData(pixels);
            if (path != null) using (var stream = File.Create(path)) target.SaveAsPng(stream, 480, 360);
            return pixels;
        }
        private static void Run(string output, string gameDir, string lessons)
        {
            Directory.CreateDirectory(output);
            using (var window = new Form { ShowInTaskbar = false })
            using (var device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef,
                new PresentationParameters { DeviceWindowHandle = window.Handle, BackBufferWidth = 480, BackBufferHeight = 360 }))
            using (var target = new RenderTarget2D(device, 480, 360, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents))
            using (var batch = new SpriteBatch(device))
            {
                // use the same isolated host as the native graphics fixtures; never touch a user's save
                var services = new GameServiceContainer();
                services.AddService(typeof(IGraphicsDeviceService), new DeviceService { GraphicsDevice = device });
                var game = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
                typeof(Game1).GetField("_instance", All).SetValue(null, game);
                typeof(Game).GetField("_services", All).SetValue(game, services);
                foreach (var field in typeof(Game).GetFields(All).Where(f => f.FieldType == typeof(IGraphicsDeviceService)))
                    field.SetValue(game, services.GetService(typeof(IGraphicsDeviceService)));
                game.contentManager = new JKContentManager();
                Game1.spriteBatch = batch;
                using (var content = new ContentManager(game.Services, Path.Combine(gameDir, "Content")))
                {
                    game.contentManager.font = new JKContentManager.Font();
                    game.contentManager.font.MenuFont = content.Load<SpriteFont>("font/sf_litter_lover2_bold");
                    string[] names = { "01-image", "02-placement", "03-animation", "04-area-caption", "05-jump-counter", "06-jump-sound", "07-saved-counter" };
                    foreach (string name in names)
                    {
                        string root = Path.Combine(lessons, name);
                        var document = new RecipeCompiler(PrimitiveRegistry.Create()).Read(root);
                        using (var scene = new SceneProgram(document))
                        using (var renderer = new NativeRenderer(scene, device, root))
                        {
                            int audioActions = 0;
                            scene.InvokeNative = action => { Check(action.Operation == "playAudio" && action.Target == "chime", name + " routes the declared audio voice"); audioActions++; };
                            scene.Tick(0, 0);
                            if (name == "04-area-caption")
                            {
                                var actor = new ActorObservation { Id = "player", Screen = 1, Bounds = new SceneBox { X = 220, Y = 280, Width = 20, Height = 28 }, Velocity = SceneValue.Vector(0, 0) };
                                scene.ObservationPoses.Refresh();
                                scene.Sensors.Observe(actor, new SurfaceObservation[0], "scene", 1.0 / 60);
                                scene.Tick(1.0 / 60, 1.0 / 60);
                                Check(scene.Properties.Read("captionPose", "visible").X == 1, "Area lesson shows its caption inside");
                                Draw(game, device, target, renderer, Path.Combine(output, "lesson-04-inside.png"));
                                actor.Screen = 2;
                                scene.Sensors.Observe(actor, new SurfaceObservation[0], "scene", 1.0 / 60);
                                scene.Tick(1.0 / 60, 1.0 / 60);
                                Check(scene.Properties.Read("captionPose", "visible").X == 0, "Area lesson hides its caption outside");
                                Check(Draw(game, device, target, renderer, null).All(p => p == Background), "Hidden area caption leaves the canvas empty");
                                continue;
                            }
                            if (name.CompareTo("05-jump-counter") >= 0)
                            {
                                Check(scene.Properties.Read("caption", "value").Text == "Jumps: 0", name + " starts at zero");
                                for (int i = 0; i < 3; i++)
                                {
                                    scene.Signals.Emit("jump", "lesson-check", "player", 1, scene.GameplayTime);
                                    scene.Tick(1.0 / 60, 1.0 / 60);
                                }
                                Check(scene.ReadVariable("jumps").X == 3 && scene.Properties.Read("caption", "value").Text == "Jumps: 3", name + " displays three jump events");
                                if (name != "05-jump-counter") Check(audioActions == 3, name + " requests one audio action per jump");
                                if (name == "07-saved-counter")
                                {
                                    string save = Path.Combine(output, "isolated-save");
                                    Directory.CreateDirectory(save);
                                    var store = new ScenePersistence(document.SaveId, () => save);
                                    store.Save(scene.SavedVariables());
                                    using (var restarted = new SceneProgram(document))
                                    {
                                        restarted.LoadVariables(store.Load());
                                        restarted.Tick(0, 0);
                                        Check(restarted.ReadVariable("jumps").X == 3, "Save lesson reloads its total in a fresh scene");
                                    }
                                    ScenePersistence.ResetContext(save);
                                    Check(new ScenePersistence(document.SaveId, () => save).Load().Count == 0, "Native reset epoch clears the lesson total");
                                }
                                else
                                {
                                    using (var restarted = new SceneProgram(document))
                                        Check(restarted.ReadVariable("jumps").X == 0 && scene.SavedVariables().Count == 0, name + " resets its Attempt counter");
                                }
                            }
                            var pixels = Draw(game, device, target, renderer, Path.Combine(output, "lesson-" + name.Substring(0, 2) + ".png"));
                            Check(pixels.Any(p => p != Background), name + " draws visible output");
                            if (name == "01-image")
                                Check(pixels.Take(160 * 480).All(p => p == Background) && pixels[0] == Background, "First panel stays below its authored top-left anchor");
                            if (name == "03-animation")
                            {
                                for (int i = 0; i < 15; i++) scene.Tick(1.0 / 60, 1.0 / 60);
                                var advanced = Draw(game, device, target, renderer, Path.Combine(output, "lesson-03-next.png"));
                                Check(!pixels.SequenceEqual(advanced), "Animation lesson changes its atlas cell over time");
                            }
                        }
                    }
                }
            }
        }
    }
}
