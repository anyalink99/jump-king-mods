using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Player;
using JumpKing.BodyCompBehaviours;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal static partial class Tests
    {
        private static int InstalledConstruction(string gameDirectory)
        {
            var lines = new List<string>(); int attempted = 0, constructed = 0, activated = 0, physics = 0, conveyors = 0, conveyorCandidates = 0;
            var workshop = Path.Combine(Directory.GetParent(gameDirectory).Parent.FullName, "workshop/content/1061090");
            var paths = Directory.GetFiles(workshop, "*.dll", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(Path.Combine(gameDirectory, "Content/JKMods"), "*.dll", SearchOption.AllDirectories))
                .Where(p => Path.GetFileName(p) != "0Harmony.dll").ToArray();
            using (new SpriteGameFixture())
            using (new WalkInputFixture())
            {
                IBlock[] original = { new BoxBlock(new Rectangle(0, 320, 480, 40)) };
                var screens = Scene(original); GimmickBlocks.Screens.SetValue(null, screens);
                typeof(LevelManager).GetField("_total_screens", Flags).SetValue(null, 1);
                typeof(JumpKing.Camera).GetField("_current_screen", Flags).SetValue(null, 0);
                var player = ResumePlayer(); player.m_body.Position = new Vector2(180, 200);
                foreach (string path in paths)
                {
                    Assembly assembly; Type[] types;
                    try { assembly = Assembly.LoadFrom(path); types = assembly.GetTypes(); }
                    catch (Exception error) { lines.Add("ASSEMBLY " + Path.GetFileName(path) + ": " + error.GetBaseException().Message); continue; }
                    foreach (var type in types.Where(t => !t.IsAbstract && typeof(IBlockFactory).IsAssignableFrom(t)))
                    {
                        IBlockFactory factory;
                        try
                        {
                            var constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                            if (constructor == null) continue;
                            factory = (IBlockFactory)new GimmickConstruction(assembly).Construct(constructor);
                        }
                        catch (Exception error) { lines.Add("FACTORY " + type.FullName + ": " + error.GetBaseException().Message); continue; }
                        var colours = new HashSet<Color>();
                        foreach (var field in type.GetFields(Flags))
                        {
                            if (field.FieldType == typeof(Color)) colours.Add((Color)field.GetValue(factory));
                            else if (typeof(IEnumerable<Color>).IsAssignableFrom(field.FieldType))
                            { var palette = field.GetValue(factory) as IEnumerable<Color>; if (palette != null) colours.UnionWith(palette.Take(4096)); }
                        }
                        foreach (var colour in colours)
                        {
                            attempted++;
                            var entry = new GimmickEntry { Id = Gimmicks.BlockId(type, colour), Kind = "Block", Factory = factory, Colour = colour };
                            GimmickRecipes.Prepare(entry);
                            if (entry.Template == null) { lines.Add("UNAVAILABLE " + type.FullName + " " + GimmickBlocks.RGB(colour) + ": " + entry.Error); continue; }
                            constructed++; Gimmicks.Add(entry);
                            if (entry.Template.GetType().FullName == "ConveyorBlockMod.Blocks.ConveyorBlock") conveyorCandidates++;
                            try
                            {
                                var rule = new GimmickRule { Id = entry.Id, Enabled = true, Application = GimmickApplication.ExistingBlocks };
                                using (var session = new GimmickSession(player))
                                {
                                    session.Apply(new[] { rule });
                                    Require(((IBlock[])GimmickBlocks.Hitboxes.GetValue(screens[0]))[0].GetType() == entry.Template.GetType(), "Installed type survives replacement");
                                    // A real installed provider, asserted only in tests; production
                                    // construction/handler matching never uses this name or colour.
                                    if (entry.Template.GetType().FullName == "JumpKing_Expansion_Blocks.Blocks.HighGravity")
                                    {
                                        rule.Application = GimmickApplication.Overlay; session.Apply(new[] { rule });
                                        var lookup = (Dictionary<Type, IBlockBehaviour>)typeof(BodyComp).GetField("m_blockBehaviourLookup", Flags).GetValue(player.m_body);
                                        var handler = lookup[entry.Template.GetType()];
                                        var context = new BehaviourContext(player.m_body);
                                        typeof(BehaviourContextCollisionInfo).GetMethod("AggregateCollisionInfo", Flags).Invoke(context.CollisionInfo,
                                            new object[] { LevelManager.GetCollisionInfo(player.m_body.GetHitbox()) });
                                        handler.ExecuteBlockBehaviour(context);
                                        Require(Math.Abs(handler.ModifyGravity(1, context) - 1.5f) < .0001f, "Installed Expansion gravity actually changes on vanilla overlay");
                                        Require(Math.Abs(handler.ModifyXVelocity(1, context) - .9f) < .0001f, "Installed Expansion horizontal modifier active");
                                        session.Apply(new GimmickRule[0]);
                                        Require(!lookup.ContainsKey(entry.Template.GetType()), "Installed handler removed on release");
                                        context = new BehaviourContext(player.m_body);
                                        typeof(BehaviourContextCollisionInfo).GetMethod("AggregateCollisionInfo", Flags).Invoke(context.CollisionInfo,
                                            new object[] { LevelManager.GetCollisionInfo(player.m_body.GetHitbox()) });
                                        handler.ExecuteBlockBehaviour(context);
                                        Require(handler.ModifyGravity(1, context) == 1, "Restored vanilla geometry no longer triggers installed gravity");
                                        physics++;
                                    }
                                    if (entry.Template.GetType().FullName == "ConveyorBlockMod.Blocks.ConveyorBlock")
                                    {
                                        rule.Application = GimmickApplication.Auto; session.Apply(new[] { rule });
                                        Require(GimmickClassification.Resolve(entry, rule) == GimmickApplication.Surface, "Installed conveyor defaults to surface replacement");
                                        var lookup = (Dictionary<Type, IBlockBehaviour>)typeof(BodyComp).GetField("m_blockBehaviourLookup", Flags).GetValue(player.m_body);
                                        var handler = lookup[entry.Template.GetType()];
                                        var context = new BehaviourContext(player.m_body);
                                        var aggregate = typeof(BehaviourContextCollisionInfo).GetMethod("AggregateCollisionInfo", Flags);
                                        var advance = typeof(BehaviourContext).GetMethod("ClearCollisionForNewFrame", Flags);
                                        aggregate.Invoke(context.CollisionInfo, new object[] { LevelManager.GetCollisionInfo(player.m_body.GetHitbox()) });
                                        advance.Invoke(context, null);
                                        var contact = handler.GetType().GetProperty("IsPlayerOnBlock");
                                        contact.SetValue(handler, true, null);
                                        bool reproduced = false;
                                        try { handler.ModifyXVelocity(0, context); } catch (NullReferenceException) { reproduced = true; }
                                        finally { contact.SetValue(handler, false, null); }
                                        Require(reproduced, "Installed conveyor reproduces missing-block crash when its contact flag is forced");
                                        GimmickStates.Discover(player);
                                        var slot = Gimmicks.Entries.Values.First(e => e.Slot != null && ReferenceEquals(e.Slot.Target, handler) && e.Slot.Getter == contact.GetGetMethod()).Slot;
                                        Reject(() => new StateOverride(slot, new GimmickRule { Value = "True" }, 1), "Installed conveyor contact cannot be forced through MGE");
                                        Require(handler.ModifyXVelocity(0, context) == 0, "Rejected override leaves conveyor inactive away from terrain");
                                        aggregate.Invoke(context.CollisionInfo, new object[] { LevelManager.GetCollisionInfo(new Rectangle(180, 319, 16, 2)) });
                                        handler.ExecuteBlockBehaviour(context);
                                        advance.Invoke(context, null);
                                        float speed = (float)entry.Template.GetType().GetProperty("Speed").GetValue(entry.Template, null);
                                        Require((bool)contact.GetValue(handler, null) && speed != 0 && handler.ModifyXVelocity(0, context) == speed,
                                            "Real conveyor contact applies the selected direction and speed");
                                        session.Apply(new GimmickRule[0]);
                                        aggregate.Invoke(context.CollisionInfo, new object[] { LevelManager.GetCollisionInfo(new Rectangle(180, 319, 16, 2)) });
                                        handler.ExecuteBlockBehaviour(context); advance.Invoke(context, null);
                                        Require(!(bool)contact.GetValue(handler, null) && handler.ModifyXVelocity(0, context) == 0,
                                            "Restored ordinary terrain no longer activates conveyor movement");
                                        conveyors++;
                                    }
                                }
                                Require(ReferenceEquals(GimmickBlocks.Hitboxes.GetValue(screens[0]), original), "Installed factory geometry restores");
                                activated++; lines.Add("READY " + entry.Template.GetType().FullName + " " + GimmickBlocks.RGB(colour));
                            }
                            catch (Exception error) { lines.Add("DEPENDENCY " + entry.Template.GetType().FullName + ": " + error.GetBaseException().Message); }
                        }
                    }
                }
            }
            string summary = "Installed factory audit: " + constructed + "/" + attempted + " constructed; " + activated + " applied/released on vanilla fixture. No startup callbacks or source-map visits.";
            lines.Insert(0, summary);
            File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installed-construction.txt"), lines);
            Console.WriteLine(summary);
            Require(physics > 0, "Installed Expansion gravity physics fixture must run and pass");
            Require(conveyors > 0 && conveyors == conveyorCandidates, "Every installed conveyor direction/speed must pass contact, refusal and restore checks; passed " + conveyors + "/" + conveyorCandidates);
            Console.WriteLine("[OK] All " + conveyors + " installed conveyor materials: crash reproduced, invalid contact refused, real surface movement and restore verified");
            Console.WriteLine("[OK] Installed Expansion gravity and horizontal modifiers activate and release on vanilla geometry");
            return 0;
        }
    }
}
