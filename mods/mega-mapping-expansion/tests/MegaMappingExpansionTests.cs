using System;
using System.IO;
using System.Linq;
using MegaMappingExpansion.Construction;
using MegaMappingExpansion.Api;
namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static int checks;
        private static void Require(bool value,string message){checks++;if(!value)throw new Exception("FAILED: "+message);}
        private static void WriteLayout(string path,string layout){File.WriteAllText(path,"<MappingSettings version='1'>"+layout+"</MappingSettings>");}
        private static int Main(string[] args)
        {
            try
            {
                string scratch=Path.GetFullPath(args[0]);Directory.CreateDirectory(scratch);
                if(args.Length>4&&args[2]=="pixel-compatibility"){PixelCompatibilityChecks(scratch,args[3],args[4]=="first");return 0;}
                if(args.Length>3&&args[2]=="pixel-native"){PixelCompiledAtlasChecks(scratch,args[3],args.Length>4?args[4]:null);return 0;}
                if(args.Length>3&&args[2]=="ending-compatibility"){EndingCompatibilityChecks(scratch,args[3]);return 0;}
                if(args.Length>3&&args[2]=="warp-shapes"){NativeWarpShapeChecks(args[3]);return 0;}
                ConstructionChecks();SceneLifecycleChecks();ChargeChecks();NativeAdapterChecks();NativeDialogueChecks();NativeVictoryChecks();NativeEventAudioChecks();AuthoringContracts(scratch);
                NativeSceneAdapter.AssertContracts();NativeMapLayout.AssertContracts();Endings.NativeEndings.AssertContracts();
                using(var hooks=new SceneHooks())
                {
                    var method=typeof(JumpKing.Level.LevelScreen).GetMethod("DrawForeground");Require(HarmonyLib.Harmony.GetPatchInfo(method).Owners.Contains("mega-mapping-expansion.construction"),"Construction rendering hooks match the installed native game");
                }
                var patch=HarmonyLib.Harmony.GetPatchInfo(typeof(JumpKing.Level.LevelScreen).GetMethod("DrawForeground"));Require(patch==null||!patch.Owners.Contains("mega-mapping-expansion.construction"),"World teardown releases the construction hooks");
                PixelCollisionChecks(scratch);CollisionShapeChecks(scratch);TransitionChecks(scratch);
                if(args.Length>1&&File.Exists(Path.Combine(args[1],MappingFiles.Settings)))
                {var document=new RecipeCompiler(PrimitiveRegistry.Create()).Read(args[1]);using(var scene=new SceneProgram(document))Require(scene.Components.Count>0,"The PNG-first SDK example validates through the runtime constructor");}
                Console.WriteLine("[OK] Mega Mapping Expansion: "+checks+" construction, state, native geometry and topology checks");return 0;
            }
            catch(Exception error){Console.Error.WriteLine(error);return 1;}
        }
        private static void AuthoringContracts(string scratch)
        {
            SceneCacheChecks(scratch);
            string root=Path.Combine(scratch,"authoring");Directory.CreateDirectory(root);string entry=Path.Combine(root,MappingFiles.Settings);bool refused;
            File.WriteAllText(entry,"<MappingSettings version='1'><MapLayout version='2' source='native' screens='2'/></MappingSettings>");var layout=MapLayout.Read(root);layout.ValidateAtlas(780,585);Require(!layout.Pixel&&layout.AtlasSide==13,"Native collision capacity is inferred from its actual atlas");
            WriteLayout(entry,"<MapLayout version='1' screens='2' atlasSide='13'/>");refused=false;try{MapLayout.Read(root);}catch(InvalidDataException){refused=true;}Require(refused,"The removed layout format has no fallback reader");
            var compiler=new RecipeCompiler(PrimitiveRegistry.Create());foreach(string section in new[]{"Options","Flags","Props","Planets","Bushes","Rains"})
            {refused=false;try{compiler.Compile(System.Xml.Linq.XDocument.Parse("<MappingSettings version='1'><"+section+"/></MappingSettings>"));}catch(InvalidDataException){refused=true;}Require(refused,"Removed semantic sections aren't accepted: "+section);}
            string source="<MappingSettings version='1'><Include src='part.xml'/></MappingSettings>";File.WriteAllText(entry,source);File.WriteAllText(Path.Combine(root,"part.xml"),"<MappingFragment version='1'><Components><Component id='nested' kind='transform'/></Components></MappingFragment>");
            Require(compiler.Read(root).Components[0].Id=="nested","Safe include fragments compile into one component scene");File.WriteAllText(Path.Combine(root,"part.xml"),"<MappingFragment version='1'><Include src='mapping_settings.xml'/></MappingFragment>");refused=false;try{compiler.Read(root);}catch(InvalidDataException){refused=true;}Require(refused,"Include cycles fail before recursive expansion");
            string saveRoot=Path.Combine(scratch,"variables");Directory.CreateDirectory(saveRoot);var store=new ScenePersistence("constructor",()=>saveRoot);store.Save(new System.Collections.Generic.Dictionary<string,string>{{"count","17"},{"text",new string('x',4096)}});
            Require(store.Load()["count"]=="17"&&store.Load()["text"].Length==4096,"Saved typed variable payloads preserve counters and bounded text");ScenePersistence.ResetContext(saveRoot);refused=false;try{store.Save(new System.Collections.Generic.Dictionary<string,string>{{"count","99"}});}catch(InvalidOperationException){refused=true;}Require(refused,"A stale save owner can't resurrect values after native reset");
        }
    }
}
