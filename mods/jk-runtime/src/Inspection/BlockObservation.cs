using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Level.Sampler;
using Microsoft.Xna.Framework;

namespace JKRuntime.Inspection
{
    // capture results at loader call sites. never compile a foreign factory just to watch it
    internal static class BlockObservation
    {
        internal sealed class Record { internal IBlockFactory Factory; internal Color Colour; internal IBlock Block; internal int Screen; }
        private sealed class Identity : IEqualityComparer<IBlock>
        {
            public bool Equals(IBlock a, IBlock b) { return ReferenceEquals(a, b); }
            public int GetHashCode(IBlock block) { return RuntimeHelpers.GetHashCode(block); }
        }
        internal static readonly List<Record> Records = new List<Record>();
        private static OwnedPatches native, sizes;
        internal static string Status = "Observation not installed";
        internal static void Install()
        {
            if (native != null) return;
            var candidate = new OwnedPatches("jk-runtime.block-observation");
            try {
                candidate.Add(typeof(LevelManager).GetMethod("LoadScreens"), prefix: Method("BeginLoad"), priority: 800);
                candidate.Add(typeof(JumpKing.Mods.ModLoader).GetMethod("CallOnLevelStartMethods"),
                    postfix: typeof(InspectorHost).GetMethod("FinalizeWorld", OwnedPatches.Members));
                candidate.ReplaceCalls(typeof(LevelManager).GetMethod("LoadBlocksInterval", OwnedPatches.Members),
                    typeof(IBlockFactory).GetMethod("GetBlock"), Method("CreateBlock"), 1);
                native = candidate; Status = "Native loader observed; unknown loaders use loaded geometry without provenance";
            } catch { candidate.Dispose(); throw; }
        }
        internal static void BeginLoad()
        {
            Records.Clear();
            GimmickBlocks.ClearWorld();
            // install only this reviewed loader adapter, after foreign startup has finished
            if (sizes != null) return;
            OwnedPatches candidate = null;
            try {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "MoreBlockSizes").ToArray();
                if (assemblies.Length == 0) return;
                if (assemblies.Length != 1 || Hash(assemblies[0]) != "2318F08EDD9D7EA8B039D3271E53C4320CE20280A8897DBF19D4259D009619CF") {
                    Status = "Unknown More Block Sizes build: loaded geometry remains available without provenance"; return;
                }
                candidate = new OwnedPatches("jk-runtime.block-observation.sizes");
                var type = assemblies[0].GetType("MoreBlockSizes.Patches.PatchLoadBlocksInterval", true);
                foreach (string name in new[] { "WithCustomSizes", "WithMeshing" })
                    candidate.ReplaceCalls(type.GetMethod(name, OwnedPatches.Members), typeof(IBlockFactory).GetMethod("GetBlock"), Method("CreateBlock"), 1);
                sizes = candidate;
            } catch (Exception error) {
                try { if (candidate != null) candidate.Dispose(); }
                catch (Exception cleanup) { Console.WriteLine("[JK Runtime] Observer cleanup failed: " + cleanup.Message); }
                Status = "Loader provenance unavailable: " + error.GetBaseException().Message;
            }
        }
        private static string Hash(Assembly assembly)
        { using (var stream = File.OpenRead(assembly.Location)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""); }
        private static MethodInfo Method(string name) { return typeof(BlockObservation).GetMethod(name, OwnedPatches.Members); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static IBlock CreateBlock(IBlockFactory factory, Color colour, Rectangle bounds, JumpKing.Workshop.Level level, LevelTexture texture, int screen, int x, int y)
        {
            var block = factory.GetBlock(colour, bounds, level, texture, screen, x, y);
            // diagnostic capacity must never change the loader's result or call count
            if (Records.Count < 131072) Records.Add(new Record { Factory = factory, Colour = colour, Block = block, Screen = screen });
            else Status = "Observation capacity reached: further blocks have unknown provenance";
            return block;
        }
        internal static void ReadFinalized()
        {
            var screens = GimmickBlocks.Screens.GetValue(null) as LevelScreen[];
            if (screens == null) return;
            var live = new HashSet<IBlock>(screens.SelectMany(s => (IBlock[])GimmickBlocks.Hitboxes.GetValue(s)), new Identity());
            foreach (var record in Records)
                if (record.Block == null || live.Contains(record.Block)) GimmickBlocks.Observe(record.Factory.GetType(), record.Colour, record.Block);
        }
    }
}
