using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using EntityComponent;
using HarmonyLib;
using JumpKing;
using JumpKing.Level;
using JumpKing.Player;
using JumpKing.GameManager;
using JumpKing.BodyCompBehaviours;
using JumpKing.MiscEntities.OldMan;
using JumpKing.MiscEntities.Merchant;
using JumpKing.Util.DrawBT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SmoothCamera;

internal static partial class CameraTests
{
    private static void BranchProjectionPixels(GraphicsDevice device,RenderTarget2D target,Texture2D white,string output)
    {
        var center=typeof(Renderer).GetField("targetCenter",Flags);var ready=typeof(Renderer).GetField("canPresent",Flags);
        var count=typeof(LevelManager).GetField("_total_screens",Flags);
        object oldScreens=Renderer.Screens.GetValue(null),oldCenter=center.GetValue(null),oldReady=ready.GetValue(null),oldCount=count.GetValue(null),oldScreen=RenderContext.NativeScreen.GetValue(null);
        float oldX=Renderer.View.X,oldY=Renderer.View.Y;Vector2 oldOffset=Camera.Offset;
        using(var blue=Solid(device,Color.DarkBlue)) using(var green=Solid(device,Color.DarkGreen)) using(var compositor=new ScreenCompositor())
        try {
            var branch=new[]{MakeScreen(0,blue,null,null),MakeScreen(1,blue,null,null),MakeScreen(2,green,null,null)};
            var art=typeof(LevelScreen).GetField("m_graphics",Flags);
            foreach(var screen in branch) {var graphics=(LevelScreen.Graphics)art.GetValue(screen);graphics.background=graphics.backbackground;art.SetValue(screen,graphics);}
            typeof(LevelScreen).GetField("m_teleport",Flags).SetValue(branch[0],new[]{new TeleportLink(3)});
            typeof(LevelScreen).GetField("m_teleport",Flags).SetValue(branch[2],new[]{new TeleportLink(1)});
            SetWalls(branch[0],new IBlock[]{new BoxBlock(new Rectangle(0,0,8,360))});
            SetWalls(branch[2],new IBlock[]{new BoxBlock(new Rectangle(472,-720,8,360)),new BoxBlock(new Rectangle(0,-368,480,8))});
            Renderer.Screens.SetValue(null,branch);count.SetValue(null,3);RenderContext.NativeScreen.SetValue(null,0);Camera.Offset=Vector2.Zero;
            center.SetValue(null,new Vector2(470,113));ready.SetValue(null,true);
            Renderer.Horizontal.Reset();Renderer.Horizontal.Observe(470,0,branch,true,false);JKRuntime.Geometry.MapTopology.Invalidate();
            int revision=800;
            foreach(float pan in new[]{-120f,-180f,-240f}) {
                Renderer.View.Place(pan,0,113);
                device.SetRenderTarget(target);Game1.instance.StartBatch();
                compositor.Draw(0,3,index=>{
                    branch[index].Draw();
                    if(index==2) Game1.spriteBatch.Draw(white,Camera.TransformRect(new Rectangle(20,-620,8,8)),Color.White);
                },false,revision++,pan,-1,2,0);
                Game1.instance.EndBatch();device.SetRenderTarget(null);
                var projected=JKRuntime.FrameComposition.ProjectWorld(new Vector2(20,-620));
                Near(projected.X,500+pan,"Branch overlay pans with the visible right-hand world image");
                Near(projected.Y,100,"Branch overlay applies the vertical rebase once");
                Check(Read(target)[((int)projected.Y+3)*480+(int)projected.X+3]==Color.White,"Projected multiplayer point disagrees with the native world sprite pixel");
                Save(target,Path.Combine(output,"branch-projection-"+(-pan)+".png"));
                using(new RenderContext(2)) using(new JKRuntime.FrameComposition.ScreenPass(target))
                    Check(JKRuntime.FrameComposition.ProjectWorld(new Vector2(20,-620))==new Vector2(20,100),"World screen pass used the late viewport projection");
            }
        } finally {
            Renderer.Screens.SetValue(null,oldScreens);center.SetValue(null,oldCenter);ready.SetValue(null,oldReady);count.SetValue(null,oldCount);RenderContext.NativeScreen.SetValue(null,oldScreen);
            Camera.Offset=oldOffset;Renderer.View.Place(oldX,oldY,0);JKRuntime.Geometry.MapTopology.Invalidate();
            Renderer.Horizontal.Reset();
        }
    }
    private static bool SkipNpcText() { return false; }
    private static void NativeNpcPixels(GraphicsDevice device, RenderTarget2D target, LevelScreen[] screens, Texture2D white, string output)
    {
        var textIsolation = new Harmony("smooth-camera.npc-fixture");
        var actors = new ISpriteEntity[2];
        for (int i = 0; i < 2; i++)
        {
            Type type = typeof(Game1).Assembly.GetType(i == 0 ? "JumpKing.MiscEntities.OldManEntity" : "JumpKing.MiscEntities.Merchant.MerchantEntity", true);
            actors[i] = (ISpriteEntity)FormatterServices.GetUninitializedObject(type);
            var settings = new OldManSettings { home_screen = 2 };
            type.GetField("m_settings", Flags).SetValue(actors[i], i == 0 ? (object)settings : new MerchantSettings { settings = settings });
            actors[i].Position = new Vector2(70 + i * 30, -40);
            actors[i].SetSprite(Sprite.CreateSprite(white, new Rectangle(0, 0, 10, 10)));
            foreach (var method in type.GetMethods(Flags))
                if (method.Name == "DrawText") textIsolation.Patch(method, prefix: new HarmonyMethod(typeof(CameraTests), "SkipNpcText"));
        }
        try
        {
            // real native Draw callers are JIT-compiled before installing camera
            // hooks, as happens when enabling the mod during an existing run
            Hooks.Uninstall(); RenderContext.NativeScreen.SetValue(null, 0);
            device.SetRenderTarget(target); Game1.instance.StartBatch();
            foreach (var actor in actors) actor.Draw();
            Game1.instance.EndBatch(); device.SetRenderTarget(null);
            Hooks.Install();
            using (var compositor = new ScreenCompositor())
            {
                device.SetRenderTarget(target); Game1.instance.StartBatch();
                compositor.Draw(180, 2, index => { screens[index].Draw(); foreach (var actor in actors) actor.Draw(); screens[index].DrawForeground(); });
                Game1.instance.EndBatch(); device.SetRenderTarget(null);
                var pixels = Read(target);
                foreach (var actor in actors)
                {
                    Check(pixels[145 * 480 + (int)actor.Position.X + 4] == Color.White, "Actual prewarmed native " + actor.GetType().Name + " appears on adjacent screen");
                    Near(actor.Position.Y, -40, "NPC position is not changed by neighbor rendering");
                }
                Check(Camera.CurrentScreenIndex1 == 1, "Native screen remains physical after adjacent NPC draw");
                Save(target, Path.Combine(output, "native-adjacent-npcs.png"));
            }
        }
        finally { textIsolation.UnpatchAll("smooth-camera.npc-fixture"); }
    }

    private static void PortalPixels(GraphicsDevice device, RenderTarget2D target, Texture2D white, string output)
    {
        using (var blue = Solid(device, Color.DarkBlue))
        using (var red = Solid(device, Color.DarkRed))
        using (var green = Solid(device, Color.DarkGreen))
        using (var purple = Solid(device, Color.Purple))
        using (var compositor = new ScreenCompositor())
        using (var full = new RenderTarget2D(device, 1920, 1440))
        {
            var screens = new[] { MakeScreen(0, blue, null, null), MakeScreen(1, red, null, null), MakeScreen(2, green, null, null), MakeScreen(3, purple, null, null) };
            foreach (float x in new[] { -180f, 180f })
            {
                device.SetRenderTarget(target); Game1.instance.StartBatch();
                compositor.Draw(180, 4, index => screens[index].Draw(), false, -1, x, 2, 2, 0);
                Game1.instance.EndBatch(); device.SetRenderTarget(null);
                var pixels = Read(target);
                for (int y = 0; y < 360; y++) for (int column = 0; column < 480; column++)
                {
                    bool destination = x > 0 ? column < 180 : column >= 300;
                    Color expected = destination ? (y < 180 ? Color.Purple : Color.DarkGreen) : (y < 180 ? Color.DarkRed : Color.DarkBlue);
                    if (pixels[y * 480 + column] != expected) throw new Exception("Portal seam/vertical neighbor pixel mismatch " + x + ":" + column + "," + y);
                }
                Check(true, "Both axes compose portal destination and its vertical neighbor without gaps");
                Save(target, Path.Combine(output, x < 0 ? "portal-right.png" : "portal-left.png"));
            }
            device.SetRenderTarget(target); Game1.instance.StartBatch();
            compositor.Draw(180, 4, index => screens[index].Draw(), true, 100, -180, 2, 2, 0);
            Game1.spriteBatch.Draw(white, new Rectangle(8, 8, 8, 8), Color.Magenta);
            Game1.instance.EndBatch(); device.SetRenderTarget(null);
            int captures = compositor.SceneRenders;
            compositor.Present(new Rectangle(0, 0, 480, 360), target, 180, -180); // existing backbuffer is valid
            device.SetRenderTarget(target); Game1.instance.StartBatch();
            compositor.Draw(180, 4, index => { throw new Exception("Unexpected scene refresh"); }, true, 100, -180.25f, 2, 2, 0);
            Game1.spriteBatch.Draw(white, new Rectangle(8, 8, 8, 8), Color.Magenta);
            Game1.instance.EndBatch(); device.SetRenderTarget(null);
            Check(compositor.SceneRenders == captures, "Horizontal subframes reuse captured world");
            device.SetRenderTarget(full); device.Clear(Color.Black);
            compositor.Present(new Rectangle(0, 0, 1920, 1440), target, 180, -180.25f);
            device.SetRenderTarget(null);
            var enlarged = Read(full);
            Check(enlarged[1000 * 1920 + 1198] == Color.DarkBlue && enlarged[1000 * 1920 + 1199] == Color.DarkGreen, "Quarter-pixel horizontal movement resolves to one output pixel at 4x");
            Check(enlarged[36 * 1920 + 36] == Color.Magenta, "Horizontal presentation keeps HUD fixed");
            NativePortalCrossing(device, target, screens, white, output);
            PortalWallPixels(device, target, screens, output);
        }
        Check(!RenderContext.Active && Camera.CurrentScreen == 0, "Portal rendering restores native camera scope");
    }

    private static void NativePortalCrossing(GraphicsDevice device, RenderTarget2D target, LevelScreen[] screens, Texture2D white, string output)
    {
        object previousScreens = Renderer.Screens.GetValue(null);
        object previousCount = typeof(LevelManager).GetField("_total_screens", Flags).GetValue(null);
        PlayerEntity previousPlayer = GameLoop.m_player;
        var player = (PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
        player.m_body = (BodyComp)FormatterServices.GetUninitializedObject(typeof(BodyComp));
        typeof(BodyComp).GetField("m_width", Flags).SetValue(player.m_body, 18);
        typeof(BodyComp).GetField("m_height", Flags).SetValue(player.m_body, 26);
        typeof(PlayerEntity).GetField("m_sprite", Flags).SetValue(player, Sprite.CreateCenteredSprite(white, new Rectangle(0, 0, 10, 10)));
        typeof(LevelScreen).GetField("m_teleport", Flags).SetValue(screens[0], new[] { new TeleportLink(3) });
        var graphicsField = typeof(LevelScreen).GetField("m_graphics", Flags);
        var oldGraphics = (LevelScreen.Graphics)graphicsField.GetValue(screens[0]);
        var withArt = oldGraphics; withArt.background = withArt.backbackground;
        graphicsField.SetValue(screens[0], withArt);
        try
        {
            GameLoop.m_player = player; Renderer.Screens.SetValue(null, screens);
            typeof(LevelManager).GetField("_total_screens", Flags).SetValue(null, 4);
            using (var compositor = new ScreenCompositor())
            foreach (bool expansion in expansionAssembly == null ? new[] { false } : new[] { false, true })
            foreach (int destination in new[] { 0, 2 })
            foreach (bool right in new[] { true, false })
            {
                typeof(LevelScreen).GetField("m_teleport", Flags).SetValue(screens[0], expansion ? new TeleportLink[0] : new[] { new TeleportLink(destination+1) });
                IBlock warp = expansion ? MultiWarp(new Rectangle(0, 0, 480, 360), (byte)(destination+1)) : null;
                SetWalls(screens[0], expansion ? new[] { warp } : new IBlock[0]);
                RenderContext.NativeScreen.SetValue(null, 0);
                player.m_body.Position = new Vector2(right ? 472 : -10, 100);
                float horizontal = right ? -180 : 180;
                var motion = new HorizontalMotion();
                motion.Observe(player.m_body.GetHitbox().Center.X, 0, screens, true, false, 0, 1, 0, 113);
                motion.Advance(1f / 60);
                float oldTranslation = motion.Translation;
                Action<int> draw = index => { screens[index].Draw(); Renderer.EntityDraw(player); };
                device.SetRenderTarget(target); Game1.instance.StartBatch();
                compositor.Draw(0, 4, draw, false, -1, horizontal, destination, destination, 0);
                Game1.instance.EndBatch(); device.SetRenderTarget(null);
                var before = Read(target);
                var beforePosition = player.m_body.Position;
                if (expansion) ExpansionWarp(player.m_body, warp);
                else new HandlePlayerTeleportBehaviour().ExecuteBehaviour(new BehaviourContext(player.m_body));
                Near(player.m_body.Position.X - beforePosition.X, (right ? -1 : 1) * (expansion ? 479.8f : 480), "Camera preserves the teleporter's actual body displacement");
                Check(Camera.CurrentScreen == destination, "Actual native body teleport reaches destination");
                float vertical = motion.Observe(player.m_body.GetHitbox().Center.X, destination, screens, true, false, destination, 0, 0, player.m_body.GetHitbox().Center.Y);
                Near(vertical, destination*360, "Real side teleport rebases the vertical camera without a snap");
                Near(motion.Translation - oldTranslation, right ? 480 : -480, "Real side teleport rebases the horizontal camera");
                device.SetRenderTarget(target); Game1.instance.StartBatch();
                compositor.Draw(vertical, 4, draw, false, -1, horizontal + motion.Translation - oldTranslation, motion.Left, motion.Right, motion.Anchor);
                Game1.instance.EndBatch(); device.SetRenderTarget(null);
                var after = Read(target);
                int shiftedPixels = 0;
                for (int i = 0; i < before.Length; i++) if (before[i] != after[i])
                {
                    // MultiWarp shifts by 479.8, native sprite rounding may move one pixel
                    bool actorEdge = expansion && (before[i] == Color.White || after[i] == Color.White)
                        && i % 480 > 0 && i % 480 < 479
                        && (after[i] == before[i - 1] || after[i] == before[i + 1]);
                    if (!actorEdge) throw new Exception("Teleport visual discontinuity, expansion=" + expansion + " right=" + right + " pixel=" + i);
                    shiftedPixels++;
                }
                Check(shiftedPixels <= 20, "Side crossing preserves every scenery pixel and the native 10px actor within its own rounding");
                Save(target, Path.Combine(output, (expansion ? "expansion" : "native") + (destination==0 ? "-self" : "") + (right ? "-portal-crossing-right.png" : "-portal-crossing-left.png")));
            }
        }
        finally
        {
            graphicsField.SetValue(screens[0], oldGraphics);
            SetWalls(screens[0], new IBlock[0]);
            Renderer.Screens.SetValue(null, previousScreens); GameLoop.m_player = previousPlayer;
            typeof(LevelManager).GetField("_total_screens", Flags).SetValue(null, previousCount); RenderContext.NativeScreen.SetValue(null, 0);
        }
    }
}
