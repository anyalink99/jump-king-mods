using JKRuntime.Input;
using System;
using System.IO;
using System.Reflection;

// Isolated process: exercise the shipped Jump% and its Harmony without opening
// a game window or modifying third-party assemblies on disk.
internal static class JumpPercentContractTests
{
    private static int Main(string[] args)
    {
        try
        {
            string game = args[0], percentDirectory = args[1];
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs request)
            {
                string filename = new AssemblyName(request.Name).Name + ".dll";
                foreach (string directory in new[] { game, percentDirectory })
                {
                    string path = Path.Combine(directory, filename);
                    if (File.Exists(path)) return Assembly.LoadFrom(path);
                }
                return null;
            };
            // build.ps1 copies these exact installed binaries beside the test.
            // Use the default context so typed test doubles and the mod share
            // one JumpKing identity (not separate LoadFrom/default identities).
            Assembly.Load("MonoGame.Framework");
            Assembly.Load("JumpKing");
            AppDomain.CurrentDomain.SetData("SubframeCharge.LogDirectory", AppDomain.CurrentDomain.BaseDirectory);
            Assembly mod = Assembly.LoadFrom(args[2]);
            Type integration = mod.GetType("SubframeCharge.JumpPercentIntegration", true);
            const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
            MethodInfo ensure = integration.GetMethod("EnsureDisplayHook", Static);
            if (args[3] == "late")
            {
                ensure.Invoke(null, null);
                if ((bool)integration.GetField("displayPatched", Static).GetValue(null))
                    throw new Exception("Patched a missing optional mod");
                integration.GetField("nextDisplayAttempt", Static).SetValue(null, 0L);
            }
            Assembly percent = Assembly.LoadFrom(Path.Combine(percentDirectory, "JumpKingLastJumpValue.dll"));
            ensure.Invoke(null, null);
            if (!(bool)integration.GetField("displayPatched", Static).GetValue(null))
                throw new Exception("Actual Jump% DrawText was not patched; inspect SubframeCharge.log");
            // Resolve the engine exactly as the installed mod/integration does.
            // Loading another path can create a second weak-named Harmony
            // identity after a shared engine has already bound the reference.
            Assembly harmony = null;
            foreach (var reference in percent.GetReferencedAssemblies())
                if (reference.Name == "0Harmony") harmony = Assembly.Load(reference);
            if (harmony == null) throw new Exception("Jump% has no Harmony dependency");
            Type harmonyType = harmony.GetType("HarmonyLib.Harmony", true);
            MethodInfo draw = percent.GetType("JumpKingLastJumpValue.Models.GameLoopDraw", true)
                .GetMethod("DrawText", Static);
            object info = harmonyType.GetMethod("GetPatchInfo").Invoke(null, new object[] { draw });
            if (info == null) throw new Exception("No actual Harmony patch info");
            object owners = info.GetType().GetProperty("Owners").GetValue(info, null);
            bool owned = false;
            foreach (object owner in (System.Collections.IEnumerable)owners)
                owned |= (string)owner == "SubframeCharge.JumpPercent.Display";
            if (!owned) throw new Exception("Missing display patch owner");
            ensure.Invoke(null, null); // idempotent installation

            Type calc = percent.GetType("JumpKingLastJumpValue.Models.JumpChargeCalc", true);
            PropertyInfo frames = calc.GetProperty("JumpFrames", BindingFlags.Public | BindingFlags.Static);
            PropertyInfo percentage = calc.GetProperty("JumpPercentage", BindingFlags.Public | BindingFlags.Static);
            frames.SetValue(null, 27, null);
            percentage.SetValue(null, 28f / 36f, null);
            MethodInfo record = integration.GetMethod("RecordLaunch", Static);
            record.Invoke(null, new object[] { null, null, false });
            if ((int)frames.GetValue(null, null) != 27
                || (float)percentage.GetValue(null, null) != 28f / 36f)
                throw new Exception("Unsupported input overwrote native Jump%");
            record.Invoke(null, new object[] { 13, 0.2, false });
            if ((int)frames.GetValue(null, null) != 12
                || Math.Abs((float)percentage.GetValue(null, null) - 13f / 36f) > 0.000001f)
                throw new Exception("Corrected input failed to update actual Jump%");
            Console.WriteLine("[OK] Installed Jump% + Harmony contract, discovery=" + args[3]);
            DirectInputDiscoveryContractTests.Run(mod);
            MouseBindingContractTests.Run(mod);
            ChargeLifecycleContractTests.Run(mod, harmony);
            mod.GetType("SubframeCharge.DiagnosticLog", true).GetMethod("Flush", Static).Invoke(null, null);
            string diagnostics = string.Empty;
            string logName = (string)mod.GetType("SubframeCharge.DiagnosticLog", true)
                .GetField("FileName", Static).GetRawConstantValue();
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, logName);
            foreach (string suffix in new[] { ".2", ".1", string.Empty })
                if (File.Exists(logPath + suffix)) diagnostics += File.ReadAllText(logPath + suffix);
            int sessionStart = diagnostics.LastIndexOf("session start id=", StringComparison.Ordinal);
            if (sessionStart < 0) throw new Exception("Missing session header in retained diagnostic history");
            diagnostics = diagnostics.Substring(sessionStart);
            foreach (string required in new[] { "inputDiagnostics=1", "reason=unsupported-device-active",
                "reason=corrected-release", "sampledRelease=True", "samplerEnabled=True" })
                if (!diagnostics.Contains(required)) throw new Exception("Missing diagnostic evidence: " + required);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
