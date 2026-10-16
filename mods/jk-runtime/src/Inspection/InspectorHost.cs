using System;
using System.Linq;
using JumpKing.GameManager;

namespace JKRuntime.Inspection
{
    internal static class InspectorHost
    {
        private static RuntimeScope attempt, active;
        private static GimmickAttempt plan;
        private static bool finalized;
        internal static void Install() { GimmickBlocks.Install(); }
        internal static void PrepareAttempt()
        {
            attempt = new RuntimeScope();
            GimmickBlocks.ClearWorld();
            try {
                Gimmicks.Initialize();
                // empty configurations don't scan factories, maps or foreign state
                if (!(InspectorSettings.Current.GimmickRules ?? new GimmickRule[0]).Any(r => r != null && r.Enabled)) return;
                GimmickBlocks.DiscoverPalette(new Microsoft.Xna.Framework.Color[0]);
                Gimmicks.RefreshConfigurations();
                plan = attempt.Own(GimmickAttempt.Prepare(false));
                // native OnLevelStart may still correct collision lines; don't keep an early geometry plan
            } catch (Exception error) { Gimmicks.Status = "Inspector preparation failed: " + error.GetBaseException().Message; }
        }
        internal static void Activate()
        {
            if (GameLoop.m_player == null || active != null) return;
            if (!finalized) FinalizeWorld();
            active = new RuntimeScope();
            try {
                Gimmicks.Session = active.Own(new GimmickSession(GameLoop.m_player, plan));
                active.Own(State.GameState.Snapshots.Register(Gimmicks.Session));
                Gimmicks.Session.Activate();
            } catch (Exception error) {
                Stop();
                Gimmicks.Status = "Inspector activation failed: " + error.GetBaseException().Message;
                Console.WriteLine("[JK Runtime] " + Gimmicks.Status);
            }
        }
        internal static void FinalizeWorld()
        {
            finalized = true;
            if (plan == null || plan.Rules.Length == 0) return;
            try {
                using (RuntimeApi.MeasureStartup("runtime.inspector.final-geometry")) {
                    GimmickBlocks.DiscoverLoaded(); Gimmicks.RefreshConfigurations();
                    plan.FinalizeGeometry();
                    if (plan.Geometry != null) RuntimeApi.Geometry.PrepareTerrain(plan.Geometry);
                }
            } catch (Exception error) { plan.Error = error.GetBaseException().Message; }
        }
        internal static void Stop()
        {
            if (active != null) { active.Dispose(); active = null; }
            Gimmicks.Session = null;
            if (attempt != null) { attempt.Dispose(); attempt = null; }
            plan = null;
            finalized = false;
        }
        internal static void ClearWorld() { BlockObservation.Records.Clear(); GimmickBlocks.ClearWorld(); }
    }
}
