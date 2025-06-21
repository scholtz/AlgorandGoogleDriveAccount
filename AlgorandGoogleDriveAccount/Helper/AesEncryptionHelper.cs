using System.Security.Cryptography;
using System.Text;

namespace AlgorandGoogleDriveAccount.Helper
{
    public static class AesEncryptionHelper
    {
        public static byte[] Encrypt(byte[] data, byte[] key, byte[] iv, string email)
        {
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key = DeriveKey(key, email, 32);
            aes.IV = DeriveKey(iv, email, 16);
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            return PerformCryptography(data, encryptor);
        }

        public static byte[] Decrypt(byte[] cipherData, byte[] key, byte[] iv, string email)
        {
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key = DeriveKey(key, email, 32);
            aes.IV = DeriveKey(iv, email, 16);
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            return PerformCryptography(cipherData, decryptor);
        }

        private static byte[] PerformCryptography(byte[] data, ICryptoTransform transform)
        {
            using var ms = new MemoryStream();
            using var cryptoStream = new CryptoStream(ms, transform, CryptoStreamMode.Write);
            cryptoStream.Write(data, 0, data.Length);
            cryptoStream.FlushFinalBlock();
            return ms.ToArray();
        }
        private static byte[] DeriveKey(byte[] baseValue, string email, int length)
        {
            using var sha256 = SHA256.Create();
            var combined = baseValue.Concat(Encoding.UTF8.GetBytes(email)).ToArray();
            var hash = sha256.ComputeHash(combined);
            return hash.Take(length).ToArray(); // truncate to required length
        }
    }
}
