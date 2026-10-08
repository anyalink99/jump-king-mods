using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Runtime.Serialization;
using HarmonyLib;
using JumpKing;
using JumpKing.Controller;
using JumpKing.Mods;
using JumpKingMultiplayer.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Steamworks;

namespace MultiplayerExpansion
{
    internal static partial class Client
    {
        internal static IPacketLink Link;
        internal static int Role;
        internal static string Session;
        private static IntPtr host;
        private static DateTime lastStatus;
        private static int applied;
        private static readonly Stopwatch clock = Stopwatch.StartNew();
        private static Harmony harmony;
        private static bool wasReady;
        private static int drawn;
        private static IntPtr lastWindow;
        private static readonly RenderCadence cadence = new RenderCadence();
        private static int updates, previousDrawn, previousUpdates;
        private static TimeSpan previousCpu;
        private static MemoryMappedFile inputMap;
        private static MemoryMappedViewAccessor input;
        private static bool nativeHost;
        private static bool readyReported;
        private static SessionMapIdentity mapIdentity;
        private static readonly Type spriteStateType = typeof(MultiplayerManager).Assembly.GetType("JumpKingMultiplayer.Extensions.PlayerSpriteStateExtensions", true);
        private static long previousFrame, maxFrameGap;
        private static readonly List<Action> restoreKeyboard = new List<Action>();

        internal static int Run()
        {
            Role = int.Parse(Environment.GetEnvironmentVariable("MPEX_ROLE"));
            Session = Environment.GetEnvironmentVariable("MPEX_SESSION");
            host = new IntPtr(long.Parse(Environment.GetEnvironmentVariable("MPEX_HOST") ?? "0"));
            try
            {
                if (!SteamAPI.Init()) throw new InvalidOperationException("Steam initialization failed. Start Steam and sign in to the account that owns Jump King.");
                using (Link = CreateLink(Role == 1, Session, Environment.GetEnvironmentVariable("MPEX_TOKEN"), Environment.GetEnvironmentVariable("MPEX_TRANSPORT")))
                {
                    if (Environment.GetEnvironmentVariable("MPEX_PROBE") == "1") return Probe();
                    WatchParent();
                    inputMap = MemoryMappedFile.OpenExisting(Environment.GetEnvironmentVariable("MPEX_INPUT"));
                    input = inputMap.CreateViewAccessor(0, 4, MemoryMappedFileAccess.Read);
                    InstallHooks();
                    JumpKing.Program.Run();
                }
                return 0;
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(Session, "client" + Role + ".error.txt"), error.ToString());
                return 1;
            }
            finally { if (input != null) input.Dispose(); if (inputMap != null) inputMap.Dispose(); SteamAPI.Shutdown(); }
        }

        internal static IPacketLink CreateLink(bool server, string session, string token, string transport)
        { return transport == "Local" ? (IPacketLink)new LocalLink(server, token) : new SteamLink(server, session, token); }

        private static int Probe()
        {
            byte[] message = new byte[4096];
            for (int i = 0; i < message.Length; i++) message[i] = (byte)(i % 251);
            int received = 0;
            while (clock.Elapsed.TotalSeconds < 30)
            {
                SteamAPI.RunCallbacks();
                Link.Pump();
                if (Link.Ready && Link.Sent < 20) Link.Send(message);
                byte[] bytes;
                while ((bytes = Link.Read()) != null)
                {
                    if (!SteamLink.Equal(bytes, message)) throw new IOException("Steam changed a packet payload.");
                    received++;
                }
                if (received == 20 && Link.Sent == 20)
                {
                    File.WriteAllText(Path.Combine(Session, "client" + Role + ".probe.txt"), "PASS: 20 packets / 81920 bytes in each direction; exact payloads; SteamNetworkingSockets / IPv4 loopback");
                    // let the peer receive its final reliable packet before we tear down Steam
                    while (clock.Elapsed.TotalSeconds < 30 && !File.Exists(Path.Combine(Session, "client" + (3 - Role) + ".probe.txt")))
                    { SteamAPI.RunCallbacks(); Thread.Sleep(10); }
                    return 0;
                }
                Thread.Sleep(10);
            }
            throw new TimeoutException("Steam transport probe timed out: " + Link.State + ", received=" + received);
        }

        private static void Patch(Type type, string method, string prefix, string postfix = null, Type[] arguments = null)
        {
            MethodInfo target = arguments == null ? AccessTools.Method(type, method) : AccessTools.Method(type, method, arguments);
            if (target == null) throw new MissingMethodException(type.FullName, method);
            harmony.Patch(target, prefix == null ? null : new HarmonyMethod(typeof(Client), prefix), postfix == null ? null : new HarmonyMethod(typeof(Client), postfix));
        }

        private static void InstallHooks()
        { InstallHooksCore(false); }

        private static void InstallHooksCore(bool inGame)
        {
            harmony = new Harmony("anyalink.multiplayer-expansion.lab");
            if (!inGame)
            {
                Patch(typeof(JumpKing.Program), "InitSteam", "AlreadyInitialized");
                Patch(typeof(Game1), "Initialize", "DebugBaseGame", "Initialized");
                Patch(typeof(ModLoader), "LoadMods", "LoadMods");
                foreach (string method in new[] { "GetModAssemblies", "LoadReferencedAssemblies" })
                    harmony.Patch(AccessTools.Method(typeof(ModLoader), method), transpiler: new HarmonyMethod(typeof(Client), "SharedAssemblies"));
                Patch(typeof(SteamUGC), "GetItemInstallInfo", null, "StagedWorkshopPath");
                Type prefs = typeof(Game1).Assembly.GetType("JumpKing.PlayerPreferences.Persocom.GraphicsPreferencesRuntime", true);
                Patch(prefs, "InitAllPrefs", "WindowedPrefs");
                Patch(prefs, "OnSetPrefs", "WindowedPrefs");
            }
            Patch(typeof(Game1), "Update", "Update");
            Patch(typeof(Game1), "Draw", null, "Drawn");
            if (!inGame) Patch(typeof(Game), "DoDraw", "LimitDraw");
            Patch(typeof(Game), "get_IsActive", "Active");
            Patch(typeof(PadInstance), "GetPadState", "Pad");
            Patch(typeof(Game1).Assembly.GetType("JumpKing.Player.DebugTeleport", true), "Update", "DebugMouse");
            Patch(typeof(Keyboard), "GetState", "KeyboardState", null, Type.EmptyTypes);
            Patch(typeof(Game1).Assembly.GetType("JumpKing.Controller.KeyboardPad", true), "GetPressedButtons", "KeyboardButtons");
            // both processes share an account, so test runs must never award or clear achievements
            foreach (MethodInfo method in typeof(SteamUserStats).GetMethods().Where(m => m.ReturnType == typeof(bool) &&
                (m.Name.StartsWith("Set") || m.Name.StartsWith("Clear") || m.Name == "StoreStats" || m.Name == "ResetAllStats" || m.Name == "IndicateAchievementProgress" || m.Name == "UpdateAvgRateStat")))
                harmony.Patch(method, new HarmonyMethod(typeof(Client), "NoSteamWrite"));
            Patch(typeof(MultiplayerManager), "Init", "InitializeMultiplayer");
            foreach (string name in new[] { "CreateLobby", "JoinLobby", "LeaveLobby", "UpdateLobbyMemberIds" }) Patch(typeof(MultiplayerManager), name, "Skip");
            Patch(typeof(MultiplayerManager), "UpdatePlayerState", null, "Applied");
            Patch(spriteStateType, "GetLevelId", null, "SessionLevelId");
            Patch(typeof(SteamNetworking), "SendP2PPacket", "Send");
            Patch(typeof(SteamNetworking), "IsP2PPacketAvailable", "Available");
            Patch(typeof(SteamNetworking), "ReadP2PPacket", "Read");
            // GetUpdates may already contain inlined Steam calls when attaching to a running game
            harmony.Patch(AccessTools.Method(typeof(MultiplayerManager), "GetUpdates"), transpiler: new HarmonyMethod(typeof(Client), "ReceiveCalls"));
            Patch(typeof(SteamFriends), "GetFriendPersonaName", "PeerName");
            Patch(typeof(SteamFriends), "ActivateGameOverlayInviteDialog", "Skip");
            File.WriteAllText(Path.Combine(Session, "client" + Role + ".hooks.txt"), "Installed lab hooks before game startup.");
        }

        internal static void AttachNative(string session, IntPtr window, string inputName, string token, string transport)
        {
            nativeHost = true; Role = 1; Session = session; host = lastWindow = window;
            mapIdentity = new SessionMapIdentity(Game1.instance.contentManager.root);
            previousFrame = maxFrameGap = 0;
            wasReady = false; applied = drawn = updates = previousDrawn = previousUpdates = 0;
            lastStatus = DateTime.MinValue; previousCpu = TimeSpan.Zero;
            try
            {
                inputMap = MemoryMappedFile.OpenExisting(inputName);
                input = inputMap.CreateViewAccessor(0, 4, MemoryMappedFileAccess.Read);
                Link = CreateLink(true, session, token, transport);
                InstallHooksCore(true);
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                    if (assembly.GetName().Name == "JKRuntime")
                    {
                        InstallRuntimeInput(assembly);
                        object source = AccessTools.Field(assembly.GetType("JKRuntime.Input.SharedKeyboard", true), "source").GetValue(null);
                        FieldInfo readField = AccessTools.Field(source.GetType(), "reader"), focusField = AccessTools.Field(source.GetType(), "foreground");
                        object oldRead = readField.GetValue(source), oldFocus = focusField.GetValue(source);
                        Func<int, short> read = null; Func<IntPtr> focus = null;
                        RouteRuntimeKeyboard(ref read, ref focus, true);
                        readField.SetValue(source, read); focusField.SetValue(source, focus);
                        restoreKeyboard.Add(delegate { readField.SetValue(source, oldRead); focusField.SetValue(source, oldFocus); });
                    }
                InitializeMultiplayer(MultiplayerManager.instance);
            }
            catch { DetachNative(); throw; }
        }

        internal static void DetachNative()
        {
            if (harmony != null) harmony.UnpatchAll(harmony.Id);
            foreach (Action restore in restoreKeyboard) restore();
            restoreKeyboard.Clear(); runtimeInputHooks.Clear();
            if (Link != null) Link.Dispose(); Link = null;
            if (input != null) input.Dispose(); input = null;
            if (inputMap != null) inputMap.Dispose(); inputMap = null;
            var manager = MultiplayerManager.instance;
            if (manager != null)
            {
                var players = (System.Collections.IList)AccessTools.Field(typeof(MultiplayerManager), "Players").GetValue(manager);
                foreach (object player in players) AccessTools.Property(player.GetType(), "IsDisposed").SetValue(player, true, null);
                players.Clear();
                manager.LobbyId = null;
                manager.LobbyPlayers.Clear(); manager.LobbyOwner = null;
            }
            nativeHost = false;
            mapIdentity = null;
        }

        private static bool AlreadyInitialized(ref bool __result)
        {
            PadInstance.RegisterSteamCallback();
            __result = true;
            return false;
        }
        private static bool Skip() { return false; }
        private static bool NoSteamWrite(ref bool __result) { __result = false; return false; }
        private static bool Active(ref bool __result) { __result = true; return false; }
        private static bool LimitDraw(GameTime gameTime)
        {
            return cadence.Allow(clock.Elapsed.Ticks, 60);
        }
        private static void SessionLevelId(ref ulong? __result)
        {
            if (mapIdentity != null && Game1.instance != null)
                __result = mapIdentity.Resolve(Game1.instance.contentManager.root, __result);
        }
        private static void DebugBaseGame()
        {
            if (LevelDebugState.instance != null) return;
            // native -debug also changes the level root; keep vanilla Content and enable its debug state
            var state = (LevelDebugState)FormatterServices.GetUninitializedObject(typeof(LevelDebugState));
            state.coordinates = true;
            state.displayLabels = true;
            AccessTools.Field(typeof(LevelDebugState), "_instance").SetValue(null, state);
        }
        private static void WindowedPrefs(ref JumpKing.PlayerPreferences.Persocom.GraphicPrefs p_settings)
        {
            p_settings.screen_mode = JumpKing.PlayerPreferences.Persocom.ScreenMode.Windowed;
            p_settings.size_mode = JumpKing.PlayerPreferences.Persocom.SizeMode.x1;
        }
        private static bool LoadMods(string modDirectory, ref List<ModAssembly> loadedModAssemblies)
        {
            string local = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "JKMods")) + Path.DirectorySeparatorChar;
            string workshop = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorkshopMods")) + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(modDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (candidate.StartsWith(local, StringComparison.OrdinalIgnoreCase) || candidate.StartsWith(workshop, StringComparison.OrdinalIgnoreCase)) return true;
            loadedModAssemblies = new List<ModAssembly>();
            return false;
        }
        internal static Assembly LoadShared(string path)
        {
            // LoadFrom can create a second load context even for byte-identical Harmony copies
            string identity = AssemblyName.GetAssemblyName(path).FullName;
            Assembly loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == identity);
            Assembly assembly = loaded ?? Assembly.LoadFrom(path);
            if (assembly.GetName().Name == "JKRuntime") InstallRuntimeInput(assembly);
            return assembly;
        }
        private static readonly HashSet<Assembly> runtimeInputHooks = new HashSet<Assembly>();
        internal static void InstallRuntimeInput(Assembly assembly)
        {
            if (runtimeInputHooks.Contains(assembly)) return;
            Type source = assembly.GetType("JKRuntime.Input.KeyboardSource", true);
            var constructor = AccessTools.Constructor(source, new[] { typeof(Func<int, short>), typeof(Func<IntPtr>), typeof(bool) });
            if (constructor == null) throw new MissingMethodException(source.FullName, ".ctor");
            harmony.Patch(constructor, prefix: new HarmonyMethod(typeof(Client), "RouteRuntimeKeyboard"));
            InstallRuntimePointer(assembly);
            runtimeInputHooks.Add(assembly);
            JumpKingModTools.BoundedTextLog.Append(Path.Combine(Session, "client" + Role + ".hooks.txt"), "Routed JK Runtime physical keyboard to the selected embedded client.");
        }
        private static void RouteRuntimeKeyboard(ref Func<int, short> read, ref Func<IntPtr> focus, bool start)
        {
            if (!start) return;
            // record the selected client at observation time, before edge consumers drain the history
            read = key => key == 117 && ReserveSwitchKey ? (short)0 : Native.GetAsyncKeyState(key);
            focus = delegate {
                IntPtr foreground = Native.GetForegroundWindow();
                return InputRouting.Foreground(foreground, Native.GetAncestor(foreground, 2), host,
                    input == null ? 0 : input.ReadInt32(0), Role, lastWindow);
            };
        }
        private static IEnumerable<CodeInstruction> SharedAssemblies(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo original = typeof(Assembly).GetMethod("LoadFrom", new[] { typeof(string) });
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(original)) { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(Client), "LoadShared"); }
                yield return instruction;
            }
        }
        private static void StagedWorkshopPath(PublishedFileId_t nPublishedFileID, ref string pchFolder, bool __result)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorkshopMods", nPublishedFileID.m_PublishedFileId.ToString());
            if (__result && Directory.Exists(path)) pchFolder = path;
        }
        private static void Initialized(Game1 __instance)
        {
            mapIdentity = new SessionMapIdentity(__instance.contentManager.root);
            __instance.InactiveSleepTime = TimeSpan.Zero;
            lastWindow = __instance.Window.Handle;
            __instance.Window.Title = "Multiplayer Expansion - Client " + Role;
            File.WriteAllText(Path.Combine(Session, "client" + Role + ".window.txt"), __instance.Window.Handle.ToInt64().ToString());
        }
        private static void Drawn(Game1 __instance)
        {
            long now = clock.Elapsed.Ticks;
            if (previousFrame != 0) maxFrameGap = Math.Max(maxFrameGap, now - previousFrame);
            previousFrame = now;
            drawn++;
            if (!nativeHost && !readyReported)
            {
                File.WriteAllText(Path.Combine(Session, "client" + Role + ".ready"), "First frame drawn.");
                readyReported = true;
            }
            if (__instance.Window.Handle != lastWindow)
            {
                lastWindow = __instance.Window.Handle;
                File.WriteAllText(Path.Combine(Session, "client" + Role + ".window.txt"), lastWindow.ToInt64().ToString());
            }
            if (drawn == 120)
            {
                File.WriteAllLines(Path.Combine(Session, "client" + Role + ".assemblies.txt"), AppDomain.CurrentDomain.GetAssemblies().Select(a => a.FullName + " | " + (a.IsDynamic ? "dynamic" : a.Location)).ToArray());
                var target = (Microsoft.Xna.Framework.Graphics.RenderTarget2D)AccessTools.Field(typeof(Game1), "m_render_target").GetValue(__instance);
                using (var output = File.Create(Path.Combine(Session, "client" + Role + ".render.png"))) target.SaveAsPng(output, target.Width, target.Height);
            }
        }
        private static bool InitializeMultiplayer(MultiplayerManager __instance)
        {
            foreach (string name in new[] { "leaderBoard", "proximityPlayers", "inviteInfo", "emptyLobbyInviteInfo" })
            {
                PropertyInfo property = AccessTools.Property(typeof(MultiplayerManager), name);
                property.SetValue(__instance, Activator.CreateInstance(property.PropertyType, true), null);
            }
            __instance.LobbyPlayers.Clear();
            __instance.LobbyPlayers.Add(new CSteamID((ulong)(3 - Role)));
            __instance.LobbyId = new CSteamID(1);
            __instance.LobbyOwner = __instance.UserSteamId;
            return false;
        }
        private static bool PeerName(CSteamID steamIDFriend, ref string __result)
        {
            if (steamIDFriend.m_SteamID > 2) return true;
            __result = "Test Client " + steamIDFriend.m_SteamID;
            return false;
        }
        private static bool Send(byte[] pubData, uint cubData, ref bool __result)
        {
            // don't fill the peer's receive queue while its mods and assets are still loading
            if (nativeHost && !NativeSession.PeerReady) { __result = false; return false; }
            if (cubData > pubData.Length || cubData > 65536) throw new IOException("Invalid Multiplayer send size.");
            byte[] bytes = new byte[cubData];
            Array.Copy(pubData, bytes, bytes.Length);
            __result = Link.Send(bytes);
            return false;
        }
        private static bool Available(ref uint pcubMsgSize, ref bool __result)
        {
            pcubMsgSize = Link.Available;
            __result = pcubMsgSize != 0;
            return false;
        }
        private static bool Read(byte[] pubDest, uint cubDest, ref uint pcubMsgSize, ref CSteamID psteamIDRemote, ref bool __result)
        {
            pcubMsgSize = 0;
            psteamIDRemote = new CSteamID((ulong)(3 - Role));
            __result = false;
            if (Link.Available == 0 || Link.Available > cubDest || Link.Available > pubDest.Length) return false;
            byte[] packet = Link.Read();
            Array.Copy(packet, pubDest, packet.Length);
            pcubMsgSize = (uint)packet.Length;
            __result = true;
            return false;
        }
        private static void Applied() { applied++; }
        private static IEnumerable<CodeInstruction> ReceiveCalls(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                var method = instruction.operand as MethodInfo;
                if (method != null && method.DeclaringType == typeof(SteamNetworking))
                {
                    if (method.Name == "IsP2PPacketAvailable") instruction.operand = AccessTools.Method(typeof(Client), "PacketAvailable");
                    if (method.Name == "ReadP2PPacket") instruction.operand = AccessTools.Method(typeof(Client), "ReadPacket");
                }
                yield return instruction;
            }
        }
        private static bool PacketAvailable(out uint size, int channel)
        { size = Link.Available; return size != 0; }
        private static bool ReadPacket(byte[] destination, uint capacity, out uint size, out CSteamID peer, int channel)
        {
            size = 0; peer = default(CSteamID); bool result = false;
            Read(destination, capacity, ref size, ref peer, ref result); return result;
        }
        private static void Update(Game1 __instance)
        {
            updates++;
            Link.Pump();
            if (Link.Ready && (!nativeHost || NativeSession.PeerReady) && !wasReady && MultiplayerManager.instance != null)
            {
                // the original sender caches even failed sends; force the first connected snapshot
                AccessTools.Field(typeof(MultiplayerManager), "lastTrackData").SetValue(MultiplayerManager.instance, new TrackData { posX = -100000, posY = -100000 });
                wasReady = true;
            }
            if (!nativeHost && File.Exists(Path.Combine(Session, "stop"))) __instance.Exit();
            if ((DateTime.UtcNow - lastStatus).TotalSeconds < 1) return;
            double elapsed = (DateTime.UtcNow - lastStatus).TotalSeconds;
            lastStatus = DateTime.UtcNow;
            string player = JumpKing.GameManager.GameLoop.m_player == null ? "menu" : JumpKing.GameManager.GameLoop.m_player.m_body.Position.ToString();
            using (var process = Process.GetCurrentProcess())
            {
                TimeSpan cpu = process.TotalProcessorTime;
                string performance = "draw=" + ((drawn - previousDrawn) / elapsed).ToString("F1") + " update=" + ((updates - previousUpdates) / elapsed).ToString("F1")
                    + " cpuCores=" + ((cpu - previousCpu).TotalSeconds / elapsed).ToString("F2") + " privateMB=" + (process.PrivateMemorySize64 / 1048576)
                    + " maxFrameMs=" + TimeSpan.FromTicks(maxFrameGap).TotalMilliseconds.ToString("F1") + " gen2=" + GC.CollectionCount(2);
                JumpKingModTools.BoundedTextLog.Append(Path.Combine(Session, "client" + Role + ".performance.txt"), DateTime.UtcNow.ToString("O") + " " + performance);
                previousCpu = cpu; previousDrawn = drawn; previousUpdates = updates;
                maxFrameGap = 0;
            }
            File.WriteAllText(Path.Combine(Session, "client" + Role + ".status.txt"), Link.State + " | TX " + Link.Sent + " RX " + Link.Received + " applied " + applied + " | " + player + PeerStatus());
        }
        private static string PeerStatus()
        {
            var manager = MultiplayerManager.instance;
            if (manager == null) return "";
            var players = (System.Collections.IList)AccessTools.Field(typeof(MultiplayerManager), "Players").GetValue(manager);
            ulong? level = (ulong?)AccessTools.Method(spriteStateType, "GetLevelId").Invoke(null, null);
            string result = " | level=" + level + " peers=" + players.Count;
            foreach (object peer in players)
            {
                var type = peer.GetType();
                result += " [level=" + AccessTools.Property(type, "LevelId").GetValue(peer, null)
                    + " screen=" + AccessTools.Property(type, "ScreenIndex1").GetValue(peer, null)
                    + " position=" + AccessTools.Property(type, "RelativePosition").GetValue(peer, null)
                    + " alive=" + ((EntityComponent.Entity)peer).IsAlive + "]";
            }
            return result;
        }
        internal static bool HasFocus()
        {
            return host != IntPtr.Zero && Native.GetAncestor(Native.GetForegroundWindow(), 2) == host;
        }
        internal static bool Selected { get { return HasFocus() && input != null && input.ReadInt32(0) == Role; } }
        private static bool ReserveSwitchKey { get { return !nativeHost && Environment.GetEnvironmentVariable("MPEX_NATIVE") != "1"; } }
        private static bool DebugMouse()
        {
            var mouse = Microsoft.Xna.Framework.Input.Mouse.GetState();
            Rectangle bounds = Game1.instance.GetGameRect();
            if (Selected && bounds.Contains(mouse.Position)) return true;
            // don't replay wheel movement accumulated while the other client had input
            AccessTools.Field(typeof(Game1).Assembly.GetType("JumpKing.Player.DebugTeleport"), "last_scroll").SetValue(null, mouse.ScrollWheelValue / 120);
            return false;
        }
        private static bool Pad(PadInstance __instance, ref PadState __result)
        {
            if (Selected) return true;
            __result = default(PadState);
            return false;
        }
        private static bool KeyboardState(ref Microsoft.Xna.Framework.Input.KeyboardState __result)
        {
            var keys = new List<Keys>();
            if (Selected) for (int key = 8; key < 255; key++)
                if ((key != 117 || !ReserveSwitchKey) && (Native.GetAsyncKeyState(key) & 0x8000) != 0) keys.Add((Keys)key);
            __result = new Microsoft.Xna.Framework.Input.KeyboardState(keys.ToArray());
            return false;
        }
        private static bool KeyboardButtons(ref int[] __result)
        {
            var state = default(Microsoft.Xna.Framework.Input.KeyboardState);
            KeyboardState(ref state);
            __result = Array.ConvertAll(state.GetPressedKeys(), key => (int)key);
            return false;
        }
    }
}
