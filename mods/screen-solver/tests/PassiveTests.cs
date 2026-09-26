using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JKRuntime;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace ScreenSolver
{
    internal static partial class Tests
    {
        private static Assembly Fixture(string file)
        { return Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file + ".dll")); }
        private static void PassiveParity(Action combined = null)
        {
            var previousReferenceStep = afterReferenceStep; var previousModelStep = afterModelStep;
            var jump = Fixture("JumpKingLastJumpValue"); var mute = Fixture("MuteJumpSfxBlock");
            var sizes = Fixture("MoreBlockSizes"); var slopes = Fixture("ForcedSlopeBlocks");
            var owners = new[] { "Phoenixx19.LastJumpValue.Harmony", "Zebra.MuteJumpSfxBlock.Harmony", "Zebra.MoreBlockSizes.Harmony", "ForcedSlopeBlocks.Harmony", "jk.runtime.run-modifiers" };
            var patches = owners.Select(o => new Harmony(o)).ToArray();
            var unknown = new Harmony("fixture.passive-spoof");
            var calc = jump.GetType("JumpKingLastJumpValue.Models.JumpChargeCalc", true);
            var metrics = calc.GetFields(NativeWorld.Flags).Where(f => f.IsStatic).ToArray();
            var audio = mute.GetType("MuteJumpSfxBlock.Behaviours.BehaviourMuteJumpSfx", true).GetProperty("IsOnBlock");
            var slopePatch = slopes.GetType("ForcedSlopeBlocks.Patches.PatchSlopeBlock", true);
            var slopeList = (IList)slopePatch.GetField("BottomLeftSlopes").GetValue(null);
            var observer = typeof(RuntimeApi).Assembly.GetType("JKRuntime.Gameplay.ModifierRegistrationObserver", true);
            var getEvidence = typeof(RuntimeApi).Assembly.GetType("JKRuntime.Gameplay.ModifierRegistrationEvidence", true).GetMethod("Get", NativeWorld.Flags);
            object[] originalMetrics = metrics.Select(f => f.GetValue(null)).ToArray();
            object originalAudio = audio.GetValue(null, null);
            var priorSlopes = slopeList.Cast<object>().ToArray();
            var threadField = observer.GetField("gameThread", NativeWorld.Flags); object oldThread = threadField.GetValue(null);
            try
            {
                patches[0].Patch(typeof(JumpState).GetMethod("MyRun", NativeWorld.Flags), postfix: new HarmonyMethod(calc.GetMethod("Run", NativeWorld.Flags)));
                patches[1].PatchAll(mute); patches[2].PatchAll(sizes); patches[3].PatchAll(slopes);
                foreach (string name in new[] { "RegisterBehaviour", "RegisterBehaviourBefore", "RegisterBehaviourAfter", "RemoveBehaviour" })
                    patches[4].Patch(typeof(BodyComp).GetMethod(name),
                        prefix: new HarmonyMethod(observer.GetMethod("Prefix", NativeWorld.Flags)),
                        postfix: new HarmonyMethod(observer.GetMethod("Postfix", NativeWorld.Flags)),
                        finalizer: new HarmonyMethod(observer.GetMethod("Finalizer", NativeWorld.Flags)));
                threadField.SetValue(null, System.Threading.Thread.CurrentThread.ManagedThreadId);
                SolveCapture.AuditPatches(typeof(BodyComp).Assembly, new VerticalWindAdapter());
                object[] saved = null; object savedAudio = null;
                afterReferenceStep = () => { if (previousReferenceStep != null) previousReferenceStep(); saved = metrics.Select(f => f.GetValue(null)).ToArray(); savedAudio = audio.GetValue(null, null); };
                afterModelStep = () => { if (previousModelStep != null) previousModelStep(); Check(metrics.Select(f => f.GetValue(null)).SequenceEqual(saved) && Equals(savedAudio, audio.GetValue(null, null)),
                    "Speculative update changed Jump% or mute state"); };
                ControlParity(true);
                if (combined != null) combined();
                afterReferenceStep = previousReferenceStep; afterModelStep = previousModelStep;
                // Constructor hooks must never receive the isolated body via public registration calls.
                var body = new BodyComp(Vector2.Zero, 18, 26);
                Check(getEvidence.Invoke(null, new object[] { body }) == null, "Native constructor generated modifier evidence");
                SlopeSnapshotParity(slopes, slopeList);
                var innocent = new HarmonyMethod(typeof(Tests).GetMethod("InnocentPostfix", NativeWorld.Flags));
                unknown.Patch(typeof(BodyComp).GetMethod("RegisterBehaviour"), postfix: innocent);
                ExpectPatchRefusal(unknown.Id); unknown.UnpatchAll(unknown.Id);
                patches[4].Patch(typeof(BodyComp).GetMethod("RemoveBehaviour"), postfix: innocent);
                ExpectPatchRefusal(owners[4]); patches[4].Unpatch(typeof(BodyComp).GetMethod("RemoveBehaviour"), innocent.method);
                unknown.Patch(typeof(JumpKing.MiscEntities.WorldItems.Inventory.InventoryManager).GetMethod("HasItemEnabled"), postfix: innocent);
                ExpectPatchRefusal(unknown.Id); unknown.UnpatchAll(unknown.Id);
                unknown.Patch(calc.GetMethod("Run", NativeWorld.Flags), postfix: innocent);
                ExpectPatchRefusal(unknown.Id);
                Console.WriteLine("[OK] Actual Jump%, Mute SFX, More Block Sizes and Runtime observer patches: 141120 exact control ticks, isolated observer state, scoped audit exceptions");
            }
            finally
            {
                afterReferenceStep = previousReferenceStep; afterModelStep = previousModelStep;
                unknown.UnpatchAll(unknown.Id); foreach (var h in patches) h.UnpatchAll(h.Id);
                for (int i = 0; i < metrics.Length; i++) metrics[i].SetValue(null, originalMetrics[i]);
                audio.GetSetMethod(true).Invoke(null, new[] { originalAudio });
                slopeList.Clear(); foreach (var value in priorSlopes) slopeList.Add(value);
                threadField.SetValue(null, oldThread);
            }
        }
        private static void SlopeSnapshotParity(Assembly slopes, IList liveList)
        {
            int samples = 0;
            var fix = slopes.GetType("ForcedSlopeBlocks.Patches.PatchSlopeBlock").GetMethod("FixSlopeHitbox");
            foreach (int size in new[] { 3, 8, 37 })
            foreach (SlopeType type in new[] { SlopeType.TopLeft, SlopeType.TopRight, SlopeType.BottomLeft, SlopeType.BottomRight })
            foreach (int variant in new[] { 0, 1, 2 })
            {
                var rect = new Rectangle(101, -97, size, size + 2);
                IBlock source = variant == 2 ? (IBlock)Activator.CreateInstance(slopes.GetType("ForcedSlopeBlocks.Blocks.SlopeBottomLeft"), rect) : new SlopeBlock(rect, type);
                if (variant == 1) fix.Invoke(null, new object[] { source });
                int count = liveList.Count;
                var clone = NativeWorld.CloneBlock(source);
                Check(liveList.Count == count, "Slope clone leaked into the foreign static list");
                var lineField = typeof(SlopeBlock).GetField("m_lines", NativeWorld.Flags);
                Check(!ReferenceEquals(lineField.GetValue(source), lineField.GetValue(clone)), "Slope line array shared with live geometry");
                for (int x = rect.X - 20; x <= rect.Right + 20; x += 2)
                for (int y = rect.Y - 28; y <= rect.Bottom + 28; y += 2)
                {
                    Rectangle a, b; var box = new Rectangle(x, y, 18, 26);
                    Check(source.Intersects(box, out a) == clone.Intersects(box, out b) && a == b, "Corrected/resized slope snapshot changed collision"); samples++;
                }
                var sourceLines = (ErikMaths.Line[])lineField.GetValue(source);
                var before = ((ErikMaths.Line[])lineField.GetValue(clone))[0].p0;
                sourceLines[0].p0.X += 55;
                Check(((ErikMaths.Line[])lineField.GetValue(clone))[0].p0 == before, "Slope clone tracks later live mutation");
            }
            Console.WriteLine("[OK] Slope snapshots: " + samples + " exact collision samples, custom sizes and corrected lines, no foreign constructor calls");
        }
    }
}
