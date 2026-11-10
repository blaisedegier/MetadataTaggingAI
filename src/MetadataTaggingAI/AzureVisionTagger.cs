using System.Globalization;
using Azure;
using Azure.AI.Vision.ImageAnalysis;

namespace MetadataTaggingAI
{
    // Asks Azure AI Vision (Image Analysis 4.0) what is in an image. One client is created and reused.
    public sealed class AzureVisionTagger
    {
        private readonly ImageAnalysisClient _client;

        public AzureVisionTagger(string endpoint, string apiKey)
        {
            _client = new ImageAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        }

        public async Task<List<string>> GetTagsAsync(string imagePath, CancellationToken cancellationToken = default)
        {
            await using var stream = File.OpenRead(imagePath);
            var data = await BinaryData.FromStreamAsync(stream, cancellationToken);
            var result = await _client.AnalyzeAsync(data, VisualFeatures.Tags, cancellationToken: cancellationToken);

            var textInfo = CultureInfo.InvariantCulture.TextInfo;
            return result.Value.Tags.Values
                         .Select(t => textInfo.ToTitleCase(t.Name.ToLowerInvariant()))
                         .ToList();
        }
    }
}
