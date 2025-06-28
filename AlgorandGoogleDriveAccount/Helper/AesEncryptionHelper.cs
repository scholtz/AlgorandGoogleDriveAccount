using AlgorandGoogleDriveAccount.Model;
using System.Security.Cryptography;
using System.Text;

namespace AlgorandGoogleDriveAccount.Helper
{
    public static class AesEncryptionHelper
    {
        /// <summary>
        /// Encrypts the specified data using AES encryption with a 256-bit key and a 128-bit initialization vector
        /// (IV).
        /// </summary>
        /// <remarks>This method uses AES encryption in CBC mode with PKCS7 padding. The encryption key
        /// and IV are derived from the provided <paramref name="key"/> and <paramref name="iv"/> using the specified
        /// <paramref name="email"/> as a salt. Ensure that the same key, IV, and email are used for decryption to
        /// successfully recover the original data.</remarks>
        /// <param name="data">The data to be encrypted. Must not be null or empty.</param>
        /// <param name="key">The base key material used to derive the encryption key. Must not be null or empty.</param>
        /// <param name="iv">The base initialization vector material used to derive the IV. Must not be null or empty.</param>
        /// <param name="email">An email address used as a salt for key and IV derivation. Must not be null or empty.</param>
        /// <returns>A byte array containing the encrypted data.</returns>
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
        /// <summary>
        /// Decrypts the specified cipher data using AES encryption with a derived key and initialization vector (IV).
        /// </summary>
        /// <remarks>This method uses AES encryption in CBC mode with PKCS7 padding. The key and IV are
        /// derived from the provided <paramref name="key"/> and <paramref name="iv"/> using the specified <paramref
        /// name="email"/> as a salt. Ensure that the same key, IV, and email are used for both encryption and
        /// decryption to maintain data integrity.</remarks>
        /// <param name="cipherData">The encrypted data to be decrypted.</param>
        /// <param name="key">The base key used for deriving the encryption key. Must not be null or empty.</param>
        /// <param name="iv">The base initialization vector used for deriving the encryption IV. Must not be null or empty.</param>
        /// <param name="email">The email address used as a salt for key and IV derivation. Must not be null or empty.</param>
        /// <returns>The decrypted data as a byte array.</returns>
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
        /// <summary>
        /// Performs a cryptographic transformation on the specified data using the provided <see
        /// cref="ICryptoTransform"/>.
        /// </summary>
        /// <remarks>This method uses a <see cref="CryptoStream"/> to apply the specified cryptographic
        /// transformation. Ensure that the <paramref name="transform"/> is properly configured for the intended
        /// operation (e.g., encryption or decryption).</remarks>
        /// <param name="data">The byte array containing the data to be transformed. Cannot be null or empty.</param>
        /// <param name="transform">The cryptographic transformation to apply to the data. Cannot be null.</param>
        /// <returns>A byte array containing the transformed data.</returns>
        private static byte[] PerformCryptography(byte[] data, ICryptoTransform transform)
        {
            using var ms = new MemoryStream();
            using var cryptoStream = new CryptoStream(ms, transform, CryptoStreamMode.Write);
            cryptoStream.Write(data, 0, data.Length);
            cryptoStream.FlushFinalBlock();
            return ms.ToArray();
        }
        /// <summary>
        /// Derives a cryptographic key by hashing a combination of a base value and an email address.
        /// </summary>
        /// <remarks>This method uses the SHA-256 hashing algorithm to derive the key. The resulting key
        /// is truncated to the specified length. Ensure that the <paramref name="length"/> parameter is appropriate for
        /// your cryptographic requirements.</remarks>
        /// <param name="baseValue">The base value used as part of the key derivation process. This must be a non-null byte array.</param>
        /// <param name="email">The email address used to personalize the derived key. This must be a non-null, non-empty string.</param>
        /// <param name="length">The desired length of the derived key, in bytes. Must be a positive integer less than or equal to the hash
        /// length.</param>
        /// <returns>A byte array containing the derived key truncated to the specified length.</returns>
        private static byte[] DeriveKey(byte[] baseValue, string email, int length)
        {
            using var sha256 = SHA256.Create();
            var combined = baseValue.Concat(Encoding.UTF8.GetBytes(email)).ToArray();
            var hash = sha256.ComputeHash(combined);
            return hash.Take(length).ToArray(); // truncate to required length
        }
        /// <summary>
        /// Generates a unique identifier based on the provided AES key and initialization vector (IV).
        /// </summary>
        /// <remarks>The method computes a SHA-256 hash of the concatenated key and IV, and returns the
        /// first 6 bytes of the hash as a lowercase hexadecimal string.</remarks>
        /// <param name="aesOptions">An <see cref="AesOptions"/> object containing the Base64-encoded AES key and IV.</param>
        /// <returns>A hexadecimal string representing the unique identifier, derived from the key and IV. The string is 6 bytes
        /// in length.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the <paramref name="aesOptions"/> contains a null key or IV.</exception>
        /// <exception cref="ArgumentException">Thrown if the key length is not 32 bytes or the IV length is not 16 bytes.</exception>
        public static string MakeAesId(AesOptions aesOptions)
        {
            var key = Convert.FromBase64String(aesOptions.Key);
            var iv = Convert.FromBase64String(aesOptions.IV);
            if (key == null || iv == null)
            {
                throw new ArgumentNullException("Key and IV cannot be null");
            }
            if (key.Length != 32 || iv.Length != 16)
            {
                throw new ArgumentException("Invalid key or IV length");
            }
            // hash the key and IV together to create a unique identifier
            // output is a hex string of length 6 bytes
            using var sha256 = SHA256.Create();
            var combined = key.Concat(iv).ToArray();
            var hash = sha256.ComputeHash(combined);
            return BitConverter.ToString(hash.Take(6).ToArray()).Replace("-", "").ToLowerInvariant();

        }
    }
}
