using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace WorldsmithExtension
{
    internal static class WorkerProcess
    {
        internal static string Quote(string value)
        {
            if (value.Contains("\""))
                throw new ArgumentException("Invalid path.");
            return "\"" + value.TrimEnd('\\') + "\"";
        }

        internal static void Run(string executable, string arguments, string directory, Action<string> status, string failure)
        {
            var info = new ProcessStartInfo(executable, arguments)
            {UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = directory};
            info.EnvironmentVariables["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
            info.EnvironmentVariables["VSLANG"] = "1033";
            using (var process = new Process{StartInfo = info})
            {
                process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                        status(args.Data);
                };
                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                        status(args.Data);
                };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new IOException(failure);
            }
        }
    }
}
