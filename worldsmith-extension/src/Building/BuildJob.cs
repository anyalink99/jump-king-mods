using System;
using System.IO;

namespace WorldsmithExtension
{
    internal static class BuildJob
    {
        internal static void Run(string root, string category, string destination, Action<string> status)
        {
            string mode = BuildPlan.Inspect(root, category).WorkerMode;
            string arguments = "--worldsmith " + WorkerProcess.Quote(Engine.Host) + " " + mode + " " + WorkerProcess.Quote(root) + " " + WorkerProcess.Quote(destination);
            WorkerProcess.Run(Path.Combine(Engine.Bundle, "WorldsmithExtension.Worker.exe"), arguments, Engine.Bundle, status, "Build failed. Nothing is ready to publish or test. See the build output.");
        }
    }
}
