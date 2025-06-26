using Algorand;
using Algorand.Algod.Model.Transactions;
using AlgorandGoogleDriveAccount.Repository;
using Org.BouncyCastle.Crypto.Parameters;

namespace AlgorandGoogleDriveAccount.BusinessLogic
{
    public class DriveService : IDriveService
    {
        private readonly GoogleDriveRepository _googleDriveRepository;
        private readonly ILogger<DriveService> _logger;

        public DriveService(
            GoogleDriveRepository googleDriveRepository,
            ILogger<DriveService> logger)
        {
            _googleDriveRepository = googleDriveRepository;
            _logger = logger;
        }

        public async Task<byte[]> SignTransactionAsync(string email, byte[] txMsgPack)
        {
            if (string.IsNullOrEmpty(email))
            {
                throw new ArgumentException("Email is required", nameof(email));
            }

            if (txMsgPack == null || txMsgPack.Length == 0)
            {
                throw new ArgumentException("Transaction data is required", nameof(txMsgPack));
            }

            try
            {
                // Try to decode as signed transaction first (multisig scenario)
                var signedTxObj = Algorand.Utils.Encoder.DecodeFromMsgPack<SignedTransaction>(txMsgPack);
                if (signedTxObj?.MSig == null)
                {
                    throw new Exception("Signed transaction is not a multisig transaction.");
                }

                // Handle multisig transaction
                var account = await _googleDriveRepository.LoadAccount(email, 0);
                var address = account.Address.EncodeAsString();
                _logger?.LogInformation($"PasswordAccountSignMsig:{address}");

                var msig = new MultisigAddress(
                    signedTxObj.MSig.Version, 
                    signedTxObj.MSig.Threshold, 
                    new List<Ed25519PublicKeyParameters>(signedTxObj.MSig.Subsigs.Select(s => s.key)));
                    
                var signed = signedTxObj.Tx.Sign(msig, account);
                return Algorand.Utils.Encoder.EncodeToMsgPackOrdered(signed);
            }
            catch (Exception exc)
            {
                _logger?.LogDebug(exc, "Failed to decode signed transaction from MsgPack, trying as unsigned transaction.");
                
                try
                {
                    // Try to decode as unsigned transaction
                    var txObj = Algorand.Utils.Encoder.DecodeFromMsgPack<Transaction>(txMsgPack);
                    if (txObj == null)
                    {
                        throw new Exception("Unable to parse data as Transaction nor SignedTransaction");
                    }

                    var account = await _googleDriveRepository.LoadAccount(email, 0);
                    var address = account.Address.EncodeAsString();
                    _logger?.LogInformation($"PasswordAccountSign:{address}");
                    
                    var signed = txObj.Sign(account);
                    return Algorand.Utils.Encoder.EncodeToMsgPackOrdered(signed);
                }
                catch (Exception innerExc)
                {
                    _logger?.LogError(innerExc, "Failed to sign transaction");
                    throw new Exception("Unable to parse or sign transaction data", innerExc);
                }
            }
        }

        public async Task<string> GetAccountAddressAsync(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                throw new ArgumentException("Email is required", nameof(email));
            }

            try
            {
                var account = await _googleDriveRepository.LoadAccount(email, 0);
                return account.Address.EncodeAsString();
            }
            catch (Exception exc)
            {
                _logger?.LogError(exc, $"Error retrieving account address for email: {email}");
                throw;
            }
        }

        public async Task<string> GetAccessTokenAsync(string userEmail)
        {
            // This method would need to be implemented based on how you want to handle access tokens
            // For now, this is a placeholder that returns a not implemented exception
            // You might want to integrate this with your authentication system
            throw new NotImplementedException("Access token retrieval needs to be implemented based on your authentication flow");
        }
    }
}