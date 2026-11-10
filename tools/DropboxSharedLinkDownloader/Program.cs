using Dropbox.Api;
using Dropbox.Api.Files;
using Dropbox.Api.Sharing;
using DropboxSharedLinkDownloader;
using Microsoft.Extensions.Configuration;

// Downloads every file behind a Dropbox shared folder link, keeping the folder structure.
// A file or folder that fails is reported and skipped; the exit code is non-zero if any did.
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
    var failures = new List<string>();
    await DownloadFolderAsync(dbx, sharedLink, string.Empty, outputDirectory, failures);

    if (failures.Count == 0)
    {
        Console.WriteLine("Done.");
        return 0;
    }

    Console.Error.WriteLine($"Done with {failures.Count} failure(s):");
    foreach (var failure in failures)
        Console.Error.WriteLine($"  {failure}");
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

// Paths are relative to the shared folder's root ("" is the root itself). Listings made through a
// shared link do not return path_lower, so each child's path is built from its parent and its name.
static async Task DownloadFolderAsync(
    DropboxClient dbx, string sharedLink, string folderPath, string localDirectory, List<string> failures)
{
    var displayPath = folderPath.Length == 0 ? "/" : folderPath;
    ListFolderResult page;
    try
    {
        Directory.CreateDirectory(localDirectory);
        page = await dbx.Files.ListFolderAsync(new ListFolderArg(folderPath, sharedLink: new SharedLink(sharedLink)));
    }
    catch (Exception ex)
    {
        failures.Add($"{displayPath}: folder skipped: {ex.Message}");
        return;
    }

    // Windows and macOS compare names ignoring case, and sanitising can map two names onto one,
    // so the second entry to claim a local name is skipped rather than allowed to overwrite the first.
    var localNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    while (true)
    {
        foreach (var entry in page.Entries)
        {
            var entryPath = $"{folderPath}/{entry.Name}";
            var localName = LocalFileName.Sanitise(entry.Name);
            if (!localNames.Add(localName))
            {
                failures.Add($"{entryPath}: skipped, because another entry in the same folder is already saved as {localName}");
                continue;
            }

            if (localName != entry.Name)
                Console.WriteLine($"Saving {entryPath} as {localName}");

            var localPath = Path.Combine(localDirectory, localName);
            if (entry.IsFolder)
            {
                Console.WriteLine($"Entering folder: {entryPath}");
                await DownloadFolderAsync(dbx, sharedLink, entryPath, localPath, failures);
            }
            else if (entry.IsFile)
            {
                Console.WriteLine($"Downloading: {entryPath}");
                try
                {
                    await DownloadFileAsync(dbx, sharedLink, entryPath, localPath);
                }
                catch (Exception ex)
                {
                    failures.Add($"{entryPath}: {ex.Message}");
                }
            }
        }

        if (!page.HasMore)
            break;

        try
        {
            page = await dbx.Files.ListFolderContinueAsync(page.Cursor);
        }
        catch (Exception ex)
        {
            failures.Add($"{displayPath}: listing stopped part-way, so later entries were not downloaded: {ex.Message}");
            return;
        }
    }
}

// Streams into "<name>.part" and renames it only once the download is complete, so an interrupted
// run never leaves a truncated file under the real name.
static async Task DownloadFileAsync(DropboxClient dbx, string sharedLink, string entryPath, string localPath)
{
    var partPath = localPath + ".part";
    try
    {
        using var response = await dbx.Sharing.GetSharedLinkFileAsync(sharedLink, entryPath);
        await using (var source = await response.GetContentAsStreamAsync())
        await using (var target = File.Create(partPath))
        {
            await source.CopyToAsync(target);
        }

        File.Move(partPath, localPath, overwrite: true);
    }
    catch
    {
        try
        {
            File.Delete(partPath);
        }
        catch (IOException)
        {
            // Best effort; the failure itself is what gets reported.
        }

        throw;
    }
}

static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
