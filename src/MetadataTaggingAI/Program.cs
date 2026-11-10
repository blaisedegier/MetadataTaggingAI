using Microsoft.Extensions.Configuration;

namespace MetadataTaggingAI
{
    public static class Program
    {
        private const string Usage = "Usage: MetadataTaggingAI [root-folder] [--dry-run]";

        public static async Task<int> Main(string[] args)
        {
            if (args.Contains("--help") || args.Contains("-h"))
            {
                Console.WriteLine(Usage);
                return 0;
            }

            var dryRun = args.Contains("--dry-run");
            var positional = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();
            if (positional.Count > 1)
            {
                Console.Error.WriteLine(Usage);
                return 2;
            }

            // appsettings.json sits beside the executable; environment variables override it.
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var rootFolder = Path.GetFullPath(
                positional.FirstOrDefault()
                ?? NullIfBlank(configuration["ImageRoot"])
                ?? Path.Combine(Directory.GetCurrentDirectory(), "Images"));

            if (!Directory.Exists(rootFolder))
            {
                Console.Error.WriteLine($"Root folder not found: {rootFolder}");
                return 1;
            }

            var endpoint = NullIfBlank(configuration["ApiEndpoint"]);
            var apiKey = NullIfBlank(configuration["ApiKey"]);
            AzureVisionTagger? tagger = null;
            if (endpoint is not null && apiKey is not null)
            {
                tagger = new AzureVisionTagger(endpoint, apiKey);
            }
            else if (dryRun)
            {
                Console.WriteLine("ApiEndpoint or ApiKey is not set, so only folder tags are shown.");
            }
            else
            {
                Console.Error.WriteLine("ApiEndpoint and ApiKey must be set in appsettings.json or the environment.");
                return 1;
            }

            Console.WriteLine($"{(dryRun ? "Dry run over" : "Tagging")} {rootFolder}");
            var failures = await ProcessFolderAsync(rootFolder, tagger, dryRun);
            Console.WriteLine(failures == 0 ? "Done." : $"Done with {failures} failure(s).");
            return failures == 0 ? 0 : 1;
        }

        // Tags every image under the root folder. A file that fails is reported and left untouched.
        private static async Task<int> ProcessFolderAsync(string rootFolder, AzureVisionTagger? tagger, bool dryRun)
        {
            var failures = 0;
            var imageFiles = Directory.EnumerateFiles(rootFolder, "*.*", SearchOption.AllDirectories)
                                      .Where(FolderTagExtractor.IsImageFile);

            foreach (var imagePath in imageFiles)
            {
                var relative = Path.GetRelativePath(rootFolder, imagePath);
                try
                {
                    var tags = FolderTagExtractor.GetFolderHierarchyTags(imagePath, rootFolder);
                    if (tagger is not null)
                    {
                        tags = tags.Union(await tagger.GetTagsAsync(imagePath)).ToList();
                    }

                    if (dryRun)
                    {
                        Console.WriteLine($"{relative}: {string.Join(", ", tags)}");
                    }
                    else
                    {
                        ImageMetadataWriter.WriteMetadata(imagePath, tags);
                        Console.WriteLine($"Tagged {relative}: {string.Join(", ", tags)}");
                    }
                }
                catch (Exception ex)
                {
                    failures++;
                    Console.Error.WriteLine($"Skipped {relative}: {ex.Message}");
                }
            }

            return failures;
        }

        private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
