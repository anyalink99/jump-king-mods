using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace JKRuntime
{
    internal static class PackageImagesTests
    {
        private static void Check(bool value, string message)
        { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
        private static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        private static Func<int> Call(Assembly assembly)
        { return (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), assembly.GetType("ImageTarget").GetMethod("Run")); }
        private static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                foreach (string mode in new[] { "cold", "cached", "corrupt", "unwritable", "conflict" })
                {
                    var info = new ProcessStartInfo(typeof(PackageImagesTests).Assembly.Location, "JKRuntime.PackageImagesTests " + mode);
                    info.UseShellExecute = false; info.CreateNoWindow = true;
                    info.RedirectStandardOutput = info.RedirectStandardError = true;
                    using (var process = Process.Start(info))
                    {
                        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                        if (!process.WaitForExit(10000)) { process.Kill(); throw new Exception("Image fixture timed out: " + mode); }
                        Console.Write(output.Result); Console.Error.Write(error.Result);
                        Check(process.ExitCode == 0, "Package image process: " + mode);
                    }
                }
                return;
            }
            string fixtures = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "package-image-fixtures");
            byte[] bytes = File.ReadAllBytes(Path.Combine(fixtures, "first", "PackageImageFixture.dll"));
            byte[] other = File.ReadAllBytes(Path.Combine(fixtures, "second", "PackageImageFixture.dll"));
            // leave room for the content hash under Framework's MAX_PATH limit
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            string cached = Path.Combine(root, Hash(bytes), "implementation.jkcode");
            string modeName = args[0];
            DateTime timestamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            if (modeName == "cached" || modeName == "corrupt")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(cached));
                File.WriteAllBytes(cached, modeName == "cached" ? bytes : other);
                File.SetLastWriteTimeUtc(cached, timestamp);
            }
            if (modeName == "unwritable") { Directory.CreateDirectory(Path.GetDirectoryName(root)); File.WriteAllText(root, "fixture"); }
            Assembly foreign = modeName == "conflict" ? Assembly.Load(other) : null;
            Assembly image = PackageImages.Load(bytes, root);
            Check((int)image.GetType("ImageCounters").GetField("Initializers").GetValue(null) == 0, "Loading does not execute the implementation initializer");
            Check(Call(image)() == 7, "Loaded code matches the verified package bytes");
            Check((int)image.GetType("ImageCounters").GetField("Initializers").GetValue(null) == 1, "Real use initializes the implementation once");
            Check(ReferenceEquals(image, PackageImages.Load(bytes, root)), "Repeated discovery retains the same implementation");
            if (modeName == "cold" || modeName == "cached")
            {
                Check(string.Equals(image.Location, cached, StringComparison.OrdinalIgnoreCase), "CLR can identify the file-backed implementation across launches: " + image.Location);
                Check(Hash(File.ReadAllBytes(cached)) == Hash(bytes), "Cached image is identical to the package payload");
                Check(Directory.GetFiles(root, "*.dll", SearchOption.AllDirectories).Length == 0, "Native recursive DLL discovery cannot find cache images");
                if (modeName == "cached") Check(File.GetLastWriteTimeUtc(cached) == timestamp, "A valid existing image is reused without writing");
            }
            else Check(image.Location.Length == 0, "Unavailable or conflicting cache retains byte-loading isolation");
            if (modeName == "corrupt") Check(Hash(File.ReadAllBytes(cached)) == Hash(other), "A corrupt cache is not loaded or overwritten");
            if (foreign != null) Check(!ReferenceEquals(foreign, image) && Call(foreign)() == 9, "Same-identity foreign assembly cannot replace package code");
        }
    }
}
