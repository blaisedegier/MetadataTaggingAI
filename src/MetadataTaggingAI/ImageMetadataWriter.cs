using ImageMagick;
using System.Xml.Linq;

namespace MetadataTaggingAI
{
    public static class ImageMetadataWriter
    {
        private const string XmpPacketId = "W5M0MpCehiHzreSzNTczkc9d";

        // Writes the tags into the image at destinationPath, or back into imagePath when no
        // destination is given. The new file is written to a temporary file in the destination's
        // folder and then moved over the destination, so a failure part-way through never leaves a
        // truncated image behind.
        public static void WriteMetadata(string imagePath, List<string> tags, string? destinationPath = null)
        {
            var destination = Path.GetFullPath(destinationPath ?? imagePath);
            var directory = Path.GetDirectoryName(destination)!;
            var tempPath = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");

            try
            {
                using var image = new MagickImage();
                var readSettings = new MagickReadSettings();
                readSettings.SetDefine("profile:preserve", "true");
                image.Read(imagePath, readSettings);

                HandleIptcMetadata(image, tags);
                HandleXmpMetadata(image, tags);

                // The temporary name has no image extension, so the format is passed explicitly.
                image.Settings.SetDefine("profile:preserve", "true");
                Directory.CreateDirectory(directory);
                image.Write(tempPath, image.Format);

                if (File.Exists(destination))
                    File.Replace(tempPath, destination, destinationBackupFileName: null);
                else
                    File.Move(tempPath, destination);
            }
            catch (Exception ex)
            {
                TryDelete(tempPath);
                throw new InvalidOperationException($"Metadata write failed for {imagePath}", ex);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // Best effort: the original file is already untouched, which is what matters.
            }
            catch (UnauthorizedAccessException)
            {
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