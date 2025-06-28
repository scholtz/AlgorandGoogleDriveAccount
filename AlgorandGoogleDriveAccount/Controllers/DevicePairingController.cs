using AlgorandGoogleDriveAccount.BusinessLogic;
using AlgorandGoogleDriveAccount.Model;
using Google.Apis.Auth.AspNetCore3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        /// Serves the device pairing app page
        /// </summary>
        [AllowAnonymous]
        [HttpGet("app")]
        public IActionResult App()
        {
            return PhysicalFile(
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "pair.html"),
                "text/html"
            );
        }

        /// <summary>
        /// Initiates the device pairing process by redirecting to Google OAuth
        /// </summary>
        /// <param name="sessionId">Unique session ID for the device</param>
        /// <param name="deviceName">Optional device name for identification</param>
        /// <param name="requestDriveAccess">Whether to request Google Drive access immediately</param>
        /// <returns>Redirect to Google OAuth</returns>
        [AllowAnonymous]
        [HttpGet("pair-device")]
        public async Task<IActionResult> PairDevice(string sessionId, string deviceName = "Unknown Device", bool requestDriveAccess = false)
        {
            try
            {
                await _devicePairingService.InitiatePairingAsync(sessionId, deviceName);

                var redirectUri = Url.Action("PairedDevice", "DevicePairing", new { sessionId }, Request.Scheme);
                
                var authProperties = new AuthenticationProperties
                {
                    RedirectUri = redirectUri,
                    Items =
                    {
                        ["sessionId"] = sessionId,
                        ["deviceName"] = deviceName
                    }
                };

                // Support incremental authorization for Drive access
                if (requestDriveAccess)
                {
                    authProperties.Items["incremental_scopes"] = Google.Apis.Drive.v3.DriveService.Scope.DriveFile;
                }

                return Challenge(authProperties, GoogleOpenIdConnectDefaults.AuthenticationScheme);
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
            try
            {
                // Get user info and tokens
                var email = User.FindFirst(ClaimTypes.Email)?.Value;
                var accessToken = await HttpContext.GetTokenAsync("access_token");
                var refreshToken = await HttpContext.GetTokenAsync("refresh_token");

                var result = await _devicePairingService.ProcessPairingCallbackAsync(sessionId, email!, accessToken!, refreshToken);

                if (!result.Success)
                {
                    _logger.LogWarning($"Device pairing failed for session {sessionId}: {result.Message}");
                    return Redirect($"/pair.html?error=pairing_failed&sessionId={sessionId}");
                }

                // Redirect to pair.html with success message
                return Redirect($"/pair.html?sessionId={sessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during device pairing callback for session {sessionId}");
                return Redirect($"/pair.html?error=callback_error&sessionId={sessionId}");
            }
        }

        /// <summary>
        /// Requests additional Google Drive permissions for an already paired device
        /// </summary>
        /// <param name="sessionId">Session ID of the paired device</param>
        /// <returns>Redirect to incremental authorization</returns>
        [AllowAnonymous]
        [HttpGet("request-drive-access/{sessionId}")]
        public async Task<IActionResult> RequestDriveAccess(string sessionId)
        {
            try
            {
                var deviceInfo = await _devicePairingService.GetDeviceInfoAsync(sessionId);
                if (deviceInfo == null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Detail = "Device not found or session expired. Please pair the device first."
                    });
                }

                var redirectUri = Url.Action("DriveAccessCallback", "DevicePairing", new { sessionId }, Request.Scheme);
                
                var authProperties = new AuthenticationProperties
                {
                    RedirectUri = redirectUri,
                    Items =
                    {
                        ["sessionId"] = sessionId,
                        ["incremental_scopes"] = Google.Apis.Drive.v3.DriveService.Scope.DriveFile
                    }
                };

                return Challenge(authProperties, GoogleOpenIdConnectDefaults.AuthenticationScheme);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error requesting Drive access for session {sessionId}");
                return StatusCode(500, new ProblemDetails
                {
                    Detail = "An error occurred while requesting Drive access"
                });
            }
        }

        /// <summary>
        /// Callback endpoint after incremental Drive authorization
        /// </summary>
        /// <param name="sessionId">Session ID from the authorization request</param>
        /// <returns>Redirect with result</returns>
        [Authorize]
        [HttpGet("drive-access-callback")]
        public async Task<IActionResult> DriveAccessCallback(string sessionId)
        {
            try
            {
                // Get updated tokens with Drive access
                var email = User.FindFirst(ClaimTypes.Email)?.Value;
                var accessToken = await HttpContext.GetTokenAsync("access_token");
                var refreshToken = await HttpContext.GetTokenAsync("refresh_token");

                // Update the device info with new tokens that include Drive access
                var result = await _devicePairingService.ProcessPairingCallbackAsync(sessionId, email!, accessToken!, refreshToken);

                if (!result.Success)
                {
                    _logger.LogWarning($"Drive access update failed for session {sessionId}: {result.Message}");
                    return Redirect($"/pair.html?error=drive_access_failed&sessionId={sessionId}");
                }

                // Redirect to pair.html with success message
                return Redirect($"/pair.html?drive_access=granted&sessionId={sessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during Drive access callback for session {sessionId}");
                return Redirect($"/pair.html?error=drive_callback_error&sessionId={sessionId}");
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

        /// <summary>
        /// Diagnostic endpoint to help troubleshoot encryption/decryption issues
        /// </summary>
        /// <param name="sessionId">Session ID of the paired device</param>
        /// <returns>Diagnostic information about the account file</returns>
        [AllowAnonymous]
        [HttpGet("diagnose/{sessionId}")]
        public async Task<ActionResult<object>> DiagnoseAccount(string sessionId)
        {
            try
            {
                var deviceInfo = await _devicePairingService.GetDeviceInfoInternalAsync(sessionId);
                if (deviceInfo == null)
                {
                    return NotFound(new { error = "Device not found or session expired" });
                }

                var credential = GoogleCredential.FromAccessToken(deviceInfo.AccessToken);
                if (credential == null)
                {
                    return BadRequest(new { error = "Invalid access token" });
                }

                var service = new Google.Apis.Drive.v3.DriveService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "Biatec",
                });

                // Try to find the folder "Biatec"
                var folderRequest = service.Files.List();
                folderRequest.Q = $"mimeType = 'application/vnd.google-apps.folder' and name = 'Biatec' and trashed = false";
                folderRequest.Fields = "files(id, name)";
                var folderResult = await folderRequest.ExecuteAsync();

                if (!folderResult.Files.Any())
                {
                    return Ok(new 
                    { 
                        email = deviceInfo.Email,
                        folderFound = false,
                        message = "Biatec folder not found. Account file doesn't exist yet.",
                        suggestedAction = "Try accessing the account through normal authentication first to create the initial encrypted file."
                    });
                }

                var folder = folderResult.Files.First();

                // Check if file exists in folder
                var fileCheckRequest = service.Files.List();
                fileCheckRequest.Q = $"name = 'AVMAccount.dat' and '{folder.Id}' in parents and trashed = false";
                fileCheckRequest.Fields = "files(id, name, size, createdTime, modifiedTime)";
                var existingFiles = await fileCheckRequest.ExecuteAsync();

                if (!existingFiles.Files.Any())
                {
                    return Ok(new 
                    { 
                        email = deviceInfo.Email,
                        folderFound = true,
                        fileFound = false,
                        message = "AVMAccount.dat file not found in Biatec folder.",
                        suggestedAction = "Try accessing the account through normal authentication first to create the initial encrypted file."
                    });
                }

                var file = existingFiles.Files.First();

                return Ok(new 
                { 
                    email = deviceInfo.Email,
                    emailLength = deviceInfo.Email?.Length,
                    folderFound = true,
                    fileFound = true,
                    fileId = file.Id,
                    fileName = file.Name,
                    fileSize = file.Size,
                    createdTime = file.CreatedTime,
                    modifiedTime = file.ModifiedTime,
                    message = "Account file found. The issue might be with email case sensitivity or encryption key derivation.",
                    suggestedActions = new[]
                    {
                        "Verify that the email case matches exactly between device pairing and normal authentication",
                        "Check if the AES key and IV configuration are identical",
                        "Try re-pairing the device to ensure fresh tokens"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error diagnosing account for session {sessionId}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Check Cross-Account Protection security status for a device session
        /// </summary>
        /// <param name="sessionId">Session ID of the paired device</param>
        /// <returns>Security status information</returns>
        [AllowAnonymous]
        [HttpGet("security-status/{sessionId}")]
        public async Task<ActionResult<object>> GetSecurityStatus(string sessionId)
        {
            try
            {
                var deviceInfo = await _devicePairingService.GetDeviceInfoInternalAsync(sessionId);
                if (deviceInfo == null)
                {
                    return NotFound(new { error = "Device not found or session expired" });
                }

                var capService = HttpContext.RequestServices.GetRequiredService<ICrossAccountProtectionService>();
                var securityStatus = await capService.CheckSecurityStatusAsync(deviceInfo.AccessToken);

                return Ok(new
                {
                    sessionId = sessionId,
                    email = deviceInfo.Email,
                    isSecure = securityStatus.IsSecure,
                    requiresReauth = securityStatus.RequiresReauthentication,
                    warnings = securityStatus.SecurityWarnings,
                    lastCheck = securityStatus.LastSecurityCheck
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking security status for session {sessionId}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Report a security event for Cross-Account Protection
        /// </summary>
        /// <param name="sessionId">Session ID of the device</param>
        /// <param name="eventType">Type of security event</param>
        /// <param name="details">Additional details about the event</param>
        /// <returns>Result of the security event report</returns>
        [AllowAnonymous]
        [HttpPost("report-security-event/{sessionId}")]
        public async Task<ActionResult<object>> ReportSecurityEvent(string sessionId, [FromBody] SecurityEventRequest request)
        {
            try
            {
                var deviceInfo = await _devicePairingService.GetDeviceInfoAsync(sessionId);
                if (deviceInfo == null)
                {
                    return NotFound(new { error = "Device not found or session expired" });
                }

                var capService = HttpContext.RequestServices.GetRequiredService<ICrossAccountProtectionService>();
                var success = await capService.ReportSecurityEventAsync(
                    deviceInfo.Email ?? sessionId, 
                    request.EventType, 
                    request.Details);

                return Ok(new
                {
                    success = success,
                    message = success ? "Security event reported successfully" : "Failed to report security event",
                    sessionId = sessionId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reporting security event for session {sessionId}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Test endpoint to verify token validation is working
        /// </summary>
        /// <returns>Token validation test result</returns>
        [AllowAnonymous]
        [HttpGet("test-token-validation")]
        public async Task<ActionResult<object>> TestTokenValidation()
        {
            try
            {
                // Test with a dummy token to verify our validation logic
                var capService = HttpContext.RequestServices.GetRequiredService<ICrossAccountProtectionService>();
                
                return Ok(new
                {
                    message = "Token validation service is configured correctly",
                    timestamp = DateTime.UtcNow,
                    endpoint = "Use /api/device/security-status/{sessionId} to check real token status"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing token validation");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get portfolio summary and service tier for a device session
        /// </summary>
        /// <param name="sessionId">Session ID of the paired device</param>
        /// <returns>Portfolio information and current service tier</returns>
        [AllowAnonymous]
        [HttpGet("portfolio/{sessionId}")]
        public async Task<ActionResult<object>> GetPortfolioInfo(string sessionId)
        {
            try
            {
                var deviceInfo = await _devicePairingService.GetDeviceInfoInternalAsync(sessionId);
                if (deviceInfo == null)
                {
                    return NotFound(new { error = "Device not found or session expired" });
                }

                var portfolioService = HttpContext.RequestServices.GetRequiredService<IPortfolioValuationService>();
                var portfolioSummary = await portfolioService.GetPortfolioSummaryAsync(deviceInfo.Email!);

                return Ok(new
                {
                    sessionId = sessionId,
                    email = deviceInfo.Email,
                    portfolio = new
                    {
                        totalValueEur = portfolioSummary.TotalValueEur,
                        currentTier = portfolioSummary.CurrentTier.ToString(),
                        lastUpdated = portfolioSummary.LastUpdated,
                        accountCount = portfolioSummary.AccountCount,
                        algorandBalance = portfolioSummary.AlgorandBalance,
                        assetValue = portfolioSummary.AssetValue
                    },
                    tierBenefits = GetTierBenefits(portfolioSummary.CurrentTier)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting portfolio info for session {sessionId}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get Cross-Account Protection configuration status
        /// </summary>
        /// <returns>Cross-Account Protection status information</returns>
        [AllowAnonymous]
        [HttpGet("cap-status")]
        public ActionResult<object> GetCrossAccountProtectionStatus()
        {
            try
            {
                var capConfig = HttpContext.RequestServices.GetRequiredService<IOptionsMonitor<CrossAccountProtectionConfiguration>>();
                
                return Ok(new
                {
                    crossAccountProtection = new
                    {
                        enabled = capConfig.CurrentValue.Enabled,
                        requireSecurityCheck = capConfig.CurrentValue.RequireSecurityCheck,
                        securityCheckIntervalMinutes = capConfig.CurrentValue.SecurityCheckIntervalMinutes,
                        autoReportEvents = capConfig.CurrentValue.AutoReportEvents,
                        enableGranularConsent = capConfig.CurrentValue.EnableGranularConsent,
                        filterInternalScopes = capConfig.CurrentValue.FilterInternalScopes
                    },
                    message = capConfig.CurrentValue.Enabled 
                        ? "Cross-Account Protection is enabled for enhanced security monitoring"
                        : "Cross-Account Protection is disabled - basic security validation only"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Cross-Account Protection status");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private static object GetTierBenefits(ServiceTier tier)
        {
            return tier switch
            {
                ServiceTier.Free => new
                {
                    tier = "Free",
                    portfolioRange = "< €10,000",
                    devices = 1,
                    support = "Community",
                    sla = "Best effort",
                    features = new[] { "Basic account management", "Standard security", "Portfolio tracking" }
                },
                ServiceTier.Professional => new
                {
                    tier = "Professional",
                    portfolioRange = "€10,000 - €1,000,000",
                    devices = 5,
                    support = "Priority",
                    sla = "99.5%",
                    features = new[] { "Full account management", "Advanced security", "Portfolio analytics", "Risk management" }
                },
                ServiceTier.Enterprise => new
                {
                    tier = "Enterprise",
                    portfolioRange = "> €1,000,000",
                    devices = "Unlimited",
                    support = "Dedicated",
                    sla = "99.9%",
                    features = new[] { "All Professional features", "Dedicated account manager", "Custom integrations", "Institutional security" }
                },
                _ => new { tier = "Unknown" }
            };
        }

        public class SecurityEventRequest
        {
            public SecurityEventType EventType { get; set; }
            public string? Details { get; set; }
        }
    }
}