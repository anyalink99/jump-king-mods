using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using JumpKing;
using JumpKing.Level;
using JumpKing.Level.Sampler;
using Microsoft.Xna.Framework;

namespace JKRuntime.Inspection
{
    internal static partial class Tests
    {
        private static void SlopeLoadingRegression(string game, string order, string mode)
        {
            string workshop = Path.GetFullPath(Path.Combine(game, "..", "..", "workshop", "content", "1061090"));
            var forced = Assembly.LoadFrom(Path.Combine(workshop, "3353090188", "ForcedSlopeBlocks.dll"));
            Action forcedStart = () => forced.GetType("ForcedSlopeBlocks.ModEntry").GetMethod("BeforeLevelLoad").Invoke(null, null);
            if (order == "runtime-first") { GimmickBlocks.Install(); forcedStart(); }
            else { forcedStart(); GimmickBlocks.Install(); }
            Assembly sizes = null;
            if (mode != "native") {
                sizes = Assembly.LoadFrom(Path.Combine(workshop, "3470750355", "MoreBlockSizes.dll"));
                sizes.GetType("MoreBlockSizes.ModEntry").GetMethod("BeforeLevelLoad").Invoke(null, null);
            }
            LevelManager.RegisterBlockFactory(new UnknownProvider.PortableFactory());
            var foreignMod = new JumpKing.Mods.ModAssembly(forced, new JumpKing.Mods.JumpKingModAttribute("Slope callback fixture"));
            foreignMod.OnLevelStartMethods.Add(forced.GetType("ForcedSlopeBlocks.ModEntry").GetMethod("OnLevelStart"));
            JumpKing.Mods.ModLoader.Instance.LoadedMods.Add(foreignMod);
            int preparations = 0;
            using (new SpriteGameFixture())
            using (RuntimeApi.Geometry.RegisterTerrainPreparation("fixture.inspector", types => {
                Require(Gimmicks.Session == null && types.Contains(typeof(UnknownProvider.PortableZone)), "Selected terrain resources prepare before attaching player behavior");
                preparations++;
            })) {
                var level = (JumpKing.Workshop.Level)FormatterServices.GetUninitializedObject(typeof(JumpKing.Workshop.Level));
                var settings = new JumpKing.Workshop.Level.LevelSettings { Tags = new[] { "FixMySlopes" } };
                typeof(JumpKing.Workshop.Level).GetField("_level", Flags).SetValue(level, settings);
                Game1.instance.contentManager.level = level;
                for (int visit = 0; visit < 3; visit++) {
                    BlockObservation.BeginLoad();
                    var texture = LevelTexture.FromDimensions(60, 45);
                    var pixels = (Color[])typeof(LevelTexture).GetField("m_data", Flags).GetValue(texture);
                    pixels[10 + 10 * 60] = Color.Red; pixels[10 + 9 * 60] = pixels[11 + 10 * 60] = Color.Black;
                    pixels[20 + 10 * 60] = new Color(255, 2, 0); pixels[30 + 10 * 60] = new Color(255, 3, 0);
                    if (sizes != null) {
                        var type = sizes.GetType("MoreBlockSizes.Patches.PatchLoadBlocksInterval");
                        type.GetProperty("CanMesh").SetValue(null, mode == "mesh", null);
                        var dimensions = LevelTexture.FromDimensions(60, 45);
                        var data = (Color[])typeof(LevelTexture).GetField("m_data", Flags).GetValue(dimensions);
                        for (int i = 0; i < data.Length; i++) data[i] = new Color(0, 7, 5);
                        type.GetProperty("Sizes").SetValue(null, mode == "sizes" ? dimensions : null, null);
                    }
                    var blocks = (IBlock[])typeof(LevelManager).GetMethod("LoadBlocksInterval", Flags).Invoke(null,
                        new object[] { texture, level, 0, false, null, 0f, null });
                    var slopes = blocks.OfType<SlopeBlock>().ToArray();
                    Require(slopes.Length == 3, "The real loader must create ordinary, forced and fixed slopes");
                    var list = (IList)forced.GetType("ForcedSlopeBlocks.Patches.PatchSlopeBlock").GetField("BottomLeftSlopes").GetValue(null);
                    Require(slopes.All(s => list.Contains(s)) && list.Count == 3, "Every live slope reaches the foreign constructor patch; observation creates no clones");
                    Require(BlockObservation.Records.Count == blocks.Length && blocks.All(b => BlockObservation.Records.Count(r => ReferenceEquals(r.Block, b)) == 1), "Loader provenance records each real result once");
                    GimmickBlocks.Screens.SetValue(null, Scene(blocks));
                    InspectorSettings.Current.GimmickRules = new[] { new GimmickRule {
                        Id = Gimmicks.BlockId(typeof(UnknownProvider.PortableFactory), UnknownProvider.PortableFactory.Code),
                        Enabled = true, Application = GimmickApplication.Overlay
                    } };
                    InspectorHost.PrepareAttempt();
                    var plan = (GimmickAttempt)typeof(InspectorHost).GetField("plan", Flags).GetValue(null);
                    Require(plan != null && plan.Error == null && plan.Geometry == null, "Actual host defers geometry until native correction: " + (plan == null ? Gimmicks.Status : plan.Error));
                    JumpKing.Mods.ModLoader.Instance.CallOnLevelStartMethods();
                    Require(plan.Geometry != null && plan.Error == null && ReferenceEquals(plan.Original[0], blocks), "Native callback postfix finalizes the actual host plan: " + plan.Error);
                    Require(preparations == visit + 1, "Each finalized saved plan prepares terrain resources before activation");
                    GimmickBlocks.DiscoverLoaded();
                    Require(list.Count == 0, "Finalized inspection doesn't invoke foreign constructors");
                    foreach (var slope in slopes) {
                        var lines = (ErikMaths.Line[])typeof(SlopeBlock).GetField("m_lines", Flags).GetValue(slope);
                        Require(lines[1].p1.X == slope.GetRect().Right, "FixMySlopes and explicit fixed blocks retain corrected geometry");
                        if (slope.GetType() != typeof(SlopeBlock)) continue;
                        var copy = (SlopeBlock)GimmickBlocks.Copy(slope, slope.GetRect());
                        var copied = (ErikMaths.Line[])typeof(SlopeBlock).GetField("m_lines", Flags).GetValue(copy);
                        Require(!ReferenceEquals(lines, copied) && lines.SequenceEqual(copied) && list.Count == 0, "Slope copies retain corrected lines without constructor side effects");
                        var rect = slope.GetRect(); rect.Offset(50, -70); rect.Width *= 2;
                        var moved = (SlopeBlock)GimmickBlocks.Copy(slope, rect);
                        var movedLines = (ErikMaths.Line[])typeof(SlopeBlock).GetField("m_lines", Flags).GetValue(moved);
                        Require(movedLines[1].p1 == new Point(rect.Right, rect.Bottom), "Translated/resized copies preserve the corrected triangle");
                    }
                    InspectorHost.Stop();
                }
            }
            Console.WriteLine("[OK] Forced Slopes: " + order + ", " + mode + ", three loads, exact provenance and constructor-free copies");
        }
    }
}
