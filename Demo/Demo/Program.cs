using Dropbox.Api.Sharing;
using Dropbox.Api;
using Microsoft.Extensions.Configuration;

// Load configuration from appsettings.json
IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
IConfigurationRoot configuration = builder.Build();

// Retrieve configuration values
string accessToken = configuration["AccessToken"] ?? throw new InvalidOperationException("AccessToken is missing in appsettings.json");
string sharedLink = configuration["SharedLink"] ?? throw new InvalidOperationException("SharedLink is missing in appsettings.json");
string apiEndpoint = configuration["ApiEndpoint"] ?? throw new InvalidOperationException("ApiEndpoint is missing in appsettings.json");
string apiKey = configuration["ApiKey"] ?? throw new InvalidOperationException("ApiKey is missing in appsettings.json");

// Folder Path
const string folderPath = @"C:\Users\user\OneDrive\Desktop\PhotoLibrary\Demo\bin";

// Initialize Dropbox client
using (var dbx = new DropboxClient(accessToken))
{
    try
    {
        Console.WriteLine("Downloading files from shared link...");

        var sharedLinkArg = dbx.Sharing.GetSharedLinkMetadataAsync(sharedLink);
        sharedLinkArg
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}
