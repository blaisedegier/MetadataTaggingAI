using ImageMagick;
using System.Xml.Linq;

namespace MetadataTaggingAI.Tests
{
    public class GetFolderHierarchyTagsTests
    {
        [Fact]
        public void IncludesRootFolderNameThenNestedFolders()
        {
            var root = Path.GetFullPath("wildlife");
            var imagePath = Path.Combine(root, "mammals", "big cats", "lion.jpg");

            var result = FolderTagExtractor.GetFolderHierarchyTags(imagePath, root);

            Assert.Equal(new List<string> { "Wildlife", "Mammals", "Big Cats" }, result);
        }

        [Fact]
        public void SplitsOnSeparatorsAndNormalisesCase()
        {
            var root = Path.GetFullPath("river-trip");
            var imagePath = Path.Combine(root, "  BIRDS 2023 ", "water_birds+egrets", "Egret.jpg");

            var result = FolderTagExtractor.GetFolderHierarchyTags(imagePath, root);

            Assert.Equal(new List<string> { "River", "Trip", "Birds 2023", "Water", "Birds", "Egrets" }, result);
        }

        [Fact]
        public void ImageInRootFolderGetsOnlyTheRootName()
        {
            var root = Path.GetFullPath("sample-shoot");
            var imagePath = Path.Combine(root, "image.jpg");

            var result = FolderTagExtractor.GetFolderHierarchyTags(imagePath, root);

            Assert.Equal(new List<string> { "Sample", "Shoot" }, result);
        }

        [Fact]
        public void DropsDuplicateFolderTags()
        {
            var root = Path.GetFullPath("birds");
            var imagePath = Path.Combine(root, "Birds", "owl.jpg");

            var result = FolderTagExtractor.GetFolderHierarchyTags(imagePath, root);

            Assert.Equal(new List<string> { "Birds" }, result);
        }
    }

    public class IsImageFileTests
    {
        [Theory]
        [InlineData("photo.jpg", true)]
        [InlineData("IMAGE.PNG", true)]
        [InlineData("scan.tiff", true)]
        [InlineData("document.txt", false)]
        [InlineData("image.webp", false)]
        [InlineData("animation.gif", false)]
        [InlineData("bitmap.bmp", false)]
        public void CorrectlyIdentifiesImageFiles(string filename, bool expected)
        {
            Assert.Equal(expected, FolderTagExtractor.IsImageFile(filename));
        }
    }

    public class IptcTagTests
    {
        private static List<string> Keywords(IIptcProfile iptc) =>
            iptc.Values.Where(v => v.Tag == IptcTag.Keyword).Select(v => v.Value).ToList();

        [Fact]
        public void ReplacesExistingKeywords()
        {
            var iptc = new IptcProfile();
            iptc.SetValue(IptcTag.Keyword, "old-tag");
            var newTags = new List<string> { "Safari", "Wildlife" };

            ImageMetadataWriter.UpdateIptcTags(iptc, newTags);

            Assert.Equal(newTags, Keywords(iptc));
        }

        [Fact]
        public void RemovesAllExistingKeywordsWhenGivenNone()
        {
            var iptc = new IptcProfile();
            iptc.SetValue(IptcTag.Keyword, "old1");
            iptc.SetValue(IptcTag.Keyword, "old2");

            ImageMetadataWriter.UpdateIptcTags(iptc, new List<string>());

            Assert.Empty(Keywords(iptc));
        }

        [Fact]
        public void DeduplicatesIgnoringCase()
        {
            var iptc = new IptcProfile();

            ImageMetadataWriter.UpdateIptcTags(iptc, new List<string> { "Lion", "lion", "Cat" });

            Assert.Equal(new List<string> { "Lion", "Cat" }, Keywords(iptc));
        }
    }

    public class XmpTagTests
    {
        private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";

        private static List<string> Subjects(IXmpProfile xmp) =>
            xmp.ToXDocument()!.Descendants(Rdf + "li").Select(e => e.Value).ToList();

        [Fact]
        public void CreatesNewProfileWhenNull()
        {
            var tags = new List<string> { "Adventure", "Nature" };

            var xmp = ImageMetadataWriter.UpdateXmpTags(null, tags);

            Assert.Equal(tags, Subjects(xmp));
        }

        [Fact]
        public void ReplacesSubjectsInExistingProfile()
        {
            var existingXmp = new XmpProfile(new XDocument(
                new XElement(Rdf + "RDF",
                    new XElement(Rdf + "Description",
                        new XElement(Dc + "subject",
                            new XElement(Rdf + "Bag",
                                new XElement(Rdf + "li", "old-tag")))))));

            var updatedXmp = ImageMetadataWriter.UpdateXmpTags(existingXmp, new List<string> { "new-tag" });

            Assert.Equal(new List<string> { "new-tag" }, Subjects(updatedXmp));
        }
    }

    public sealed class WriteMetadataTests : IDisposable
    {
        private readonly string _folder = Directory.CreateTempSubdirectory("metadata-tagging-").FullName;

        public void Dispose() => Directory.Delete(_folder, recursive: true);

        private string CreateJpeg(string name)
        {
            var path = Path.Combine(_folder, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var blank = new MagickImage(MagickColors.SkyBlue, 32, 32);
            blank.Write(path, MagickFormat.Jpeg);
            return path;
        }

        private static (List<string> Iptc, List<string> Xmp) ReadKeywords(string path)
        {
            using var image = new MagickImage(path);
            var iptc = image.GetIptcProfile();
            var xmp = image.GetXmpProfile();
            Assert.NotNull(iptc);
            Assert.NotNull(xmp);
            return (
                iptc!.Values.Where(v => v.Tag == IptcTag.Keyword).Select(v => v.Value).ToList(),
                xmp!.ToXDocument()!.Descendants().Where(e => e.Name.LocalName == "li").Select(e => e.Value).ToList());
        }

        [Fact]
        public void WritesIptcAndXmpKeywordsIntoAJpeg()
        {
            var path = CreateJpeg("lion.jpg");
            var tags = new List<string> { "Wildlife", "Lion" };

            ImageMetadataWriter.WriteMetadata(path, tags);

            var (iptc, xmp) = ReadKeywords(path);
            Assert.Equal(tags, iptc);
            Assert.Equal(tags, xmp);
            Assert.Equal(new[] { path }, Directory.GetFiles(_folder));
        }

        [Fact]
        public void WritesACopyAndLeavesTheOriginalUntouched()
        {
            var path = CreateJpeg("lion.jpg");
            var originalBytes = File.ReadAllBytes(path);
            var copy = Path.Combine(_folder, "tagged", "big-cats", "lion.jpg");
            var tags = new List<string> { "Wildlife", "Lion" };

            ImageMetadataWriter.WriteMetadata(path, tags, copy);

            Assert.Equal(originalBytes, File.ReadAllBytes(path));
            Assert.Equal(tags, ReadKeywords(copy).Iptc);
            Assert.Equal(new[] { copy }, Directory.GetFiles(Path.GetDirectoryName(copy)!));
        }

        [Fact]
        public void LeavesNoTemporaryFileWhenTheImageCannotBeRead()
        {
            var path = Path.Combine(_folder, "broken.jpg");
            File.WriteAllText(path, "not an image");

            Assert.Throws<InvalidOperationException>(
                () => ImageMetadataWriter.WriteMetadata(path, new List<string> { "Lion" }));

            Assert.Equal("not an image", File.ReadAllText(path));
            Assert.Equal(new[] { path }, Directory.GetFiles(_folder));
        }
    }
}
