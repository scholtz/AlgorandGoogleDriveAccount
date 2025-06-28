using Algorand.Algod.Model;
using AlgorandGoogleDriveAccount.Helper;
using Google.Apis.Auth.AspNetCore3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace AlgorandGoogleDriveAccount.Repository
{
    public class GoogleDriveRepository
    {
        private readonly IGoogleAuthProvider _auth;
        private readonly IOptionsMonitor<Model.Configuration> _config;
        private readonly IOptionsMonitor<Model.AesOptions> _aes;

        public GoogleDriveRepository(
            IGoogleAuthProvider auth,
            IOptionsMonitor<Model.Configuration> config,
            IOptionsMonitor<Model.AesOptions> aes
            )
        {
            _auth = auth;
            _config = config;
            _aes = aes;
        }
        public async Task<Account> LoadAccount(string email, int slot, GoogleCredential? googleCredential = null)
        {
            try
            {
                var cred = googleCredential ?? await _auth.GetCredentialAsync();

                var service = new DriveService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = cred,
                    ApplicationName = _config.CurrentValue.ApplicationName,
                });

                string folderName = _config.CurrentValue.StorageFolderName;
                string fileName = _config.CurrentValue.StorageFileName;

                var aesid = AesEncryptionHelper.MakeAesId(_aes.CurrentValue);

                fileName = fileName.Replace("%AESID%", aesid);

                // Try to find the folder "Biatec"
                var folderRequest = service.Files.List();
                folderRequest.Q = $"mimeType = 'application/vnd.google-apps.folder' and name = '{folderName}' and trashed = false";
                folderRequest.Fields = "files(id, name)";

                try
                {
                    var folderResult = await folderRequest.ExecuteAsync();

                    var folder = folderResult.Files.FirstOrDefault();
                    if (folder == null)
                    {
                        // Create the folder if not found
                        var folderMetadata = new Google.Apis.Drive.v3.Data.File
                        {
                            Name = folderName,
                            MimeType = "application/vnd.google-apps.folder"
                        };

                        var folderCreateRequest = service.Files.Create(folderMetadata);
                        folderCreateRequest.Fields = "id";
                        folder = await folderCreateRequest.ExecuteAsync();
                    }

                    // Check if file with same name already exists in that folder
                    var fileCheckRequest = service.Files.List();
                    fileCheckRequest.Q = $"name = '{fileName}' and '{folder.Id}' in parents and trashed = false";
                    fileCheckRequest.Fields = "files(id, name)";
                    var existingFiles = await fileCheckRequest.ExecuteAsync();

                    if (!existingFiles.Files.Any())
                    {
                        // Prepare file metadata
                        var fileMetadata = new Google.Apis.Drive.v3.Data.File
                        {
                            Name = fileName,
                            MimeType = "text/plain",
                            Parents = new List<string> { folder.Id }
                        };

                        var newAccount = new Account();
                        var encryptedData = AesEncryptionHelper.Encrypt(Encoding.UTF8.GetBytes(newAccount.ToMnemonic()), Convert.FromBase64String(_aes.CurrentValue.Key), Convert.FromBase64String(_aes.CurrentValue.IV), email);

                        // File content
                        var stream = new MemoryStream(encryptedData);

                        // Upload request
                        var request = service.Files.Create(fileMetadata, stream, "text/plain");
                        request.Fields = "id, name";

                        var result = await request.UploadAsync();

                        if (result.Status != Google.Apis.Upload.UploadStatus.Completed)
                        {
                            throw new Exception("File upload failed: " + result.Exception?.Message);
                        }
                        existingFiles = await fileCheckRequest.ExecuteAsync();

                        if (!existingFiles.Files.Any())
                        {
                            throw new Exception("File upload failed, file not found after upload.");
                        }
                    }

                    var file = existingFiles.Files.FirstOrDefault() ?? throw new Exception("File not found after upload.");

                    var requestDownload = service.Files.Get(file.Id);
                    var streamDownloadFile = new MemoryStream();
                    requestDownload.MediaDownloader.ProgressChanged += progress =>
                    {
                        // Optional: log download progress
                    };

                    await requestDownload.DownloadAsync(streamDownloadFile);
                    streamDownloadFile.Position = 0;
                    var fileContent = streamDownloadFile.ToArray();

                    try
                    {
                        var decryptedData = AesEncryptionHelper.Decrypt(fileContent, Convert.FromBase64String(_aes.CurrentValue.Key), Convert.FromBase64String(_aes.CurrentValue.IV), email);
                        var account = AlgorandARC76AccountDotNet.ARC76.GetEmailAccount(email, Encoding.UTF8.GetString(decryptedData), slot);
                        return account;
                    }
                    catch (CryptographicException cryptoEx)
                    {
                        // Handle padding or decryption errors specifically
                        if (cryptoEx.Message.Contains("Padding"))
                        {
                            throw new Exception($"Decryption failed for email '{email}'. This might be due to: 1) Email case mismatch (ensure exact email case), 2) File was encrypted with different credentials, 3) Corrupted file data. File size: {fileContent.Length} bytes. Try using the diagnostic endpoint: /api/device/diagnose/{{sessionId}}. Original error: {cryptoEx.Message}");
                        }
                        throw new Exception($"Cryptographic error during decryption for email '{email}': {cryptoEx.Message}", cryptoEx);
                    }
                    catch (Exception decryptEx)
                    {
                        throw new Exception($"Failed to decrypt account data for email '{email}'. File size: {fileContent.Length} bytes. Error: {decryptEx.Message}", decryptEx);
                    }
                }
                catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException($"Google Drive access denied. The access token may be expired or invalid. Error: {ex.Message}", ex);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Re-throw authorization exceptions as-is
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading account from Google Drive for email {email}: {ex.Message}", ex);
            }
        }
    }
}
