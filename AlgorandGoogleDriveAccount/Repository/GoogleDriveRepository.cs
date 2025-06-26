using Algorand.Algod.Model;
using AlgorandGoogleDriveAccount.Helper;
using Google.Apis.Auth.AspNetCore3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.Extensions.Options;
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
            var cred = googleCredential ?? await _auth.GetCredentialAsync();

            var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = cred,
                ApplicationName = _config.CurrentValue.ApplicationName,
            });

            string folderName = _config.CurrentValue.StorageFolderName;
            string fileName = _config.CurrentValue.StorageFileName;

            // Try to find the folder "Biatec"
            var folderRequest = service.Files.List();
            folderRequest.Q = $"mimeType = 'application/vnd.google-apps.folder' and name = '{folderName}' and trashed = false";
            folderRequest.Fields = "files(id, name)";
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

            var decryptedData = AesEncryptionHelper.Decrypt(fileContent, Convert.FromBase64String(_aes.CurrentValue.Key), Convert.FromBase64String(_aes.CurrentValue.IV), email);
            var account = AlgorandARC76AccountDotNet.ARC76.GetEmailAccount(email, Encoding.UTF8.GetString(decryptedData), slot);
            return account;
        }
    }
}
