using Algorand;
using Algorand.Algod;
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
            public string Error { get; set; } = string.Empty;
        }

        [McpServerTool(Name = "getAlgorandAddress"), Description("Loads the Algorand account address stored at the google store.")]
        public async Task<GetAccountAddressResponse> GetAccountAddress(IMcpServer mcpServer, [Description("You can use slot to identify the account. Default account is at slot 1. Second account can be slot 2, and so on.")] int slot = 1)
        {
            try
            {
                var sessionId = mcpServer.SessionId;
                if (string.IsNullOrEmpty(sessionId)) throw new Exception("Unable to determine the session id");

                var deviceInfo = await _devicePairingService.GetDeviceInfoInternalAsync(sessionId);
                if (string.IsNullOrEmpty(deviceInfo?.AccessToken))
                {
                    throw new Exception($"Initiate google access and pair your device by signing at {_config.CurrentValue.Host}/pair.html?session={sessionId}");
                }

                var credential = GoogleCredential.FromAccessToken(deviceInfo.AccessToken);

                if (credential == null)
                {
                    throw new Exception($"Invalid access token. Initiate google access token by signing at {_config.CurrentValue.Host}/pair.html?session={sessionId}");
                }

                if (string.IsNullOrEmpty(deviceInfo.Email))
                {
                    throw new Exception($"Unable to determine the email from the access token. You can try login again at {_config.CurrentValue.Host}/pair.html?session={sessionId}");
                }

                var account = await _googleDriveRepository.LoadAccount(deviceInfo.Email, slot, credential);
                if (account == null)
                {
                    throw new Exception($"Unable to load the Algorand account from google store. Make sure the claim to access the google store to create files and load created files is granted to biatec app and try to login again. You can try login again at {_config.CurrentValue.Host}/pair.html?session={sessionId}");
                }
                return new GetAccountAddressResponse { Address = account.Address.EncodeAsString() };
            }
            catch (UnauthorizedAccessException unauthorizedEx)
            {
                // Handle authorization exceptions from GoogleDriveRepository
                return new GetAccountAddressResponse
                {
                    Error = $"Google access token has expired or is invalid. Please re-authenticate at {_config.CurrentValue.Host}/pair.html?session={mcpServer.SessionId}. Details: {unauthorizedEx.Message}"
                };
            }
            catch (Google.GoogleApiException googleEx) when (googleEx.HttpStatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // Handle Google API unauthorized error specifically
                return new GetAccountAddressResponse
                {
                    Error = $"Google access token has expired or is invalid. Please re-authenticate at {_config.CurrentValue.Host}/pair.html?session={mcpServer.SessionId}"
                };
            }
            catch (Exception ex)
            {
                return new GetAccountAddressResponse { Error = ex.Message };
            }
        }

        public class TransferAssetResponse
        {
            public string TxId { get; set; } = string.Empty;
            public string Error { get; set; } = string.Empty;
            public string ErrorType { get; set; } = string.Empty;
            public string ExplorerLink { get; internal set; }
        }

        [McpServerTool(Name = "transferAsset"), Description("Allows the google store account transfer the assets.")]
        public async Task<TransferAssetResponse> TransferAsset(
            IMcpServer mcpServer,
            [Description("You can use slot to identify the account which will sign the transfer. Default account is at slot 1. Second account can be slot 2, and so on.")] int slot = 1,
            [Description("Receiver. If empty it will execute self signed transaction.")] string receiverAccount = "",
            [Description("ASA id to transfer. If asset id is 0, it will execute native token transaction.")] ulong assetId = 0,
            [Description("Amount to transfer")] ulong amount = 0,
            [Description("Note to attach to the transaction. If empty, it will not attach any note.")] string note = "",
            //[Description("Expiration duration in blocks. Valid until round is calculated as current block round plus validUntilDiff.")] ulong validUntilDiff = 1000,
            [Description("Blockchain genesis id. mainnet-v1.0 for algorand mainnet, testnet-v1.0 for algorand testnet")] string genesisId = "mainnet-v1.0"
            )
        {
            try
            {
                var sessionId = mcpServer.SessionId;
                if (string.IsNullOrEmpty(sessionId)) throw new Exception("Unable to determine the session id");

                var deviceInfo = await _devicePairingService.GetDeviceInfoInternalAsync(sessionId);
                if (string.IsNullOrEmpty(deviceInfo?.AccessToken))
                {
                    throw new Exception($"Initiate google access and pair your device by signing at {_config.CurrentValue.Host}/pair.html?session={sessionId}");
                }


                string ALGOD_API_ADDR = "";
                string ALGOD_API_TOKEN = "";


                switch (genesisId.ToLowerInvariant())
                {
                    case "mainnet-v1.0":
                        ALGOD_API_ADDR = "https://mainnet-api.4160.nodely.dev";
                        ALGOD_API_TOKEN = "";
                        break;
                    case "testnet-v1.0":
                        ALGOD_API_ADDR = "https://testnet-api.4160.nodely.dev";
                        ALGOD_API_TOKEN = "";
                        break;
                    default:
                        throw new Exception($"Unsupported genesis id: {genesisId}");
                }

                var httpClient = HttpClientConfigurator.ConfigureHttpClient(ALGOD_API_ADDR, ALGOD_API_TOKEN);
                DefaultApi algodApiInstance = new DefaultApi(httpClient);

                var credential = GoogleCredential.FromAccessToken(deviceInfo.AccessToken);

                if (credential == null)
                {
                    throw new Exception($"Invalid access token. Initiate google access token by signing at {_config.CurrentValue.Host}/pair.html?session={sessionId}");
                }

                if (string.IsNullOrEmpty(deviceInfo.Email))
                {
                    throw new Exception($"Unable to determine the email from the access token. You can try login again at {_config.CurrentValue.Host}/pair.html?session={sessionId}");
                }

                var account = await _googleDriveRepository.LoadAccount(deviceInfo.Email, slot, credential);
                if (account == null)
                {
                    throw new Exception($"Unable to load the Algorand account from google store. Make sure the claim to access the google store to create files and load created files is granted to biatec app and try to login again. You can try login again at {_config.CurrentValue.Host}/pair.html?session={sessionId}");
                }
                if (assetId == 0)
                {
                    var result = await account.MakePaymentTo(new Algorand.Address(receiverAccount), amount, note, algodApiInstance);
                    return new TransferAssetResponse { TxId = result.Txid, ExplorerLink = $"https://allo.info/tx/{result.Txid}" };
                }
                else
                {
                    var result = await account.MakeAssetTransferTo(new Algorand.Address(receiverAccount), amount, assetId, note, algodApiInstance);
                    return new TransferAssetResponse { TxId = result.Txid, ExplorerLink = $"https://allo.info/tx/{result.Txid}" };
                }
            }
            catch (Algorand.ApiException<Algorand.Algod.Model.ErrorResponse> ex)
            {
                // Handle authorization exceptions from GoogleDriveRepository
                return new TransferAssetResponse
                {
                    Error = ex.Result.Message,
                    ErrorType = ex.GetType().ToString()
                };
            }catch (UnauthorizedAccessException unauthorizedEx)
            {
                // Handle authorization exceptions from GoogleDriveRepository
                return new TransferAssetResponse
                {
                    Error = $"Google access token has expired or is invalid. Please re-authenticate at {_config.CurrentValue.Host}/pair.html?session={mcpServer.SessionId}. Details: {unauthorizedEx.Message}",
                    ErrorType = unauthorizedEx.GetType().ToString()

                };
            }
            catch (Google.GoogleApiException googleEx) when (googleEx.HttpStatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // Handle Google API unauthorized error specifically
                return new TransferAssetResponse
                {
                    Error = $"Google access token has expired or is invalid. Please re-authenticate at {_config.CurrentValue.Host}/pair.html?session={mcpServer.SessionId}",
                    ErrorType = googleEx.GetType().ToString()
                };
            }
            catch (Exception ex)
            {
                return new TransferAssetResponse
                {
                    Error = ex.Message,
                    ErrorType = ex.GetType().ToString()
                };
            }
        }
    }
}