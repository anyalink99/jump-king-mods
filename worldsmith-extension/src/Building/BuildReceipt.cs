using System;
using System.IO;
using System.Xml.Linq;

namespace WorldsmithExtension
{
    // every producer uses the same receipt, publishing checks both sides of it
    internal static class BuildReceipt
    {
        static string PathFor(string content)
        {
            if (String.IsNullOrWhiteSpace(content) || !Directory.Exists(content))
                throw new InvalidDataException("Build a checked copy before publishing.");
            return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(content)), "ready.xml");
        }

        internal static void Save(string root, string content, string revision)
        {
            if (Files.Fingerprint(root) != revision)
                throw new IOException("Project changed during build. Build again before publishing.");
            Files.Text(PathFor(content), new XElement("Build", new XAttribute("source", Path.GetFullPath(root)), new XAttribute("sourceHash", revision), new XAttribute("contentHash", Files.ContentFingerprint(content))).ToString());
        }

        internal static void Validate(string root, string content)
        {
            var ready = Files.Xml(PathFor(content)).Root;
            if (ready == null || ready.Name != "Build")
                throw new InvalidDataException("The build receipt is invalid. Build a new checked copy.");
            if (!String.Equals((string)ready.Attribute("source"), Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) || (string)ready.Attribute("sourceHash") != Files.Fingerprint(root))
                throw new IOException("Project changed since the build. Build again before publishing.");
            if ((string)ready.Attribute("contentHash") != Files.ContentFingerprint(content))
                throw new IOException("Built content changed. Build again before publishing.");
        }
    }
}
