using ImageMagick;
using System.Xml.Linq;

namespace PrototypeV1
{
    public class ImageProcessorTests
    {
        public class GetFolderHierarchyTagsTests
        {
            [Fact]
            public void ReturnsCorrectTagsForNestedFolders()
            {
                // Arrange
                var root = Path.GetFullPath("test-root");
                var imagePath = Path.GetFullPath(Path.Combine(root, "mammals", "big cats", "lion.jpg"));

                // Act
                var result = ImageProcessor.GetFolderHierarchyTags(imagePath, root);

                // Assert
                Assert.Equal(new List<string> { "Mammals", "Big Cats" }, result);
            }

            [Fact]
            public void HandlesMixedCaseAndSpaces()
            {
                var root = Path.GetFullPath("test-root");
                var imagePath = Path.GetFullPath(Path.Combine(root, "  BIRDS 2023 ", "water-birds", "Egret.jpg"));

                var result = ImageProcessor.GetFolderHierarchyTags(imagePath, root);

                Assert.Equal(new List<string> { "Birds 2023", "Water Birds" }, result);
            }

            [Fact]
            public void ReturnsEmptyListForRootFolder()
            {
                // Arrange
                var root = Path.GetFullPath("test-root");
                var imagePath = Path.GetFullPath(Path.Combine(root, "image.jpg"));

                // Act
                var result = ImageProcessor.GetFolderHierarchyTags(imagePath, root);

                // Assert
                Assert.Empty(result);
            }
        }

        public class IsImageFileTests
        {
            [Theory]
            [InlineData("photo.jpg", true)]
            [InlineData("IMAGE.PNG", true)]
            [InlineData("document.txt", false)]
            [InlineData("image.webp", false)]
            public void CorrectlyIdentifiesImageFiles(string filename, bool expected)
            {
                // Act
                var result = ImageProcessor.IsImageFile(filename);

                // Assert
                Assert.Equal(expected, result);
            }
        }

        public class IptcTagTests
        {
            [Fact]
            public void UpdatesKeywordsCorrectly()
            {
                // Arrange
                var iptc = new IptcProfile();
                iptc.SetValue(IptcTag.Keyword, "old-tag");
                var newTags = new List<string> { "safari", "wildlife" };

                // Act
                ImageProcessor.UpdateIptcTags(iptc, newTags);

                // Assert
                var keywords = iptc.Values
                    .Where(v => v.Tag == IptcTag.Keyword)
                    .Select(v => v.Value)
                    .ToList();

                Assert.Equal(newTags, keywords);
            }

            [Fact]
            public void RemovesAllExistingKeywords()
            {
                // Arrange
                var iptc = new IptcProfile();
                iptc.SetValue(IptcTag.Keyword, "old1");
                iptc.SetValue(IptcTag.Keyword, "old2");

                // Act
                ImageProcessor.UpdateIptcTags(iptc, new List<string>());

                // Assert
                Assert.Empty(iptc.Values.Where(v => v.Tag == IptcTag.Keyword));
            }
        }

        public class XmpTagTests
        {
            [Fact]
            public void CreatesNewProfileWhenNull()
            {
                // Arrange
                var tags = new List<string> { "adventure", "nature" };

                // Act
                var xmp = ImageProcessor.UpdateXmpTags(null, tags);
                var xdoc = xmp.ToXDocument()!;

                // Assert
                var liElements = xdoc.Descendants()
                    .Where(e => e.Name.LocalName == "li")
                    .Select(e => e.Value)
                    .ToList();

                Assert.Equal(tags, liElements);
            }

            [Fact]
            public void UpdatesExistingProfile()
            {
                // Arrange
                var existingXmp = new XmpProfile(new XDocument(
                    new XElement(XNamespace.Get("http://www.w3.org/1999/02/22-rdf-syntax-ns#") + "RDF",
                        new XElement(XNamespace.Get("http://www.w3.org/1999/02/22-rdf-syntax-ns#") + "Description",
                            new XElement(XNamespace.Get("http://purl.org/dc/elements/1.1/") + "subject",
                                new XElement(XNamespace.Get("http://www.w3.org/1999/02/22-rdf-syntax-ns#") + "Bag",
                                    new XElement(XNamespace.Get("http://www.w3.org/1999/02/22-rdf-syntax-ns#") + "li", "old-tag")
                                )
                            )
                        )
                    )
                ));

                // Act
                var updatedXmp = ImageProcessor.UpdateXmpTags(existingXmp, new List<string> { "new-tag" });
                var xdoc = updatedXmp.ToXDocument()!;

                // Assert
                var liValues = xdoc.Descendants()
                    .Where(e => e.Name.LocalName == "li")
                    .Select(e => e.Value)
                    .ToList();

                Assert.Equal(new List<string> { "new-tag" }, liValues);
            }
        }
    }
}