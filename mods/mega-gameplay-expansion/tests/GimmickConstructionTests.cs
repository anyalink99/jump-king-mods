using System;
using System.Collections.Generic;
using System.Linq;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Player;
using JumpKing.BodyCompBehaviours;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal static partial class Tests
    {
        private static void GimmickConstructionRegression()
        {
            using (new SpriteGameFixture())
            using (new WalkInputFixture())
            {
                // Never call this provider's GetBlock or startup, nor visit a source map.
                var factory = new UnknownProvider.PortableFactory();
                LevelManager.RegisterBlockFactory(factory);
                var pins = Settings.Current.GimmickPins;
                var encoded = new Color(13, 91, 90);
                string savedId = Gimmicks.BlockId(factory.GetType(), encoded);
                try
                {
                    Settings.Current.GimmickPins = new[] { savedId };
                    GimmickBlocks.DiscoverPalette(new Color[0]);
                    Require(Gimmicks.Entries.ContainsKey(savedId), "Saved encoded palette colour resolves without a source-map index or visit");
                    GimmickRecipes.Prepare(Gimmicks.Entries[savedId]);
                    Require(((UnknownProvider.PortableZone)Gimmicks.Entries[savedId].Template).Parameter == 93, "Rediscovered saved colour retains its parameter");
                }
                finally { Settings.Current.GimmickPins = pins; }
                var entry = new GimmickEntry { Id = "fixture:constructed", Kind = "Block", Factory = factory, Colour = UnknownProvider.PortableFactory.Code };
                GimmickRecipes.Prepare(entry);
                Require(entry.Template is UnknownProvider.PortableZone, "Build unseen IBlock from installed factory IL: " + entry.Error);
                Require(((UnknownProvider.PortableZone)entry.Template).Parameter == 45, "RGB-encoded constructor parameters preserved");
                Require(UnknownProvider.PortableFactory.LastMap == ulong.MaxValue && UnknownProvider.PortableFactory.Screens.Count == 0, "Factory stores and collection writes remain isolated");
                var rejected = new GimmickEntry { Kind = "Block", Factory = factory, Colour = Color.Black };
                GimmickRecipes.Prepare(rejected);
                Require(rejected.Template == null && rejected.Error.Contains("WriteAllText"), "Native file writes refused by constructor interpreter");
                Require(!System.IO.File.Exists("construction-must-not-write.txt"), "No constructor-side filesystem mutation");
                Require(UnknownProvider.PortableFactory.Screens.Count == 0, "Failure cannot leak prior collection writes");
                Reject(() => new GimmickConstruction(typeof(UnknownProvider.StatefulConstructor).Assembly).Construct(typeof(UnknownProvider.StatefulConstructor).GetConstructor(Type.EmptyTypes)),
                    "Constructor requiring external writes refused instead of returning a partial controller");
                Require(UnknownProvider.Registration.StartupCalls == 0, "Refused constructor did not alter provider state");
                var source = new IBlock[] { new BoxBlock(new Rectangle(0, 320, 480, 40)) };
                var screens = Scene(source); GimmickBlocks.Screens.SetValue(null, screens);
                typeof(LevelManager).GetField("_total_screens", Flags).SetValue(null, 1);
                typeof(JumpKing.Camera).GetField("_current_screen", Flags).SetValue(null, 0);
                var player = ResumePlayer(); player.m_body.Position = new Vector2(180, 200);
                var lookup = (Dictionary<Type, IBlockBehaviour>)typeof(BodyComp).GetField("m_blockBehaviourLookup", Flags).GetValue(player.m_body);
                Gimmicks.Add(entry);
                using (var session = new GimmickSession(player))
                {
                    var dependent = new GimmickEntry { Id = "fixture:publication", Kind = "Block", Template = new UnknownProvider.PublishedZone(new Rectangle(0, 0, 8, 8)) };
                    Gimmicks.Add(dependent);
                    Reject(() => session.Apply(new[] {
                        new GimmickRule { Id = entry.Id, Enabled = true, Application = GimmickApplication.Overlay },
                        new GimmickRule { Id = dependent.Id, Enabled = true, Application = GimmickApplication.Overlay }
                    }), "Handler requiring extra publication cannot silently activate a partial mechanic");
                    Require(!lookup.ContainsKey(typeof(UnknownProvider.PortableZone)) && !lookup.ContainsKey(typeof(UnknownProvider.PublishedZone)), "Failed multi-rule activation releases newly acquired handlers");
                    Require(UnknownProvider.Registration.Published == null && ReferenceEquals(GimmickBlocks.Hitboxes.GetValue(screens[0]), source), "Failed handler preparation preserves provider publication and world geometry");
                    session.Apply(new[] { new GimmickRule { Id = entry.Id, Enabled = true, Application = GimmickApplication.Overlay } });
                    Require(lookup.ContainsKey(typeof(UnknownProvider.PortableZone)), "Missing handler registered on vanilla world from explicit IL pair");
                    Require(UnknownProvider.Registration.StartupCalls == 0, "Foreign startup callback never replayed");
                    IBlockBehaviour handler = lookup[typeof(UnknownProvider.PortableZone)];
                    handler.ExecuteBlockBehaviour(new BehaviourContext(player.m_body));
                    Require(handler.ModifyGravity(1, new BehaviourContext(player.m_body)) == 3, "Constructed zone really changes native gravity via injected collision query");
                    session.Apply(new GimmickRule[0]);
                    Require(!lookup.ContainsKey(typeof(UnknownProvider.PortableZone)), "Disable removes only library-owned handler");
                    Require(ReferenceEquals(GimmickBlocks.Hitboxes.GetValue(screens[0]), source), "Disable restores original world with constructed material");
                }
                var existing = new UnknownProvider.PortableHandler(LevelManager.Instance);
                player.m_body.RegisterBlockBehaviour(typeof(UnknownProvider.PortableZone), existing);
                Require(GimmickHandlers.Acquire(player, typeof(UnknownProvider.PortableZone)) == null && ReferenceEquals(lookup[typeof(UnknownProvider.PortableZone)], existing), "Pre-existing provider handler retained");
            }
            Console.WriteLine("[OK] Unseen factory construction, isolated map-ID/collection writes, file-call refusal, custom rectangular IBlock, inferred handler dependency and real gravity on vanilla geometry");
        }
    }
}
