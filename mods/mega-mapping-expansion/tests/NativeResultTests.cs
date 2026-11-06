using System;
using System.IO;
using System.Xml.Linq;
using MegaMappingExpansion.Api;
using MegaMappingExpansion.Construction;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void NativeResultChecks()
        {
            var definition=new RecipeCompiler(PrimitiveRegistry.Create()).Compile(XDocument.Parse(@"<MappingSettings version='1'>
<Variables><Variable id='score' kind='Integer' value='0'/></Variables>
<Components>
 <Component id='count' kind='result'><Value name='page' value='Challenge'/><Value name='role' value='main'/><Value name='label' value='Jumps'/></Component>
 <Component id='mean' kind='result'><Value name='page' value='Challenge'/><Value name='role' value='main'/><Value name='order' value='1'/><Value name='label' value='Mean F'/><Value name='value' value='11.5177'/></Component>
 <Component id='hidden' kind='result'><Value name='page' value='Challenge'/><Value name='role' value='main'/><Value name='enabled' value='false'/></Component>
</Components>
<Graph><Node id='score' op='input' source='variable:score'/><Node id='text' op='format' inputs='score'/></Graph>
<Bindings><Bind id='count' source='text' target='count/value'/></Bindings></MappingSettings>"));
            using(var text=new SceneProgram(new RecipeCompiler(PrimitiveRegistry.Create()).Compile(XDocument.Parse(@"<MappingSettings version='1'><Components><Component id='page' kind='intro'><Value name='order' value='2'/><Value name='font' value='small'/></Component></Components><Graph><Node id='pattern' op='constant' kind='Text' value='Score {0}, F{1:0.0000}'/><Node id='score' op='constant' kind='Integer' value='9'/><Node id='force' op='constant' value='11.5177'/><Node id='caption' op='template' inputs='pattern;score;force'/></Graph><Bindings><Bind id='caption' source='caption' target='page/value'/></Bindings></MappingSettings>"))))
            {
                text.Tick(0,0);Require(text.Properties.Read("page","value").Text=="Score 9, F11.5177","Reusable text templates substitute typed values with invariant precision");
                Require(NativeNarrativeText.IntroPages(text).Length==1,"Intro pages use the same component registry and bindings");
            }
            ResultPageSnapshot[] pages;
            using(var scene=new SceneProgram(definition))
            {
                scene.SetVariable("score",SceneValue.Integer(9));pages=NativeResults.Capture(scene);
                Require(scene.GameplayTime==0&&scene.PresentationTime==0,"Final result capture composes pending API writes without advancing clocks");
                Require(pages.Length==1&&pages[0].Role=="main"&&pages[0].Lines.Length==3&&pages[0].Lines[1]=="Jumps: 9"&&pages[0].Lines[2]=="Mean F: 11.5177","Result rows compose bound values in stable page order");
                scene.SetVariable("score",SceneValue.Integer(99));scene.Tick(0,0);
            }
            Require(pages[0].Lines[1]=="Jumps: 9","A final result snapshot survives scene mutation and disposal");
            var cursor=new ResultCursor(pages);
            Require(cursor.Page==-1&&!cursor.Advance(.1,false),"Native statistics stay first");
            Require(cursor.Advance(.1,true)&&cursor.Page==-1,"Early input doesn't skip native statistics");
            Require(cursor.Advance(.2,true)&&cursor.Page==0,"Confirmation advances to the first map result page");
            Require(!cursor.Advance(1,false)&&cursor.Page==0,"Map results wait for a new confirmation");
            Require(!cursor.Advance(.1,true),"Final confirmation releases native completion");
            bool rejected=false;
            try
            {
                var invalid=new RecipeCompiler(PrimitiveRegistry.Create()).Compile(XDocument.Parse("<MappingSettings version='1'><Components><Component id='a' kind='result'><Value name='page' value='Same'/><Value name='role' value='main'/></Component><Component id='b' kind='result'><Value name='page' value='Same'/><Value name='role' value='owl'/></Component></Components></MappingSettings>"));
                using(var scene=new SceneProgram(invalid)){}
            }
            catch(InvalidDataException){rejected=true;}
            Require(rejected,"One result page can't mix ending roles");
        }
    }
}
