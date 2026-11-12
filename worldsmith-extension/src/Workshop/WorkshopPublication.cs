using System.IO;
using System.Xml.Linq;

namespace WorldsmithExtension
{
    internal static class WorkshopPublication
    {
        internal static void Save(string root, PublishRequest request, ulong owner)
        {
            // save the local identity before metadata so a partial failure can be retried
            WorkshopCatalog.Library.Link(root, request.Id);
            Files.Text(Path.Combine(root, ".worldsmith-extension", "workshop.xml"),
                new XElement("Workshop", new XAttribute("id", request.Id)).ToString());
            WorkshopCatalog.Remember(PublishSession.Metadata(request, owner));
            if (WorkshopLibrary.SameFolder(Engine.ProjectRoot, root))
                Engine.Set(Engine.Project, "SteamPublishedId", request.Id);
        }
    }
}
