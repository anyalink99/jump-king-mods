using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;

namespace HammerKing
{
    internal static class Preview
    {
        private sealed class PreviewPixel : PixelTexture
        {
            internal PreviewPixel(Texture2D pixel) { _texture = pixel; }
            protected override void CreatePixel() { }
            protected override void UnloadPixel() { }
        }
        private sealed class DeviceService : IGraphicsDeviceService
        {
            public GraphicsDevice GraphicsDevice { get; set; }
            public event EventHandler<EventArgs> DeviceCreated { add { } remove { } }
            public event EventHandler<EventArgs> DeviceDisposing { add { } remove { } }
            public event EventHandler<EventArgs> DeviceReset { add { } remove { } }
            public event EventHandler<EventArgs> DeviceResetting { add { } remove { } }
        }
        [STAThread]
        public static int Main(string[] args)
        {
            try { Render(args[0], args[1]); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        private static void Render(string gameDir, string output)
        {
            using (var window = new Form { ShowInTaskbar = false })
            using (var device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef,
                new PresentationParameters { DeviceWindowHandle = window.Handle, BackBufferWidth = 480, BackBufferHeight = 360, IsFullScreen = false }))
            using (var batch = new SpriteBatch(device))
            using (var target = new RenderTarget2D(device, 480, 360))
            using (var pixel = new Texture2D(device, 1, 1))
            {
                var services = new GameServiceContainer();
                services.AddService(typeof(IGraphicsDeviceService), new DeviceService { GraphicsDevice = device });
                using (var content = new ContentManager(services, gameDir))
                {
                    Texture2D atlas = content.Load<Texture2D>("Content/king/base");
                    SpriteFont font = content.Load<SpriteFont>("Content/font/sf_small");
                    Sprite king = new JumpKing.JKMemory.KingSprites(atlas).regular.GetSprite(JumpKing.JKMemory.KingSpriteLayers.Regular.SpriteKey.idle);
                    pixel.SetData(new[] { Color.White });
                    var game = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
                    game.contentManager = (JKContentManager)FormatterServices.GetUninitializedObject(typeof(JKContentManager));
                    game.contentManager.Pixel = new PreviewPixel(pixel);
                    typeof(Game1).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, game);
                    Game1.spriteBatch = batch;
                    VerifyModalResume(king, batch, target, device);
                    device.SetRenderTarget(target);
                    device.Clear(new Color(20, 25, 34));
                    batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
                    batch.DrawString(font, "HAMMER KING / PHYSICS PROTOTYPE", new Vector2(18, 15), new Color(238, 209, 151));
                    Vector2[] feet = { new Vector2(85, 145), new Vector2(330, 145), new Vector2(85, 315), new Vector2(330, 310) };
                    Vector2[] offsets = { new Vector2(22, -12), new Vector2(0, 11), new Vector2(0, -8), new Vector2(-20, -18) };
                    string[] labels = { "EXTENDED", "UNDER-BODY PUSH", "RETRACTED ABOVE", "LEDGE PULL" };
                    for (int i = 0; i < feet.Length; i++)
                    {
                        Vector2 pivot = feet[i] + new Vector2(0, -13);
                        Vector2 head = pivot + offsets[i];
                        if (i == 1) batch.Draw(pixel, new Rectangle(260, 145, 160, 8), new Color(66, 77, 85));
                        if (i == 2) batch.Draw(pixel, new Rectangle(25, 315, 160, 8), new Color(66, 77, 85));
                        if (i == 3) batch.Draw(pixel, new Rectangle((int)head.X - 26, (int)head.Y + 2, 26, 8), new Color(66, 77, 85));
                        new HammerSprite(king, new HammerPhysics { Ready = true, Head = head, Contact = i == 1 || i == 3 })
                            .Draw(feet[i], i == 3 ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
                        batch.DrawString(font, labels[i], new Vector2(i % 2 == 0 ? 25 : 265, i < 2 ? 52 : 210), new Color(186, 192, 190));
                    }
                    batch.End();
                    device.SetRenderTarget(null);
                    var colors = new Color[480 * 360]; target.GetData(colors);
                    var kingPixels = new Color[king.source.Width * king.source.Height];
                    atlas.GetData(0, king.source, kingPixels, 0, kingPixels.Length);
                    int verified = 0;
                    for (int pose = 0; pose < feet.Length; pose++)
                    {
                        Point top = (feet[pose] - king.source.Size.ToVector2() * king.center).ToPoint();
                        for (int y = 0; y < king.source.Height; y++)
                        for (int x = 0; x < king.source.Width; x++)
                        {
                            Color expected = kingPixels[y * king.source.Width + (pose == 3 ? king.source.Width - 1 - x : x)];
                            if (expected.A != 255) continue;
                            if (colors[(top.Y + y) * 480 + top.X + x] != expected)
                                throw new Exception("Hammer obscures an opaque native king pixel");
                            verified++;
                        }
                    }
                    Console.WriteLine("[OK] Production sprite layering preserves " + verified + " opaque king pixels, including retracted overlap");
                    int metal = 0;
                    foreach (Color color in colors) if (color.R > 100 && color.G > 110 && color.B > 120) metal++;
                    if (metal < 80) throw new Exception("Hammer preview is blank or missing metal pixels");
                    using (var stream = File.Create(output)) target.SaveAsPng(stream, 480, 360);
                    MoreItems.HammerDefinition.Register();
                    var offer = System.Linq.Enumerable.Single(JKRuntime.UI.UIApi.GetMerchantOffers(), item => item.Id == "more-items.hammer");
                    device.SetRenderTarget(target); device.Clear(new Color(20, 25, 34));
                    batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
                    batch.DrawString(font, "MORE ITEMS / HAMMER", new Vector2(24, 20), Color.White);
                    offer.DrawIcon(new Rectangle(208, 110, 64, 64));
                    batch.DrawString(font, "3 SILVER / EQUIP IN INVENTORY", new Vector2(24, 250), Color.White);
                    batch.End(); device.SetRenderTarget(null);
                    using (var stream = File.Create(Path.Combine(Path.GetDirectoryName(output), "hammer-item-preview.png"))) target.SaveAsPng(stream, 480, 360);
                }
            }
            Console.WriteLine("[OK] Native GPU hammer preview: " + output);
        }

        private static void VerifyModalResume(Sprite king, SpriteBatch batch, RenderTarget2D target, GraphicsDevice device)
        {
            var player = (JumpKing.Player.PlayerEntity)FormatterServices.GetUninitializedObject(typeof(JumpKing.Player.PlayerEntity));
            typeof(EntityComponent.Entity).GetField("m_components", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(player, new System.Collections.Generic.List<EntityComponent.Component>());
            player.m_body = new JumpKing.Player.BodyComp(new Vector2(200, 200), 18, 26);
            player.SetSprite(king);
            var visual = new HammerVisual(player);
            player.AddComponents(false, visual);
            var physics = new HammerPhysics { Ready = true, Head = new Vector2(230, 200), Contact = true };
            visual.Attach(physics);
            var suspend = typeof(JKRuntime.UI.UIApi).Assembly.GetType("JKRuntime.UI.ModalHost", true)
                .GetMethod("SuspendComponents", BindingFlags.Static | BindingFlags.NonPublic);
            var spriteField = typeof(JumpKing.Player.PlayerEntity).GetField("m_sprite", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int cycle = 0; cycle < 3; cycle++)
            {
                using ((IDisposable)suspend.Invoke(null, new object[] { player.GetComponents() }))
                {
                    if (visual.Enabled) throw new Exception("Merchant must suspend the visual component");
                    if (!ReferenceEquals(spriteField.GetValue(player), king)) throw new Exception("Suspension must restore the king sprite");
                }
                if (!(spriteField.GetValue(player) is HammerSprite)) throw new Exception("Resume must restore Hammer before the next update");
                visual.Refresh();
                device.SetRenderTarget(target);
                batch.Begin();
                player.Draw();
                batch.End();
                device.SetRenderTarget(null);
                if (physics.Head != new Vector2(230, 200) || !physics.Contact)
                    throw new Exception("Closing a modal changed the planted hammer");
            }
            using ((IDisposable)suspend.Invoke(null, new object[] { player.GetComponents() }))
                visual.Detach();
            visual.Refresh();
            if (!ReferenceEquals(spriteField.GetValue(player), king)) throw new Exception("A released modal resurrected a detached Hammer");
            visual.Attach(new HammerPhysics { Ready = true, Head = new Vector2(210, 190) });
            visual.Detach();
            visual.Enabled = true;
            visual.Refresh();
            if (!ReferenceEquals(spriteField.GetValue(player), king)) throw new Exception("Re-enabled detached visual created an invalid wrapper");
            Console.WriteLine("[OK] Native player draws after repeated merchant component suspension/resume");
        }
    }
}
