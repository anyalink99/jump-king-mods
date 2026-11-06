using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using MegaMappingExpansion;

namespace WorldsmithExtension
{
    internal static class TransitionAuthoring
    {
        internal static TransitionPlan Validate(string root, string text, int screens)
        {
            var candidate = TransitionPlan.FromText(text, screens);
            var layout = MapLayout.ReadForScreens(root, screens);
            var previous = MappingFiles.Links(root);
            var replaced = new HashSet<int>();
            if (previous != null) foreach (XmlElement link in previous.ChildNodes.OfType<XmlElement>().Where(e => e.Name == "SideLink"))
                replaced.Add((int.Parse(link.GetAttribute("screen"), CultureInfo.InvariantCulture) - 1) * 2 + (link.GetAttribute("side") == "left" ? 0 : 1));
            var document = new XmlDocument { XmlResolver = null }; document.LoadXml(text);
            var declared = document.DocumentElement.ChildNodes.OfType<XmlElement>().Where(e => e.Name == "SideLink").Select(e => (int.Parse(e.GetAttribute("screen"), CultureInfo.InvariantCulture) - 1) * 2 + (e.GetAttribute("side") == "left" ? 0 : 1));
            var dynamic = candidate.Exits.Select(e => e.Key).Concat(candidate.Pools.Where(p => p.Schemes != null).SelectMany(p => p.Schemes[0].Links.Select(e => e.Key))).Concat(declared).ToArray();
            if (layout != null) foreach (var pair in layout.Links) for (int side = 0; side < 2; side++)
                if (pair.Value[side] > 0 && !replaced.Contains(pair.Key * 2 + side) && dynamic.Contains(pair.Key * 2 + side))
                    throw new InvalidDataException("This exit already belongs to MapLayout. Move its SideLink into transitions.xml first.");
            return candidate;
        }
        internal static void Save(string root, string text, int screens)
        {
            if (Operations.Current.Busy) throw new InvalidOperationException("Wait for the current build or publication before saving transitions.");
            Validate(root, text, screens);
            Files.Text(Path.Combine(root, MappingFiles.Transitions), text);
        }
        internal static string Preview(TransitionPlan plan, ulong seed, string id, int count)
        {
            var pool = plan.Pools.SingleOrDefault(p => p.Id == id);
            if (pool == null || count < 1 || count > 100) throw new InvalidDataException("Choose a pool and 1..100 selections.");
            var state = new TransitionChoice { Random = TransitionSelection.Seed(plan.FixedSeed ?? seed, pool.Id) };
            var output = new StringBuilder();
            output.AppendLine("Seed: " + (plan.FixedSeed ?? seed).ToString(CultureInfo.InvariantCulture));
            output.AppendLine("Pool: " + pool.Id + " (" + pool.Mode + ", refresh=" + pool.Refresh + ")");
            for (int i = 0; i < count; i++)
            {
                int selected = TransitionSelection.Pick(pool, state);
                output.Append(i + 1).Append(": ");
                if (pool.Schemes == null) output.Append("screen ").Append(pool.Screens[selected]);
                else { var scheme = pool.Schemes[selected]; output.Append(scheme.Id).Append(" — ").Append(string.Join("; ", scheme.Links.Select(l => (l.Screen + 1) + " " + TransitionPlan.Side(l.Side) + " → " + l.Target))); }
                output.AppendLine();
            }
            return output.ToString();
        }
    }
}
