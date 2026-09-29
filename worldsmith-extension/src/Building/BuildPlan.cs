using System.IO;

namespace WorldsmithExtension
{
    internal enum BuildIntent { CompileSources, PreservePackage, BuildMod }

    internal sealed class BuildPlan
    {
        internal string Root { get; private set; }
        internal string Category { get; private set; }
        internal BuildIntent Intent { get; private set; }
        internal ProjectFormat Format { get; private set; }
        internal string WorkerMode { get { return Intent == BuildIntent.BuildMod ? "--package-mod" : Intent == BuildIntent.PreservePackage ? "--package-content" : "--build"; } }
        internal string Description { get { return Intent == BuildIntent.BuildMod ? "Build validates and prepares the mod package." : Intent == BuildIntent.PreservePackage ? "Build preserves the compiled package." : "Build compiles available sources and retains compiled-only resources."; } }

        internal static BuildPlan Inspect(string root, string category)
        {
            var format = ProjectFormat.Inspect(root, category);
            return new BuildPlan { Root = Path.GetFullPath(root), Category = category, Format = format,
                Intent = category == "Mod" ? BuildIntent.BuildMod : !format.Package || (format.HasSources && format.Editable) ? BuildIntent.CompileSources : BuildIntent.PreservePackage };
        }
    }
}
