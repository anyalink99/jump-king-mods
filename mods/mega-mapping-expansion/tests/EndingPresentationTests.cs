using System;
using System.IO;
using System.Xml.Linq;
using MegaMappingExpansion.Api;
using MegaMappingExpansion.Construction;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void EndingPresentationChecks()
        {
            var compiler=new RecipeCompiler(PrimitiveRegistry.Create());
            using(var scene=new SceneProgram(compiler.Compile(XDocument.Parse(@"<MappingSettings version='1'><Components>
<Component id='main' kind='endingPresentation'><Value name='credits' value='false'/><Value name='statistics' value='false'/></Component>
<Component id='owl' kind='endingPresentation'><Value name='role' value='owl'/><Value name='picture' value='false'/></Component>
<Component id='portrait' kind='capture'><Value name='mode' value='hold'/><Value name='selection' value='actor:player.idle'/><Value name='onVictory' value='true'/></Component>
</Components><Graph><Node id='a' op='constant' kind='Color' value='#FFFFFF'/><Node id='b' op='constant' kind='Color' value='#000000'/><Node id='half' op='constant' value='.5'/><Node id='shade' op='lerp' inputs='a;b;half'/></Graph>
<Rules><Rule id='capture' event='pose'><Action op='capture' target='portrait'/><Action op='clearCapture' target='portrait'/></Rule></Rules></MappingSettings>"))))
            {
                scene.Tick(0,0);var main=EndingPolicy.Read(scene,"main");var owl=EndingPolicy.Read(scene,"owl");var nbp=EndingPolicy.Read(scene,"nbp");
                Require(!main.Credits&&main.Picture&&!main.Statistics&&owl.Credits&&!owl.Picture&&owl.Statistics&&nbp.Credits&&nbp.Picture&&nbp.Statistics,"Ending stages are independent, role-specific and native by default");
                Require(scene.Graph.Read("shade").X==.5&&scene.Graph.Read("shade").W==1,"Lerp interpolates color channels without losing alpha");
            }
            using(var scene=new SceneProgram(compiler.Compile(XDocument.Parse(@"<MappingSettings version='1'><Components><Component id='tail' kind='media'><Value name='source' value='voice'/><Value name='active' value='true'/></Component></Components></MappingSettings>"))))
            using(var media=new SceneMedia(scene))
            {
                int pauses=0,resumes=0,stops=0;var lease=media.Register("voice",null,p=>{if(p)pauses++;else resumes++;},()=>stops++);
                bool rejected=false;try{media.Register("voice",null,null,null);}catch(ArgumentException){rejected=true;}
                Require(rejected,"A media source key has a single owner");
                media.Tick(true,false);media.Tick(true,true);media.Tick(true,true);media.Tick(true,false);media.Stop();media.Stop();lease.Dispose();
                Require(pauses==1&&resumes==1&&stops==1,"A media handoff preserves playback, owns pause edges and stops exactly once");
                var fresh=media.Register("voice",null,p=>pauses++,()=>stops++);lease.Dispose();
                Require(stops==1,"A stale media lease can't stop its replacement");fresh.Dispose();
            }
            foreach(string components in new[]{
                "<Component id='a' kind='endingPresentation'/><Component id='b' kind='endingPresentation'/>",
                "<Component id='a' kind='endingActor'/><Component id='b' kind='endingActor'/>",
                "<Component id='a' kind='capture'><Value name='mode' value='hold'/></Component>",
                "<Component id='a' kind='capture'><Value name='onVictory' value='true'/></Component>",
                "<Component id='a' kind='media'/>"})
            {
                bool rejected=false;try{using(var scene=new SceneProgram(compiler.Compile(XDocument.Parse("<MappingSettings version='1'><Components>"+components+"</Components></MappingSettings>")))){}}catch(InvalidDataException){rejected=true;}
                Require(rejected,"Ambiguous policies and incomplete retained resources fail scene validation");
            }
        }
    }
}
