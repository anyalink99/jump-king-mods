using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Linq;
using BehaviorTree;
using EntityComponent;
using JKRuntime;
using JumpKing;
using JumpKing.MiscEntities.OldMan;
using JumpKing.Util;
using JumpKing.Util.DrawBT;
using MegaMappingExpansion.Construction;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void NativeDialogueChecks()
        {
            const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
            var gameField=typeof(Game1).GetField("_instance",flags);object previousGame=gameField.GetValue(null);
            var managerField=typeof(EntityManager).GetField("_instance",flags);object previousManager=managerField.GetValue(null);
            var current=typeof(NativeScene).GetProperty("Current",flags);object previousScene=current.GetValue(null,null);
            var game=(Game1)FormatterServices.GetUninitializedObject(typeof(Game1));game.contentManager=(JKContentManager)FormatterServices.GetUninitializedObject(typeof(JKContentManager));
            game.contentManager.oldMan=new JKContentManager.OldMan();game.contentManager.audio=new JKContentManager.Audio();
            var settings=new OldManSettings{name="fixture",home_screen=1};
            var field=typeof(JKContentManager.OldMan).GetField("_settings",flags);
            var dictionary=(IDictionary)Activator.CreateInstance(field.FieldType);
            var valueType=field.FieldType.GetGenericArguments()[1];var value=Activator.CreateInstance(valueType);
            valueType.GetField("settings",flags).SetValue(value,settings);dictionary.Add("fixture",value);field.SetValue(game.contentManager.oldMan,dictionary);
            var npcType=typeof(Game1).Assembly.GetType("JumpKing.MiscEntities.OldManEntity",true);
            var entity=(Entity)FormatterServices.GetUninitializedObject(npcType);
            typeof(Entity).GetField("m_components",flags).SetValue(entity,new List<Component>());
            var boardType=typeof(Entity).Assembly.GetType("EntityComponent.BlackBoardComp",true);
            var board=(Component)Activator.CreateInstance(boardType);entity.AddComponents(board);
            boardType.GetMethod("Set").MakeGenericMethod(typeof(string)).Invoke(board,new object[]{SayLine.BB_LINE_KEY,"A B"});
            var say=new SayLine(entity,.05f,1);var pause=new PauseNode(2.2f);
            npcType.GetField("m_settings",flags).SetValue(entity,settings);npcType.GetField("m_say",flags).SetValue(entity,say);npcType.GetField("m_say_pause",flags).SetValue(entity,pause);
            var timer=(JumpKing.Timer)typeof(PauseNode).GetField("m_timer",flags).GetValue(pause);
            try
            {
                gameField.SetValue(null,game);managerField.SetValue(null,null);new EntityManager().AddObject(entity);
                var definition=new RecipeCompiler(PrimitiveRegistry.Create()).Compile(XDocument.Parse(@"<MappingSettings version='1'><Components>
<Component id='speaker' kind='npc'><Value name='name' value='fixture'/><Value name='muted' value='true'/><Value name='linePause' value='0.7'/></Component>
</Components></MappingSettings>"));
                using(var scene=new SceneProgram(definition))using(var sounds=new NativeSoundPool())using(var owner=new NativeControllers(scene,".",sounds))
                {
                    var native=(NativeScene)FormatterServices.GetUninitializedObject(typeof(NativeScene));
                    typeof(NativeScene).GetField("Controllers",flags).SetValue(native,owner);current.SetValue(null,native,null);
                    int starts=0,chars=0,ends=0;scene.Signals.SubscribeEvent("speaker.started",e=>starts++);
                    scene.Signals.SubscribeEvent("speaker.character",e=>chars++);scene.Signals.SubscribeEvent("speaker.finished",e=>ends++);
                    using(var attempt=new RuntimeScope())
                    {
                        owner.Activate(attempt);owner.Tick(.01,true,false,false);
                        using(var candidate=new NativeControllers(scene,".",sounds)){}
                        Require(Math.Abs(timer.Duration-.7f)<.001,"NPC adapter applies the native line pause");
                        for(int i=0;i<4;i++){say.Run(new TickData(.1f,i));scene.Signals.Dispatch();}
                        Require(starts==1&&chars==3&&ends==1,"Real native SayLine reports each event once after a reload candidate is disposed");
                        Require(scene.Signals.Read("speaker.line").Value.Text=="A B"&&scene.Signals.Read("speaker.revealed").Value.X==3,"Native quote selection remains the source of scene observations");
                    }
                    Require(Math.Abs(timer.Duration-2.2f)<.001,"Attempt teardown restores the original native pause");
                }
            }
            finally{current.SetValue(null,previousScene,null);gameField.SetValue(null,previousGame);managerField.SetValue(null,previousManager);}
        }
        private static void NativeVictoryChecks()
        {
            const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
            var screens=typeof(JumpKing.Level.LevelManager).GetField("m_screens",flags);object previousScreens=screens.GetValue(null);
            var count=typeof(JumpKing.Level.LevelManager).GetField("_total_screens",flags);object previousCount=count.GetValue(null);
            var camera=typeof(Camera).GetField("_current_screen",flags);object previousCamera=camera.GetValue(null);
            var current=typeof(NativeScene).GetProperty("Current",flags);object previousScene=current.GetValue(null,null);
            var player=(JumpKing.Player.PlayerEntity)FormatterServices.GetUninitializedObject(typeof(JumpKing.Player.PlayerEntity));
            var body=(JumpKing.Player.BodyComp)FormatterServices.GetUninitializedObject(typeof(JumpKing.Player.BodyComp));
            typeof(Entity).GetField("m_components",flags).SetValue(player,new List<Component>{body});
            typeof(JumpKing.Player.PlayerEntity).GetField("m_body",flags).SetValue(player,body);
            typeof(JumpKing.Player.BodyComp).GetField("m_width",flags).SetValue(body,10);typeof(JumpKing.Player.BodyComp).GetField("m_height",flags).SetValue(body,10);
            var grounded=typeof(JumpKing.Player.BodyComp).GetField("_is_on_ground",flags);grounded.SetValue(body,true);
            var ending=(JumpKing.GameManager.MultiEnding.NormalEnding.NormalEnding)FormatterServices.GetUninitializedObject(typeof(JumpKing.GameManager.MultiEnding.NormalEnding.NormalEnding));
            try
            {
                screens.SetValue(null,new JumpKing.Level.LevelScreen[2]);count.SetValue(null,2);camera.SetValue(null,1);
                var definition=new RecipeCompiler(PrimitiveRegistry.Create()).Compile(XDocument.Parse(@"<MappingSettings version='1'><Variables><Variable id='area' kind='Vector2' value='20,20'/></Variables><Components>
<Component id='win' kind='ending'><Value name='screen' value='2'/><Value name='condition' value='true'/><Value name='position' value='100,100'/><Value name='size' value='20,20'/></Component>
</Components><Graph><Node id='area' op='input' source='variable:area' units='px'/></Graph><Bindings><Bind id='area' source='area' target='win/size'/></Bindings></MappingSettings>"));
                using(var scene=new SceneProgram(definition))using(var sounds=new NativeSoundPool())using(var owner=new NativeControllers(scene,".",sounds))using(var attempt=new RuntimeScope())
                {
                    var native=(NativeScene)FormatterServices.GetUninitializedObject(typeof(NativeScene));typeof(NativeScene).GetField("Controllers",flags).SetValue(native,owner);current.SetValue(null,native,null);
                    owner.Activate(attempt);body.Position=new Microsoft.Xna.Framework.Vector2(90,-260);
                    using(var candidate=new NativeControllers(scene,".",sounds)){}
                    Require(!ending.CheckWin(player),"Touching an authored win-area edge doesn't count as overlap");
                    body.Position.X=91;Require(ending.CheckWin(player),"The real native CheckWin sees fresh body overlap in the authored screen");
                    scene.SetVariable("area",MegaMappingExpansion.Api.SceneValue.Vector(0,20));scene.Tick(0,0);
                    Require(!ending.CheckWin(player),"A bound zero-width area cannot remove the victory hitbox requirement");
                    scene.SetVariable("area",MegaMappingExpansion.Api.SceneValue.Vector(20,20));scene.Tick(0,0);
                    grounded.SetValue(body,false);Require(!ending.CheckWin(player),"The authored victory rectangle preserves its grounded requirement");
                    grounded.SetValue(body,true);body.Position.X=130;Require(!ending.CheckWin(player),"A body outside the authored area cannot win from stale scene samples");
                }
                body.Position.X=91;Require(!ending.CheckWin(player),"Disposing the adapter restores the original native victory predicate");
            }
            finally{current.SetValue(null,previousScene,null);screens.SetValue(null,previousScreens);count.SetValue(null,previousCount);camera.SetValue(null,previousCamera);}
        }
    }
}
