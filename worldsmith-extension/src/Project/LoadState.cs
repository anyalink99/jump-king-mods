using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace WorldsmithExtension
{
    internal static class LoadState
    {
        static Stopwatch timer;
        internal static volatile bool Busy;
        internal static volatile bool Failed;
        static readonly Dictionary<string, string> Seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        internal static void Register()
        {
            var evt = Engine.Type("JKWorldsmith.Models.Projects.CurrentProjectManager").GetEvent("OnProjectLoaded");
            var parameters = evt.EventHandlerType.GetMethod("Invoke").GetParameters().Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();
            var method = typeof(LoadState).GetMethod("Loaded", BindingFlags.Static | BindingFlags.NonPublic);
            var body = Expression.Call(method, parameters.Select(p => (Expression)Expression.Convert(p, typeof(object))).ToArray());
            evt.AddEventHandler(null, Expression.Lambda(evt.EventHandlerType, body, parameters).Compile());
        }

        internal static void Starting(object project)
        {
            Busy = true;
            Failed = false;
            timer = Stopwatch.StartNew();
            lock (Seen)
                Seen.Clear();
            Panel.Status("Loading project: " + Engine.Get(project, "Name"));
        }

        static void Loaded(object project, object args)
        {
            var task = (Task)Engine.Get(args, "TaskResult");
            Failed = task.IsFaulted || task.IsCanceled;
            string message = Failed ? "Project load failed; inspect the error before editing." : "Project loaded in " + timer.Elapsed.TotalSeconds.ToString("F2") + " s; background compilation may still be running.";
            Engine.UI(() =>
            {
                Busy = false;
                Panel.Status(message);
                if (task.IsFaulted)
                    Panel.Error(task.Exception);
                else if (!task.IsCanceled)
                    EditorUI.RefreshFormat();
            });
        }

        internal static Exception StartFinished(Exception __exception)
        {
            if (__exception != null)
            {
                Busy = false;
                Failed = true;
                Engine.Log("Project load could not start: " + __exception);
            }
            return __exception;
        }

        internal static bool Watch(object __instance, object sender, object e)
        {
            var args = (FileSystemEventArgs)e;
            string root = (string)Engine.Get(__instance, "Path"), path = args.FullPath;
            string relative = Files.Relative(root, path);
            string[] parts = relative.Split(Path.DirectorySeparatorChar);
            bool ignored = Busy || Directory.Exists(path) || parts.Any(Files.IgnoredDirectory) || Files.IgnoredFile(Path.GetFileName(path));
            if (ignored)
            {
                Engine.Set(e, "Handled", true);
                return false;
            }

            var info = new FileInfo(path);
            info.Refresh();
            string stamp = args.ChangeType + ":" + (info.Exists ? info.LastWriteTimeUtc.Ticks : 0) + ":" + (info.Exists ? info.Length : 0);
            lock (Seen)
            {
                string old;
                if (Seen.TryGetValue(path, out old) && old == stamp)
                {
                    Engine.Set(e, "Handled", true);
                    return false;
                }

                Seen[path] = stamp;
            }

            if (args.ChangeType != WatcherChangeTypes.Deleted)
            {
                bool ready = (bool)Engine.Type("JKWorldsmith.Models.FileWatcher.ProperFileWatcher").GetMethod("WaitForFile").Invoke(null, new object[]{new FileInfo(path), 10});
                if (!ready)
                {
                    Engine.Set(e, "Handled", true);
                    lock (Seen)
                        Seen.Remove(path);
                    Panel.Status("File is still locked: " + relative + ". Build again after the writer finishes.");
                    return false;
                }
            }

            object change = Activator.CreateInstance(Engine.Type("JKWorldsmith.Models.FileWatcher.FileChangedEventArgs"), new[]{e});
            Engine.Call(Engine.Type("JKWorldsmith.Models.Projects.CurrentProjectManager"), "OnFileChanged", sender, change);
            return false;
        }
    }
}
