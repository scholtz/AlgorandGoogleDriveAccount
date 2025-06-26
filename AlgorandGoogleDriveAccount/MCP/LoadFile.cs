using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using AlgorandGoogleDriveAccount.Model;

namespace AlgorandGoogleDriveAccount.MCP
{
    [McpServerToolType]
    public class LoadFile
    {
        private readonly IDistributedCache _cache;

        public LoadFile(IDistributedCache cache)
        {
            _cache = cache;
        }

        [McpServerTool, Description("Loads and returns the content of a file from Google Drive using the provided file ID and access token.")]
        public async Task<string> LoadGoogleDriveFile(
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

        [McpServerTool, Description("Loads and returns the content of a file from Google Drive using a paired device session ID.")]
        public async Task<string> LoadGoogleDriveFileBySession(
            [Description("The Google Drive file ID to load")] string fileId, 
            [Description("The session ID of a paired device")] string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                throw new ArgumentException("Session ID is required");
            }

            var cacheKey = $"device_session:{sessionId}";
            var deviceInfoJson = await _cache.GetStringAsync(cacheKey);

            if (string.IsNullOrEmpty(deviceInfoJson))
            {
                throw new InvalidOperationException("Device not found or session expired. Please pair the device again.");
            }

            var deviceInfo = JsonSerializer.Deserialize<PairedDeviceInfo>(deviceInfoJson);
            
            if (deviceInfo == null)
            {
                throw new InvalidOperationException("Invalid device information. Please pair the device again.");
            }

            // Check if token is expired
            if (DateTime.UtcNow > deviceInfo.ExpiresAt)
            {
                await _cache.RemoveAsync(cacheKey);
                throw new InvalidOperationException("Device session expired. Please pair the device again.");
            }

            // Use the device's access token to load the file
            return await LoadGoogleDriveFile(fileId, deviceInfo.AccessToken);
        }
    }
}
