using AlgorandGoogleDriveAccount.Model;
using AlgorandGoogleDriveAccount.Repository;
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

namespace AlgorandGoogleDriveAccount.MCP
{
    [McpServerToolType]
    public class BiatecMCPGoogle
    {
        private readonly IDistributedCache _cache;
        private readonly GoogleDriveRepository _googleDriveRepository;

        public BiatecMCPGoogle(IDistributedCache cache, GoogleDriveRepository googleDriveRepository)
        {
            _cache = cache;
            _googleDriveRepository = googleDriveRepository;
        }

        [McpServerTool, Description("Loads the Algorand account address stored at the google store.")]
        public async Task<string> GetAccountAddress([Description("The OAuth2 access token for Google Drive API access")] string accessToken)
        {
            var credential = GoogleCredential.FromAccessToken(accessToken);

            if (credential == null)
            {
                throw new ArgumentException("Invalid access token");
            }
            var email = credential.UnderlyingCredential?.GetType().GetProperty("Email")?.GetValue(credential.UnderlyingCredential)?.ToString();
            var account = await _googleDriveRepository.LoadAccount(email, 0, credential);
            return account.Address.EncodeAsString();
        }
    }
}
