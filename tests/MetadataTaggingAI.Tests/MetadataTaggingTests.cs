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

    public class WriteMetadataTests
    {
        [Fact]
        public void WritesIptcAndXmpKeywordsIntoAJpeg()
        {
            var path = Path.Combine(Path.GetTempPath(), $"metadata-tagging-{Guid.NewGuid():N}.jpg");
            try
            {
                using (var blank = new MagickImage(MagickColors.SkyBlue, 32, 32))
                {
                    blank.Write(path, MagickFormat.Jpeg);
                }

                var tags = new List<string> { "Wildlife", "Lion" };
                ImageMetadataWriter.WriteMetadata(path, tags);

                using var image = new MagickImage(path);
                var iptc = image.GetIptcProfile();
                var xmp = image.GetXmpProfile();

                Assert.NotNull(iptc);
                Assert.NotNull(xmp);
                Assert.Equal(tags, iptc!.Values.Where(v => v.Tag == IptcTag.Keyword).Select(v => v.Value).ToList());
                Assert.Equal(tags, xmp!.ToXDocument()!.Descendants()
                    .Where(e => e.Name.LocalName == "li").Select(e => e.Value).ToList());
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
