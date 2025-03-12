using Dropbox.Api;
using Dropbox.Api.Files;
using Dropbox.Api.Sharing;
using Microsoft.Extensions.Configuration;

// Load configuration from appsettings.json
IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
IConfigurationRoot configuration = builder.Build();

string accessToken = configuration["AccessToken"];
string sharedLink = configuration["SharedLink"];

// Initialize Dropbox client
using (var dbx = new DropboxClient(accessToken))
{
    try
    {
        Console.WriteLine("Fetching shared link metadata...");

        // Get shared link metadata
        var sharedLinkMetadata = await dbx.Sharing.GetSharedLinkMetadataAsync(sharedLink);

        // If the shared link points to a file
        if (sharedLinkMetadata is FileLinkMetadata fileMetadata)
        {
            Console.WriteLine($"File: {fileMetadata.Name}");
        }
        // If the shared link points to a folder, list its contents
        else if (sharedLinkMetadata is FolderLinkMetadata folderMetadata)
        {
            // Use root path for shared folder
            var path = folderMetadata.PathLower ?? string.Empty;
            var list = await dbx.Files.ListFolderAsync(new ListFolderArg(path, sharedLink: new SharedLink(sharedLink)));

            Console.WriteLine("Contents of the shared folder:");
            foreach (var entry in list.Entries)
            {
                Console.WriteLine($"{(entry.IsFolder ? "Folder" : "File")}: {entry.Name}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}
