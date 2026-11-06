using System;
using System.IO;
using System.Reflection;

internal static class BuildFailures
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    internal static void Run(Assembly engine, Assembly native, string fixtures, string host)
    {
        string source = Path.Combine(fixtures, "stock-template"), output = Path.Combine(fixtures, "stock-template-build", "content");
        Copy(Path.Combine(host, "Resources", "Templates", "Level"), source);
        // these have no resources; an archive doesn't need entries for empty folders
        foreach (string folder in new[] { "audio", "screens" })
        {
            string path = Path.Combine(source, folder);
            if (Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length == 0) Directory.Delete(path);
        }
        if (Directory.Exists(Path.Combine(source, "screens"))) throw new Exception("Stock fixture should have no screen resources");
        var run = engine.GetType("WorldsmithExtension.BuildJob").GetMethod("Run", All);
        var status = new Action<string>(text => {});
        run.Invoke(null, new object[] { source, "Level", output, status });
        foreach (string folder in new[] { "audio", "ending", "king", "particles", "props", "screens", "screens/scrolling/textures", "props/textures/old_man/lines", "props/textures/raven", "props/hidden_walls/textures", "props/hidden wall props", "props/messages" })
            if (!Directory.Exists(Path.Combine(output, folder))) throw new Exception("Build omitted a runtime resource folder: " + folder);
        if (Directory.Exists(Path.Combine(source, "screens"))) throw new Exception("Build added directories to the source project");
        if (!File.Exists(Path.Combine(output, "level.xnb"))) throw new Exception("Stock template didn't compile");

        string broken = Path.Combine(fixtures, "bad-texture"), failedOutput = Path.Combine(fixtures, "bad-texture-build", "content");
        Directory.CreateDirectory(broken);
        File.WriteAllText(Path.Combine(broken, "unreadable.png"), "not an image");
        try
        {
            run.Invoke(null, new object[] { broken, "Skin", failedOutput, status });
            throw new Exception("Invalid texture unexpectedly built");
        }
        catch (TargetInvocationException error)
        {
            string message = (string)engine.GetType("WorldsmithExtension.ErrorDetails").GetMethod("Message", All).Invoke(null, new object[] { error });
            if (!message.Contains("unreadable.png") || !message.Contains("exit code")) throw new Exception("Build dialog lost the failed asset or worker exit code", error);
            string report = error.InnerException.ToString();
            if (!report.Contains("--build") || !report.Contains("Working directory:") || !report.Contains("Standard error")) throw new Exception("Copied build report lost the worker diagnostics", error);
        }
        Console.WriteLine("[OK] Stock template builds without empty source folders; worker failures retain the asset, exit code and copied diagnostics.");
    }
    static void Copy(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (string child in Directory.GetDirectories(source)) Copy(child, Path.Combine(destination, Path.GetFileName(child)));
    }
}
