using System;
using System.Reflection;
using System.Threading;
using JumpKing.Mods;

namespace JKRuntime
{
    // the native loader constructs this marker during discovery, before any mod callbacks
    internal sealed class RuntimeModAttribute : JumpKingModAttribute
    {
        public RuntimeModAttribute() : base("JK Runtime") { HarmonyLogGuard.Start(); }
    }

    internal static class HarmonyLogGuard
    {
        private static int started;
        internal static string InstallError;

        internal static void Start()
        {
            if (Interlocked.Exchange(ref started, 1) != 0) return;
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) TryInstall(assembly);
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        { TryInstall(args.LoadedAssembly); }

        private static void TryInstall(Assembly assembly)
        {
            if (assembly.GetName().Name != "0Harmony") return;
            try { HarmonyLogScope.Install(assembly); }
            catch (Exception error)
            {
                // an unsupported logger mustn't prevent the native loader from discovering Runtime
                InstallError = assembly.FullName + ": " + error.GetType().Name + ": " + error.Message;
            }
        }
    }
}
