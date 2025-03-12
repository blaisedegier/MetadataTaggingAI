using System;
using System.Linq;
using System.Xml.Linq;
using ImageMagick;
using XmpCore;

class XmpTagReader
{
    static void Main(string[] args)
    {
        string filePath = @"~\Images\sample-shoot\DSC08398.jpg";

        try
        {
            using var image = new MagickImage(filePath);

            // Get XMP tags
            var xmpProfile = image.GetXmpProfile();
            var xmpTags = GetXmpTags(xmpProfile);

            // Get IPTC tags
            var iptcTags = GetIptcTags(image);

            Console.WriteLine("XMP dc:subject tags:");
            Console.WriteLine(string.Join("\n", xmpTags));

            Console.WriteLine("\nIPTC Keywords:");
            Console.WriteLine(string.Join("\n", iptcTags));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static string[] GetXmpTags(IXmpProfile xmpProfile)
    {
        if (xmpProfile == null)
            return Array.Empty<string>();

        var xdoc = xmpProfile.ToXDocument();
        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        XNamespace dc = "http://purl.org/dc/elements/1.1/";

        return xdoc.Descendants(dc + "subject")
                   .Elements(rdf + "Bag")
                   .Elements(rdf + "li")
                   .Select(li => li.Value)
                   .ToArray();
    }

    static string[] GetIptcTags(MagickImage image)
    {
        var iptcProfile = image.GetIptcProfile();
        if (iptcProfile == null)
            return Array.Empty<string>();

        return iptcProfile.Values
            .Where(v => v.Tag == IptcTag.Keyword)
            .Select(v => v.Value)
            .ToArray();
    }
}