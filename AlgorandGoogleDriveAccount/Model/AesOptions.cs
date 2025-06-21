namespace AlgorandGoogleDriveAccount.Model
{
    public class AesOptions
    {
        public string Key { get; set; } = string.Empty; // base64 encoded
        public string IV { get; set; } = string.Empty;  // base64 encoded
    }
}
