using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Windows.Forms;
using BehaviorTree;
using HarmonyLib;
using JumpKing;
using JumpKing.Mods;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using JumpKingMultiplayer.Models;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;

namespace MultiplayerExpansion
{
    internal static class NativeSession
    {
        internal static string Status = "Two-client mode is off";
        private static Process staging, child;
        private static string request, session, transport, token;
        private static Form window;
        private static Panel peerPanel;
        private static Button first, second, stopButton;
        private static Label footer;
        private static IntPtr peerWindow;
        private static MemoryMappedFile inputMap;
        private static MemoryMappedViewAccessor input;
        private static int selected = 1;
        private static Harmony layoutHooks;
        private static DateTime lastPoll;
        private static bool attached, closingHooked;
        private static readonly SessionLifecycle lifecycle = new SessionLifecycle();
        private static readonly Stopwatch operation = new Stopwatch();
        private static bool stopping { get { return lifecycle.Phase == SessionPhase.Stopping; } }
        private static volatile string preparation = "Preparing files";
        private static Size oldSize;
        private static Size oldMinimumSize;
        private static FormBorderStyle oldBorder;
        private static FormWindowState oldState;
        private static Point oldLocation;
        private static bool oldFullscreen, oldResize;
        private static TimeSpan oldInactiveSleep;
        private static string oldTitle;
        private static string preparationTitle;
        private static readonly object logLock = new object();
        private static bool launchModeChecked;
        internal static bool PeerReady { get { return lifecycle.Phase == SessionPhase.Running; } }
        private static string Package { get { return Path.GetDirectoryName(typeof(NativeMod).Assembly.Location); } }

        internal static void Start(string mode)
        {
            if (!NativeMod.DebugEnabled) { Status = "Two clients require Debug mode"; return; }
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MPEX_ROLE"))) { Status = "Use the first client's menu"; return; }
            StartDecision decision = lifecycle.Start(mode);
            if (decision == StartDecision.Ignore) return;
            if (decision == StartDecision.Stop) { BeginStop(); return; }
            try
            {
                operation.Restart();
                session = null; preparation = "Preparing files";
                window = Control.FromHandle(Game1.instance.Window.Handle) as Form;
                if (window == null) throw new InvalidOperationException("Unsupported game window");
                if (!closingHooked) { window.FormClosing += Closing; closingHooked = true; }
                preparationTitle = window.Text;
                if (MultiplayerManager.instance == null) throw new InvalidOperationException("Multiplayer mod isn't initialized");
                if (MultiplayerManager.instance.LobbyId.HasValue) throw new InvalidOperationException("Leave the current Steam lobby first");
                string rootFile = Path.Combine(Package, "session-root.txt");
                string root = File.Exists(rootFile) ? File.ReadAllText(rootFile).Trim() : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JumpKing", "MultiplayerExpansion");
                request = Path.Combine(root, "native-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(request);
                transport = mode; token = Guid.NewGuid().ToString("N");
                string game = Path.GetDirectoryName(typeof(Game1).Assembly.Location);
                string map = Game1.instance.contentManager.root;
                if (map == "Content" || Path.GetFullPath(map).TrimEnd('\\') == Path.GetFullPath(Path.Combine(game, "Content")).TrimEnd('\\')) map = "";
                var info = new ProcessStartInfo("powershell.exe") {
                    UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(Path.Combine(Package, "stage-native.ps1"))
                        + " -GameDir " + Quote(game) + " -BinaryDir " + Quote(Package) + " -SessionRoot " + Quote(request)
                        + " -OutputFile " + Quote(Path.Combine(request, "session.txt")) + " -ParentId " + Process.GetCurrentProcess().Id
                        + (map.Length == 0 ? "" : " -MapDir " + Quote(Path.GetFullPath(map)))
                };
                var pending = new Process { StartInfo = info };
                pending.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) {
                    if (e.Data != null && e.Data.StartsWith("[stage] ")) preparation = e.Data.Substring(8);
                    Log(e.Data);
                };
                pending.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { Log(e.Data); };
                try { pending.Start(); } catch { pending.Dispose(); throw; }
                staging = pending; staging.BeginOutputReadLine(); staging.BeginErrorReadLine();
                staging.PriorityClass = ProcessPriorityClass.BelowNormal;
                Status = preparation;
            }
            catch (Exception error) { Fail(error); }
        }
        private static string Quote(string value) { return "\"" + value.TrimEnd('\\') + "\""; }
        private static void Log(string text)
        {
            if (text == null || request == null) return;
            lock (logLock) try { JumpKingModTools.BoundedTextLog.Append(Path.Combine(request, "native.log"), text); } catch (IOException) { }
        }
        internal static void Tick()
        {
            if ((DateTime.UtcNow - lastPoll).TotalMilliseconds < 200) return;
            lastPoll = DateTime.UtcNow;
            try
            {
                if (!launchModeChecked)
                {
                    launchModeChecked = true;
                    string mode = SessionLifecycle.StartupMode(Environment.GetEnvironmentVariable("MPEX_NATIVE_TRANSPORT"));
                    if (mode == "Local" || mode == "Steam") Start(mode);
                }
                if (staging != null && staging.HasExited)
                {
                    int code = staging.ExitCode; staging.WaitForExit(); staging.Dispose(); staging = null;
                    if (stopping) { FinishStop(); return; }
                    if (code != 0) throw new IOException("Couldn't prepare the second client; see native.log");
                    session = File.ReadAllText(Path.Combine(request, "session.txt")).Trim();
                    if (lifecycle.Prepared()) { operation.Restart(); Attach(); Status = "Loading client 2"; }
                }
                if (child != null && child.HasExited)
                {
                    child.Dispose(); child = null;
                    if (attached) Detach();
                    FinishStop(); return;
                }
                if (stopping)
                {
                    if (operation.Elapsed.TotalSeconds > 8)
                    {
                        if (staging != null && !staging.HasExited) staging.Kill();
                        if (child != null && !child.HasExited) child.Kill();
                    }
                    if (staging == null && child == null) FinishStop();
                    return;
                }
                if (lifecycle.Phase == SessionPhase.Preparing)
                {
                    Status = preparation;
                    window.Text = preparationTitle + " | Client 2: " + preparation;
                    if (operation.Elapsed.TotalMinutes > 5) throw new TimeoutException("Preparation timed out");
                }
                if (lifecycle.Phase == SessionPhase.Starting && operation.Elapsed.TotalSeconds > 120)
                    throw new TimeoutException("Client startup timed out");
                if (lifecycle.Phase == SessionPhase.Running && !Client.Link.Ready)
                    throw new IOException(Client.Link.State);
                if (!attached) return;
                foreach (string action in new[] { "switch", "stop" })
                {
                    string command = Path.Combine(session, "native-" + action + ".request");
                    if (!File.Exists(command)) continue;
                    File.Delete(command);
                    if (action == "switch") Switch(); else { Stop(); return; }
                }
                string handleFile = Path.Combine(session, "client2.window.txt");
                if (File.Exists(handleFile))
                {
                    long handle;
                    if (long.TryParse(File.ReadAllText(handleFile), out handle) && peerWindow != new IntPtr(handle))
                    {
                        peerWindow = new IntPtr(handle);
                        Native.SetWindowLong(peerWindow, -16, (Native.GetWindowLong(peerWindow, -16) & ~unchecked((int)0x80CF0000)) | 0x56000000);
                        Native.SetParent(peerWindow, peerPanel.Handle);
                        if (Native.GetAncestor(peerWindow, 1) != peerPanel.Handle) throw new IOException("Couldn't attach the second game window");
                        Place();
                    }
                }
                if (peerWindow != IntPtr.Zero && File.Exists(Path.Combine(session, "client2.ready")) && Client.Link.Ready)
                    lifecycle.Ready();
                second.Enabled = lifecycle.Phase == SessionPhase.Running;
                second.Text = second.Enabled ? "Control client 2" : "Loading client 2...";
                Status = second.Enabled ? transport + " | input " + selected : "Loading client 2";
                footer.Text = Status + " | " + Client.Link.State + " | 60 FPS each | TX " + Client.Link.Sent + " RX " + Client.Link.Received;
            }
            catch (IOException error) { Fail(error); }
            catch (Exception error) { Fail(error); }
        }
        private static void Attach()
        {
            window = Control.FromHandle(Game1.instance.Window.Handle) as Form;
            if (window == null) throw new InvalidOperationException("The game window isn't a supported WinForms window");
            oldSize = window.ClientSize; oldBorder = window.FormBorderStyle; oldState = window.WindowState; oldLocation = window.Location;
            oldMinimumSize = window.MinimumSize;
            oldFullscreen = Game1.graphics.IsFullScreen; oldResize = Game1.instance.Window.AllowUserResizing; oldTitle = preparationTitle;
            oldInactiveSleep = Game1.instance.InactiveSleepTime;
            attached = true;
            // the embedded child can deactivate the parent's form; its old Tick may inline IsActive
            Game1.instance.InactiveSleepTime = TimeSpan.Zero;
            inputMap = MemoryMappedFile.CreateNew("Local\\MPEX-" + token, 4); input = inputMap.CreateViewAccessor(0, 4); input.Write(0, 1); selected = 1;
            Client.AttachNative(session, window.Handle, "Local\\MPEX-" + token, token, transport);
            layoutHooks = new Harmony("anyalink.multiplayer-expansion.native-layout");
            layoutHooks.Patch(AccessTools.Method(typeof(Game1), "GetGameRect"), postfix: new HarmonyMethod(typeof(NativeSession), "GameRect"));
            layoutHooks.Patch(AccessTools.Method(typeof(Game1), "CalculateGameRect"), postfix: new HarmonyMethod(typeof(NativeSession), "CalculatedRect"));
            Game1.graphics.IsFullScreen = false;
            Game1.graphics.PreferredBackBufferWidth = 1200; Game1.graphics.PreferredBackBufferHeight = 520;
            Game1.graphics.ApplyChanges();
            window.WindowState = FormWindowState.Normal; window.FormBorderStyle = FormBorderStyle.Sizable;
            window.MinimumSize = new Size(760, 380);
            Game1.instance.Window.AllowUserResizing = true;
            first = Selector("Control client 1", 1); second = Selector("Control client 2", 2);
            stopButton = new SelectorButton { Text = "Stop client 2", FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(65, 39, 39), TabStop = false };
            stopButton.Click += delegate { Stop(); };
            peerPanel = new Panel { BackColor = Color.Black };
            footer = new Label { BackColor = Color.FromArgb(23, 25, 30), ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter };
            window.Controls.AddRange(new Control[] { first, second, stopButton, peerPanel, footer });
            window.Resize += Resized;
            second.Enabled = false;
            Select(1); Place();
            string directory = Path.Combine(session, "client2");
            var info = new ProcessStartInfo(Path.Combine(directory, "MultiplayerExpansion.exe")) {
                WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true
            };
            info.EnvironmentVariables["MPEX_ROLE"] = "2"; info.EnvironmentVariables["MPEX_HOST"] = window.Handle.ToInt64().ToString();
            info.EnvironmentVariables["MPEX_INPUT"] = "Local\\MPEX-" + token; info.EnvironmentVariables["MPEX_SESSION"] = session;
            info.EnvironmentVariables["MPEX_TOKEN"] = token; info.EnvironmentVariables["MPEX_TRANSPORT"] = transport;
            info.EnvironmentVariables["MPEX_NATIVE"] = "1";
            info.EnvironmentVariables["MPEX_PARENT_ID"] = Process.GetCurrentProcess().Id.ToString();
            info.EnvironmentVariables["SteamAppId"] = info.EnvironmentVariables["SteamGameId"] = "1061090";
            if (Directory.Exists(Path.Combine(directory, "map"))) info.Arguments = "-debug " + Quote(Path.Combine(directory, "map"));
            else if (File.Exists(Path.Combine(directory, "debug-root.txt"))) info.Arguments = "-debug " + Quote(Path.Combine(directory, "Content"));
            child = Process.Start(info); child.PriorityClass = ProcessPriorityClass.BelowNormal;
            File.WriteAllText(Path.Combine(session, "native-host.txt"), Process.GetCurrentProcess().Id.ToString());
            Log("Attached native host; transport=" + transport);
        }
        private static Button Selector(string label, int role)
        {
            var button = new SelectorButton { Text = label, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, TabStop = false };
            button.Click += delegate { Select(role); }; return button;
        }
        private sealed class SelectorButton : Button
        { protected override bool ProcessDialogKey(Keys keyData) { return true; } }
        private static void Select(int role)
        {
            if (!attached || input == null) return;
            if (role == 2 && lifecycle.Phase != SessionPhase.Running) return;
            selected = role; input.Write(0, role);
            first.BackColor = role == 1 ? Color.FromArgb(38, 91, 73) : Color.FromArgb(37, 40, 47);
            second.BackColor = role == 2 ? Color.FromArgb(38, 91, 73) : Color.FromArgb(37, 40, 47);
            window.Text = oldTitle + " | Multiplayer Expansion | " + transport + " | Input: " + role;
        }
        private static bool ChildRequest(string action)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MPEX_ROLE"))) return false;
            string parent = Environment.GetEnvironmentVariable("MPEX_SESSION");
            File.WriteAllText(Path.Combine(parent, "native-" + action + ".request"), action);
            return true;
        }
        internal static void Switch() { if (!ChildRequest("switch")) Select(3 - selected); }
        private static void Resized(object sender, EventArgs e) { Place(); }
        private static void Place()
        {
            if (peerPanel == null) return;
            int half = Math.Max(1, window.ClientSize.Width / 2), height = Math.Max(1, window.ClientSize.Height - 68);
            first.SetBounds(0, 0, half, 36); second.SetBounds(half, 0, half - 110, 36);
            stopButton.SetBounds(window.ClientSize.Width - 110, 0, 110, 36);
            peerPanel.SetBounds(half, 36, half, height); footer.SetBounds(0, window.ClientSize.Height - 32, window.ClientSize.Width, 32);
            if (peerWindow != IntPtr.Zero)
            {
                var bounds = Fit(half, height, 0, 0);
                // don't block the main game on the other process's window thread
                Native.SetWindowPos(peerWindow, IntPtr.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x4074);
            }
        }
        internal static XnaRectangle Fit(int width, int height, int left, int top)
        {
            int w = Math.Max(1, Math.Min(width, height * 4 / 3)), h = Math.Max(1, w * 3 / 4);
            return new XnaRectangle(left + (width - w) / 2, top + (height - h) / 2, w, h);
        }
        private static void GameRect(ref XnaRectangle __result)
        { if (attached && window != null) __result = Fit(window.ClientSize.Width / 2, Math.Max(1, window.ClientSize.Height - 68), 0, 36); }
        private static void CalculatedRect(Game1 __instance)
        {
            var bounds = default(XnaRectangle); GameRect(ref bounds);
            if (attached) AccessTools.Field(typeof(Game1), "_game_rect").SetValue(__instance, bounds);
        }
        internal static void Stop()
        {
            if (ChildRequest("stop")) return;
            lifecycle.Cancel(); BeginStop();
        }
        private static void BeginStop()
        {
            operation.Restart();
            if (request != null) File.WriteAllText(Path.Combine(request, "cancel"), "Cancel preparation.");
            if (session != null) File.WriteAllText(Path.Combine(session, "stop"), "Stop the second client.");
            if (attached) Detach();
            Status = staging != null ? "Cancelling preparation" : "Closing client 2";
            if (staging == null && child == null) FinishStop();
        }
        private static void FinishStop()
        {
            string next = lifecycle.Stopped();
            if (window != null && !window.IsDisposed && preparationTitle != null) window.Text = preparationTitle;
            Status = lifecycle.Error == null ? "Two-client mode is off" : "Error: see native.log";
            if (next != null) Start(next);
        }
        private static void Closing(object sender, FormClosingEventArgs e)
        {
            if (request != null) File.WriteAllText(Path.Combine(request, "cancel"), "Main game closed.");
            if (session != null) File.WriteAllText(Path.Combine(session, "stop"), "Main game closed.");
        }
        private static void Detach()
        {
            attached = false;
            if (layoutHooks != null) layoutHooks.UnpatchAll(layoutHooks.Id); layoutHooks = null;
            Client.DetachNative();
            Game1.instance.InactiveSleepTime = oldInactiveSleep;
            window.Resize -= Resized;
            if (peerWindow != IntPtr.Zero)
            {
                Native.SetWindowPos(peerWindow, IntPtr.Zero, 0, 0, 0, 0, 0x4097);
                Native.SetParent(peerWindow, IntPtr.Zero);
            }
            foreach (Control control in new Control[] { first, second, stopButton, peerPanel, footer }) if (control != null) control.Dispose();
            first = second = stopButton = null; peerPanel = null; footer = null; peerWindow = IntPtr.Zero;
            if (input != null) input.Dispose(); input = null;
            if (inputMap != null) inputMap.Dispose(); inputMap = null;
            Game1.instance.Window.AllowUserResizing = oldResize;
            window.MinimumSize = oldMinimumSize;
            Game1.graphics.PreferredBackBufferWidth = oldSize.Width; Game1.graphics.PreferredBackBufferHeight = oldSize.Height;
            Game1.graphics.IsFullScreen = oldFullscreen; Game1.graphics.ApplyChanges();
            window.FormBorderStyle = oldBorder; window.WindowState = oldState; window.Location = oldLocation; window.Text = oldTitle;
        }
        private static void Fail(Exception error)
        {
            Log(error.ToString());
            lifecycle.Fail(error.GetBaseException().Message);
            BeginStop();
        }
    }
}
