using System;
using System.Reflection;

namespace JKRuntime
{
    // Compile the shared implementation once, then execute each suite in its own
    // process. Static engine state and native patches never cross suite boundaries.
    internal static class TestHost
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0) throw new ArgumentException("A suite type is required");
                Type suite = typeof(TestHost).Assembly.GetType(args[0], true);
                MethodInfo entry = suite.GetMethod("Main", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (entry == null || suite == typeof(TestHost)) throw new ArgumentException("Unknown test suite");
                string[] forwarded = new string[args.Length - 1];
                Array.Copy(args, 1, forwarded, 0, forwarded.Length);
                object result = entry.Invoke(null, entry.GetParameters().Length == 0 ? null : new object[] { forwarded });
                return result is int ? (int)result : 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error is TargetInvocationException && error.InnerException != null ? error.InnerException : error);
                return 1;
            }
        }
    }
}
