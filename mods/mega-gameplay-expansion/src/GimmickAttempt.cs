using System;
using System.Collections.Generic;
using System.Linq;
using JKRuntime;

namespace MegaGameplayExpansion
{
    // Player-independent work belongs to BeforeAttempt. This plan owns no player,
    // handler instance, live state slot or menu factory from the previous attempt.
    internal sealed class GimmickAttempt : IDisposable
    {
        internal GimmickRule[] Rules;
        internal Type[] StateTypes = new Type[0];
        internal string Error;
        internal bool Disposed;
        internal JumpKing.Level.IBlock[][] Original, Geometry;
        internal static GimmickAttempt Prepare()
        {
            var plan = new GimmickAttempt { Rules = (Settings.Current.GimmickRules ?? new GimmickRule[0])
                .Where(r => r != null && r.Enabled).Select(Gimmicks.Copy).ToArray() };
            if (plan.Rules.Length == 0) return plan;
            try
            {
                Gimmicks.RefreshConfigurations();
                using (RuntimeApi.MeasureStartup("mega-gameplay.prepare-saved-blocks"))
                    foreach (var rule in plan.Rules)
                    {
                        GimmickEntry entry;
                        if (!Gimmicks.Entries.TryGetValue(rule.Id, out entry) || entry.Kind != "Block") continue;
                        GimmickRecipes.Prepare(entry);
                        if (entry.Template != null) GimmickHandlers.Prepare(entry.Template.GetType());
                    }
                using (RuntimeApi.MeasureStartup("mega-gameplay.prepare-state-paths"))
                    plan.StateTypes = GimmickStates.PrepareSelected(plan.Rules.Select(r => r.Id).Where(id => id.StartsWith("state:", StringComparison.Ordinal)).ToArray());
                if (plan.Rules.Any(r => Gimmicks.Entries.ContainsKey(r.Id) && Gimmicks.Entries[r.Id].Kind == "Block"))
                using (RuntimeApi.MeasureStartup("mega-gameplay.prepare-geometry-plan")) {
                    var screens = (JumpKing.Level.LevelScreen[])GimmickBlocks.Screens.GetValue(null);
                    plan.Original = screens.Select(s => (JumpKing.Level.IBlock[])GimmickBlocks.Hitboxes.GetValue(s)).ToArray();
                    plan.Geometry = GimmickSession.CompileGeometry(plan.Original, plan.Rules);
                }
            }
            catch (Exception error) { plan.Error = error.GetBaseException().Message; }
            return plan;
        }
        internal bool MatchesSettings()
        {
            var current = (Settings.Current.GimmickRules ?? new GimmickRule[0]).Where(r => r != null && r.Enabled).ToArray();
            return Matches(current);
        }
        internal bool Matches(GimmickRule[] current)
        {
            return current.Length == Rules.Length && current.Zip(Rules, (a, b) => a.Id == b.Id && a.Application == b.Application
                && a.FirstScreen == b.FirstScreen && a.LastScreen == b.LastScreen && a.Value == b.Value && a.Contract == b.Contract
                && a.ConvertSlopes == b.ConvertSlopes && a.SourceId == b.SourceId && a.WindStrength == b.WindStrength && a.WindImmediate == b.WindImmediate
                && (a.Screens == null) == (b.Screens == null) && (a.Screens ?? new int[0]).SequenceEqual(b.Screens ?? new int[0])).All(equal => equal);
        }
        public void Dispose() { Disposed = true; Rules = new GimmickRule[0]; StateTypes = new Type[0]; Original = Geometry = null; }
    }
}
