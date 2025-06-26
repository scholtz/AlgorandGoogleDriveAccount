using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.IO;

namespace AlgorandGoogleDriveAccount.MCP
{
    [McpServerToolType]
    public class LoadFile
    {
        [McpServerTool, Description("Loads and returns the content of a file from Google Drive using the provided file ID and access token.")]
        public static async Task<string> LoadGoogleDriveFile(
            [Description("The Google Drive file ID to load")] string fileId, 
            [Description("The OAuth2 access token for Google Drive API access")] string accessToken)
        {
            var credential = GoogleCredential.FromAccessToken(accessToken);

            var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "MCP Drive Loader"
            });

            var request = service.Files.Get(fileId);
            var stream = new MemoryStream();
            await request.DownloadAsync(stream);
            stream.Position = 0;

            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
    }
}
