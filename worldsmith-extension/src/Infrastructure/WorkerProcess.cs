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
            var output = new OutputTail();
            var errors = new OutputTail();
            var info = new ProcessStartInfo(executable, arguments)
            {UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = directory};
            info.EnvironmentVariables["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
            info.EnvironmentVariables["VSLANG"] = "1033";
            using (var process = new Process{StartInfo = info})
            {
                process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        output.Add(args.Data);
                        status(args.Data);
                    }
                };
                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        errors.Add(args.Data);
                        status(args.Data);
                    }
                };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new WorkerProcessFailure(failure, process.ExitCode, executable, arguments, directory, output.Text, errors.Text);
            }
        }

        // keep the useful end of noisy compiler output without retaining an entire build
        sealed class OutputTail
        {
            readonly StringBuilder text = new StringBuilder();
            string first;
            bool truncated;
            internal void Add(string line)
            {
                lock (text)
                {
                    if (first == null && !String.IsNullOrWhiteSpace(line)) first = line.Length > 1200 ? line.Substring(0, 1200) : line;
                    text.AppendLine(line);
                    if (text.Length > 8192) { text.Remove(0, text.Length - 8192); truncated = true; }
                }
            }
            internal string Text { get { lock (text) return (truncated ? first + "\n[earlier output omitted]\n" : "") + text.ToString(); } }
        }
    }

    internal sealed class WorkerProcessFailure : IOException
    {
        readonly string details;
        internal WorkerProcessFailure(string failure, int exitCode, string executable, string arguments, string directory, string output, string errors)
            : base(Summary(failure, exitCode, output, errors))
        {
            details = "Executable: " + executable + "\nArguments: " + arguments + "\nWorking directory: " + directory +
                "\nStandard error (tail):\n" + errors + "\nStandard output (tail):\n" + output;
        }
        static string Summary(string failure, int code, string output, string errors)
        {
            string detail = (String.IsNullOrWhiteSpace(errors) ? output : errors).Trim();
            int newline = detail.IndexOf('\n');
            if (newline >= 0) detail = detail.Substring(0, newline).TrimEnd();
            // reflection wrappers can share a line with the actual file or compiler error
            int inner = detail.LastIndexOf("--->", StringComparison.Ordinal);
            if (inner >= 0) detail = detail.Substring(inner + 4).Trim();
            if (detail.Length > 1200) detail = detail.Substring(0, 1200) + "...";
            return failure + " Worker exit code: " + code + (detail.Length == 0 ? " No diagnostic output was received." : Environment.NewLine + detail);
        }
        public override string ToString() { return base.ToString() + Environment.NewLine + details; }
    }
}
