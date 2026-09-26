using System;
using System.IO;
using System.Linq;
using System.Reflection;
using EntityComponent.BT;
using JKRuntime.Gameplay;
using JumpKing.Player;

namespace HammerKing
{
    internal static partial class Tests
    {
        private static Assembly chargeAssembly;
        private static Assembly casualAssembly;
        private static Assembly LoadImplementation(string path)
        {
            var package = Assembly.LoadFrom(path);
            using (var stream = package.GetManifestResourceStream("JKRuntime.Module"))
            {
                if (stream == null) return package;
                using (var bytes = new MemoryStream()) { stream.CopyTo(bytes); return Assembly.Load(bytes.ToArray()); }
            }
        }
        private static void ConfigureChargeTest(string path, string harmonyPath)
        {
            var loop = new JumpKing.GameManager.GameLoop();
            var completion = loop.GetType().GetField("m_ending_body_modifiers", Flags);
            completion.SetValue(loop, Activator.CreateInstance(completion.FieldType, true));
            var save = typeof(PlayerEntity).Assembly.GetType("JumpKing.SaveThread.SaveLube", true);
            save.GetProperty("CombinedSave").SetValue(null, new JumpKing.SaveThread.CombinedSaveFile {
                full_run = new JumpKing.SaveThread.SaveComponents.SaveCompCushion<JumpKing.SaveThread.SaveComponents.FullRunSave> { initialized = true }
            }, null);
            JumpKing.SaveThread.SaveComponents.FullRunSave.fullRunSave = new JumpKing.SaveThread.SaveComponents.FullRunSave();
            AppDomain.CurrentDomain.SetData("SubframeCharge.LogDirectory", AppDomain.CurrentDomain.BaseDirectory);
            var package = Assembly.LoadFrom(path);
            using (var stream = package.GetManifestResourceStream("JKRuntime.Module"))
            {
                if (stream == null) chargeAssembly = package;
                else using (var bytes = new MemoryStream()) { stream.CopyTo(bytes); chargeAssembly = Assembly.Load(bytes.ToArray()); }
            }
            // Production activation loads settings before installing the charge
            // policy. Establish that prerequisite without reading user files.
            var settings = chargeAssembly.GetType("SubframeCharge.SettingsStore", true);
            var defaults = chargeAssembly.GetType("SubframeCharge.SubframeChargeSettings", true);
            settings.GetProperty("Current", Flags).SetValue(null, Activator.CreateInstance(defaults), null);
            // Do not read physical bindings or create a native window in this
            // fixture. Exercise the real installer, sampler lifetime and graph.
            var harmonyAssembly = Assembly.LoadFrom(harmonyPath);
            var harmonyType = harmonyAssembly.GetType("HarmonyLib.Harmony", true);
            var methodType = harmonyAssembly.GetType("HarmonyLib.HarmonyMethod", true);
            var harmony = Activator.CreateInstance(harmonyType, new object[] { "hammer-king.tests.charge" });
            var patch = harmonyType.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5);
            var state = chargeAssembly.GetType("SubframeCharge.SubframeChargeState", true);
            foreach (string method in new[] { "ConfigureSampler", "IsGameActive" })
            {
                var prefix = Activator.CreateInstance(methodType, new object[] { typeof(Tests).GetMethod("NoPhysicalInput", Flags) });
                patch.Invoke(harmony, new object[] { state.GetMethod(method, Flags), prefix, null, null, null });
            }
        }
        private static bool NoPhysicalInput(ref bool __result) { __result = false; return false; }

        private static void ChargeComposition(PlayerEntity player, BehaviorTreeComp tree)
        {
            var installer = chargeAssembly.GetType("SubframeCharge.SubframeChargeInstaller", true);
            var install = installer.GetMethod("Install", Flags);
            var detach = installer.GetMethod("DetachChargePolicy", Flags);
            var current = installer.GetField("replacementJumpState", Flags);
            installer.GetMethod("EnsureTrajectoryProbe", Flags).Invoke(null, new object[] { player });
            var original = Nodes(tree);
            try
            {
                foreach (bool correction in new[] { false, true })
                foreach (bool controllerFirst in new[] { false, true })
                {
                    HammerNativeControl controller = null;
                    Action enable = delegate { controller = new HammerNativeControl(player, tree); };
                    Action disable = delegate { if (controller != null) { controller.Dispose(); controller = null; } };
                    if (controllerFirst) JumpSlot.Recompose(enable);
                    using (var policy = JumpSlot.RegisterChargePolicy(
                        delegate { install.Invoke(null, new object[] { player, correction }); },
                        delegate { detach.Invoke(null, null); }))
                    {
                        try
                        {
                            if (!controllerFirst)
                            {
                                Check(current.GetValue(null) != null, "Actual SFC installed before controller");
                                JumpSlot.Recompose(enable);
                            }
                            for (int cycle = 0; cycle < 3; cycle++)
                            {
                                Check(current.GetValue(null) == null && JumpSlot.ChargePolicySuspended, "SFC deferred while another controller owns native jump");
                                JumpSlot.Refresh();
                                Check(current.GetValue(null) == null, "SFC refresh cannot override controller");
                                JumpSlot.Recompose(disable);
                                Check(current.GetValue(null) != null && !JumpSlot.ChargePolicySuspended, "Actual SFC automatically resumes");
                                Check(Nodes(tree).Any(n => n.GetType().Name == "SubframeChargeState"), "SFC restored to the live native graph");
                                JumpSlot.Recompose(enable);
                            }
                        }
                        finally { JumpSlot.Recompose(disable); }
                    }
                    Check(original.SetEquals(Nodes(tree)), "Both modules release every native graph edge");
                    Check(JKRuntime.Input.SharedActionSampler.ActiveStreams == 0, "No leaked SFC sampler after toggles");
                }
                if (chargeAssembly.GetName().Version >= new Version(0, 15, 1, 0))
                {
                    // Legacy controllers can replace the graph without declaring
                    // a reservation. SFC must also handle that without stealing it.
                    var nativeJump = tree.GetRaw().FindNode<JumpState>();
                    var foreign = new ForeignJump(player);
                    var bindings = JumpNodeBindings.Replace(tree.GetRaw(), player, nativeJump, foreign);
                    try
                    {
                        foreach (bool correction in new[] { false, true })
                        {
                            install.Invoke(null, new object[] { player, correction });
                            Check(current.GetValue(null) == null && Nodes(tree).Contains(foreign), "Undeclared foreign jump ownership is respected");
                        }
                    }
                    finally { bindings.Restore(); }
                    var rootField = typeof(BehaviorTree.BTmanager).GetField("m_root_node", Flags);
                    var root = rootField.GetValue(tree.GetRaw());
                    rootField.SetValue(tree.GetRaw(), new BehaviorTree.BTselector());
                    try
                    {
                        bool rejected = false;
                        try { install.Invoke(null, new object[] { player, true }); }
                        catch (TargetInvocationException error) { rejected = error.InnerException is InvalidOperationException; }
                        Check(rejected, "Malformed graph is reported instead of silently treated as a controller");
                    }
                    finally { rootField.SetValue(tree.GetRaw(), root); }
                }
            }
            finally { installer.GetMethod("Uninstall", Flags).Invoke(null, null); }
            if (casualAssembly != null) CasualChargeComposition(player, tree);
            Console.WriteLine("[OK] Real SFC " + chargeAssembly.GetName().Version + ": both load orders, correction on/off, repeated controller/settings toggles, graph and sampler cleanup");
        }
        private static void CasualChargeComposition(PlayerEntity player, BehaviorTreeComp tree)
        {
            var managerField = typeof(EntityComponent.EntityManager).GetField("_instance", Flags);
            object oldManager = managerField.GetValue(null);
            managerField.SetValue(null, null);
            var manager = new EntityComponent.EntityManager();
            ((System.Collections.Generic.List<EntityComponent.Entity>)typeof(EntityComponent.EntityManager).GetField("entities", Flags).GetValue(manager)).Add(player);
            var settings = casualAssembly.GetType("CasualJumping.SettingsStore", true);
            var preferences = Activator.CreateInstance(casualAssembly.GetType("CasualJumping.CasualJumpingSettings", true));
            settings.GetProperty("Current", Flags).SetValue(null, preferences, null);
            settings.GetField("loaded", Flags).SetValue(null, true);
            var casual = casualAssembly.GetType("CasualJumping.CasualControllerInstaller", true);
            var charge = chargeAssembly.GetType("SubframeCharge.SubframeChargeInstaller", true);
            var before = Nodes(tree); var bodyBefore = player.m_body.GetBehaviourList().ToArray();
            try
            {
                foreach (int mode in new[] { 1, 2 })
                foreach (bool correction in new[] { false, true })
                foreach (string order in new[] { "CSH", "CHS", "SCH", "SHC", "HCS", "HSC" })
                {
                    var modeProperty = preferences.GetType().GetProperty("Mode");
                    modeProperty.SetValue(preferences, Enum.ToObject(modeProperty.PropertyType, mode), null);
                    HammerNativeControl hammer = null; IDisposable policy = null;
                    Action equip = delegate { hammer = new HammerNativeControl(player, tree); };
                    Action unequip = delegate { if (hammer != null) { hammer.Dispose(); hammer = null; } };
                    try
                    {
                        foreach (char item in order)
                        {
                            if (item == 'C') casual.GetMethod("ApplyCurrentMode", Flags).Invoke(null, null);
                            if (item == 'S')
                            {
                                charge.GetMethod("EnsureTrajectoryProbe", Flags).Invoke(null, new object[] { player });
                                policy = JumpSlot.RegisterChargePolicy(
                                    delegate { charge.GetMethod("Install", Flags).Invoke(null, new object[] { player, correction }); },
                                    delegate { charge.GetMethod("DetachChargePolicy", Flags).Invoke(null, null); });
                            }
                            if (item == 'H') JumpSlot.Recompose(equip);
                        }
                        Check(JumpSlot.ChargePolicySuspended && PlayerControl.Owner(player.m_body) == "more-items.hammer", "Triple controller ownership: " + order);
                        for (int cycle = 0; cycle < 3; cycle++)
                        {
                            JumpSlot.Refresh();
                            Check(!Nodes(tree).Any(n => n.GetType().Name == "SubframeChargeState"), "Hammer reserves charge across settings refresh");
                            JumpSlot.Recompose(unequip);
                            Check(JumpSlot.ChargePolicySuspended == (mode == 2) && PlayerControl.Owner(player.m_body) == null, "Hammer releases movement and returns charge eligibility to the Casual mode");
                            Check(Nodes(tree).Any(n => n.GetType().Name == "SubframeChargeState") == (mode == 1), "Casual mode determines whether native charge can resume");
                            JumpSlot.Recompose(equip);
                        }
                    }
                    finally
                    {
                        JumpSlot.Recompose(unequip);
                        if (policy != null) policy.Dispose();
                        casual.GetMethod("Uninstall", Flags).Invoke(null, null);
                        charge.GetMethod("Uninstall", Flags).Invoke(null, null);
                    }
                    Check(before.SetEquals(Nodes(tree)) && bodyBefore.SequenceEqual(player.m_body.GetBehaviourList()), "Triple composition restores exact graph and body hooks: " + order);
                    Check(JKRuntime.Input.SharedActionSampler.ActiveStreams == 0, "Triple composition leaves no input sampler");
                }
            }
            finally { managerField.SetValue(null, oldManager); }
            Console.WriteLine("[OK] Real Casual/SFC/Hammer: six orders, both Casual modes, correction on/off, three toggle cycles and exact graph/body/input cleanup");
        }
        private sealed class ForeignJump : JumpState
        {
            internal ForeignJump(PlayerEntity player) : base(player) { }
            public override BehaviorTree.BTresult Run(BehaviorTree.TickData data) { return m_last_result = BehaviorTree.BTresult.Failure; }
        }
    }
}
