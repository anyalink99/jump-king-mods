using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using JumpKing;
using MegaMappingExpansion.Endings;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void EndingLatePatchChecks(string scratch,string fixture)
        {
            Assembly provider=Assembly.LoadFrom(fixture);Type probe=provider.GetType("MoreEndingOptions.Probe");
            string map=Path.Combine(scratch,"late-endings");Directory.CreateDirectory(Path.Combine(map,"ending"));
            File.WriteAllText(Path.Combine(map,"ending/custom_main_ending.xml"),"<PauseNode>0.01</PauseNode>");
            var gameField=typeof(Game1).GetField("_instance",BindingFlags.Static|BindingFlags.NonPublic);object previous=gameField.GetValue(null);
            var game=(Game1)FormatterServices.GetUninitializedObject(typeof(Game1));GC.SuppressFinalize(game);
            game.contentManager=(JKContentManager)FormatterServices.GetUninitializedObject(typeof(JKContentManager));game.contentManager.root=map;gameField.SetValue(null,game);
            var owner=new Harmony("mme.tests.dynamic-provider");
            try{
                NativeEndings.Prepare(map);
                Require(JKRuntime.Compatibility.CompatibilityEvidence.Inspect().Any(r=>r.Feature=="mme.endings.discovery" && r.Outcome==JKRuntime.Compatibility.CompatibilityOutcome.Unknown),"Partial type loading must retain its unknown evidence");
                Require((int)probe.GetField("TargetCalls").GetValue(null)==0,"Preflight must not execute a foreign target factory");
                owner.CreateClassProcessor(provider.GetType("MoreEndingOptions.DynamicTarget")).Patch();
                var binding=NativeEndings.Bindings.Single(b=>b.Role=="main_ending");
                object actor=FormatterServices.GetUninitializedObject(binding.Method.DeclaringType);
                binding.Method.Invoke(actor,null);
                Require((int)probe.GetField("Calls").GetValue(null)==0,"Dynamic late target is guarded before its first native invocation");
                owner.Patch(binding.Method,postfix:new HarmonyMethod(probe.GetMethod("LatePostfix")));
                binding.Method.Invoke(actor,null);
                Require((int)probe.GetField("Calls").GetValue(null)==0,"Manual late mutation is reconciled without a new world");
                NativeEndings.Prepare(map);binding.Method.Invoke(actor,null);
                Require((int)probe.GetField("Calls").GetValue(null)==0,"Same-role preparation retains every guard");
                NativeEndings.Release();probe.GetMethod("LatePostfix").Invoke(null,new object[]{null});
                Require((int)probe.GetField("Calls").GetValue(null)==1,"World exit restores foreign callback behavior");
                owner.UnpatchAll(owner.Id);
                owner.Patch(binding.Method,prefix:new HarmonyMethod(probe.GetMethod("Prefix")));
                bool changedKind=false;try{NativeEndings.Prepare(map);}catch(NotSupportedException){changedKind=true;}
                Require(changedKind,"A changed hook kind must refuse the required ending");
                owner.UnpatchAll(owner.Id);
                owner.Patch(binding.Method,postfix:new HarmonyMethod(probe.GetMethod("Unsupported")));
                bool refused=false;try{NativeEndings.Prepare(map);}catch(NotSupportedException){refused=true;}
                Require(refused,"A result-changing callback needs a separate adapter, not silent suppression");
            }finally{NativeEndings.Release();owner.UnpatchAll(owner.Id);gameField.SetValue(null,previous);}
            Console.WriteLine("[OK] Dynamic/manual late ending patches, preflight purity, same-role reprepare, release and unsupported callback");
        }
    }
}
