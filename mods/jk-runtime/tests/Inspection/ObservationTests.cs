using System;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace JKRuntime.Inspection
{
    internal static partial class Tests
    {
        private static void ObservationRegression()
        {
            var previous = GimmickBlocks.Screens.GetValue(null);
            try {
                BlockObservation.Records.Clear(); GimmickBlocks.ClearWorld();
                var block = new UnknownProvider.Material(new Rectangle(8, 24, 32, 16), 42);
                GimmickBlocks.Screens.SetValue(null, Scene(new IBlock[] { block }));
                GimmickBlocks.DiscoverLoaded();
                string id = "loaded:" + Gimmicks.TypeId(block.GetType());
                Require(Gimmicks.Entries.ContainsKey(id) && Gimmicks.Entries[id].Factory == null
                    && BlockObservation.Records.Count == 0, "An unobserved loader still exposes geometry without invented factory provenance");
                var factory = new UnknownProvider.Factory();
                var entry = new GimmickEntry { Id = "observation:unprepared", Kind = "Block", Label = "Unprepared material",
                    Factory = factory, Colour = UnknownProvider.Factory.Code };
                Gimmicks.Add(entry);
                var page = new GimmickPage(null, new JumpKing.PauseMenu.GuiFormat(), false);
                PageCall(page, "Details", entry.Id);
                Require(!entry.ConstructionAttempted && entry.Template == null && factory.Calls == 0,
                    "Opening a material card neither calls its factory nor interprets a construction recipe");
                BlockObservation.BeginLoad();
                Require(!Gimmicks.Entries.ContainsKey(id) && !Gimmicks.Entries.ContainsKey(entry.Id), "A new world drops old material templates");
            } finally { GimmickBlocks.Screens.SetValue(null, previous); BlockObservation.Records.Clear(); GimmickBlocks.ClearWorld(); }
            Console.WriteLine("[OK] Unobserved-loader fallback, honest provenance, read-only cards and world cache invalidation");
        }
    }
}
