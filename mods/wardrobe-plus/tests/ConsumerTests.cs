using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using JumpKing;
using JumpKing.MiscEntities.WorldItems;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WardrobePlus;

internal static partial class WardrobeTests
{
    private static void ConsumerTests(string repo, Game1 game, GraphicsDevice device, SpriteBatch batch, RenderTarget2D target, MaterialKind itemMaterial = MaterialKind.Diamond)
    {
        var ball = Assembly.LoadFrom(Path.Combine(repo, "build", "morph-ball", "_INTERNAL", "MorphBall.Module.dll"));
        var replay = Assembly.LoadFrom(Path.Combine(repo, "build", "replays", "_INTERNAL", "Replays.Module.dll"));
        var mapping = Assembly.LoadFrom(Path.Combine(repo, "build", "mega-mapping-expansion", "_INTERNAL", "MegaMappingExpansion.Module.dll"));
        var signature = ball.GetType("MorphBallMod.SpriteLayerAccess", true).GetMethod("GetSignature", Flags);
        var buildBall = ball.GetType("MorphBallMod.OutfitTextureBuilder", true).GetMethod("Build", Flags);
        var priorTime=CosmicRenderer.TestTime;CosmicRenderer.TestTime=0;
        var resolveReplay = replay.GetType("Replays.ReplayAppearanceSprites", true).GetMethod("Resolve", Flags);
        var pose = Enum.Parse(replay.GetType("Replays.ReplayPose", true), "Idle");
        var sceneType = mapping.GetType("MegaMappingExpansion.Construction.NativeCapture", true);
        var scene = FormatterServices.GetUninitializedObject(sceneType);
        sceneType.GetField("screens", Flags).SetValue(scene, new JumpKing.Level.LevelScreen[1]);
        sceneType.GetField("graphics", Flags).SetValue(scene, new[] { new JumpKing.Level.LevelScreen.Graphics() });
        var player = (PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
        var body = new BodyComp(new Vector2(231,194)-Camera.Offset, 18, 26);
        typeof(PlayerEntity).GetField("m_body",Flags).SetValue(player,body);
        var wrapper = game.contentManager.playerSprites.idle; player.SetSprite(wrapper);
        int priorSignature = (int)signature.Invoke(null, new object[] { wrapper });
        var previousReplay = (Sprite)resolveReplay.Invoke(null, new object[] { pose, new[] { (int)Items.Cap, (int)Items.Shoes } });
        sceneType.GetMethod("Attach", Flags).Invoke(scene, new object[] { player });
        Func<Color[]> captureMapping = () => {
            device.SetRenderTarget(target); device.Clear(Color.Transparent);
            sceneType.GetMethod("Draw", Flags).Invoke(scene, new object[] { "actor:player", 1 });
            device.SetRenderTarget(null); var pixels = new Color[480 * 360]; target.GetData(pixels); return pixels;
        };
        var previousMapping = captureMapping();
        Check(previousMapping.Any(c => c.A != 0), "Mega Mapping Expansion captures the mixed native outfit");

        var saved = Controller.Data.Current.Copy(); var fitted = saved.Copy();
        fitted.Material = MaterialKind.Gold;
        fitted.SetMaterial((int)Items.Cap, itemMaterial);
        fitted.SetFit(new FitAdjustment { BaseId = Controller.Active.Resolved[NativeAppearance.BaseItem].Id,
            SourceId = Controller.Active.Resolved[(int)Items.Cap].Id, Item = (int)Items.Cap, Group = 0, Frame = 0, X = 7, Y = -5 });
        Controller.Apply(fitted); Controller.Pump();
        Check((int)signature.Invoke(null, new object[] { wrapper }) != priorSignature, "Ball King's real outfit cache detects fitting changes without replacing the wrapper");
        var currentReplay = (Sprite)resolveReplay.Invoke(null, new object[] { pose, new[] { (int)Items.Cap, (int)Items.Shoes } });
        Check(!ReferenceEquals(currentReplay, previousReplay) && NativeAppearance.Layers(currentReplay).Contains(NativeAppearance.Frames(Controller.Active.Items[Items.Cap].regular)[0]),
            "Replays invalidates its cache and uses the fitted item sprite");
        var currentMapping = captureMapping();
        Check(!currentMapping.SequenceEqual(previousMapping), "Mega Mapping Expansion capture observes the fitted outfit without stale pixels");

        Func<Sprite, Color[]> render = sprite => {
            device.SetRenderTarget(target); device.Clear(Color.Transparent); batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            sprite.Draw(new Vector2(240, 220), SpriteEffects.None); batch.End(); device.SetRenderTarget(null);
            var pixels = new Color[480 * 360]; target.GetData(pixels); return pixels;
        };
        var nativePixels = render(wrapper);
        Check(render(currentReplay).SequenceEqual(nativePixels), "Replay outfit rendering matches the fitted native pixels");
        Check(currentMapping.SequenceEqual(nativePixels), "Mapping's captured effect source matches fitted native shader pixels");
        var sample=JKRuntime.Presentation.PlayerAppearance.FromSprite(wrapper,new Vector2(240,220)-Camera.Offset);
        var captured=JKRuntime.Presentation.AppearanceCapture.Read(sample,256);
        using(var texture=captured.CreateTexture(device))
        {
            device.SetRenderTarget(target);device.Clear(Color.Transparent);batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
            batch.Draw(texture,captured.TopLeft(new Vector2(240,220)),Color.White);batch.End();device.SetRenderTarget(null);
            var actual=new Color[480*360];target.GetData(actual);
            Check(actual.SequenceEqual(nativePixels),"Shared form input preserves fitted material pixels and anchors");
        }
        using(var texture=(Texture2D)buildBall.Invoke(null,new object[]{wrapper}))
        {
            var pixels=new Color[texture.Width*texture.Height];texture.GetData(pixels);
            Check(texture.Width==18&&texture.Height==18&&pixels.Any(c=>c.A>0),"Ball builds its form from shared fitted outfit capture");
        }
        CosmicRenderer.TestTime=priorTime;
        Controller.Apply(saved); Controller.Pump();
    }
}
