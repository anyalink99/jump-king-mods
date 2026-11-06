using System;
using JumpKing;
using JumpKing.Controller;
using Microsoft.Xna.Framework;
namespace JKRuntime
{
    internal static class StartupDiagnosticHooks
    {
        private static DiagnosticHooks hooks;
        private static OwnedPatches bodyCalls;
        private static bool detailedHooks;
        internal static void Configure(bool enabled, bool detailed = true)
        {
            if(hooks != null && enabled && detailedHooks != detailed) Configure(false);
            if (!enabled) {
                if(bodyCalls != null) { bodyCalls.Dispose(); bodyCalls = null; }
                if(hooks != null) { hooks.Dispose(); hooks = null; } return;
            }
            if(hooks != null) return;
            detailedHooks = detailed;
            hooks = new DiagnosticHooks("jk-runtime.startup-diagnostics");
            try
            {
                if (detailed)
                {
                    bodyCalls = new OwnedPatches("jk-runtime.startup-diagnostics");
                    bodyCalls.ReplaceCalls(typeof(JumpKing.Player.BodyComp).GetMethod("UpdateInternal",OwnedPatches.Members),
                        typeof(JumpKing.API.IBodyCompBehaviour).GetMethod("ExecuteBehaviour"),
                        typeof(StartupDiagnosticHooks).GetMethod("ExecuteBody",OwnedPatches.Members),1);
                    // Install before patching the caller so its replacement body
                    // can't inline the small component dispatch methods
                    foreach (string method in new[] { "LowUpdate", "LowLateUpdate" })
                    {
                        Hook(typeof(EntityComponent.Component), method, "TraceWorkBegin", true);
                        Hook(typeof(EntityComponent.Component), method, "TraceObjectEnd", false);
                    }
                    Hook(typeof(EntityComponent.Entity), "UpdateComponents", "TraceWorkBegin", true);
                    Hook(typeof(EntityComponent.Entity), "UpdateComponents", "TraceObjectEnd", false);
                    foreach (Type type in new[] { typeof(ControllerManager), typeof(JumpKing.Level.LevelManager),
                        typeof(EntityComponent.EntityManager), typeof(WeatherManager), typeof(JumpGame) })
                    {
                        Hook(type, "Update", "TraceWorkBegin", true);
                        Hook(type, "Update", "TracePhaseEnd", false);
                    }
                }
                Hook(typeof(Game1), "Update", "TraceUpdateBegin", true);
                Hook(typeof(Game1), "Update", "TraceUpdateEnd", false);
                Hook(typeof(Game1), "Draw", "TraceDrawBegin", true);
                Hook(typeof(Game1), "Draw", "TraceDrawEnd", false);
            }
            catch {
                if(bodyCalls != null) { bodyCalls.Dispose(); bodyCalls = null; }
                hooks.Dispose(); hooks = null; throw;
            }
        }
        internal static bool ExecuteBody(JumpKing.API.IBodyCompBehaviour behaviour,JumpKing.BodyCompBehaviours.BehaviourContext context)
        {
            long begin=StartupTrace.BeginWork();
            try { return behaviour.ExecuteBehaviour(context); }
            finally {
                if(begin!=0)StartupTrace.EndWork("ExecuteBehaviour",behaviour.GetType(),begin,System.Diagnostics.Stopwatch.GetTimestamp(),true);
            }
        }
        private static void Hook(Type type, string method, string callback, bool prefix)
        { hooks.Add(type, method, typeof(UI.PointerHooks), prefix ? callback : null, prefix ? null : callback); }
    }
}
