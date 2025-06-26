namespace AlgorandGoogleDriveAccount.Model
{
    public class Configuration
    {
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
}
