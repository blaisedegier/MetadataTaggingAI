using ImageMagick;
using System.Xml.Linq;

namespace MetadataTaggingAI
{
    public static class ImageMetadataWriter
    {
        private const string XmpPacketId = "W5M0MpCehiHzreSzNTczkc9d";

        public static void WriteMetadata(string imagePath, List<string> tags)
        {
            try
            {
                // Read image with metadata preservation
                using var image = new MagickImage();
                var readSettings = new MagickReadSettings();
                readSettings.SetDefine("profile:preserve", "true");
                image.Read(imagePath, readSettings);

                // Update metadata
                HandleIptcMetadata(image, tags);
                HandleXmpMetadata(image, tags);

                // Write with metadata preservation
                image.Settings.SetDefine("profile:preserve", "true");
                image.Write(imagePath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Metadata write failed for {imagePath}", ex);
            }
        }

        private static void HandleIptcMetadata(MagickImage image, List<string> tags)
        {
            var iptcProfile = image.GetIptcProfile() ?? new IptcProfile();
            UpdateIptcTags(iptcProfile, tags);
            image.SetProfile(iptcProfile);
        }

        private static void HandleXmpMetadata(MagickImage image, List<string> tags)
        {
            var xmpProfile = image.GetXmpProfile();
            var updatedXmp = UpdateXmpTags(xmpProfile, tags);
            image.SetProfile(updatedXmp);
        }

        internal static void UpdateIptcTags(IIptcProfile iptc, List<string> tags)
        {
            iptc.Values
                .Where(v => v.Tag == IptcTag.Keyword)
                .ToList()
                .ForEach(k => iptc.RemoveValue(k.Tag, k.Value));

            foreach (var tag in tags.Distinct(StringComparer.OrdinalIgnoreCase))
                iptc.SetValue(IptcTag.Keyword, tag);
        }

        public static IXmpProfile UpdateXmpTags(IXmpProfile? existingProfile, List<string> tags)
        {
            var xdoc = existingProfile?.ToXDocument() ?? CreateNewXmpDocument();
            EnsureXmpStructure(xdoc);
            UpdateDcSubjectTags(xdoc, tags);
            return new XmpProfile(xdoc);
        }

        private static XDocument CreateNewXmpDocument()
        {
            var xmpNs = XNamespace.Get("adobe:ns:meta/");
            var rdfNs = XNamespace.Get("http://www.w3.org/1999/02/22-rdf-syntax-ns#");
            var dcNs = XNamespace.Get("http://purl.org/dc/elements/1.1/");

            return new XDocument(
                new XProcessingInstruction("xpacket", $"begin='\ufeff' id='{XmpPacketId}'"),
                new XElement(xmpNs + "xmpmeta",
                    new XAttribute(XNamespace.Xmlns + "xmp", xmpNs),
                    new XElement(rdfNs + "RDF",
                        new XAttribute(XNamespace.Xmlns + "rdf", rdfNs),
                        new XAttribute(XNamespace.Xmlns + "dc", dcNs),
                        new XElement(rdfNs + "Description",
                            new XAttribute(rdfNs + "about", "")
                        )
                    )
                ),
                new XProcessingInstruction("xpacket", "end='r'")
            );
        }

        private static void EnsureXmpStructure(XDocument xdoc)
        {
            var hasXpacket = xdoc.Nodes()
                .Any(n => n is XProcessingInstruction pi && pi.Target == "xpacket");

            if (!hasXpacket)
            {
                xdoc.AddFirst(new XProcessingInstruction("xpacket", $"begin='\ufeff' id='{XmpPacketId}'"));
                xdoc.Add(new XProcessingInstruction("xpacket", "end='r'"));
            }
        }

        private static void UpdateDcSubjectTags(XDocument xdoc, List<string> tags)
        {
            var rdfNs = XNamespace.Get("http://www.w3.org/1999/02/22-rdf-syntax-ns#");
            var dcNs = XNamespace.Get("http://purl.org/dc/elements/1.1/");

            var description = xdoc.Descendants(rdfNs + "Description").FirstOrDefault()
                ?? throw new InvalidOperationException("Missing RDF Description element");

            var subject = description.Element(dcNs + "subject") ?? new XElement(dcNs + "subject");
            if (subject.Parent == null)
                description.Add(subject);

            var bag = subject.Element(rdfNs + "Bag") ?? new XElement(rdfNs + "Bag");
            if (bag.Parent == null)
                subject.Add(bag);

            bag.RemoveAll();
            foreach (var tag in tags.Distinct(StringComparer.OrdinalIgnoreCase))
                bag.Add(new XElement(rdfNs + "li", tag));
        }
    }
}