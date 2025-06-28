using AlgorandGoogleDriveAccount.BusinessLogic;
using AlgorandGoogleDriveAccount.Model;
using AlgorandGoogleDriveAccount.Repository;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
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
        private readonly IDevicePairingService _devicePairingService;
        private readonly IOptionsMonitor<Model.Configuration> _config;

        public BiatecMCPGoogle(
            IDistributedCache cache,
            GoogleDriveRepository googleDriveRepository,
            IDevicePairingService devicePairingService,
            IOptionsMonitor<Model.Configuration> config
            )
        {
            _cache = cache;
            _googleDriveRepository = googleDriveRepository;
            _devicePairingService = devicePairingService;
            _config = config;
        }

        public class GetAccountAddressResponse
        {
            public string Address { get; set; } = string.Empty;
            public string Errror { get; set; } = string.Empty;
        }

        [McpServerTool(Name = "getAlgorandAddress"), Description("Loads the Algorand account address stored at the google store.")]
        public async Task<GetAccountAddressResponse> GetAccountAddress(IMcpServer mcpServer)
        {
            try
            {
                var sessionId = mcpServer.SessionId;
                if (string.IsNullOrEmpty(sessionId)) throw new Exception("Unable to determine the session id");

                var accessToken = await _devicePairingService.GetDeviceAccessTokenAsync(sessionId);
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception($"Initiate google access and pair your device by signing at {_config.CurrentValue.Host}/pair.html?session={sessionId}");

                }
                var credential = GoogleCredential.FromAccessToken(accessToken);

                if (credential == null)
                {
                    throw new Exception($"Invalid access token. Initiate google access token by signing at {_config.CurrentValue.Host}/pair.html?session={sessionId}");
                }
                var email = credential.UnderlyingCredential?.GetType().GetProperty("Email")?.GetValue(credential.UnderlyingCredential)?.ToString();
                if (string.IsNullOrEmpty(email))
                {
                    throw new Exception($"Unable to determine the email from the access token. You can try login again at {_config.CurrentValue.Host}/pair.html?session={sessionId}");
                }
                var account = await _googleDriveRepository.LoadAccount(email, 0, credential);
                if (account == null)
                {
                    throw new Exception($"Unable to load the Algorand account from google store. Make sure the claim to access the google store to create files and load created files is granted to biatec app and try to login again. You can try login again at {_config.CurrentValue.Host}/pair.html?session={sessionId}");
                }
                return new GetAccountAddressResponse { Address = account.Address.EncodeAsString() };
            }
            catch (Exception ex)
            {
                return new GetAccountAddressResponse { Errror = ex.Message };
            }
        }
    }
}