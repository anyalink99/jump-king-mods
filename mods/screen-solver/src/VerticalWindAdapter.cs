using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using JumpKing.BodyCompBehaviours;

namespace ScreenSolver
{
    // Audited Workshop 3437222016. Its marker factory returns null, so the
    // collider list cannot tell us which screens rotate wind from X to Y.
    internal sealed class VerticalWindAdapter
    {
        internal const string Owner = "McOuille.VerticalWindMod";
        internal const string Sha256 = "425a617b79fba61ba40d39e70f264c5ceb32e069c79330ed3f451276f07a976d";
        internal const string PatchType = "VerticalWindMod.WindVelocityUpdateBehaviourExecuteBehaviourPatch";
        private Assembly accepted;

        internal bool Accepts(MethodBase original, string kind, string owner, MethodInfo patch)
        {
            if (kind != "Transpilers" || owner != Owner || patch == null ||
                original != typeof(WindVelocityUpdateBehaviour).GetMethod("ExecuteBehaviour") ||
                patch.Name != "Transpiler" || patch.DeclaringType.FullName != PatchType) return false;
            var assembly = patch.DeclaringType.Assembly;
            if (assembly.GetName().Name != "VerticalWindMod" ||
                assembly.ManifestModule.ModuleVersionId != new Guid("f538a3ec-e88c-4e66-9f6e-86bd7486956e")) return false;
            if (accepted != null) return accepted == assembly;
            if (string.IsNullOrEmpty(assembly.Location)) return false;
            using (var stream = File.OpenRead(assembly.Location))
            using (var sha = SHA256.Create())
                if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() != Sha256) return false;
            accepted = assembly;
            return true;
        }

        internal bool[] Capture(int screenCount)
        {
            var result = new bool[screenCount];
            if (accepted == null) return result;
            var managerType = accepted.GetType("VerticalWindMod.VerticalWindManager", true);
            var instanceField = managerType.GetField("_instance", NativeWorld.Flags);
            var screensField = managerType.GetField("_screensWithWind", NativeWorld.Flags);
            if (instanceField == null || screensField == null)
                throw new NotSupportedException("Vertical Wind marker storage changed");
            var instance = instanceField.GetValue(null);
            // A null singleton is the audited mod's initial empty state. Do not
            // call Instance: even capture must not initialize another mod.
            if (instance == null) return result;
            var screens = screensField.GetValue(instance) as HashSet<int>;
            if (screens == null) throw new NotSupportedException("Cannot read Vertical Wind screen markers");
            foreach (int screen in screens.ToArray())
            {
                if (screen < 0 || screen >= screenCount)
                    throw new NotSupportedException("Vertical Wind has an invalid screen marker: " + screen);
                result[screen] = true;
            }
            return result;
        }
    }
}
