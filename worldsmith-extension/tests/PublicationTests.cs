using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WorldsmithExtension
{
    internal static class PublicationTests
    {
        internal static void Run(string dir, Action<bool, string> Assert, Action<Action, string> Fails)
        {
            string root = Path.Combine(dir, "project"), output = Path.Combine(dir, "stage", "content");
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(output);
            Files.Text(Path.Combine(root, "source.txt"), "source");
            Files.Text(Path.Combine(output, "payload.txt"), "compiled");
            Action ready = () => Files.Text(Path.Combine(dir, "stage", "ready.xml"), new XElement("Build", new XAttribute("source", root), new XAttribute("sourceHash", Files.Fingerprint(root)), new XAttribute("contentHash", Files.ContentFingerprint(output))).ToString());
            ready();
            var request = new PublishRequest{Root = root, Content = output, Title = "Test map", Tags = new[]{"Level"}, Visibility = 2};
            var fake = new Fake();
            var publisher = new Publisher(fake);
            bool success = false;
            ulong saved = 0;
            publisher.Start(request, x => saved = x, (msg, ok) => success = ok);
            Assert(publisher.Busy, "creation pending");
            Fails(() => publisher.Start(request, x =>
            {
            }, (m, o) =>
            {
            }), "duplicate job blocked");
            fake.Created(123, null, false);
            Assert(saved == 123 && fake.Submitted != null, "ID persisted before upload");
            fake.Submitted("Fail", false);
            Assert(!success && !publisher.Busy, "failed result cannot become success");
            publisher.Start(request, x => saved = x, (msg, ok) => success = ok);
            fake.Submitted(null, false);
            Assert(success && !publisher.Busy, "update success");
            request.Id = 0;
            publisher.Start(request, x =>
            {
                throw new IOException("disk full");
            }, (msg, ok) => success = ok);
            fake.Submitted = null;
            fake.Created(124, null, false);
            Assert(!publisher.Busy && !success && fake.Submitted == null, "persist failure prevents upload");
            request.Id = 0;
            publisher.Start(request, x => saved = x, (msg, ok) => success = ok);
            Files.Text(Path.Combine(root, "source.txt"), "edited while creating item");
            fake.Created(125, null, false);
            Assert(saved == 125 && !publisher.Busy && !success && fake.Submitted == null, "edits during item creation block submission but retain ID");
            ready();
            request.Id = 123;
            Files.Text(Path.Combine(root, "worldsmith-extension.xml"), "<WorldsmithExtension collisionSource=\"atlas\"/>");
            Fails(request.Validate, "changed source configuration blocks upload");
            ready();
            Files.Text(Path.Combine(output, "payload.txt"), "tampered");
            Fails(request.Validate, "changed staging blocks upload");
            ready();
            string addedDirectory = Path.Combine(root, "resources", "empty");
            Directory.CreateDirectory(addedDirectory);
            Fails(request.Validate, "added empty source directory blocks upload");
            ready();
            Directory.Delete(addedDirectory);
            Fails(request.Validate, "removed empty source directory blocks upload");
            string sourceRevision = Files.Fingerprint(root);
            Directory.CreateDirectory(Path.Combine(root, "bin", "ignored-empty"));
            Assert(Files.Fingerprint(root) == sourceRevision, "ignored directory does not invalidate a build");
        }
        sealed class Fake : IWorkshop
        {
            internal Action<ulong, string, bool> Created;
            internal Action<string, bool> Submitted;
            public void Create(Action<ulong, string, bool> value)
            {
                Created = value;
            }

            public void Submit(PublishRequest r, Action<string, bool> value)
            {
                Submitted = value;
            }

            public string Progress()
            {
                return "pending";
            }
        }
    }
}
