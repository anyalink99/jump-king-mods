using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WorldsmithExtension
{
    internal static class Tests
    {
        static int count;
        static void Assert(bool ok, string name)
        {
            if (!ok)
                throw new Exception(name);
            count++;
        }

        static void Fails(Action a, string name)
        {
            try
            {
                a();
            }
            catch (Exception error)
            {
                if (!(error is IOException || error is InvalidDataException || error is InvalidOperationException || error is ArgumentException || error is FormatException || error is NotSupportedException || error is System.Xml.XmlException)) throw;
                count++;
                return;
            }

            throw new Exception(name);
        }

        static int Main(string[] args)
        {
            if (args.Length != 0 && args[0] == "--worker-fixture")
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine(args[1]);
                Console.Error.WriteLine("stderr captured");
                Console.WriteLine(Environment.GetEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE"));
                return args.Length == 3 ? 7 : 0;
            }

            string dir = Path.Combine(Path.GetTempPath(), "WorldsmithExtensionTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                UpdateTests.Run(dir, Assert, Fails);
                NewsTests.Run(dir, Assert, Fails);
                WorkshopTests.Run(dir, Assert, Fails);
                WorkflowTests.Run(dir, Assert);
                TestBuildTests.Run(dir, Assert, Fails);
                EnvironmentTests.Run(dir, Assert, Fails);
                CollisionTests.Run(dir, Assert, Fails);
                FileTests.Run(dir, Assert, Fails);
                PublicationTests.Run(dir, Assert, Fails);
                PackageTests.Run(dir, Assert, Fails);
                Console.WriteLine("[OK] " + count + " checks; collision pixels, atomic saves, layout and offline publishing state machine.");
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return 1;
            }
        }

    }
}
