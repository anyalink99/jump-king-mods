using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using BehaviorTree;
using JKRuntime.UI;
using JumpKing;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JKRuntime.Inspection
{
    internal static partial class Tests
    {
        private static void DebugMenuGraphics(GraphicsDevice device, RenderTarget2D target, SpriteBatch batch,
            GuiFormat format, string output)
        {
            Func<Action, string, Color[]> render = (draw, name) => {
                device.SetRenderTarget(target); device.Clear(Color.CornflowerBlue);
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
                draw(); batch.End(); device.SetRenderTarget(null);
                var pixels = new Color[480 * 360]; target.GetData(pixels);
                if (name != null) using (var file = File.Create(Path.Combine(output, name + ".png")))
                    target.SaveAsPng(file, 480, 360);
                return pixels;
            };
            foreach (bool hooks in new[] { false, true })
            {
                if (hooks) PointerHooks.Install();
                foreach (bool pause in new[] { false, true })
                {
                    // use the installed factory's registration and draw order, not a page-only capture
                    var factoryType = typeof(Game1).Assembly.GetType("JumpKing.PauseMenu.MenuFactory", true);
                    var factory = FormatterServices.GetUninitializedObject(factoryType);
                    factoryType.GetField("m_drawables", Flags).SetValue(factory, new List<JumpKing.Util.IDrawable>());
                    IBTSimpleMenuItem item;
                    Require(VanillaMenuAdapter.TryCreateSetting(factory, format, typeof(ModEntry).GetMethod(
                        pause ? "PauseMenuDebugActions" : "MainMenuDebugActions"), out item), "Native debug menu registration");
                    var menu = (MenuSelector)((IBTMenuDecorator)item).Child;
                    var tree = new BTmanager(menu);
                    try
                    {
                        var state = typeof(IBTnode).GetField("m_last_result", Flags);
                        var childState = typeof(MenuSelector).GetField("m_last_child_result", Flags);
                        state.SetValue(menu, BTresult.Running);
                        Action drawFactory = () => factoryType.GetMethod("Draw", Flags, null, new[] { typeof(BTmanager) }, null)
                            .Invoke(factory, new object[] { tree });
                        var prefix = "debug-" + (pause ? "pause" : "main") + (hooks ? "-hooks" : "-native");
                        var parent = render(drawFactory, prefix + "-hub");
                        Require(parent.Any(c => c != Color.CornflowerBlue), "Debug menu renders when no child is open");
                        for (int selected = 0; selected < 2; selected++)
                        {
                            typeof(MenuSelector).GetField("_index", Flags).SetValue(menu, selected);
                            var child = ((IBTMenuDecorator)menu.Children[selected]).Child;
                            var embedded = child as EmbeddedMenuPageNode;
                            if (embedded != null)
                            {
                                var session = (UiPageSession)typeof(EmbeddedMenuPageNode).GetField("session", Flags).GetValue(embedded);
                                // seed the open page; these checks isolate rendering from catalogue discovery and input
                                typeof(UiPageSession).GetProperty("Active", Flags).SetValue(session, true, null);
                                typeof(GimmickPage).GetMethod("Home", Flags).Invoke(session.Page, null);
                            }
                            for (int visit = 0; visit < 2; visit++)
                            {
                                state.SetValue(child, BTresult.Running); childState.SetValue(menu, BTresult.Running);
                                var expected = render(((JumpKing.Util.IDrawable)child).Draw, null);
                                Require(expected.Any(c => c != Color.CornflowerBlue), "Opened child renders");
                                var actual = render(drawFactory, prefix + "-child-" + selected);
                                Require(expected.SequenceEqual(actual), "Parent covered debug child " + selected + " / " + prefix);
                                state.SetValue(child, BTresult.Success); childState.SetValue(menu, BTresult.Success);
                                var returned = render(drawFactory, null);
                                Require(returned.SequenceEqual(render(menu.Draw, null)), "Back leaves only the parent visible");
                                Require(returned.Any(c => c != Color.CornflowerBlue), "Parent returns after child closes");
                            }
                        }
                    }
                    finally { GC.SuppressFinalize(tree); }
                }
            }
            Console.WriteLine("[OK] Native debug menu composition: both children, main/pause, Back/reopen, pointer hooks on/off");
        }
    }
}
