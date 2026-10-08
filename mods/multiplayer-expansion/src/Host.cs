using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MultiplayerExpansion
{
    internal static class Entry
    {
        [STAThread]
        private static int Main()
        {
            string role = Environment.GetEnvironmentVariable("MPEX_ROLE");
            string session = Environment.GetEnvironmentVariable("MPEX_SESSION");
            if (!string.IsNullOrEmpty(role)) Environment.SetEnvironmentVariable("HARMONY_LOG_FILE", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "harmony.log.txt"));
            if (!string.IsNullOrEmpty(role) && !string.IsNullOrEmpty(session))
            {
                string log = Path.Combine(session, "client" + role + ".load.txt");
                object logLock = new object();
                Action<Assembly> record = delegate(Assembly assembly) {
                    if (assembly.IsDynamic) return;
                    lock (logLock)
                    {
                        try { JumpKingModTools.BoundedTextLog.Append(log, assembly.FullName + " | " + assembly.Location); }
                        catch (IOException) { }
                    }
                };
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) record(assembly);
                AppDomain.CurrentDomain.AssemblyLoad += delegate(object sender, AssemblyLoadEventArgs args) { record(args.LoadedAssembly); };
            }
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs args) {
                string name = new AssemblyName(args.Name).Name;
                if (name != "JumpKingMultiplayer" && name != "Newtonsoft.Json") return null;
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorkshopMods", "3190590114", name + ".dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MPEX_ROLE"))) return Client.Run();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { Application.Run(new LabWindow()); return 0; }
            catch (Exception error) { MessageBox.Show(error.ToString(), "Multiplayer Expansion"); return 1; }
        }
    }

    internal static class Native
    {
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr window, uint flags);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out Rect bounds);
        [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")] internal static extern bool ScreenToClient(IntPtr window, ref Point point);
        [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetParent(IntPtr child, IntPtr parent);
        [DllImport("user32.dll", SetLastError = true)] internal static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll", SetLastError = true)] internal static extern int SetWindowLong(IntPtr window, int index, int value);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    }

    internal sealed class LabWindow : Form
    {
        private readonly string session;
        private readonly Process[] clients = new Process[2];
        private readonly IntPtr[] windows = new IntPtr[2];
        private readonly Panel[] panels = new Panel[2];
        private readonly Label[] status = new Label[2];
        private readonly Timer timer = new Timer { Interval = 250 };
        private readonly Timer inputTimer = new Timer { Interval = 16 };
        private bool closing;
        private DateTime closeStarted;
        private readonly DateTime started = DateTime.UtcNow;
        private readonly MemoryMappedFile inputMap;
        private readonly MemoryMappedViewAccessor input;
        private readonly string inputName;
        private readonly Button[] selectors = new Button[2];
        private int selected = 1;
        private bool switchHeld;

        internal LabWindow()
        {
            session = Environment.GetEnvironmentVariable("MPEX_SESSION");
            if (string.IsNullOrEmpty(session) || !Directory.Exists(session)) throw new DirectoryNotFoundException("Launch with start-lab.ps1.");
            inputName = "Local\\MPEX-" + Guid.NewGuid().ToString("N");
            inputMap = MemoryMappedFile.CreateNew(inputName, 4);
            input = inputMap.CreateViewAccessor(0, 4);
            input.Write(0, selected);
            Text = "Multiplayer Expansion | Steam local test lab";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1200, 540);
            MinimumSize = new Size(992, 466);
            BackColor = Color.FromArgb(23, 25, 30);
            ForeColor = Color.White;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 2 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            string[] labels = {
                "CONTROL CLIENT 1   |   Original game bindings\nClick here or press F6 to switch input",
                "CONTROL CLIENT 2   |   Original game bindings\nClick here or press F6 to switch input"
            };
            for (int i = 0; i < 2; i++)
            {
                int role = i + 1;
                selectors[i] = new Button { Text = labels[i], Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, TabStop = false };
                selectors[i].Click += delegate { SelectClient(role); };
                layout.Controls.Add(selectors[i], i, 0);
                panels[i] = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
                layout.Controls.Add(panels[i], i, 1);
                status[i] = new Label { Text = "Starting isolated client...", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
                layout.Controls.Add(status[i], i, 2);
            }
            Controls.Add(layout);
            SelectClient(1);
            Shown += delegate { StartClients(); timer.Start(); inputTimer.Start(); };
            Resize += delegate { PlaceWindows(); };
            timer.Tick += delegate { Tick(); };
            inputTimer.Tick += delegate {
                bool down = Native.GetAncestor(Native.GetForegroundWindow(), 2) == Handle && (Native.GetAsyncKeyState(117) & 0x8000) != 0;
                if (down && !switchHeld) SelectClient(3 - selected);
                switchHeld = down;
            };
            FormClosing += OnClosing;
            FormClosed += delegate { timer.Dispose(); inputTimer.Dispose(); input.Dispose(); inputMap.Dispose(); foreach (Process client in clients) if (client != null) client.Dispose(); };
        }

        private void SelectClient(int role)
        {
            selected = role;
            input.Write(0, selected);
            for (int i = 0; i < 2; i++) selectors[i].BackColor = i + 1 == role ? Color.FromArgb(38, 91, 73) : Color.FromArgb(37, 40, 47);
            ActiveControl = null;
            Text = "Multiplayer Expansion | Debug | Input: Client " + role + " | F6 switches client";
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (keyData == Keys.F6 && !switchHeld) { SelectClient(3 - selected); switchHeld = true; }
            if (keyData == (Keys.Alt | Keys.F4)) return base.ProcessCmdKey(ref message, keyData);
            // game keys must not activate the header buttons
            return true;
        }

        private void StartClients()
        {
            StartClient(0);
            File.WriteAllText(Path.Combine(session, "host.txt"), Process.GetCurrentProcess().Id.ToString());
        }

        private void StartClient(int i)
        {
            string directory = Path.Combine(session, "client" + (i + 1));
            var info = new ProcessStartInfo(Path.Combine(directory, "MultiplayerExpansion.exe")) {
                WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true
            };
            info.EnvironmentVariables["MPEX_ROLE"] = (i + 1).ToString();
            info.EnvironmentVariables["MPEX_HOST"] = Handle.ToInt64().ToString();
            info.EnvironmentVariables["MPEX_INPUT"] = inputName;
            if (Directory.Exists(Path.Combine(directory, "map"))) info.Arguments = "-debug \"" + Path.Combine(directory, "map") + "\"";
            else if (File.Exists(Path.Combine(directory, "debug-root.txt"))) info.Arguments = "-debug \"" + Path.Combine(directory, "Content") + "\"";
            clients[i] = Process.Start(info);
            clients[i].PriorityClass = ProcessPriorityClass.BelowNormal;
        }

        private void Tick()
        {
            // don't make both content loaders compete for memory and disk during startup
            if (!closing && clients[1] == null && clients[0] != null && !clients[0].HasExited
                && File.Exists(Path.Combine(session, "client1.window.txt"))) StartClient(1);
            bool allExited = true;
            for (int i = 0; i < 2; i++)
            {
                if (clients[i] == null) continue;
                if (clients[i].HasExited) { status[i].Text = "Client exited (" + clients[i].ExitCode + "). Logs: " + session; continue; }
                allExited = false;
                string prefix = Path.Combine(session, "client" + (i + 1));
                try
                {
                    if (File.Exists(prefix + ".window.txt"))
                    {
                        long handle;
                        if (long.TryParse(File.ReadAllText(prefix + ".window.txt"), out handle) && windows[i] != new IntPtr(handle))
                        {
                            windows[i] = new IntPtr(handle);
                            int style = Native.GetWindowLong(windows[i], -16);
                            Native.SetWindowLong(windows[i], -16, (style & ~unchecked((int)0x80CF0000)) | 0x56000000);
                            Native.SetParent(windows[i], panels[i].Handle);
                            if (Native.GetAncestor(windows[i], 1) != panels[i].Handle) throw new InvalidOperationException("Could not embed the game window.");
                            PlaceWindows();
                        }
                    }
                    if (File.Exists(prefix + ".status.txt")) status[i].Text = File.ReadAllText(prefix + ".status.txt");
                    if (File.Exists(prefix + ".error.txt")) status[i].Text = "Client failed. See " + prefix + ".error.txt";
                }
                catch (IOException) { }
            }
            if (closing && allExited) { timer.Stop(); Close(); }
            if (closing && (DateTime.UtcNow - closeStarted).TotalSeconds > 10)
            {
                foreach (Process client in clients) if (client != null && !client.HasExited) client.Kill();
            }
            if (Environment.GetEnvironmentVariable("MPEX_SMOKE") == "1" && (DateTime.UtcNow - started).TotalSeconds > 38 && !closing) Close();
        }

        private void PlaceWindows()
        {
            for (int i = 0; i < 2; i++)
            {
                if (windows[i] == IntPtr.Zero) continue;
                Size area = panels[i].ClientSize;
                int width = Math.Min(area.Width, area.Height * 4 / 3);
                int height = width * 3 / 4;
                Native.SetWindowPos(windows[i], IntPtr.Zero, (area.Width - width) / 2, (area.Height - height) / 2, width, height, 0x0074);
                Native.Rect bounds;
                Native.GetWindowRect(windows[i], out bounds);
                File.WriteAllText(Path.Combine(session, "client" + (i + 1) + ".embedded.txt"),
                    "visible=" + Native.IsWindowVisible(windows[i]) + " rect=" + bounds.Left + "," + bounds.Top + "," + bounds.Right + "," + bounds.Bottom + " style=" + Native.GetWindowLong(windows[i], -16).ToString("X"));
            }
        }

        private void OnClosing(object sender, FormClosingEventArgs args)
        {
            bool running = false;
            foreach (Process client in clients) if (client != null && !client.HasExited) running = true;
            if (!running) return;
            args.Cancel = true;
            if (closing) return;
            closing = true;
            closeStarted = DateTime.UtcNow;
            File.WriteAllText(Path.Combine(session, "stop"), "Close both isolated clients.");
            Text = "Multiplayer Expansion | Closing test clients...";
        }
    }
}
