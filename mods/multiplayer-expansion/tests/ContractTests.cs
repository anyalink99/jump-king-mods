using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using JumpKing;
using JumpKingMultiplayer;
using Microsoft.Xna.Framework.Input;

namespace MultiplayerExpansion
{
    internal static class ContractTests
    {
        private static int Main(string[] args)
        {
            try
            {
                Client.Session = args[0];
                Client.Role = 1;
                if (NativeMod.DebugEnabled || NativeMod.Steam(null, default(JumpKing.PauseMenu.GuiFormat)) != null
                    || NativeMod.Local(null, default(JumpKing.PauseMenu.GuiFormat)) != null
                    || NativeMod.Switch(null, default(JumpKing.PauseMenu.GuiFormat)) != null
                    || NativeMod.Stop(null, default(JumpKing.PauseMenu.GuiFormat)) != null
                    || NativeMod.Status(null, default(JumpKing.PauseMenu.GuiFormat)) != null)
                    throw new Exception("Two-client controls escaped into normal gameplay.");
                LocalTransportTests.Run();
                SessionLifecycleTests.Run();
                var firstMap = new SessionMapIdentity(@"C:\lab\base\Content");
                var secondMap = new SessionMapIdentity(@"C:\lab\client2\Content");
                if (firstMap.Resolve(@"c:\lab\base\Content\", 3810459639) != secondMap.Resolve(@"C:\lab\client2\Content", null))
                    throw new Exception("Copied Debug maps inherited different saved Workshop identities.");
                if (firstMap.Resolve(@"C:\other-map", 123) != 123 || secondMap.Resolve(@"C:\other-map", null) != null)
                    throw new Exception("The session identity leaked to another map.");
                foreach (int rate in new[] { 15, 30, 60 })
                {
                    var cadence = new RenderCadence();
                    int frames = 0;
                    for (int tick = 0; tick < 2400; tick++) if (cadence.Allow(tick * 41667L, rate)) frames++;
                    if (frames != rate * 10) throw new Exception("Draw cap drifted at " + rate + " FPS: " + frames);
                    if (!cadence.Allow(2400 * 41667L, 30)) throw new Exception("Draw cap froze after a rate change.");
                }
                NativeReceiveTests.Warm();
                typeof(Client).GetMethod("InstallHooks", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                NativeReceiveTests.Run();
                // exercise the foreign patch installer too; two Harmony engines used to break this
                new MultiplayerPatches(new Harmony("multiplayer-expansion.compatibility-test"));
                foreach (string name in new[] { "0Harmony.dll", "JumpKingMultiplayer.dll" })
                {
                    string nested = Path.Combine(args[0], name);
                    File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name), nested);
                    Assembly expected = name == "0Harmony.dll" ? typeof(Harmony).Assembly : typeof(MultiplayerPatches).Assembly;
                    if (!ReferenceEquals(Client.LoadShared(nested), expected)) throw new Exception("Mod discovery loaded a second assembly context: " + name);
                }
                new MultiplayerPatches(new Harmony("multiplayer-expansion.compatibility-test-after-scan"));
                var discovered = new System.Collections.Generic.List<string>();
                typeof(JumpKing.Mods.ModLoader).GetMethod("GetModAssemblies", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(new JumpKing.Mods.ModLoader(), new object[] { args[0], discovered });
                if (Array.FindAll(AppDomain.CurrentDomain.GetAssemblies(), a => a.GetName().Name == "0Harmony").Length != 1)
                    throw new Exception("Native mod discovery created another Harmony context.");
                if (args.Length > 1)
                {
                    foreach (string dll in Directory.GetFiles(args[1], "*.dll", SearchOption.AllDirectories))
                    {
                        try { Client.LoadShared(dll).GetTypes(); }
                        catch (BadImageFormatException) { }
                        catch (ReflectionTypeLoadException error) { Console.WriteLine(dll + ": " + error.LoaderExceptions[0].Message); }
                    }
                    File.WriteAllLines(Path.Combine(args[0], "assemblies.txt"), Array.ConvertAll(AppDomain.CurrentDomain.GetAssemblies(), a => a.FullName + " | " + (a.IsDynamic ? "dynamic" : a.Location)));
                    new MultiplayerPatches(new Harmony("multiplayer-expansion.compatibility-test-all-mods"));
                }
                if (Keyboard.GetState().GetPressedKeys().Length != 0) throw new Exception("An unfocused client accepted keyboard input.");
                if (Steamworks.SteamUserStats.SetAchievement("lab-should-never-submit")) throw new Exception("Lab Steam achievement writes were not blocked.");
                if (Client.Selected) throw new Exception("A client without its host accepted input.");
                string runtimePath = Environment.GetEnvironmentVariable("MPEX_TEST_RUNTIME");
                if (!string.IsNullOrEmpty(runtimePath)) RuntimeInputTests.Run(runtimePath);
                typeof(Client).GetMethod("DebugBaseGame", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                if (LevelDebugState.instance == null || !LevelDebugState.instance.coordinates) throw new Exception("Native Debug state was not enabled.");
                Console.WriteLine("[OK] Real game + Multiplayer hooks on one Harmony engine; inactive input, Steam write isolation and native Debug activation");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
