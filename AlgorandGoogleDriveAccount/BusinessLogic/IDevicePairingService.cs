using AlgorandGoogleDriveAccount.Model;

namespace AlgorandGoogleDriveAccount.BusinessLogic
{
    public interface IDevicePairingService
    {
        Task<string> InitiatePairingAsync(string sessionId, string deviceName);
        Task<DevicePairingResponse> ProcessPairingCallbackAsync(string sessionId, string email, string accessToken, string? refreshToken);
        Task<string?> GetDeviceAccessTokenAsync(string sessionId);
        Task<PairedDeviceInfo?> GetDeviceInfoAsync(string sessionId);
        Task<DevicePairingResponse> UnpairDeviceAsync(string sessionId);
    }
}