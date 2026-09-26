using System;
using System.Linq;
using System.Xml.Linq;

namespace JKRuntime
{
    internal static class MapPolicyTests
    {
        private static void Check(bool value, string text) { if (!value) throw new Exception(text); Console.WriteLine("PASS: " + text); }
        public static void Main(string[] args)
        {
            try
            {
                if (args.Length == 2 && args[0] == "--validate") { using (var reader = System.Xml.XmlReader.Create(args[1], new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 })) MapPolicy.Parse(XElement.Load(reader)); Console.WriteLine("PASS: map policy syntax"); return; }
                MapPolicy.Parse(XElement.Parse("<MapPolicy version='1'><Module id='optional' mode='suspend' reason='Map controls movement'/><Mechanic id='flight' reason='Platforming route'/></MapPolicy>"));
                int started = 0;
                var kernel = new RuntimeKernel();
                kernel.Register(new ModuleDefinition("optional", new Version(1, 0), c => started++) { MapSuspendable = true });
                kernel.Register(new ModuleDefinition("normal", new Version(1, 0), c => started++));
                kernel.Prepare(order => Check(order.SequenceEqual(new[] { "normal" }), "Suspended module performs no preparation"));
                Check(kernel.Activate() && started == 1, "Only unrestricted modules activate"); kernel.Deactivate();
                Check(!MapPolicy.AllowsMechanic("flight") && MapPolicy.MechanicReason("flight") == "Platforming route", "Mechanic policy is explicit and explainable");
                MapPolicy.Clear(); Check(kernel.Activate() && started == 3, "Next world restores modules without changing settings"); kernel.Deactivate();
                MapPolicy.Parse(XElement.Parse("<MapPolicy version='1'><Module id='normal' mode='suspend' reason='Test'/></MapPolicy>"));
                bool rejected = false; try { kernel.ValidateMapPolicy(); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "Nonparticipating modules cannot be silently suspended");
                MapPolicy.Parse(XElement.Parse("<MapPolicy version='1'><Module id='missing' mode='require' minimum='2.0' reason='Required mechanics'/></MapPolicy>"));
                rejected = false; try { kernel.ValidateMapPolicy(); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "Missing dependencies fail before player activation");
                MapPolicy.Parse(XElement.Parse("<MapPolicy version='1'><Assembly id='ForeignFixture' reason='Incompatible patch'/></MapPolicy>"));
                rejected = false; try { MapPolicy.Validate(new ModuleDefinition[0], new[] { "ForeignFixture" }); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "Foreign assembly conflicts require explicit removal, never unpatching");
                MapPolicy.Clear();
                var frozen = new ModuleDefinition("consent", new Version(1, 0), c => { }); kernel.Register(frozen);
                rejected = false; try { frozen.MapSuspendable = true; } catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "Suspension consent cannot change after registration");
                MapPolicy.Parse(XElement.Parse("<MapPolicy version='1'><Module id='required' mode='require' reason='Essential'/></MapPolicy>"));
                var missingDependency = new RuntimeKernel(); missingDependency.Register(new ModuleDefinition("required", new Version(1, 0), c => { }, requires: new[] { new CapabilityRequirement("missing", 1, 0) }));
                rejected = false; try { missingDependency.Prepare(order => { }); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "An installed but graph-rejected required module blocks preparation");
            }
            finally { MapPolicy.Clear(); }
        }
    }
}
