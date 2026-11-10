using Dropbox.Api;
using Dropbox.Api.Files;
using Dropbox.Api.Sharing;
using Microsoft.Extensions.Configuration;

// Downloads every file behind a Dropbox shared folder link, keeping the folder structure.
// Usage: DropboxSharedLinkDownloader [shared-link] [output-folder]
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var accessToken = NullIfBlank(configuration["AccessToken"]);
var sharedLink = NullIfBlank(args.ElementAtOrDefault(0)) ?? NullIfBlank(configuration["SharedLink"]);
var outputDirectory = Path.GetFullPath(
    NullIfBlank(args.ElementAtOrDefault(1))
    ?? NullIfBlank(configuration["OutputDirectory"])
    ?? Path.Combine(Directory.GetCurrentDirectory(), "dropbox-download"));

if (accessToken is null || sharedLink is null)
{
    Console.Error.WriteLine("Set AccessToken in appsettings.json or the environment, and SharedLink there or as the first argument.");
    return 1;
}

using var dbx = new DropboxClient(accessToken);
try
{
    var metadata = await dbx.Sharing.GetSharedLinkMetadataAsync(sharedLink);
    if (metadata is not FolderLinkMetadata)
    {
        Console.Error.WriteLine("The shared link is not a folder.");
        return 1;
    }

    Console.WriteLine($"Downloading to {outputDirectory}");
    await DownloadFolderAsync(dbx, sharedLink, string.Empty, outputDirectory);
    Console.WriteLine("Done.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

// Paths are relative to the shared folder's root ("" is the root itself). Listings made through a
// shared link do not return path_lower, so each child's path is built from its parent and its name.
static async Task DownloadFolderAsync(DropboxClient dbx, string sharedLink, string folderPath, string localDirectory)
{
    Directory.CreateDirectory(localDirectory);

    var page = await dbx.Files.ListFolderAsync(new ListFolderArg(folderPath, sharedLink: new SharedLink(sharedLink)));
    while (true)
    {
        foreach (var entry in page.Entries)
        {
            var entryPath = $"{folderPath}/{entry.Name}";
            var localPath = Path.Combine(localDirectory, Path.GetFileName(entry.Name));

            if (entry.IsFolder)
            {
                Console.WriteLine($"Entering folder: {entryPath}");
                await DownloadFolderAsync(dbx, sharedLink, entryPath, localPath);
            }
            else if (entry.IsFile)
            {
                Console.WriteLine($"Downloading: {entryPath}");
                using var response = await dbx.Sharing.GetSharedLinkFileAsync(sharedLink, entryPath);
                await using var source = await response.GetContentAsStreamAsync();
                await using var target = File.Create(localPath);
                await source.CopyToAsync(target);
            }
        }

        if (!page.HasMore)
            break;

        page = await dbx.Files.ListFolderContinueAsync(page.Cursor);
    }
}

static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
