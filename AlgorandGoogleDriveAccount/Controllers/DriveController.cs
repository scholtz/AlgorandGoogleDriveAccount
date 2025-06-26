using AlgorandGoogleDriveAccount.BusinessLogic;
using Google.Apis.Auth.AspNetCore3;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AlgorandGoogleDriveAccount.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/drive")]
    public class DriveController : ControllerBase
    {
        private readonly IDriveService _driveService;
        private readonly ILogger<DriveController> _logger;

        public DriveController(
            IDriveService driveService,
            ILogger<DriveController> logger)
        {
            _driveService = driveService;
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
            try
            {
                var email = User.FindFirst(ClaimTypes.Email)?.Value;
                if (string.IsNullOrEmpty(email))
                {
                    return BadRequest(new ProblemDetails() { Detail = "Email not found in claims. Please login first." });
                }

                var signedTransaction = await _driveService.SignTransactionAsync(email, txMsgPack);
                return Ok(signedTransaction);
            }
            catch (ArgumentException exc)
            {
                return BadRequest(new ProblemDetails() { Detail = exc.Message });
            }
            catch (Exception exc)
            {
                _logger?.LogError(exc, "Error signing transaction");
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
        /// Get the account address for the authenticated user
        /// </summary>
        /// <returns></returns>
        [Authorize]
        [HttpGet("address")]
        public async Task<ActionResult<string>> GetAddress()
        {
            try
            {
                var email = User.FindFirst(ClaimTypes.Email)?.Value;
                if (string.IsNullOrEmpty(email))
                {
                    return BadRequest(new ProblemDetails() { Detail = "Email not found in claims. Please login first." });
                }

                var address = await _driveService.GetAccountAddressAsync(email);
                return Ok(address);
            }
            catch (ArgumentException exc)
            {
                return BadRequest(new ProblemDetails() { Detail = exc.Message });
            }
            catch (Exception exc)
            {
                _logger?.LogError(exc, "Error retrieving account address");
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
}