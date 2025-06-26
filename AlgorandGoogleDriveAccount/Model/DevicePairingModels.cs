namespace AlgorandGoogleDriveAccount.Model
{
    public class DevicePairingRequest
    {
        public string SessionId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
    }

    public class DevicePairingResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? SessionId { get; set; }
    }

    public class PairedDeviceInfo
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public DateTime PairedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}