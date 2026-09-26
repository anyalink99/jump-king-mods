using System;
using System.Linq;
using System.Reflection;
using JumpKing.Level;

namespace MegaGameplayExpansion
{
    internal static class GimmickClassification
    {
        internal static bool? ConstantBoolean(MethodInfo method)
        {
            if (method == null) return null;
            try {
                var code = GimmickIl.Read(method).Where(i => i.Code.Name != "nop").ToArray();
                if (code.Length == 2 && code[1].Code.Name == "ret") {
                    if (code[0].Code.Name == "ldc.i4.0") return false;
                    if (code[0].Code.Name == "ldc.i4.1") return true;
                }
            } catch { }
            return null;
        }
        // Proof is structural, not a sample collision or a mod/type name heuristic.
        internal static bool? Blocking(IBlock block)
        {
            if (block == null) return null;
            if (block.GetType() == typeof(SlopeBlock)) return true;
            if (!(block is BoxBlock)) return null;
            var map = block.GetType().GetInterfaceMap(typeof(IBlock));
            for (int i = 0; i < map.InterfaceMethods.Length; i++)
                if (map.InterfaceMethods[i].Name == "Intersects" && map.TargetMethods[i].DeclaringType != typeof(BoxBlock)) return null;
            var property = block.GetType().GetProperty("canBlockPlayer", Gimmicks.Members);
            return ConstantBoolean(property == null ? null : property.GetGetMethod(true));
        }
        internal static void Inspect(GimmickEntry entry)
        {
            if (entry.Kind == "Setting") { entry.Family = "Mod settings"; entry.Trigger = "Persistent setting"; entry.Classification = "The mod's own saved setting, not an MGE override. Edit individually; Restore and bulk reset leave it unchanged."; return; }
            if (entry.Kind == "Wind") { entry.Family = "Environment"; entry.Geometry = "Metadata"; entry.Trigger = "Screen"; return; }
            if (entry.Kind == "Colour") { entry.Family = "Unknown"; entry.Geometry = "Unknown"; entry.Classification = "Indexed collision colour with no declared factory. Source occurrence is not a working effect."; return; }
            if (entry.Slot != null && entry.Slot.ReadOnlyReason != null) {
                entry.Family = "Diagnostics"; entry.Trigger = "Block contact"; entry.Classification = entry.Slot.ReadOnlyReason; return;
            }
            if (entry.Kind != "Block") { entry.Family = entry.Slot != null ? "Advanced state" : "Player mechanic"; entry.Trigger = "State"; return; }
            if (entry.Template == null) { entry.Classification = entry.Error ?? "Open details to prepare this material."; return; }
            if (entry.Template is SlopeBlock) { entry.Family = "Geometry"; entry.Geometry = "Blocking slope"; entry.Classification = "A slope changes rectangular terrain shape. Select an advanced replacement explicitly."; return; }
            bool? blocking = Blocking(entry.Template);
            entry.Geometry = blocking.HasValue ? (blocking.Value ? "Blocking" : "Nonblocking") : "Conditional / unknown";
            if (!blocking.HasValue) { entry.Family = "Unknown"; entry.Classification = "Collision is conditional or opaque. Choose an application explicitly."; return; }
            if (blocking.Value) { entry.Family = "Surface"; entry.Trigger = "Contact"; entry.Classification = "Constant blocking rectangle; preserve geometry. The original handler determines contact behavior."; }
            else if (entry.Template.GetType().Assembly != typeof(BoxBlock).Assembly && GimmickHandlers.HasAdditionalContact(entry.Template.GetType())) {
                entry.Family = "Contact"; entry.Trigger = "Additional collision";
                entry.Classification = "Nonblocking material with additional collision logic. Choose placement explicitly; it is not assumed to be a medium.";
            } else { entry.Family = "Medium / zone"; entry.Trigger = "Overlap"; entry.Classification = "Constant nonblocking volume; inferred overlap placement. Original handler behavior is retained."; }
        }
        internal static GimmickApplication Resolve(GimmickEntry entry, GimmickRule rule)
        {
            if (rule.Application != GimmickApplication.Auto) return rule.Application;
            Inspect(entry);
            if (entry.Family == "Surface") return GimmickApplication.Surface;
            if (entry.Family == "Medium / zone") return GimmickApplication.FillEmpty;
            throw new InvalidOperationException(entry.Classification ?? "Choose an application for this unclassified effect.");
        }
    }
}
