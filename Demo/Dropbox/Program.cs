using Dropbox.Api;
using Dropbox.Api.Files;
using Dropbox.Api.Sharing;
using Microsoft.Extensions.Configuration;

// Load configuration from appsettings.json
IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
IConfigurationRoot configuration = builder.Build();

const string accessToken = configuration["AccessToken"];
const string sharedLink = configuration["SharedLink"];
const string localDirectory = @"~\Dropbox\bin";

// Initialize Dropbox client
using (var dbx = new DropboxClient(accessToken))
{
    try
    {
        Console.WriteLine("Downloading files...");

        // Get shared link metadata
        var sharedLinkMetadata = await dbx.Sharing.GetSharedLinkMetadataAsync(sharedLink);

        // Check if the shared link is a folder
        if (sharedLinkMetadata is FolderLinkMetadata folderMetadata)
        {
            // Create a temporary folder to store the files
            Directory.CreateDirectory(localDirectory);

            // Process the root folder
            await ProcessFolder(dbx, sharedLink, sharedLinkMetadata.PathLower ?? string.Empty, localDirectory);
        }
        else
        {
            Console.WriteLine("The shared link is not a folder.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}

static async Task ProcessFolder(DropboxClient dbx, string sharedLink, string path, string localDirectory)
{
    // List the folder contents
    var listFolderResult = await dbx.Files.ListFolderAsync(new ListFolderArg(path, sharedLink: new SharedLink(sharedLink)));

    // Loop through the folder contents
    foreach (var entry in listFolderResult.Entries)
    {
        if (entry.IsFolder)
        {
            // Create a subfolder locally
            var subfolder = Path.Combine(localDirectory, entry.Name);
            Directory.CreateDirectory(subfolder);
            Console.WriteLine($"Entering folder: {entry.Name}");

            // Recursively call for subfolders
            await ProcessFolder(dbx, sharedLink, entry.PathLower ?? string.Empty, subfolder);
        }
        else if (entry.IsFile)
        {
            // Download file
            var fileMetadata = entry.AsFile;
            string localFilePath = Path.Combine(localDirectory, entry.Name);
            Console.WriteLine($"Downloading file: {entry.Name}");

            using (var response = await dbx.Files.DownloadAsync(new DownloadArg(fileMetadata.PathLower ?? string.Empty, sharedLink)))
            {
                var fileBytes = await response.GetContentAsByteArrayAsync();
                await File.WriteAllBytesAsync(localFilePath, fileBytes);
            }
        }
    }

    // Check for pagination
    if (listFolderResult.HasMore)
    {
        await ProcessFolder(dbx, sharedLink, path, localDirectory);
    }
}
