using System;
using JumpKing;
using JumpKing.Controller;
using Microsoft.Xna.Framework;
namespace JKRuntime
{
    internal static class StartupDiagnosticHooks
    {
        private static DiagnosticHooks hooks;
        internal static void Configure(bool enabled)
        {
            if (!enabled) { if(hooks != null) { hooks.Dispose(); hooks = null; } return; }
            if(hooks != null) return;
            hooks = new DiagnosticHooks("jk-runtime.startup-diagnostics");
            try
            {
                if (enabled)
                {
                    // Install before patching the caller so its replacement body
                    // cannot inline the small component dispatch methods.
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
                    Hook(typeof(Game1), "Update", "TraceUpdateBegin", true);
                    Hook(typeof(Game1), "Update", "TraceUpdateEnd", false);
                    Hook(typeof(Game1), "Draw", "TraceDrawBegin", true);
                    Hook(typeof(Game1), "Draw", "TraceDrawEnd", false);
                }
            }
            catch { hooks.Dispose(); hooks = null; throw; }
        }
        private static void Hook(Type type, string method, string callback, bool prefix)
        { hooks.Add(type, method, typeof(UI.PointerHooks), prefix ? callback : null, prefix ? null : callback); }
    }
}
