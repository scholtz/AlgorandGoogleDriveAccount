using AlgorandGoogleDriveAccount.Model;

namespace AlgorandGoogleDriveAccount.BusinessLogic
{
    public interface IDevicePairingService
    {
        Task<string> InitiatePairingAsync(string sessionId, string deviceName);
        Task<DevicePairingResponse> ProcessPairingCallbackAsync(string sessionId, string email, string accessToken, string? refreshToken);
        Task<string?> GetDeviceAccessTokenAsync(string sessionId);
        Task<PairedDeviceInfo?> GetDeviceInfoAsync(string sessionId);
        Task<PairedDeviceInfo?> GetDeviceInfoInternalAsync(string sessionId);
        Task<DevicePairingResponse> UnpairDeviceAsync(string sessionId);
    }

    public interface IGoogleAuthorizationService
    {
        Task<string> GetIncrementalAuthorizationUrlAsync(string[] additionalScopes, string? sessionId = null, string? redirectUri = null);
        Task<bool> HasScopeAsync(string scope, string? sessionId = null);
        Task<string?> GetAccessTokenWithScopeAsync(string scope, string? sessionId = null);
    }
}