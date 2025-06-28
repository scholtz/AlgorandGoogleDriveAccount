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
        public bool Enabled { get; set; } = true;
        public bool RequireSecurityCheck { get; set; } = true;
        public int SecurityCheckIntervalMinutes { get; set; } = 60;
        public bool AutoReportEvents { get; set; } = true;
    }
}
