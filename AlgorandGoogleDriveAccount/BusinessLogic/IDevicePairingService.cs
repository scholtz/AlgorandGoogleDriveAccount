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

    public interface ICrossAccountProtectionService
    {
        Task<bool> IsUserAccountSecureAsync(string userId);
        Task<CrossAccountProtectionStatus> CheckSecurityStatusAsync(string accessToken);
        Task<bool> ReportSecurityEventAsync(string userId, SecurityEventType eventType, string? details = null);
        Task<ReauthenticationResult> RequestReauthenticationAsync(string userId, string[] scopes);
    }

    public interface IPortfolioValuationService
    {
        Task<decimal> GetPortfolioValueAsync(string email);
        Task<ServiceTier> GetServiceTierAsync(string email);
        Task<PortfolioSummary> GetPortfolioSummaryAsync(string email);
        Task UpdatePortfolioValuationAsync(string email);
    }

    public enum ServiceTier
    {
        Free,
        Professional,
        Enterprise
    }

    public class PortfolioSummary
    {
        public decimal TotalValueEur { get; set; }
        public ServiceTier CurrentTier { get; set; }
        public DateTime LastUpdated { get; set; }
        public int AccountCount { get; set; }
        public decimal AlgorandBalance { get; set; }
        public decimal AssetValue { get; set; }
    }

    public enum SecurityEventType
    {
        SuspiciousLogin,
        UnusualAccess,
        DataBreach,
        AccountCompromise,
        PhishingAttempt
    }

    public class CrossAccountProtectionStatus
    {
        public bool IsSecure { get; set; }
        public string[] SecurityWarnings { get; set; } = Array.Empty<string>();
        public DateTime LastSecurityCheck { get; set; }
        public bool RequiresReauthentication { get; set; }
    }

    public class ReauthenticationResult
    {
        public bool Success { get; set; }
        public string? ReauthUrl { get; set; }
        public string? ErrorMessage { get; set; }
    }
}