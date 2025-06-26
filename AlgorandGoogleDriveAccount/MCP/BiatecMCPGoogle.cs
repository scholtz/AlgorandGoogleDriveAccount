using AlgorandGoogleDriveAccount.BusinessLogic;
using AlgorandGoogleDriveAccount.Model;
using AlgorandGoogleDriveAccount.Repository;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
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
        private readonly IMcpEndpoint _mcpServer;
        private readonly IDevicePairingService _devicePairingService;

        public BiatecMCPGoogle(IDistributedCache cache, GoogleDriveRepository googleDriveRepository, IMcpEndpoint mcpServer, IDevicePairingService devicePairingService)
        {
            _cache = cache;
            _googleDriveRepository = googleDriveRepository;
            _mcpServer = mcpServer;
            _devicePairingService = devicePairingService;
        }

        [McpServerResource, Description("Loads the Algorand account address stored at the google store.")]
        public async Task<string> GetAccountAddress()
        {
            var sessionId = _mcpServer.SessionId;
            if (string.IsNullOrEmpty(sessionId)) throw new Exception("Unable to determine the session id");

            var accessToken  = await _devicePairingService.GetDeviceAccessTokenAsync(sessionId);
            var credential = GoogleCredential.FromAccessToken(accessToken);

            if (credential == null)
            {
                throw new ArgumentException($"Invalid access token. Initiate google access token by signing at https://google.biatec.io/pair.html?session={sessionId}");
            }
            var email = credential.UnderlyingCredential?.GetType().GetProperty("Email")?.GetValue(credential.UnderlyingCredential)?.ToString();
            var account = await _googleDriveRepository.LoadAccount(email, 0, credential);
            return account.Address.EncodeAsString();
        }
    }
}
