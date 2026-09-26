using System;
using JumpKing.Controller;
using JKRuntime.UI;

namespace JKRuntime
{
    public sealed class UiServices
    {
        public string Version { get { return UIApi.Version; } }
        public bool Supports(string capability) { return UIApi.Supports(capability); }
        public int[][] ResolvePhysicalBinding(PadInstance pad, int[] buttons)
        { return UIApi.ResolvePhysicalBinding(pad, buttons); }
        public UiRegistrationScope CreateScope(string owner) { return new UiRegistrationScope(owner); }
    }

    /// <summary>Single Runtime SDK entry point. Mutable services and registrations are game-thread only; feature checks do not activate services.</summary>
    public static class RuntimeApi
    {
        public const string Version = "1.32.0";
        public const int ApiMajor = 1;
        public const int ApiMinor = 32;
        internal static readonly RuntimeKernel Kernel = new RuntimeKernel();
        private static Simulation.SimulationRegistry simulation;
        private static Geometry.GeometryRegistry geometry;
        private static Gameplay.MechanicRegistry mechanics;
        private static Gameplay.MaterialRegistry materials;
        /// <summary>Exact-type declared material capabilities and bounded pure support queries.</summary>
        public static Gameplay.MaterialRegistry Materials
        { get { Kernel.CheckThread(); return materials ?? (materials = new Gameplay.MaterialRegistry()); } }
        /// <summary>Lazily allocated, explicitly selected actor geometry profiles. Does not replace native collision.</summary>
        public static Geometry.GeometryRegistry Geometry
        { get { Kernel.CheckThread(); return geometry ?? (geometry = new Geometry.GeometryRegistry()); } }
        /// <summary>On-demand mechanic inventory and declared conflicts. Does not change activation or run flags.</summary>
        public static Gameplay.MechanicRegistry Mechanics
        { get { Kernel.CheckThread(); return mechanics ?? (mechanics = new Gameplay.MechanicRegistry()); } }
        /// <summary>Lazy simulation registry. No capture, tick, worker or timer runs until explicitly requested.</summary>
        public static Simulation.SimulationRegistry Simulation
        {
            get { Kernel.CheckThread(); return simulation ?? (simulation = new Simulation.SimulationRegistry()); }
        }
        internal static void InvalidateSimulation() { if (simulation != null) simulation.Invalidate(); }
        /// <summary>Measure a named game-thread substage in opt-in diagnostic mode. Names should be stable and module-prefixed; no physical input is recorded.</summary>
        public static PerformanceMeasurement MeasurePerformance(string stage)
        {
            if (string.IsNullOrEmpty(stage) || stage.Length > 128) throw new ArgumentException("A stable stage name up to 128 characters is required", "stage");
            return new PerformanceMeasurement(stage);
        }
        public static string State { get { return Kernel.State; } }

        public static bool Supports(string feature)
        {
            return feature == "contact-evidence-v1" || feature == "support-predicates-v1" || feature == "movement-stages-v1"
                || feature == "method-validity-v1" || feature == "material-capabilities-v1" || feature == "movement-trace-v1" || feature == "motion-observation-v1" || feature == "map-policy-v1" || feature == "intro-preparation-v1" || feature == "protected-settings-v1" || feature == "bounded-background-work-v1" || feature == "owned-patches-v1"
                || feature == "shared-keyboard-v1" || feature == "presentation-scheduling-v1" || feature == "frame-composition-v1"
                || feature == "player-control-leases-v1" || feature == "native-optimizations-v1" || feature == "native-input-frames-v1" || feature == "performance-measurements-v1" || feature == "dependent-settings-v1" || feature == "module-preparation-v1" || feature == "startup-measurements-v1"
                || feature == "module-lifecycle-v1" || feature == "capabilities-v1"
                || feature == "diagnostics-v1" || feature == "runtime-packages-v1"
                || feature == "physical-input-v1" || feature == "state-transactions-v1"
                || feature == "run-modifier-attribution-v1" || feature == "mouse-buttons-v1"
                || feature == "simulation-sessions-v1" || feature == "geometry-profiles-v1"
                || feature == "mechanic-inventory-v1" || feature == "native-world-geometry-v1"
                || feature == "presentation-activity-v1" || feature == "gameplay-events-v1"
                || feature == "action-input-v1" || feature == "shared-action-sampling-v1"
                || feature == "component-suspension-v1" || feature == "mechanic-composition-v1"
                || feature == "block-catalog-v1" || feature == "native-flight-v1" || feature == "native-flight-teleports-v1"
                || feature == "simulation-conformance-v1" || feature == "bounded-journal-v1";
        }
        public static IDisposable Register(ModuleDefinition module) { return Kernel.Register(module); }
        /// <summary>Measure a named startup substage when StartupTrace is enabled. Use stable module-prefixed labels on the game thread.</summary>
        public static StartupMeasurement MeasureStartup(string stage)
        {
            Kernel.CheckThread();
            if (string.IsNullOrWhiteSpace(stage)) throw new ArgumentException("A stage name is required", "stage");
            return new StartupMeasurement(stage);
        }


        public static bool TryGetCapability(string id, int major, int minimumMinor, out object service)
        { return Kernel.TryGetCapability(id, major, minimumMinor, out service); }
        public static ModuleStatus[] GetModules() { return Kernel.GetModules(); }
        public static string[] GetOrder() { Kernel.CheckThread(); return Kernel.Order; }
        public static string[] GetErrors() { Kernel.CheckThread(); return Kernel.Errors; }
        /// <summary>Write an on-demand report and return its path. Includes loaded builds, patches, resources and bounded observations.</summary>
        public static string ExportDiagnostics() { Kernel.CheckThread(); return RuntimeHost.ExportDiagnostics(); }
    }
}
