using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using JumpKing.API;
using JumpKing.Player;

namespace JKRuntime.Compatibility
{
    internal static class MovingPlatformCompatibility
    {
        private const string Hash = "6BD709BBBF473A632686CF07103AE14EAA02DE6BB16072C98A62BE42C482B399";
        private static readonly FieldInfo Behaviours = typeof(BodyComp).GetField("m_behaviours", OwnedPatches.Members);
        private static OwnedPatches patches;
        private static Type updater;
        internal static string Status = "Not checked";

        internal static void Apply()
        {
            try
            {
                if (!ModCompatibility.Setting.Value)
                {
                    if (patches != null) { patches.Dispose(); patches = null; }
                    Status = "Disabled; existing attempt history retained";
                    return;
                }
                if (patches != null) return;
                var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "BoGMod3").ToArray();
                if (assemblies.Length == 0) { Status = "Not needed: BoGMod3 not loaded"; return; }
                if (assemblies.Length != 1 || !KnownBuild(assemblies[0]))
                { Status = "Unsupported: unreviewed BoGMod3 build"; return; }
                if (Behaviours == null || Behaviours.FieldType != typeof(LinkedList<IBodyCompBehaviour>))
                    throw new MissingMemberException("BodyComp behaviour list contract changed");
                updater = assemblies[0].GetType("MovingPlatformMod.MovingPlatformUpdater", true);
                if (!typeof(IBodyCompBehaviour).IsAssignableFrom(updater))
                    throw new MissingMemberException("MovingPlatformUpdater contract changed");
                var target = assemblies[0].GetType("MovingPlatformMod.ModEntry", true).GetMethod("OnLevelStart", OwnedPatches.Members);
                var candidate = new OwnedPatches("jk-runtime.compatibility.moving-platform");
                patches = candidate;
                try
                {
                    // change only the reviewed caller. native registration and its
                    // observers must keep seeing every other mod normally
                    candidate.ReplaceCalls(target, typeof(BodyComp).GetMethod("RegisterBehaviourBefore"), Callback("RegisterBefore"), 1);
                    candidate.ReplaceCalls(target, typeof(BodyComp).GetMethod("RegisterBehaviour"), Callback("Register"), 2);
                }
                // keep the owner reachable if cleanup fails, so disabling can retry
                catch { candidate.Dispose(); patches = null; throw; }
                Status = "Active: unmarked platform registration on next level start";
            }
            catch (Exception error)
            {
                Status = "Unavailable: " + error.GetBaseException().Message;
                RuntimeJournal.Record("jk-runtime", "moving-platform-compatibility", error.ToString());
            }
        }

        private static MethodInfo Callback(string name)
        { return typeof(MovingPlatformCompatibility).GetMethod(name, OwnedPatches.Members); }

        private static bool KnownBuild(Assembly assembly)
        {
            using (var stream = File.OpenRead(assembly.Location))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") == Hash;
        }

        private static bool UseAdapter(IBodyCompBehaviour behaviour)
        { return ModCompatibility.Setting.Value && behaviour != null && behaviour.GetType() == updater; }

        private static bool RegisterBefore(BodyComp body, IBodyCompBehaviour behaviour, IBodyCompBehaviour anchor)
        {
            if (!UseAdapter(behaviour)) return body.RegisterBehaviourBefore(behaviour, anchor);
            var list = (LinkedList<IBodyCompBehaviour>)Behaviours.GetValue(body);
            var node = list.Find(anchor);
            if (node == null) return false;
            // same placement and return value, without a native modifier entry
            // BoGMod3 owns this slot for the body's lifetime and never removes it
            list.AddBefore(node, behaviour);
            return true;
        }

        private static bool Register(BodyComp body, IBodyCompBehaviour behaviour)
        {
            if (!UseAdapter(behaviour)) return body.RegisterBehaviour(behaviour);
            ((LinkedList<IBodyCompBehaviour>)Behaviours.GetValue(body)).AddLast(behaviour);
            return true;
        }
    }
}
