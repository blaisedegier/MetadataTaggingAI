using System.Globalization;

namespace MetadataTaggingAI
{
    // Turns an image's place in the folder tree into keywords.
    public static class FolderTagExtractor
    {
        private static readonly char[] Separators = { '-', '+', '_', '.' };

        // Formats that can carry IPTC and XMP. GIF and BMP are left out: BMP has nowhere to store
        // either, and re-saving an animated GIF keeps only its first frame.
        private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".tiff" };

        // The root folder's own name and every folder between it and the image become tags.
        // Each folder name is split on - + _ . and title-cased, so "big-cats" gives "Big" and "Cats".
        public static List<string> GetFolderHierarchyTags(string imagePath, string rootFolder)
        {
            var relativePath = Path.GetRelativePath(rootFolder, imagePath);
            var rootFolderName = new DirectoryInfo(rootFolder).Name;
            var textInfo = CultureInfo.InvariantCulture.TextInfo;

            return relativePath.Split(Path.DirectorySeparatorChar)
                               .Take(..^1)
                               .Prepend(rootFolderName)
                               .SelectMany(p => p.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
                               .Select(p => p.Trim().ToLowerInvariant())
                               .Where(p => p.Length > 0)
                               .Select(textInfo.ToTitleCase)
                               .Distinct()
                               .ToList();
        }

        public static bool IsImageFile(string file)
        {
            return ImageExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase);
        }
    }
}
