using AlgorandGoogleDriveAccount.BusinessLogic;
using AlgorandGoogleDriveAccount.Model;
using Google.Apis.Auth.AspNetCore3;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AlgorandGoogleDriveAccount.Controllers
{
    [ApiController]
    [Route("api/device")]
    public class DevicePairingController : ControllerBase
    {
        private readonly IDevicePairingService _devicePairingService;
        private readonly ILogger<DevicePairingController> _logger;

        public DevicePairingController(
            IDevicePairingService devicePairingService,
            ILogger<DevicePairingController> logger)
        {
            _devicePairingService = devicePairingService;
            _logger = logger;
        }

        /// <summary>
        /// Serves the device pairing demo page
        /// </summary>
        [AllowAnonymous]
        [HttpGet("demo")]
        public IActionResult Demo()
        {
            return PhysicalFile(
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "device-pairing-demo.html"),
                "text/html"
            );
        }

        /// <summary>
        /// Initiates the device pairing process by redirecting to Google OAuth
        /// </summary>
        /// <param name="sessionId">Unique session ID for the device</param>
        /// <param name="deviceName">Optional device name for identification</param>
        /// <returns>Redirect to Google OAuth</returns>
        [AllowAnonymous]
        [HttpGet("pair-device")]
        public async Task<IActionResult> PairDevice(string sessionId, string deviceName = "Unknown Device")
        {
            try
            {
                await _devicePairingService.InitiatePairingAsync(sessionId, deviceName);

                var redirectUri = Url.Action("PairedDevice", "DevicePairing", new { sessionId }, Request.Scheme);
                
                return Challenge(new AuthenticationProperties
                {
                    RedirectUri = redirectUri,
                    Items = 
                    {
                        ["sessionId"] = sessionId,
                        ["deviceName"] = deviceName
                    }
                }, GoogleOpenIdConnectDefaults.AuthenticationScheme);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ProblemDetails 
                { 
                    Detail = ex.Message 
                });
            }
        }

        /// <summary>
        /// Callback endpoint after successful Google OAuth authentication
        /// Stores the auth token in Redis with 1-day expiration
        /// </summary>
        /// <param name="sessionId">Session ID from the pairing request</param>
        /// <returns>Device pairing result</returns>
        [Authorize]
        [HttpGet("paired-device")]
        public async Task<ActionResult<DevicePairingResponse>> PairedDevice(string sessionId)
        {
            // Get user info and tokens
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var refreshToken = await HttpContext.GetTokenAsync("refresh_token");

            var result = await _devicePairingService.ProcessPairingCallbackAsync(sessionId, email!, accessToken!, refreshToken);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            // Redirect to demo page with success message
            return Redirect($"/api/device/demo?sessionId={sessionId}");
        }

        /// <summary>
        /// Gets the access token for a paired device using session ID
        /// </summary>
        /// <param name="sessionId">Session ID of the paired device</param>
        /// <returns>Access token if device is paired and not expired</returns>
        [AllowAnonymous]
        [HttpGet("access-token/{sessionId}")]
        public async Task<ActionResult<string>> GetDeviceAccessToken(string sessionId)
        {
            try
            {
                var accessToken = await _devicePairingService.GetDeviceAccessTokenAsync(sessionId);

                if (accessToken == null)
                {
                    return NotFound(new ProblemDetails 
                    { 
                        Detail = "Device not found or session expired. Please pair the device again." 
                    });
                }

                return Ok(accessToken);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ProblemDetails 
                { 
                    Detail = ex.Message 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving access token for session ID: {sessionId}");
                return StatusCode(500, new ProblemDetails 
                { 
                    Detail = "An error occurred while retrieving the access token" 
                });
            }
        }

        /// <summary>
        /// Gets device information for a paired device
        /// </summary>
        /// <param name="sessionId">Session ID of the paired device</param>
        /// <returns>Device information if device is paired and not expired</returns>
        [AllowAnonymous]
        [HttpGet("info/{sessionId}")]
        public async Task<ActionResult<PairedDeviceInfo>> GetDeviceInfo(string sessionId)
        {
            try
            {
                var deviceInfo = await _devicePairingService.GetDeviceInfoAsync(sessionId);

                if (deviceInfo == null)
                {
                    return NotFound(new ProblemDetails 
                    { 
                        Detail = "Device not found or session expired" 
                    });
                }

                return Ok(deviceInfo);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ProblemDetails 
                { 
                    Detail = ex.Message 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving device info for session ID: {sessionId}");
                return StatusCode(500, new ProblemDetails 
                { 
                    Detail = "An error occurred while retrieving device information" 
                });
            }
        }

        /// <summary>
        /// Unpairs a device by removing it from cache
        /// </summary>
        /// <param name="sessionId">Session ID of the device to unpair</param>
        /// <returns>Result of the unpair operation</returns>
        [AllowAnonymous]
        [HttpDelete("unpair/{sessionId}")]
        public async Task<ActionResult<DevicePairingResponse>> UnpairDevice(string sessionId)
        {
            var result = await _devicePairingService.UnpairDeviceAsync(sessionId);

            if (!result.Success)
            {
                if (result.Message.Contains("required"))
                {
                    return BadRequest(result);
                }
                return StatusCode(500, result);
            }

            return Ok(result);
        }
    }
}