using System;
using System.IO;
using System.Reflection;
using System.Xml;
using JumpKing;
using JumpKing.Level;
using JumpKing.SaveThread;
using JumpKing.BodyCompBehaviours;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    // Explicit debug command only. Uses loaded native screens, the actual native
    // teleporter and encrypted save codec; never opens or overwrites player saves.
    internal static class TopologyProbe
    {
        internal static void Run(string root)
        {
            var layout = NativeMapLayout.Current;
            if (layout == null) throw new InvalidOperationException("Topology probe requires map.xml");
            var player = JumpKing.GameManager.GameLoop.m_player;
            if (player == null) throw new InvalidOperationException("No debug player");
            var screens = (LevelScreen[])typeof(LevelManager).GetField("m_screens", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var body = player.m_body;
            Vector2 previous = body.Position, velocity = body.Velocity;
            int crossings = 0, saves = 0;
            string folder = Path.Combine(root, "props/mega-mapping-expansion/preview");
            Directory.CreateDirectory(folder);
            string save = Path.Combine(folder, "topology-" + Guid.NewGuid().ToString("N") + ".sav");
            Type codec = typeof(Game1).Assembly.GetType("FileUtil.Encryption.Encryption", true);
            try
            {
                foreach (var pair in layout.Links)
                {
                    for (int side = 0; side < 2; side++)
                    {
                        int target = pair.Value[side];
                        if (target < 1) continue;
                        NativeSceneAdapter.PreviewScreen(pair.Key + 1, 120, 220);
                        body.Position.X = side == 0 ? -32 : 481;
                        var context = new BehaviourContext(body);
                        new HandlePlayerTeleportBehaviour().ExecuteBehaviour(context);
                        if (!context.ContainsKey(HandlePlayerTeleportBehaviour.TeleportedPlayerFlag)
                            || Camera.CurrentScreenIndex1 != target)
                            throw new InvalidOperationException("Native crossing failed: " + (pair.Key + 1) + " -> " + target);
                        crossings++;
                        var state = new SaveState { position = body.Position, velocity = body.Velocity };
                        codec.GetMethod("SaveFile").MakeGenericMethod(typeof(SaveState)).Invoke(null, new object[] { save, state });
                        var restored = (SaveState)codec.GetMethod("LoadFile").MakeGenericMethod(typeof(SaveState)).Invoke(null, new object[] { save });
                        if (restored.position != state.position || restored.velocity != state.velocity)
                            throw new InvalidOperationException("Native encrypted position round trip failed on screen " + target);
                        saves++;
                    }
                }
                using (var writer = XmlWriter.Create(Path.Combine(folder, "topology-result.xml"), new XmlWriterSettings { Indent = true }))
                {
                    writer.WriteStartElement("TopologyProbe");
                    writer.WriteAttributeString("result", "ok");
                    writer.WriteAttributeString("contentScreens", layout.Screens.ToString());
                    writer.WriteAttributeString("loadedScreens", screens.Length.ToString());
                    writer.WriteAttributeString("nativeCrossings", crossings.ToString());
                    writer.WriteAttributeString("encryptedSaveRoundTrips", saves.ToString());
                    writer.WriteEndElement();
                }
                ModEntry.Log("Native topology probe passed: screens=" + screens.Length + ", crossings=" + crossings + ", encryptedSaves=" + saves);
            }
            finally
            {
                body.Position = previous; body.Velocity = velocity;
                Camera.UpdateCamera(body.GetHitbox().Center);
                if (File.Exists(save)) File.Delete(save); // Only this invocation's scratch save.
            }
        }
    }
}
