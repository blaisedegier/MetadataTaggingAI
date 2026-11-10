using System.Xml.Linq;
using ImageMagick;

// Prints the XMP dc:subject tags and IPTC keywords stored in an image.
if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: MetadataInspector <image-path>");
    return 2;
}

var filePath = args[0];

try
{
    using var image = new MagickImage(filePath);

    Console.WriteLine("XMP dc:subject tags:");
    foreach (var tag in GetXmpTags(image.GetXmpProfile()))
        Console.WriteLine($"  {tag}");

    Console.WriteLine("IPTC Keywords:");
    foreach (var tag in GetIptcTags(image.GetIptcProfile()))
        Console.WriteLine($"  {tag}");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

static string[] GetXmpTags(IXmpProfile? xmpProfile)
{
    var xdoc = xmpProfile?.ToXDocument();
    if (xdoc is null)
        return Array.Empty<string>();

    XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    XNamespace dc = "http://purl.org/dc/elements/1.1/";

    return xdoc.Descendants(dc + "subject")
               .Elements(rdf + "Bag")
               .Elements(rdf + "li")
               .Select(li => li.Value)
               .ToArray();
}

static string[] GetIptcTags(IIptcProfile? iptcProfile)
{
    if (iptcProfile is null)
        return Array.Empty<string>();

    return iptcProfile.Values
        .Where(v => v.Tag == IptcTag.Keyword)
        .Select(v => v.Value)
        .ToArray();
}
