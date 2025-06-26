using AlgorandGoogleDriveAccount.Model;
using Google.Apis.Auth.AspNetCore3;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Claims;
using System.Text.Json;

namespace AlgorandGoogleDriveAccount.Controllers
{
    [ApiController]
    [Route("api/device")]
    public class DevicePairingController : ControllerBase
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<DevicePairingController> _logger;
        private const int CacheExpirationDays = 1;

        public DevicePairingController(
            IDistributedCache cache,
            ILogger<DevicePairingController> logger)
        {
            _cache = cache;
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
            if (string.IsNullOrEmpty(sessionId))
            {
                return BadRequest(new ProblemDetails 
                { 
                    Detail = "Session ID is required for device pairing" 
                });
            }

            // Store the session info temporarily for the callback
            var tempSessionData = new
            {
                SessionId = sessionId,
                DeviceName = deviceName,
                InitiatedAt = DateTime.UtcNow
            };

            // Store in cache temporarily (5 minutes should be enough for OAuth flow)
            var tempKey = $"temp_session:{sessionId}";
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            };

            await _cache.SetStringAsync(tempKey, JsonSerializer.Serialize(tempSessionData), options);

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
            try
            {
                if (string.IsNullOrEmpty(sessionId))
                {
                    return BadRequest(new DevicePairingResponse
                    {
                        Success = false,
                        Message = "Session ID is required"
                    });
                }

                // Get temporary session data
                var tempKey = $"temp_session:{sessionId}";
                var tempDataJson = await _cache.GetStringAsync(tempKey);
                
                if (string.IsNullOrEmpty(tempDataJson))
                {
                    return BadRequest(new DevicePairingResponse
                    {
                        Success = false,
                        Message = "Session not found or expired. Please initiate pairing again."
                    });
                }

                var tempData = JsonSerializer.Deserialize<JsonElement>(tempDataJson);
                
                // Get user info and tokens
                var email = User.FindFirst(ClaimTypes.Email)?.Value;
                if (string.IsNullOrEmpty(email))
                {
                    return BadRequest(new DevicePairingResponse
                    {
                        Success = false,
                        Message = "Email not found in claims. Authentication failed."
                    });
                }

                var accessToken = await HttpContext.GetTokenAsync("access_token");
                var refreshToken = await HttpContext.GetTokenAsync("refresh_token");

                if (string.IsNullOrEmpty(accessToken))
                {
                    return BadRequest(new DevicePairingResponse
                    {
                        Success = false,
                        Message = "No access token found. Authentication failed."
                    });
                }

                // Extract device name from temp data
                var deviceName = "Unknown Device";
                if (tempData.TryGetProperty("DeviceName", out var deviceNameElement))
                {
                    deviceName = deviceNameElement.GetString() ?? "Unknown Device";
                }

                // Create device info
                var deviceInfo = new PairedDeviceInfo
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken ?? string.Empty,
                    Email = email,
                    DeviceName = deviceName,
                    PairedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(CacheExpirationDays)
                };

                // Store in Redis with 1-day expiration
                var cacheKey = $"device_session:{sessionId}";
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(CacheExpirationDays)
                };

                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(deviceInfo), cacheOptions);

                // Clean up temporary session data
                await _cache.RemoveAsync(tempKey);

                _logger.LogInformation($"Device paired successfully. SessionId: {sessionId}, Email: {email}");

                // Redirect to demo page with success message
                return Redirect($"/api/device/demo?sessionId={sessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error pairing device with session ID: {sessionId}");
                return StatusCode(500, new DevicePairingResponse
                {
                    Success = false,
                    Message = "An error occurred while pairing the device"
                });
            }
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
                if (string.IsNullOrEmpty(sessionId))
                {
                    return BadRequest(new ProblemDetails 
                    { 
                        Detail = "Session ID is required" 
                    });
                }

                var cacheKey = $"device_session:{sessionId}";
                var deviceInfoJson = await _cache.GetStringAsync(cacheKey);

                if (string.IsNullOrEmpty(deviceInfoJson))
                {
                    return NotFound(new ProblemDetails 
                    { 
                        Detail = "Device not found or session expired. Please pair the device again." 
                    });
                }

                var deviceInfo = JsonSerializer.Deserialize<PairedDeviceInfo>(deviceInfoJson);
                
                if (deviceInfo == null)
                {
                    return NotFound(new ProblemDetails 
                    { 
                        Detail = "Invalid device information. Please pair the device again." 
                    });
                }

                // Check if token is expired
                if (DateTime.UtcNow > deviceInfo.ExpiresAt)
                {
                    await _cache.RemoveAsync(cacheKey);
                    return NotFound(new ProblemDetails 
                    { 
                        Detail = "Device session expired. Please pair the device again." 
                    });
                }

                return Ok(deviceInfo.AccessToken);
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
                if (string.IsNullOrEmpty(sessionId))
                {
                    return BadRequest(new ProblemDetails 
                    { 
                        Detail = "Session ID is required" 
                    });
                }

                var cacheKey = $"device_session:{sessionId}";
                var deviceInfoJson = await _cache.GetStringAsync(cacheKey);

                if (string.IsNullOrEmpty(deviceInfoJson))
                {
                    return NotFound(new ProblemDetails 
                    { 
                        Detail = "Device not found or session expired" 
                    });
                }

                var deviceInfo = JsonSerializer.Deserialize<PairedDeviceInfo>(deviceInfoJson);
                
                if (deviceInfo == null)
                {
                    return NotFound(new ProblemDetails 
                    { 
                        Detail = "Invalid device information" 
                    });
                }

                // Check if token is expired
                if (DateTime.UtcNow > deviceInfo.ExpiresAt)
                {
                    await _cache.RemoveAsync(cacheKey);
                    return NotFound(new ProblemDetails 
                    { 
                        Detail = "Device session expired" 
                    });
                }

                // Don't return sensitive tokens in info endpoint
                var safeDeviceInfo = new PairedDeviceInfo
                {
                    AccessToken = "***", // Hide for security
                    RefreshToken = "***", // Hide for security
                    Email = deviceInfo.Email,
                    DeviceName = deviceInfo.DeviceName,
                    PairedAt = deviceInfo.PairedAt,
                    ExpiresAt = deviceInfo.ExpiresAt
                };

                return Ok(safeDeviceInfo);
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
            try
            {
                if (string.IsNullOrEmpty(sessionId))
                {
                    return BadRequest(new DevicePairingResponse
                    {
                        Success = false,
                        Message = "Session ID is required"
                    });
                }

                var cacheKey = $"device_session:{sessionId}";
                await _cache.RemoveAsync(cacheKey);

                _logger.LogInformation($"Device unpaired. SessionId: {sessionId}");

                return Ok(new DevicePairingResponse
                {
                    Success = true,
                    Message = "Device unpaired successfully",
                    SessionId = sessionId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error unpairing device with session ID: {sessionId}");
                return StatusCode(500, new DevicePairingResponse
                {
                    Success = false,
                    Message = "An error occurred while unpairing the device"
                });
            }
        }
    }
}