using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using HarmonyLib;

namespace WorldsmithExtension
{
    public static class Engine
    {
        internal static string Host, Bundle, Data;
        internal static Assembly Native;
        internal static readonly object LogGate = new object ();
        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern bool SetDllDirectory(string path);
        public static int Run(string host, string bundle, string[] args)
        {
            InterfaceLanguage.Initialize();
            Host = host;
            Bundle = bundle;
            Data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorldsmithExtension");
            Directory.CreateDirectory(Data);
            Directory.SetCurrentDirectory(host);
            SetDllDirectory(host);
            System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
            Environment.SetEnvironmentVariable("SteamAppId", "2245910");
            Environment.SetEnvironmentVariable("SteamGameId", "2245910");
            Native = Assembly.LoadFrom(Path.Combine(host, "JKWorldsmith.exe"));
            // WPF normally looks in the process entry assembly for unqualified pack resources
            // ours is a portable bootstrap, set the resource assembly before creating any WPF
            // objects or loading a resource dictionary
            typeof(Application).GetField("_resourceAssembly", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, Native);
            int build = Array.IndexOf(args, "--build");
            if (build >= 0)
            {
                Builder.Build(args[build + 1], args[build + 2], s => Console.WriteLine(s));
                return 0;
            }

            int mod = Array.IndexOf(args, "--package-mod");
            if (mod >= 0)
            {
                ModPackage.Build(args[mod + 1], args[mod + 2], s => Console.WriteLine(s));
                return 0;
            }

            int package = Array.IndexOf(args, "--package-content");
            if (package >= 0)
            {
                ProjectFormat.Snapshot(args[package + 1], args[package + 2], Console.WriteLine);
                return 0;
            }

            int recover = Array.IndexOf(args, "--recover");
            if (recover >= 0)
            {
                Console.WriteLine(ProjectFormat.Recover(args[recover + 1], args[recover + 2], Console.WriteLine));
                return 0;
            }

            Patches.Install();
            if (args.Contains("--check"))
            {
                Log("Contract and patch check passed");
                return 0;
            }

            Log("Starting Worldsmith with portable extension");
            return AppDomain.CurrentDomain.ExecuteAssembly(Path.Combine(host, "JKWorldsmith.exe"), new string[0]);
        }

        internal static object Service(string type)
        {
            return Type("JKWorldsmith.App").GetMethod("GetService", BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(Type(type)).Invoke(null, null);
        }

        internal static Type Type(string name)
        {
            return Native.GetType(name, true);
        }

        internal static object Get(object obj, string name)
        {
            var member = NativeMembers.Value(obj as Type ?? obj.GetType(), name);
            object target = obj is Type ? null : obj;
            var property = member as PropertyInfo;
            return property != null ? property.GetValue(target, null) : ((FieldInfo)member).GetValue(target);
        }

        internal static void Set(object obj, string name, object value)
        {
            var member = NativeMembers.Value(obj as Type ?? obj.GetType(), name);
            object target = obj is Type ? null : obj;
            var property = member as PropertyInfo;
            if (property != null) property.SetValue(target, value, null);
            else ((FieldInfo)member).SetValue(target, value);
        }

        internal static object Call(object obj, string name, params object[] args)
        {
            var type = obj as Type ?? obj.GetType();
            return NativeMembers.Method(type, name, args.Select(value => value == null ? null : value.GetType()).ToArray()).Invoke(obj is Type ? null : obj, args);
        }

        internal static void ExecuteCommand(object command, object parameter)
        {
            // RelayCommand<T> has Execute(T) and Execute(object)
            // use the public contract, name-only reflection is ambiguous
            ((System.Windows.Input.ICommand)command).Execute(parameter);
        }

        internal static object Project
        {
            get
            {
                return Get(Type("JKWorldsmith.Models.Projects.CurrentProjectManager"), "Instance");
            }
        }

        internal static string ProjectRoot
        {
            get
            {
                var p = Project;
                return p == null ? null : (string)Get(p, "Directory");
            }
        }

        internal static void UI(Action a)
        {
            if (Application.Current == null)
                return;
            Application.Current.Dispatcher.BeginInvoke(a);
        }

        internal static void Log(string text)
        {
            lock (LogGate)
                File.AppendAllText(Path.Combine(Data, "extension.log"), DateTime.UtcNow.ToString("o") + " " + text + Environment.NewLine);
        }
    }
}
