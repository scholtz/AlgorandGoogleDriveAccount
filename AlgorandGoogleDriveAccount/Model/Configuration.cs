namespace AlgorandGoogleDriveAccount.Model
{
    public class Configuration
    {
        public string Host { get; set; } = "https://google.biatec.io";
        public string StorageFolderName { get; set; } = "Biatec";
        public string StorageFileName { get; set; } = "AVMAccount.dat";
        public string ApplicationName { get; set; } = "Biatec";
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
    }

    public class RedisConfiguration
    {
        public string ConnectionString { get; set; } = "localhost:6379";
    }

    public class CorsConfiguration
    {
        public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    }

    public class CrossAccountProtectionConfiguration
    {
        public bool Enabled { get; set; } = false; // Changed from true to false (disabled by default)
        public bool RequireSecurityCheck { get; set; } = true;
        public int SecurityCheckIntervalMinutes { get; set; } = 60;
        public bool AutoReportEvents { get; set; } = true;
        public bool EnableGranularConsent { get; set; } = false; // New setting for granular consent
        public bool FilterInternalScopes { get; set; } = true; // New setting to filter internal Google scopes
    }

    public class AlgodConfiguration
    {
        public Dictionary<string, AlgodNetworkSettings> Networks { get; set; } = new Dictionary<string, AlgodNetworkSettings>();
    }

    public class AlgodNetworkSettings
    {
        public string ApiAddress { get; set; } = string.Empty;
        public string ApiToken { get; set; } = string.Empty;
        public string ExplorerBaseUrl { get; set; } = "https://allo.info/tx/";
    }
}
