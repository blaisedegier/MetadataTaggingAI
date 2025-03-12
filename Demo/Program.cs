using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Extensions.Configuration;

// Load configuration from appsettings.json
IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
IConfigurationRoot configuration = builder.Build();

// Retrieve configuration values
string apiKey = configuration["ApiKey"] ?? string.Empty;
string apiEndpoint = configuration["ApiEndpoint"] ?? string.Empty;
string folderPath = configuration["FolderPath"] ?? string.Empty;

// Validate configuration values
var client = new ComputerVisionClient(new ApiKeyServiceClientCredentials(apiKey)) { Endpoint = apiEndpoint };

// Process each file in the specified folder
foreach (var filePath in Directory.GetFiles(folderPath))
{
    // Log the file being processed
    Console.WriteLine($"Processing file: {Path.GetFileName(filePath)}");
    try
    {
        // Open the file as a stream
        using (var imageStream = File.OpenRead(filePath))
        {
            // Attempt to analyze the file
            var analysis = await client.AnalyzeImageInStreamAsync(imageStream, new List<VisualFeatureTypes?> { VisualFeatureTypes.Tags });

            // Log the tags detected in the file
            Console.WriteLine("Tags:");
            foreach (var tag in analysis.Tags)
            {
                Console.WriteLine($"  - {tag.Name} (Confidence: {tag.Confidence:P})");
            }
        }
    }
    catch (Exception ex)
    {
        // Log errors for files that cannot be processed
        Console.WriteLine($"Could not process {Path.GetFileName(filePath)}: {ex.Message}");
    }
}
