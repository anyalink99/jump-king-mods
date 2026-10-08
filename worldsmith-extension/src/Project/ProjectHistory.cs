using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Xml.Linq;
using System.Xml.Serialization;
using HarmonyLib;

namespace WorldsmithExtension
{
    internal static class ProjectHistory
    {
        [ThreadStatic] static bool reading;
        static readonly HashSet<string> Damaged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        const BindingFlags Methods = BindingFlags.Static | BindingFlags.NonPublic;

        internal static void Install(Harmony harmony)
        {
            harmony.Patch(Engine.Type("JKWorldsmith.Models.Projects.Project").GetConstructor(Type.EmptyTypes),
                postfix: new HarmonyMethod(typeof(ProjectHistory).GetMethod("Created", Methods)));
            foreach (string name in new[] { "RecentProjects", "FavoriteProjects" })
                harmony.Patch(AccessTools.Method(Engine.Type("JKWorldsmith.Models.Projects." + name), "InitProjects"),
                    new HarmonyMethod(typeof(ProjectHistory).GetMethod("Initialize", Methods)));
        }

        static void Created(object __instance)
        {
            // XML sets Name before Directory, the title isn't necessarily a valid Windows path
            // wait for the saved directory instead
            if (reading) Engine.Set(__instance, "AutoCreateFolder", false);
        }

        static bool Initialize(object __instance)
        {
            var type = __instance.GetType();
            bool recent = type.Name == "RecentProjects";
            string path = (string)Engine.Get(type, recent ? "RECENT_FILE_NAMES" : "FAVES_FILE_NAMES");
            var warnings = new List<string>();
            var projects = Load(path, recent ? "RecentData" : "FavoriteData", warnings.Add);
            if (recent) projects = projects.OrderByDescending(p => (DateTime)Engine.Get(p, "LastOpened")).ToList();
            var list = (IList)Activator.CreateInstance(typeof(BindingList<>).MakeGenericType(Engine.Type("JKWorldsmith.Models.Projects.Project")));
            foreach (object project in projects) list.Add(project);
            // set the backing list directly, the setter can trigger a save
            // don't write half-loaded history if init runs again
            Engine.Set(__instance, "items", list);
            var handler = Delegate.CreateDelegate(typeof(EventHandler), AccessTools.Method(type, "UpdateFile"));
            var updated = type.GetEvent("OnUpdate");
            updated.RemoveEventHandler(null, handler);
            updated.AddEventHandler(null, handler);
            if (warnings.Count != 0)
            {
                string message = String.Join(Environment.NewLine, warnings) + Environment.NewLine +
                    "Project folders have not been deleted. You can reopen them with Open project. The original history will be preserved before the next save.";
                Engine.Log(message);
                MessageBox.Show(message, "Project history recovery", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return false;
        }

        internal static List<object> Load(string path, string root, Action<string> report)
        {
            path = Path.GetFullPath(path);
            foreach (string candidate in new[] { path, path + ".worldsmith-backup", path + ".temp" })
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    var document = Files.Xml(candidate);
                    if (document.Root == null || document.Root.Name != root)
                        throw new InvalidDataException("Unexpected history XML root.");
                    var projects = new List<object>();
                    var serializer = new XmlSerializer(Engine.Type("JKWorldsmith.Models.Projects.Project"));
                    var entries = document.Root.Element("Projects");
                    int rejected = 0;
                    foreach (var entry in entries == null ? Enumerable.Empty<XElement>() : entries.Elements("Project"))
                    {
                        bool previous = reading;
                        try
                        {
                            reading = true;
                            object project;
                            using (var reader = entry.CreateReader()) project = serializer.Deserialize(reader);
                            if (project == null) throw new InvalidDataException("Empty project entry.");
                            string directory = (string)Engine.Get(project, "Directory");
                            if (String.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))
                                throw new InvalidDataException("Missing absolute project directory.");
                            Path.GetFullPath(directory);
                            projects.Add(project);
                        }
                        catch (Exception error)
                        {
                            if (!(error is InvalidOperationException || error is ArgumentException || error is InvalidDataException || error is IOException)) throw;
                            rejected++;
                            report("Cannot load a project from " + candidate + ": " + error.GetBaseException().Message);
                        }
                        finally { reading = previous; }
                    }
                    if (rejected != 0) lock (Damaged) Damaged.Add(path);
                    if (rejected != 0 && projects.Count == 0) continue;
                    if (candidate != path)
                    {
                        lock (Damaged) Damaged.Add(path);
                        report("Recovered project history from " + candidate + ".");
                    }
                    return projects;
                }
                catch (Exception error)
                {
                    if (!(error is System.Xml.XmlException || error is InvalidDataException || error is IOException || error is UnauthorizedAccessException)) throw;
                    lock (Damaged) Damaged.Add(path);
                    report("Cannot read " + candidate + ": " + error.GetBaseException().Message);
                }
            }
            return new List<object>();
        }

        internal static void BeforeSave(string path)
        {
            path = Path.GetFullPath(path);
            lock (Damaged)
            {
                if (!Damaged.Contains(path)) return;
                // the regular backup gets replaced on the next save
                // keep separate copies here, including any backups we couldn't read
                foreach (string source in new[] { path, path + ".worldsmith-backup", path + ".temp" })
                    if (File.Exists(source)) File.Copy(source, source + ".recovery-" + Guid.NewGuid().ToString("N"), false);
                Damaged.Remove(path);
            }
        }
    }
}
