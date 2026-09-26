using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using EntityComponent.BT;
using JumpKing.MiscEntities.OldMan;
using JumpKing.Props.RattmanText;
using Microsoft.Xna.Framework.Graphics;

namespace JKRuntime.Compatibility
{
    // An explicit, build-gated exception to our read-only foreign-patch policy.
    // Never loads Harmony/MoreTextOptions or writes their files. Game thread only.
    internal static class MoreTextOptionsCompatibility
    {
        private const string ModHash = "115FD1FB5884EED3E022541099AA7324432C3E2401B092DA9848366181C227E8";
        private const string ForeignOwner = "Zebra.MoreTextOptions.Harmony";
        internal static string Status = "Not checked";
        private static readonly MethodInfo Target = typeof(TargetLine).GetMethod("MyRun", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo Replacement = typeof(MoreTextOptionsCompatibility).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
        private static Type oldMan, rattman;
        private static FieldInfo oldSettings, rattmanSettings, values;
        private static Func<OldManFont, SpriteFont> font;
        private static Func<EntityBTNode, object> blackboard;

        internal static void TryInstall()
        {
            RuntimeApi.Kernel.CheckThread();
            try
            {
                var mods = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "MoreTextOptions").ToArray();
                if (mods.Length == 0) { SetStatus("Not needed: MoreTextOptions not loaded"); return; }
                if (mods.Length != 1 || !KnownBuild(mods[0])) { SetStatus("Unsupported: unreviewed MoreTextOptions build; no changes"); return; }
                var engines = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "0Harmony").ToArray();
                if (engines.Length != 1) { SetStatus("Unsupported: expected one loaded Harmony engine; no changes"); return; }
                var engine = engines[0];
                var access = engine.GetType("HarmonyLib.AccessTools", true);
                if (access.GetMethods().Any(m => m.Name == "MethodDelegate" && m.IsGenericMethodDefinition &&
                    m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(MethodInfo), typeof(object), typeof(bool) })))
                { SetStatus("Not needed: original MethodDelegate ABI available"); return; }
                if (!new[] { "2.3.3.0", "2.3.5.0", "2.3.6.0" }.Contains(engine.GetName().Version.ToString()))
                { SetStatus("Unsupported: unreviewed Harmony version; no changes"); return; }
                var source = mods[0].GetType("MoreTextOptions.Patches.PatchTargetLine", true).GetMethod("Postfix");
                var harmony = engine.GetType("HarmonyLib.Harmony", true);
                var harmonyMethod = engine.GetType("HarmonyLib.HarmonyMethod", true);
                var getInfo = harmony.GetMethod("GetPatchInfo", new[] { typeof(MethodBase) });
                var patch = harmony.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5);
                var unpatch = harmony.GetMethod("Unpatch", new[] { typeof(MethodBase), typeof(MethodInfo) });
                var info = getInfo.Invoke(null, new object[] { Target });
                var originals = Entries(info, "Postfixes").Where(p => Equals(Read(p, "PatchMethod"), source)).ToArray();
                var replacements = Entries(info, "Postfixes").Where(p => Equals(Read(p, "PatchMethod"), Replacement)).ToArray();
                if (originals.Length == 0)
                {
                    SetStatus(replacements.Length == 1 ? "Active: MoreTextOptions native delegate adapter" : "Waiting: original TargetLine postfix not registered");
                    return;
                }
                if (originals.Length != 1 || replacements.Length > 1 || !Equals(Read(originals[0], "owner"), ForeignOwner))
                    throw new InvalidOperationException("Unexpected TargetLine patch ownership/count");
                // Adding a patch gets a new tie-break index. Refuse equal-priority
                // peer postfixes rather than silently change their ordering.
                int priority = (int)Read(originals[0], "priority");
                if (Entries(info, "Postfixes").Any(p => !Equals(Read(p, "PatchMethod"), source) &&
                    !Equals(Read(p, "PatchMethod"), Replacement) && (int)Read(p, "priority") == priority))
                    throw new InvalidOperationException("Equal-priority foreign postfix requires a reviewed ordering adapter");
                PrepareNativeDelegates(); // Resolve everything before touching a patch.
                var owner = Activator.CreateInstance(harmony, new object[] { ForeignOwner });
                var originalMetadata = Metadata(harmonyMethod, source, originals[0]);
                bool added = false, removed = false;
                try
                {
                    if (replacements.Length == 0)
                    {
                        added = true; // Also clean up a partially successful Patch.
                        patch.Invoke(owner, new object[] { Target, null, Metadata(harmonyMethod, Replacement, originals[0]), null, null });
                    }
                    removed = true;
                    unpatch.Invoke(owner, new object[] { Target, source });
                    var updated = getInfo.Invoke(null, new object[] { Target });
                    if (Entries(updated, "Postfixes").Any(p => Equals(Read(p, "PatchMethod"), source)) ||
                        Entries(updated, "Postfixes").Count(p => Equals(Read(p, "PatchMethod"), Replacement)) != 1)
                        throw new InvalidOperationException("Replacement coverage check failed");
                    SetStatus("Active: MoreTextOptions native delegate adapter; Harmony " + engine.GetName().Version);
                }
                catch (Exception failure)
                {
                    // Restore the original registration if replacement fails.
                    // Never leave a silently disabled dialogue formatter.
                    try
                    {
                        if (removed && !Entries(getInfo.Invoke(null, new object[] { Target }), "Postfixes")
                            .Any(p => Equals(Read(p, "PatchMethod"), source)))
                            patch.Invoke(owner, new object[] { Target, null, originalMetadata, null, null });
                        if (added) unpatch.Invoke(owner, new object[] { Target, Replacement });
                    }
                    catch (Exception rollback) { throw new AggregateException("Compatibility rollback failed; restart required", failure, rollback); }
                    throw;
                }
            }
            catch (Exception error) { SetStatus("Unavailable: " + error.GetBaseException().Message + "; crash protection not guaranteed"); }
        }

        private static bool KnownBuild(Assembly assembly)
        {
            if (assembly.ManifestModule.ModuleVersionId != new Guid("fc64abb4-d965-4965-8047-3868d34b34a0") || string.IsNullOrEmpty(assembly.Location)) return false;
            using (var input = File.OpenRead(assembly.Location))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "") == ModHash;
        }
        private static object[] Entries(object info, string kind)
        { return info == null ? new object[0] : ((IEnumerable)Read(info, kind)).Cast<object>().ToArray(); }
        private static object Read(object value, string name)
        {
            var type = value.GetType();
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return field != null ? field.GetValue(value) : type.GetProperty(name).GetValue(value, null);
        }
        private static object Metadata(Type type, MethodInfo method, object original)
        {
            var result = Activator.CreateInstance(type, new object[] { method });
            foreach (string name in new[] { "priority", "before", "after" }) type.GetField(name).SetValue(result, Read(original, name));
            return result;
        }
        private static void SetStatus(string status)
        { if (Status != status) Console.WriteLine("[JK Runtime] MoreTextOptions compatibility: " + status); Status = status; }
        private static void PrepareNativeDelegates()
        {
            var game = typeof(TargetLine).Assembly;
            oldMan = game.GetType("JumpKing.MiscEntities.OldManEntity", true);
            rattman = game.GetType("JumpKing.Props.RattmanText.RattmanEntity", true);
            var boardType = game.GetType("EntityComponent.BlackBoardComp", true);
            oldSettings = RequireField(oldMan, "m_settings", typeof(OldManSettings));
            rattmanSettings = RequireField(rattman, "m_settings", typeof(RattmanSettings));
            values = RequireField(boardType, "m_values", typeof(Dictionary<string, object>));
            font = (Func<OldManFont, SpriteFont>)Delegate.CreateDelegate(typeof(Func<OldManFont, SpriteFont>),
                oldMan.GetMethod("GetOldManFont", BindingFlags.Public | BindingFlags.Static));
            var get = typeof(EntityBTNode).GetMethod("GetComponent").MakeGenericMethod(boardType);
            if (get.IsVirtual) throw new InvalidOperationException("Native GetComponent contract changed");
            blackboard = (Func<EntityBTNode, object>)Delegate.CreateDelegate(typeof(Func<EntityBTNode, object>), get);
        }
        private static FieldInfo RequireField(Type type, string name, Type expected)
        {
            var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null || field.FieldType != expected) throw new MissingFieldException(type.FullName, name);
            return field;
        }
        // Equivalent to the reviewed MoreTextOptions 1.6.2.1 formatter, without
        // referencing any fields on its poisoned beforefieldinit type.
        private static void Postfix(TargetLine __instance)
        {
            var entity = __instance.game_object;
            OldManFont selectedFont; int width;
            if (entity.GetType() == rattman)
            { var settings = (RattmanSettings)rattmanSettings.GetValue(entity); selectedFont = settings.font; width = settings.bubble_format.width; }
            else if (entity.GetType() == oldMan)
            { var settings = (OldManSettings)oldSettings.GetValue(entity); selectedFont = settings.font; width = settings.bubble_format.width; }
            else return;
            var dictionary = (Dictionary<string, object>)values.GetValue(blackboard(__instance));
            dictionary["BB_LINE_KEY"] = string.Join("", SpeechBubbleFormat.ChopString((string)dictionary["BB_LINE_KEY"], font(selectedFont), width));
        }
    }
}
