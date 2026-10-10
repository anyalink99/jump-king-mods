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
        var sceneType = mapping.GetType("MegaMappingExpansion.SceneHost", true);
        var scene = FormatterServices.GetUninitializedObject(sceneType);
        foreach (string name in new[] { "playerAppearances", "playerAppearanceVersions", "playerAtlasPixels", "silhouetteTextures" })
        {
            var field = sceneType.GetField(name, Flags); field.SetValue(scene, Activator.CreateInstance(field.FieldType));
        }
        var player = (PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
        var body=(BodyComp)FormatterServices.GetUninitializedObject(typeof(BodyComp));body.Position=new Vector2(231,194)-Camera.Offset;
        typeof(PlayerEntity).GetField("m_body",Flags).SetValue(player,body);
        var wrapper = game.contentManager.playerSprites.idle; player.SetSprite(wrapper);
        int priorSignature = (int)signature.Invoke(null, new object[] { wrapper });
        var previousReplay = (Sprite)resolveReplay.Invoke(null, new object[] { pose, new[] { (int)Items.Cap, (int)Items.Shoes } });
        object[] mappingArgs = { player, null, null, Rectangle.Empty, SpriteEffects.None };
        var mapRender = sceneType.GetMethod("TryPlayerSprite", Flags);
        Check((bool)mapRender.Invoke(scene, mappingArgs), "Mega Mapping Expansion resolves the mixed native outfit");
        var previousMapping = (Sprite)mappingArgs[1];

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
        mappingArgs = new object[] { player, null, null, Rectangle.Empty, SpriteEffects.None };
        Check((bool)mapRender.Invoke(scene, mappingArgs) && !ReferenceEquals(mappingArgs[1], previousMapping) && previousMapping.texture.IsDisposed,
            "Mega Mapping Expansion rebuilds its flattened outfit and releases the old texture");

        Func<Sprite, Color[]> render = sprite => {
            device.SetRenderTarget(target); device.Clear(Color.Transparent); batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            sprite.Draw(new Vector2(240, 220), SpriteEffects.None); batch.End(); device.SetRenderTarget(null);
            var pixels = new Color[480 * 360]; target.GetData(pixels); return pixels;
        };
        var nativePixels = render(wrapper);
        Check(render(currentReplay).SequenceEqual(nativePixels), "Replay outfit rendering matches the fitted native pixels");
        Check(render((Sprite)mappingArgs[1]).SequenceEqual(nativePixels), "Mapping's captured effect source matches fitted native shader pixels");
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
        sceneType.GetMethod("DisposePlayerAppearances", Flags).Invoke(scene, null);
        Controller.Apply(saved); Controller.Pump();
    }
}
