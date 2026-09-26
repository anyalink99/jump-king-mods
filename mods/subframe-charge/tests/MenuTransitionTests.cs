using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using BehaviorTree;
using HarmonyLib;
using JumpKing;
using JumpKing.Controller;
using JumpKing.GameManager.TitleScreen;
using JumpKing.Util;
using Microsoft.Xna.Framework;
using SubframeCharge;

internal static partial class PerformanceTests
{
    private static int titleStarts;
    private static float exitFade;
    private sealed class ContinueNode : IBTnode
    {
        internal int Activations;
        protected override BTresult MyRun(TickData data)
        {
            if(!ControllerManager.instance.MenuController.GetPadState().confirm) return BTresult.Running;
            Activations++; return BTresult.Success;
        }
    }
    private sealed class LoadNode : IBTnode
    {
        internal int Loads;
        protected override BTresult MyRun(TickData data) { Loads++; return BTresult.Success; }
    }
    private static bool StartTitleFixture(GameTitleScreen __instance)
    {
        titleStarts++;
        MenuCadence.TitleStart(__instance);
        var menu=new ContinueNode(); var root=new BTsequencor(menu,new PauseNode(exitFade));
        AccessTools.Field(typeof(GameTitleScreen),"m_menu").SetValue(__instance,menu);
        AccessTools.Field(typeof(GameTitleScreen),"m_root_node").SetValue(__instance,root);
        AccessTools.Field(typeof(GameTitleScreen),"m_btmanager").SetValue(__instance,new BTmanager(root));
        return false;
    }
    private static void NoDownloads(JumpKing.Workshop.DownloadManager manager,float delta) { }
    private static IEnumerable<CodeInstruction> HeadlessTitle(IEnumerable<CodeInstruction> source)
    {
        foreach(var instruction in source)
        {
            if(instruction.Calls(AccessTools.Method(typeof(JumpKing.Workshop.DownloadManager),"Update")))
            { instruction.opcode=OpCodes.Call; instruction.operand=AccessTools.Method(typeof(PerformanceTests),"NoDownloads"); }
            yield return instruction;
        }
    }
    private static void ContinueTransitionTests()
    {
        foreach(bool refresh in new[]{false,true})
        foreach(bool disableAtExit in new[]{false,true})
        foreach(float fade in new[]{0f,.05f})
        {
            var fixture=new Harmony("sfc.continue-transition.tests"); var game=MakeGame();
            fixture.Patch(AccessTools.Method(typeof(GameTitleScreen),"OnNewRun"),prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"StartTitleFixture")));
            fixture.Patch(AccessTools.Method(typeof(GameTitleScreen),"MyRun"),transpiler:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"HeadlessTitle")));
            fixture.Patch(AccessTools.Method(typeof(MenuCadence),"Pump"),transpiler:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"HeadlessMenu")));
            fixture.Patch(AccessTools.Method(typeof(JKRuntime.UI.UiPointer),"Update"),prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"Skip")){priority=Priority.First});
            var manager=(ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager)); ControllerManager.instance=manager;
            AccessTools.Field(typeof(ControllerManager),"m_pads").SetValue(manager,new List<PadInstance>());
            AccessTools.Field(typeof(ControllerManager),"_menu_controller").SetValue(manager,new MenuController(manager));
            var jump=(JumpGame)FormatterServices.GetUninitializedObject(typeof(JumpGame)); AccessTools.Field(typeof(JumpGame),"_instance").SetValue(null,jump);
            SettingsStore.Current.SetInputs(true); SettingsStore.Current.HighRefresh=refresh; PerformanceFeatures.Install(); FeatureClock.BeforeTick(game); menuPlaying=false;
            var title=new GameTitleScreen(); var load=new LoadNode(); var playing=new MenuNode();
            var parent=new BTmanager(new BTsequencor(title,load,playing)); exitFade=fade; titleStarts=0;
            try
            {
                parent.Run(1f/60);
                var menu=(ContinueNode)AccessTools.Field(typeof(GameTitleScreen),"m_menu").GetValue(title);
                // Publish one presentation press through the same latch as production.
                var devices=(Dictionary<PadInstance,InputLatch>)AccessTools.Field(typeof(ResponsiveInput),"devices").GetValue(null);
                devices.Add(new PadInstance(new Pad()),new InputLatch{UiEdges=64});
                MenuCadence.Pump(1f/240);
                if(disableAtExit) { SettingsStore.Current.SetInputs(false); FeatureClock.BeforeTick(game); }
                for(int frame=0;frame<30;frame++)
                {
                    for(int sub=0;sub<4;sub++)MenuCadence.Pump(1f/240);
                    parent.Run(1f/60);
                }
                Check(titleStarts==1 && menu.Activations==1 && load.Loads==1 && playing.Calls>0,
                    "Native title parent reaches gameplay exactly once: refresh="+refresh+" disable="+disableAtExit+" fade="+fade);
                // A real new native lifetime must still be able to start normally.
                title.ResetResult(); parent.Reset(); parent.Run(1f/60);
                Check(titleStarts==2 && load.Loads==1,"Returning to the title creates a fresh menu without replaying Continue");
            }
            finally
            {
                PerformanceFeatures.Uninstall(); fixture.UnpatchAll(fixture.Id); ControllerManager.instance=null;
                AccessTools.Field(typeof(JumpGame),"_instance").SetValue(null,null); AccessTools.Field(typeof(Game1),"_instance").SetValue(null,null);
            }
        }
    }
}
