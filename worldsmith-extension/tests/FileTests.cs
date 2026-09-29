using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WorldsmithExtension
{
    internal static class FileTests
    {
        internal static void Run(string dir, Action<bool, string> Assert, Action<Action, string> Fails)
        {
            var failures = new AggregateException(
                new System.Reflection.TargetInvocationException(new InvalidDataException("Cannot read props/prop1.xml", new System.Xml.XmlException("Unexpected closing tag"))),
                new System.Reflection.TargetInvocationException(new IOException("Cannot read king/skin_settings.xml")));
            string explanation = ErrorDetails.Message(failures);
            Assert(explanation.Contains("props/prop1.xml") && explanation.Contains("Unexpected closing tag") && explanation.Contains("king/skin_settings.xml") &&
                !explanation.Contains("target of an invocation"), "parallel load failures retain all underlying errors and file context");
            string inventory = Path.Combine(dir, "inventory");
            Files.Text(Path.Combine(inventory, "z.xml"), "z");
            Files.Text(Path.Combine(inventory, "a.xml"), "a");
            Files.Text(Path.Combine(inventory, "assets", "b.xml"), "b");
            Files.Text(Path.Combine(inventory, "bin", "ignored.xml"), "ignored");
            Files.Text(Path.Combine(inventory, "assets", "ignored.worldsmith-backup"), "backup");
            Directory.CreateDirectory(Path.Combine(inventory, "assets", "empty"));
            Assert(Files.Sources(inventory).Select(p => Files.Relative(inventory, p)).SequenceEqual(new[] { "a.xml", "z.xml", "assets\\b.xml" }), "source scan excludes generated files and keeps stable ordering");
            Assert(Files.SourceDirectories(inventory).Count() == 2, "source scan retains empty resource directories");
            string atomic = Path.Combine(dir, "save.xml");
            Files.Text(atomic, "original");
            Fails(() => Files.Atomic(atomic, s =>
            {
                s.WriteByte(42);
                throw new IOException();
            }), "failed save");
            Assert(File.ReadAllText(atomic) == "original", "original survives failed save");
            Files.Text(atomic, "changed");
            Assert(File.ReadAllText(atomic + ".worldsmith-backup") == "original", "rollback backup");
            Fails(() => Files.Relative(dir, Path.GetFullPath(Path.Combine(dir, "../outside"))), "traversal");
            Fails(() => Files.PrepareOutput(dir, dir.ToUpperInvariant()), "same destination with different case");
            Fails(() => Files.PrepareOutput(dir, Path.Combine(dir, "nested")), "destination inside project");
            Fails(() => BuildReceipt.Validate(dir, null), "missing checked build");
            Fails(() => new Layout{Screens = 0}.Validate(), "zero screens");
            Fails(() => new Layout{Screens = 170, Side = 13}.Validate(), "undersized atlas");
            var bad = new Layout{Screens = 3};
            bad.Links.Add(Tuple.Create(1, "left", 2));
            bad.Links.Add(Tuple.Create(1, "left", 3));
            Fails(bad.Validate, "duplicate side link");
        }
    }
}
