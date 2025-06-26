using Algorand;
using Algorand.Algod.Model.Transactions;
using AlgorandGoogleDriveAccount.Repository;
using Google.Apis.Auth.AspNetCore3;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Org.BouncyCastle.Crypto.Parameters;
using System.Security.Claims;

[Authorize]
[ApiController]
[Route("api/drive")]
public class DriveController : ControllerBase
{
    private readonly GoogleDriveRepository _googleDriveRepository;
    private readonly ILogger<DriveController> _logger;

    public DriveController(
        GoogleDriveRepository googleDriveRepository,
        ILogger<DriveController> logger
        )
    {
        _googleDriveRepository = googleDriveRepository;
        _logger = logger;
    }

    /// <summary>
    /// sign unsigned transaction, or combine signed multisig transaction with the account's private key
    /// </summary>
    /// <returns></returns>
    [Authorize]
    [HttpPost("sign")]
    public async Task<ActionResult<byte[]>> Sign([FromForm] byte[] txMsgPack)
    {
        var messagePack = new byte[0];
        try
        {
            try
            {
                var signedTxObj = Algorand.Utils.Encoder.DecodeFromMsgPack<SignedTransaction>(txMsgPack) ?? throw new Exception("Error in signedTxBytes");
                if (signedTxObj.MSig == null)
                {
                    // signed basic tx
                    throw new Exception("Signed transaction is not a multisig transaction.");
                }
                else
                {
                    // signed msig tx

                    var email = User.FindFirst(ClaimTypes.Email)?.Value;
                    if (string.IsNullOrEmpty(email)) throw new Exception("Email not found in claims. Please login first.");
                    var account = await _googleDriveRepository.LoadAccount(email, 0);

                    var address = account.Address.EncodeAsString();
                    _logger?.LogInformation($"PasswordAccountSignMsig:{address}");

                    var msig = new MultisigAddress(signedTxObj.MSig.Version, signedTxObj.MSig.Threshold, new List<Ed25519PublicKeyParameters>(signedTxObj.MSig.Subsigs.Select(s => s.key)));
                    var signed = signedTxObj.Tx.Sign(msig, account);

                    messagePack = Algorand.Utils.Encoder.EncodeToMsgPackOrdered(signed);
                }
            }
            catch (Exception exc)
            {
                _logger?.LogDebug(exc, "Failed to decode signed transaction from MsgPack.");
                var txObj = Algorand.Utils.Encoder.DecodeFromMsgPack<Transaction>(txMsgPack) ?? throw new Exception("Unable to parse data as Transaction nor SignedTransaction");
                // usinged tx

                var email = User.FindFirst(ClaimTypes.Email)?.Value;
                if (string.IsNullOrEmpty(email)) throw new Exception("Email not found in claims. Please login first.");
                var account = await _googleDriveRepository.LoadAccount(email, 0);

                var address = account.Address.EncodeAsString();
                _logger?.LogInformation($"PasswordAccountSignMsig:{address}");
                var signed = txObj.Sign(account);
                messagePack = Algorand.Utils.Encoder.EncodeToMsgPackOrdered(signed);
            }

            return Ok(messagePack);
        }
        catch (Exception exc)
        {
            _logger?.LogError(exc.Message);
            return BadRequest(new ProblemDetails() { Detail = exc.Message });
        }
    }

    /// <summary>
    /// Get the current user's access token for use with MCP server
    /// </summary>
    /// <returns>Access token for Google Drive API</returns>
    [Authorize]
    [HttpGet("access-token")]
    public async Task<ActionResult<string>> GetAccessToken()
    {
        try
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                return BadRequest(new ProblemDetails() { Detail = "No access token found. Please login first." });
            }
            
            return Ok(accessToken);
        }
        catch (Exception exc)
        {
            _logger?.LogError(exc, "Error retrieving access token");
            return BadRequest(new ProblemDetails() { Detail = exc.Message });
        }
    }

    /// <summary>
    /// sign unsigned transaction, or combine signed multisig transaction with the account's private key
    /// </summary>
    /// <returns></returns>
    [Authorize]
    [HttpGet("address")]
    public async Task<ActionResult<string>> GetAddress()
    {
        try
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email)) throw new Exception("Email not found in claims. Please login first.");
            var account = await _googleDriveRepository.LoadAccount(email, 0);
            var address = account.Address.EncodeAsString();
            return Ok(address);
        }
        catch (Exception exc)
        {
            _logger?.LogError(exc.Message);
            return BadRequest(new ProblemDetails() { Detail = exc.Message });
        }
    }

    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login(string redirectUri = "https://localhost:44305/swagger/")
    {
        return Challenge(new AuthenticationProperties
        {
            RedirectUri = redirectUri
        }, GoogleOpenIdConnectDefaults.AuthenticationScheme);
    }

    [Authorize]
    [HttpGet("logout")]
    public IActionResult Logout(string redirectUri = "https://localhost:44305/swagger/")
    {
        return SignOut(new AuthenticationProperties
        {
            RedirectUri = redirectUri
        }, CookieAuthenticationDefaults.AuthenticationScheme);
    }
}