using DropboxSharedLinkDownloader;

namespace MetadataTaggingAI.Tests
{
    public class LocalFileNameTests
    {
        [Theory]
        [InlineData("lion.jpg", "lion.jpg")]
        [InlineData("lion: day 1?.jpg", "lion_ day 1_.jpg")]
        [InlineData("a<b>c|d*e\"f.jpg", "a_b_c_d_e_f.jpg")]
        [InlineData("notes. ", "notes")]
        [InlineData("...", "_")]
        [InlineData("CON", "_CON")]
        [InlineData("nul.txt", "_nul.txt")]
        [InlineData("Console.jpg", "Console.jpg")]
        public void MakesNamesSafeToStoreOnWindows(string name, string expected)
        {
            Assert.Equal(expected, LocalFileName.Sanitise(name));
        }
    }
}
