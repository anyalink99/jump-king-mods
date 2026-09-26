// XML vocabulary and native-node mappings adapted from MoreEndingOptions (MIT).
// Copyright (c) 2025 Zebra. See THIRD_PARTY_NOTICES.md for license and provenance.
namespace MegaMappingExpansion.Endings
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Xml.Linq;
    using BehaviorTree;
    using BehaviorTree.Util;
    using EntityComponent.BT;
    using HarmonyLib;
    using JumpKing;
    using JumpKing.GameManager.MultiEnding;
    using JumpKing.GameManager.MultiEnding.NewBabePlusEnding.Actors;
    using JumpKing.GameManager.MultiEnding.NormalEnding;
    using JumpKing.GameManager.MultiEnding.NormalEnding.Babe;
    using JumpKing.GameManager.MultiEnding.NormalEnding.Cherubs;
    using JumpKing.GameManager.MultiEnding.NormalEnding.King;
    using JumpKing.JKMemory.KingSpriteLayers;
    using JumpKing.MiscEntities.WorldItems;
    using JumpKing.Util;
    using JumpKing.Util.BroadcastBT;
    using JumpKing.Util.DrawBT;
    using JumpKing.XnaWrappers;
    using Microsoft.Xna.Framework;
    using Microsoft.Xna.Framework.Graphics;

    internal static class EndingXmlParser
    {
        public enum Ending
        {
            MainBabe,
            NewBabePlus,
            GhostOfTheBabe,
        }

        public static IBTnode GetBtTree(ISpriteEntity entity, XElement root, Ending ending)
        {
            var node = GetBtNode(entity, root, ending);
            var ibtComposite = node as IBTcomposite;
            if (ibtComposite != null)
            {
                ParseRecursively(entity, ibtComposite, root, ending);
            }

            return node;
        }

        private static void ParseRecursively(ISpriteEntity entity, IBTcomposite composite, XElement root, Ending ending)
        {
            foreach (var element in root.Elements())
            {
                var child = GetBtNode(entity, element, ending);
                var ibtComposite = child as IBTcomposite;
                if (ibtComposite != null)
                {
                    ParseRecursively(entity, ibtComposite, element, ending);
                }

                composite.AddChild(child);
            }
        }

        private static IBTnode GetBtNode(ISpriteEntity entity, XElement element, Ending ending)
        {
            IBTnode btNode;
            switch (element.Name.LocalName)
            {
                case "EmitSceneEvent":
                    return new SceneBridgeNode(element.Name.LocalName, element.Value, null, SceneBridgeNode.ActiveScene);
                case "SetSceneFlag": case "CheckSceneFlag": case "WaitSceneFlag":
                    return new SceneBridgeNode(element.Name.LocalName, element.Element("Key").Value, element.Element("Value").Value, SceneBridgeNode.ActiveScene);
                // ibtComposites
                case "RestartSequencer":
                    btNode = new BT_RestartSequencor();
                    break;
                case "Selector":
                    btNode = new BTselector();
                    break;
                case "SequenceOnce":
                    btNode = new BTsequenceOnce();
                    break;
                case "Sequencer":
                    btNode = new BTsequencor();
                    break;
                case "Simultaneous":
                    btNode = new BTsimultaneous();
                    break;
                case "RandomSelector":
                    btNode = new BTrandomSelector();
                    break;
                case "RunAllAnySuccess":
                    btNode = new RunAllAnySuccess();
                    break;
                // other bt nodes
                case "Evaluator":
                    var condition = GetBtTree(entity, element.Element("Condition").Elements().First(), ending);
                    var func = GetBtTree(entity, element.Element("Func").Elements().First(), ending);
                    btNode = new BTevaluator(condition, func);
                    break;
                case "EndingScroll":
                    var endingStepElement = element.Element("Step");
                    var endingStep = new Vector2(float.Parse(endingStepElement.Element("X").Value, CultureInfo.InvariantCulture),
                        float.Parse(endingStepElement.Element("Y").Value, CultureInfo.InvariantCulture));
                    var esRepetitions = int.Parse(element.Element("Repetitions").Value);
                    btNode = EndingScroll.CreateFrom30FPS(endingStep, esRepetitions);
                    break;
                case "CoupleAnim":
                    var spriteWalkOne = GetSprite(element.Element("WalkOne").Value);
                    var spriteWalkTwo = GetSprite(element.Element("WalkTwo").Value);
                    var spriteWalkSmear = GetSprite(element.Element("WalkSmear").Value);
                    var speed = element.Element("Speed") == null ? 1 : int.Parse(element.Element("Speed").Value, CultureInfo.InvariantCulture);
                    btNode = new CoupleAnim(entity, spriteWalkOne, spriteWalkTwo, spriteWalkSmear, speed);
                    break;
                case "PauseNode":
                    btNode = new PauseNode(float.Parse(element.Value, CultureInfo.InvariantCulture));
                    break;
                case "SetSpriteNode":
                case "GetSpriteNode":
                    btNode = new SetSpriteNode(entity, GetSprite(element.Value));
                    break;
                case "SetSpriteEffectNode":
                    btNode = new SetSpriteEffectNode(entity,
                        (SpriteEffects)Enum.Parse(typeof(SpriteEffects), element.Value));
                    break;
                case "BroadcastNode":
                    btNode = new BroadcastNode(element.Value);
                    break;
                case "ReceiverWaitNode":
                    btNode = new RecieverWaitNode(element.Value);
                    break;
                case "PlaySFX":
                    btNode = new PlaySFX(GetSound(element.Value, Game1.instance.contentManager.audio));
                    break;
                case "MoveNode":
                    var mnRepetitions = int.Parse(element.Element("Repetitions").Value);
                    var deltaMoveElement = element.Element("DeltaMove");
                    var deltaMove = new Vector2(float.Parse(deltaMoveElement.Element("X").Value, CultureInfo.InvariantCulture),
                        float.Parse(deltaMoveElement.Element("Y").Value, CultureInfo.InvariantCulture));
                    btNode = MoveNode.CreateFrom30FPS(entity, mnRepetitions, deltaMove);
                    break;
                case "CheckBBKey":
                    var checkBbKey = element.Element("Key").Value;
                    var checkValue = int.Parse(element.Element("Value").Value);
                    var enumCheckNode = typeof(CheckBBKey<>).MakeGenericType(typeof(object));
                    btNode = (IBTnode)Activator.CreateInstance(
                        enumCheckNode,
                        entity,
                        checkBbKey,
                        checkValue);
                    break;
                case "SetBBKeyNode":
                    var setBbKey = element.Element("Key").Value;
                    var setValue = int.Parse(element.Element("Value").Value);
                    var enumSetNode = typeof(SetBBKeyNode<>).MakeGenericType(typeof(object));
                    btNode = (IBTnode)Activator.CreateInstance(
                        enumSetNode,
                        entity,
                        setBbKey,
                        setValue);
                    break;
                case "CherubsDeliver":
                    switch (ending)
                    {
                        case Ending.MainBabe:
                            btNode = new CherubsDeliver(entity);
                            break;
                        case Ending.NewBabePlus:
                            btNode =
                                new JumpKing.GameManager.MultiEnding.NewBabePlusEnding.Actors.Cherubs.CherubsDeliver(
                                    entity);
                            break;
                        case Ending.GhostOfTheBabe:
                            throw new Exception("Ghost of the babe ending has no cherubs");
                        default:
                            throw new ArgumentOutOfRangeException("ending", ending, null);
                    }

                    break;
                case "CherubsEscape":
                    switch (ending)
                    {
                        case Ending.MainBabe:
                            btNode = new CherubsEscape(entity);
                            break;
                        case Ending.NewBabePlus:
                            btNode =
                                new JumpKing.GameManager.MultiEnding.NewBabePlusEnding.Actors.Cherubs.CherubsEscape(
                                    entity);
                            break;
                        case Ending.GhostOfTheBabe:
                            throw new Exception("Ghost of the babe ending has no cherubs");
                        default:
                            throw new ArgumentOutOfRangeException("ending", ending, null);
                    }

                    break;
                case "GiveWearableItemNode":
                    btNode = new GiveWearableItemNode((Items)Enum.Parse(typeof(Items), element.Value));
                    break;
                case "StaticNode":
                    var child = GetBtTree(entity, element.Elements().First(elem => elem.Name.LocalName != "Result"),
                        ending);
                    var staticResult = (BTresult)Enum.Parse(typeof(BTresult), element.Element("Result").Value);
                    btNode = new StaticNode(child, staticResult);
                    break;
                case "LoopingAnimNode":
                    var loopStep = float.Parse(element.Element("Step").Value, CultureInfo.InvariantCulture);
                    var loopSprites = element.Element("Sprites")
                        .Elements("Sprite").Select(sprite => GetSprite(sprite.Value)).ToArray();
                    btNode = new LoopingAnimNode(entity, loopSprites, loopStep);
                    break;
                case "PlayMusic":
                    switch (ending)
                    {
                        case Ending.MainBabe:
                            btNode = new BTPlayMusic(Game1.instance.contentManager.audio.music.Ending);
                            break;
                        case Ending.NewBabePlus:
                            btNode = new BTPlayMusic(Game1.instance.contentManager.audio.music.Ending2);
                            break;
                        case Ending.GhostOfTheBabe:
                            btNode = new BTPlayMusic(Game1.instance.contentManager.audio.music.Ending3);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException("ending", ending, null);
                    }

                    break;
                case "EndNode":
                    btNode = new EndNode();
                    break;
                case "SuicideNode":
                    btNode = new SuicideNode(entity);
                    break;
                // mb specific
                case "JumpUp":
                    btNode = new JumpUp(entity);
                    break;
                case "FallDown":
                    btNode = new FallDown(entity);
                    break;
                case "JumpToTargetHeight":
                    var velocity = float.Parse(element.Element("Velocity").Value, CultureInfo.InvariantCulture);
                    var targetMove = float.Parse(element.Element("TargetMove").Value, CultureInfo.InvariantCulture);
                    btNode = new JumpToTargetHeight(entity, velocity, targetMove);
                    break;
                case "JumpInPlace":
                    btNode = new JumpInPlace(entity, int.Parse(element.Element("Speed").Value));
                    break;
                case "Jump":
                    btNode = new Jump(entity);
                    break;
                case "IdleAnim":
                    btNode = new IdleAnim(entity);
                    break;
                case "PutOnCrown":
                    var typePutOnCrown = typeof(Game1).Assembly.GetType(
                        "JumpKing.GameManager.MultiEnding.NormalEnding.EndingKing+PutOnCrown");
                    btNode = (IBTnode)Activator.CreateInstance(typePutOnCrown);
                    break;
                case "CherubsDeliverAnim":
                    btNode = new CherubsDeliverAnim(entity);
                    break;
                case "CherubsEscapeAnim":
                    btNode = new CherubsEscapeAnim(entity);
                    break;
                // nb+ specific
                case "BabeJump":
                    btNode = new BabeJump(entity);
                    break;
                case "GiveCrownNBP":
                    btNode = new GiveWearableItemNode(Items.CrownNBP);
                    break;
                //gotb specific
                case "IsBirdDone":
                    var typeIsBirdDone = typeof(Game1).Assembly.GetType(
                        "JumpKing.GameManager.MultiEnding.OwlEnding.OwlBirdEntity+IsBirdDone");
                    btNode = (IBTnode)Activator.CreateInstance(
                        typeIsBirdDone,
                        new object[] { Traverse.Create(entity).Field("m_is_done").GetValue<bool>() }
                    );
                    break;
                case "PlayEventSFX":
                    btNode = new PlayEventSFX(element.Value);
                    break;
                case "SpawnLightning":
                    var typeSpawnLightning = typeof(Game1).Assembly.GetType(
                        "JumpKing.GameManager.MultiEnding.OwlEnding.OwlKingEntity+SpawnLightning");
                    btNode = (IBTnode)Activator.CreateInstance(typeSpawnLightning);
                    break;
                default:
                    throw new Exception("Unknown behaviour tree node: " + element.Name);
            }

            return btNode;
        }

        internal static Sprite GetSprite(string value)
        {
            var contentManager = Game1.instance.contentManager;
            var currentSprites = contentManager.playerSprites._CurrentSprites;

            Sprite sprite;
            var split = value.Split('.');
            switch (split[0])
            {
                case "Regular":
                    var regularSprites = currentSprites.regular;
                    sprite = regularSprites.GetSprite(
                        (Regular.SpriteKey)Enum.Parse(typeof(Regular.SpriteKey), split[1]));
                    break;
                case "Babe":
                    var mainBabeSprites = currentSprites.babe;
                    sprite = mainBabeSprites.GetSprite((Babe.SpriteKey)Enum.Parse(typeof(Babe.SpriteKey), split[1]));
                    break;
                case "BabeCouple":
                    var babeCoupleSprites = currentSprites.babe_couple;
                    sprite = babeCoupleSprites.GetSprite(
                        (BabeCouple.SpriteKey)Enum.Parse(typeof(BabeCouple.SpriteKey), split[1]));
                    break;
                case "Ending1Misc":
                    var regularEndingSprites = currentSprites.regular_ending;
                    sprite = regularEndingSprites.GetSprite(
                        (Ending1Misc.SpriteKey)Enum.Parse(typeof(Ending1Misc.SpriteKey), split[1]));
                    break;
                case "NBPKing":
                    var nbpKingSprites = currentSprites.nbp_king;
                    sprite = nbpKingSprites.GetSprite(
                        (NBPKing.SpriteKey)Enum.Parse(typeof(NBPKing.SpriteKey), split[1]));
                    break;
                case "NBPBabe":
                    var nbpBabeSprites = currentSprites.nbp_babe;
                    sprite = nbpBabeSprites.GetSprite(
                        (NBPBabe.SpriteKey)Enum.Parse(typeof(NBPBabe.SpriteKey), split[1]));
                    break;
                case "OWLKing":
                    var owlKingSprites = currentSprites.owl_king;
                    sprite = owlKingSprites.GetSprite(
                        (OwlKing.SpriteKey)Enum.Parse(typeof(OwlKing.SpriteKey), split[1]));
                    break;
                case "OWLBabe":
                    var owlBabeSprites = currentSprites.owl_babe;
                    sprite = owlBabeSprites.GetSprite(
                        (OwlBabe.SpriteKey)Enum.Parse(typeof(OwlBabe.SpriteKey), split[1]));
                    break;
                case "OWLGargoyle":
                    var owlGargoyleSprites = currentSprites.owl_gargoyle;
                    sprite = owlGargoyleSprites.GetSprite(
                        (OwlGargoyle.SpriteKey)Enum.Parse(typeof(OwlGargoyle.SpriteKey), split[1]));
                    break;
                case "OWLBird":
                    var owlBirdSprites = currentSprites.owl_bird;
                    sprite = owlBirdSprites.GetSprite(
                        (OwlBird.SpriteKey)Enum.Parse(typeof(OwlBird.SpriteKey), split[1]));
                    break;
                default:
                    throw new Exception("Unknown sprite: " + value);
            }

            return sprite;
        }

        internal static IJKSound GetSound(string value, JKContentManager.Audio audio)
        {
            IJKSound sound;
            switch (value)
            {
                case "Audio.Plink":
                    sound = audio.Plink;
                    break;
                case "Audio.PressStart":
                    sound = audio.PressStart;
                    break;
                case "Audio.NewLocation":
                    sound = audio.NewLocation;
                    break;
                case "Audio.Talking":
                    sound = audio.Talking;
                    break;
                case "Audio.RaymanSFX":
                    sound = audio.RaymanSFX;
                    break;
                case "Audio.WaterSplashEnter":
                    sound = audio.WaterSplashEnter;
                    break;
                case "Audio.WaterSplashExit":
                    sound = audio.WaterSplashExit;
                    break;
                case "Babe.Jump":
                    sound = audio.babe.Jump;
                    break;
                case "Babe.Kiss":
                    sound = audio.babe.Kiss;
                    break;
                case "Babe.Mou":
                    sound = audio.babe.Mou;
                    break;
                case "Babe.Pickup":
                    sound = audio.babe.Pickup;
                    break;
                case "Babe.Scream":
                    sound = audio.babe.Scream;
                    break;
                case "Babe.Surprised":
                    sound = audio.babe.Surprised;
                    break;
                case "Menu.CursorMove":
                    sound = audio.menu.CursorMove;
                    break;
                case "Menu.Select":
                    sound = audio.menu.Select;
                    break;
                case "Menu.MenuOpen":
                    sound = audio.menu.MenuOpen;
                    break;
                case "Menu.MenuFail":
                    sound = audio.menu.MenuFail;
                    break;
                case "Menu.TitleHit":
                    sound = audio.menu.TitleHit;
                    break;
                case "Music.TitleScreen":
                    sound = audio.music.TitleScreen;
                    break;
                case "Music.Opening":
                    sound = audio.music.Opening;
                    break;
                case "Music.Ending":
                    sound = audio.music.Ending;
                    break;
                case "Music.Ending2":
                    sound = audio.music.Ending2;
                    break;
                case "Music.Ending3":
                    sound = audio.music.Ending3;
                    break;
                case "Player.Jump":
                    sound = audio.player.Jump;
                    break;
                case "Player.Land":
                    sound = audio.player.Land;
                    break;
                case "Player.Bump":
                    sound = audio.player.Bump;
                    break;
                case "Player.Splat":
                    sound = audio.player.Splat;
                    break;
                case "Player.IceJump":
                    sound = audio.player.IceJump;
                    break;
                case "Player.IceLand":
                    sound = audio.player.IceLand;
                    break;
                case "Player.SnowJump":
                    sound = audio.player.SnowJump;
                    break;
                case "Player.SnowLand":
                    sound = audio.player.SnowLand;
                    break;
                case "Player.SnowSplat":
                    sound = audio.player.SnowSplat;
                    break;
                case "Player.IronLand":
                    sound = audio.player.IronLand;
                    break;
                case "Player.IronSplat":
                    sound = audio.player.IronSplat;
                    break;
                case "Player.WaterJump":
                    sound = audio.player.WaterJump;
                    break;
                case "Player.WaterLand":
                    sound = audio.player.WaterLand;
                    break;
                case "Player.WaterBump":
                    sound = audio.player.WaterBump;
                    break;
                case "Player.WaterSplat":
                    sound = audio.player.WaterSplat;
                    break;
                case "Player.SandLand":
                    sound = audio.player.SandLand;
                    break;
                case "Player.EndingParasol":
                    sound = audio.player.EndingParasol;
                    break;
                default:
                    throw new Exception("Unknown sound: " + value);
            }

            return sound;
        }
    }
}
