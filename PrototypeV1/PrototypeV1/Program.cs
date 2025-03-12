using Azure;
using Azure.AI.Vision.ImageAnalysis;
using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace PrototypeV1
{
    public class ImageProcessor
    {
        // Load configuration from appsettings.json
        IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
        IConfigurationRoot configuration = builder.Build();

        // Target directory for image processing
        const string TARGET_DIR = @"~/Images";

        // Azure Cognitive Services credentials
        const string AZURE_KEY = configuration["ApiEndpoint"];
        const string AZURE_ENDPOINT = configuration["ApiKey"];

        // Main method to run the application
        public static async Task Main()
        {
            try
            {
                // Get the first folder in the target directory
                var rootFolder = Directory.GetDirectories(TARGET_DIR).First();

                // Process the folder
                await ProcessFolder(rootFolder);

                Console.WriteLine("Processing complete. Press any key to exit...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting the application: {ex.Message}");
            }
        }

        // Method to process a folder of images
        public static async Task ProcessFolder(string folderPath)
        {
            try
            {
                // Get all image files in the folder and subfolders where the file is an image
                var imageFiles = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                                         .Where(IsImageFile);

                // Loop through each image file and process it
                foreach (var imagePath in imageFiles)
                {
                    try
                    {
                        // Get tags from folder hierarchy and Azure Cognitive Services
                        var folderTags = GetFolderHierarchyTags(imagePath, folderPath);
                        var azureTags = await GetAzureTags(imagePath);

                        // Write tags to image metadata
                        ImageMetadataWriter.WriteMetadata(imagePath, folderTags.Union(azureTags).ToList());

                        Console.WriteLine($"Processed: {Path.GetFileName(imagePath)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing {imagePath}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing folder {folderPath}: {ex.Message}");
            }
        }

        // Method to get tags from folder hierarchy
        public static List<string> GetFolderHierarchyTags(string imagePath, string rootFolder)
        {
            try
            {
                // Get relative path from root folder
                var relativePath = Path.GetRelativePath(rootFolder, imagePath);

                // Get the root folder's name
                var rootFolderName = new DirectoryInfo(rootFolder).Name;

                var textInfo = CultureInfo.CurrentCulture.TextInfo;

                // Define characters to replace with space
                char[] separators = { '-', '+', '_', '.' };

                // Split path, remove file name, prepend root folder name, process tags
                return relativePath.Split(Path.DirectorySeparatorChar)
                                  .Take(..^1)
                                  .Prepend(rootFolderName)
                                  .SelectMany(p => p.Split(separators, StringSplitOptions.RemoveEmptyEntries))
                                  .Select(p => p.Trim().ToLower())
                                  .Select(p => textInfo.ToTitleCase(p))
                                  .Where(p => !string.IsNullOrEmpty(p))
                                  .Distinct()
                                  .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting tags from folder hierarchy: {ex.Message}");
                return new List<string>();
            }
        }

        // Method to get tags from Azure Cognitive Services
        public static async Task<List<string>> GetAzureTags(string imagePath)
        {
            try
            {
                // Create a new client and open the image file
                var client = new ImageAnalysisClient(new Uri(AZURE_ENDPOINT), new AzureKeyCredential(AZURE_KEY));

                // Open the image file as a stream
                using var stream = File.OpenRead(imagePath);

                // Analyze the image and return the tags
                var result = await client.AnalyzeAsync(BinaryData.FromStream(stream), VisualFeatures.Tags);

                // Title case the tags and return as a list
                var textInfo = CultureInfo.CurrentCulture.TextInfo;
                return result.Value.Tags.Values.Select(t => textInfo.ToTitleCase(t.Name.ToLower())).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting tags from Azure Cognitive Services: {ex.Message}");
                return new List<string>();
            }
        }

        // Method to check if a file is an image
        public static bool IsImageFile(string file)
        {
            // Check if the file extension is in the list of image extensions
            return new[] { ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".bmp" }
                .Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase);
        }
    }
}