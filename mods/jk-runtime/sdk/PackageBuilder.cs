using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.CSharp;
using JKRuntime.Modules;

// Build-time only. Native discovery sees only the generated shell, never any
// CLR type referring to JKRuntime. No per-mod loader or dependency DLL copy.
internal static class PackageBuilder
{
    private static string Quote(string s) { return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""; }
    public static int Main(string[] args)
    {
        try
        {
            string payload = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[1]);
            string game = Path.GetFullPath(args[2]);
            // Explicit package references are recorded by identity and hash. The
            // runtime may resolve only these sibling files; it never scans a mod
            // or Workshop directory for arbitrary assemblies.
            string[] references = args.Skip(3).Select(Path.GetFullPath).ToArray();
            foreach (string reference in references) Assembly.LoadFrom(reference);
            Assembly assembly = Assembly.LoadFrom(payload);
            Type entry = assembly.GetTypes().Single(t => t.IsDefined(typeof(RuntimeModuleAttribute), false));
            var module = (RuntimeModuleAttribute)Attribute.GetCustomAttribute(entry, typeof(RuntimeModuleAttribute));
            byte[] hash; using (var sha = SHA256.Create()) hash = sha.ComputeHash(File.ReadAllBytes(payload));
            var manifest = new XElement("module", new XAttribute("schema", 1),
                new XAttribute("id", module.Id), new XAttribute("name", module.Name),
                new XAttribute("apiMajor", JKRuntime.RuntimeApi.ApiMajor),
                new XAttribute("apiMinor", JKRuntime.RuntimeApi.ApiMinor),
                new XAttribute("entry", entry.FullName), new XAttribute("assembly", assembly.GetName().Name),
                new XAttribute("version", assembly.GetName().Version),
                new XAttribute("sha256", BitConverter.ToString(hash).Replace("-", "")));
            foreach (string required in module.Requires ?? new string[0]) manifest.Add(new XElement("requires", required));
            foreach (string provided in module.Provides ?? new string[0]) manifest.Add(new XElement("provides", provided));
            foreach (string before in module.Before ?? new string[0]) manifest.Add(new XElement("before", before));
            foreach (string after in module.After ?? new string[0]) manifest.Add(new XElement("after", after));
            foreach (string reference in references)
            {
                AssemblyName identity = AssemblyName.GetAssemblyName(reference);
                byte[] referenceHash;
                using (var sha = SHA256.Create()) referenceHash = sha.ComputeHash(File.ReadAllBytes(reference));
                manifest.Add(new XElement("assemblyReference",
                    new XAttribute("name", identity.Name),
                    new XAttribute("fullName", identity.FullName),
                    new XAttribute("file", Path.GetFileName(reference)),
                    new XAttribute("sha256", BitConverter.ToString(referenceHash).Replace("-", ""))));
            }
            string manifestPath = Path.Combine(Path.GetDirectoryName(payload), "module.xml");
            manifest.Save(manifestPath);
            var code = new StringBuilder();
            code.Append("using System; using System.Linq; using System.Reflection; using JumpKing.Mods; using JumpKing.PauseMenu; using BehaviorTree;\n");
            code.Append("[assembly: AssemblyVersion(" + Quote(assembly.GetName().Version.ToString()) + ")]\n");
            code.Append("[JumpKingMod(" + Quote(module.Name) + ")] public static class PackageEntry {\n");
            code.Append(@"private static object Call(string method, params object[] args) {
                var runtime = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == ""JKRuntime"");
                if(runtime == null) throw new InvalidOperationException(""This mod requires JK Runtime 1.x. Install/enable the runtime and restart Jump King."");
                try { return runtime.GetType(""JKRuntime.PackageHost"", true).GetMethod(method).Invoke(null,args); }
                catch(TargetInvocationException e) { throw new InvalidOperationException(""JK Runtime package: "" + e.InnerException.Message, e.InnerException); }
            }
            [BeforeLevelLoad] public static void RequireRuntime() { Call(""Discover""); }
            ");
            foreach (MethodInfo method in entry.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                string attribute = method.IsDefined(typeof(MainMenuItemSettingAttribute), false) ? "MainMenuItemSetting" :
                    method.IsDefined(typeof(PauseMenuItemSettingAttribute), false) ? "PauseMenuItemSetting" : null;
                if (attribute == null) continue;
                if (method.GetParameters().Length != 2 || !typeof(BehaviorTree.IBTSimpleMenuItem).IsAssignableFrom(method.ReturnType))
                    throw new InvalidOperationException("Invalid menu factory: " + method.Name);
                code.Append("[" + attribute + "] public static IBTSimpleMenuItem " + method.Name + "(object factory, GuiFormat format) { return (IBTSimpleMenuItem)Call(\"Menu\", " + Quote(module.Id) + ", " + Quote(method.Name) + ", factory, format); }\n");
            }
            code.Append("}");
            var parameters = new CompilerParameters(new[] { "System.dll", "System.Core.dll", Path.Combine(game, "JumpKing.exe"), Path.Combine(game, "MonoGame.Framework.dll") }, output);
            parameters.CompilerOptions = "/optimize+ /langversion:5 /resource:\"" + payload + "\",JKRuntime.Module /resource:\"" + manifestPath + "\",JKRuntime.Manifest";
            using (var compiler = new CSharpCodeProvider())
            {
                var result = compiler.CompileAssemblyFromSource(parameters, code.ToString());
                if (result.Errors.HasErrors) throw new InvalidOperationException(string.Join("\n", result.Errors.Cast<CompilerError>().Select(e => e.ToString())));
            }
            Console.WriteLine("[OK] Runtime package: " + module.Id + " -> " + output);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
