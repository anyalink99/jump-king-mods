using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using BehaviorTree;
using JKRuntime;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.GameManager.MultiEnding;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MegaMappingExpansion.Api;
using MegaMappingExpansion.Construction;

namespace MegaMappingExpansion
{
    internal static class NativeResultGraphics
    {
        private const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static bool pressed;
        private static bool Setup(BTmanager ___m_BT,ref string[] ___m_lines)
        {___m_BT.Reset();___m_lines=new[]{"NATIVE STATISTICS"};return false;}
        private static bool Input(ref BTresult __result){__result=pressed?BTresult.Success:BTresult.Failure;return false;}
        private static bool Ending(ref EndingType __result){__result=default(EndingType);return false;}
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Draw(StatsScreen screen){screen.Draw();}
        private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS: "+message);}
        internal static void Run(Game1 game,GraphicsDevice device,RenderTarget2D target,string root,string gameDir)
        {
            string previousRoot=game.contentManager.root;var previousFont=game.contentManager.font.MenuFont;
            var fontFields=new[]{"MenuFontSmall","StyleFont","LocationFont","GargoyleFont"}.Select(name=>game.contentManager.font.GetType().GetField(name)).ToArray();
            var previousFonts=fontFields.Select(field=>field.GetValue(game.contentManager.font)).ToArray();
            using(var content=new ContentManager(game.Services,Path.Combine(gameDir,"Content")))
            using(var fixture=new OwnedPatches("mega-mapping-expansion.result-fixture"))
            {
                game.contentManager.root=root;game.contentManager.font.MenuFont=content.Load<SpriteFont>("font/sf_litter_lover2_bold");
                fixture.Add(typeof(StatsScreen).GetMethod("OnNewRun",All),prefix:typeof(NativeResultGraphics).GetMethod("Setup",All));
                fixture.Add(typeof(JumpKing.Util.AnyInputNode).GetMethod("MyRun",All),prefix:typeof(NativeResultGraphics).GetMethod("Input",All));
                fixture.Add(typeof(GameEnding).GetMethod("GetEnding",All),prefix:typeof(NativeResultGraphics).GetMethod("Ending",All));
                try
                {
                    string[] fontAssets={"sf_small","sf_pixolde","sf_pixolde_bold","sf_double_homicide"};
                    for(int i=0;i<fontFields.Length;i++)fontFields[i].SetValue(game.contentManager.font,content.Load<SpriteFont>("font/"+fontAssets[i]));
                    foreach(string fontId in new[]{"menu","small","style","location","gargoyle"})
                    {
                        var fontScene=new RecipeCompiler(PrimitiveRegistry.Create()).Compile(XDocument.Parse("<MappingSettings version='1'><Components><Component id='pose' kind='transform'><Value name='position' value='240,180'/></Component><Component id='label' kind='text'><Value name='transform' value='pose'/><Value name='font' value='"+fontId+"'/><Value name='value' value='Native text 123'/></Component></Components></MappingSettings>"));
                        using(var scene=new SceneProgram(fontScene))using(var renderer=new NativeRenderer(scene,device,root))
                        {
                            scene.Tick(0,0);renderer.Tick();device.SetRenderTarget(target);device.Clear(Color.Black);game.StartBatch();try{renderer.DrawLayer("world",1);}finally{game.EndBatch();}
                            var pixels=new Color[480*360];target.GetData(pixels);Check(pixels.Any(c=>c.R>0),"Native font preset renders through the scene: "+fontId);
                        }
                    }
                    var stats=new StatsScreen();var tree=new BTmanager(stats);pressed=false;tree.Run(1f/60);
                    Action render=()=>{device.SetRenderTarget(target);device.Clear(Color.Black);game.StartBatch();try{Draw(stats);}finally{game.EndBatch();}};
                    render();var native=new Color[480*360];target.GetData(native);stats.ResetResult();
                    var definition=new RecipeCompiler(PrimitiveRegistry.Create()).Compile(XDocument.Parse("<MappingSettings version='1'><Variables><Variable id='score' kind='Integer' value='0'/></Variables><Components><Component id='result' kind='result'><Value name='page' value='Just One Jump'/><Value name='role' value='main'/><Value name='label' value='Jumps'/></Component></Components><Graph><Node id='score' op='input' source='variable:score'/><Node id='text' op='format' inputs='score'/></Graph><Bindings><Bind id='value' source='text' target='result/value'/></Bindings></MappingSettings>"));
                    using(var owner=new NativeResults(root))
                    {
                        using(var scene=new SceneProgram(definition))
                        using(var activation=new RuntimeScope())
                        {
                            NativeResults.Prepare(scene);NativeResults.Attach(scene,activation);
                            scene.SetVariable("score",SceneValue.Integer(9));scene.Tick(0,0);
                            scene.Signals.Publish("scene.completed",SceneValue.Boolean(true),ObservationQuality.Exact);NativeResults.Freeze(scene);
                        }
                        for(int tick=0;tick<40;tick++)tree.Run(1f/60);
                        render();var before=new Color[480*360];target.GetData(before);
                        Check(before.SequenceEqual(native),"Native final statistics remain first after scene disposal");
                        pressed=true;Check(tree.Run(1f/60)==BTresult.Running,"Native confirmation opens the authored result page");
                        render();var after=new Color[480*360];target.GetData(after);
                        Check(!after.SequenceEqual(native)&&after.Any(c=>c.R>0),"The already-compiled native drawing caller renders map result text");
                        Check(((string[])typeof(StatsScreen).GetField("m_lines",All).GetValue(stats)).SequenceEqual(new[]{"NATIVE STATISTICS"}),"Native statistics text is restored after the result drawing pass");
                        using(var file=File.Create(Path.Combine(root,"native-results.png")))target.SaveAsPng(file,480,360);
                        pressed=false;bool waiting=true;for(int tick=0;tick<40;tick++)waiting&=tree.Run(1f/60)==BTresult.Running;
                        Check(waiting,"Result page waits for confirmation");
                        pressed=true;Check(tree.Run(1f/60)==BTresult.Success,"Final native confirmation completes the result sequence");
                    }
                    var patch=HarmonyLib.Harmony.GetPatchInfo(typeof(StatsScreen).GetMethod("DrawStats",All));
                    Check(patch==null||!patch.Owners.Contains("mega-mapping-expansion.results"),"World disposal releases only the result adapter's hooks");
                }
                finally{game.contentManager.root=previousRoot;game.contentManager.font.MenuFont=previousFont;for(int i=0;i<fontFields.Length;i++)fontFields[i].SetValue(game.contentManager.font,previousFonts[i]);}
            }
        }
    }
}
