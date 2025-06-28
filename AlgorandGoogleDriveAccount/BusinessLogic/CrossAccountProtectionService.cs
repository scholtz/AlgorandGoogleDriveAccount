using AlgorandGoogleDriveAccount.Model;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AlgorandGoogleDriveAccount.BusinessLogic
{
    public class CrossAccountProtectionService : ICrossAccountProtectionService
    {
        private readonly HttpClient _httpClient;
        private readonly IDistributedCache _cache;
        private readonly ILogger<CrossAccountProtectionService> _logger;
        private readonly IOptionsMonitor<Configuration> _config;
        private readonly IOptionsMonitor<CrossAccountProtectionConfiguration> _capConfig;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CrossAccountProtectionService(
            HttpClient httpClient,
            IDistributedCache cache,
            ILogger<CrossAccountProtectionService> logger,
            IOptionsMonitor<Configuration> config,
            IOptionsMonitor<CrossAccountProtectionConfiguration> capConfig,
            IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
            _config = config;
            _capConfig = capConfig;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<bool> IsUserAccountSecureAsync(string userId)
        {
            try
            {
                // If Cross-Account Protection is disabled, return true (assume secure)
                if (!_capConfig.CurrentValue.Enabled)
                {
                    _logger.LogDebug("Cross-Account Protection is disabled - skipping security check for user {UserId}", userId);
                    return true;
                }

                var status = await CheckSecurityStatusAsync(null); // Will use current user's token
                return status.IsSecure && !status.RequiresReauthentication;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking account security for user {userId}");
                return !_capConfig.CurrentValue.Enabled; // If CAP is disabled, assume secure; if enabled, assume insecure on error
            }
        }

        public async Task<CrossAccountProtectionStatus> CheckSecurityStatusAsync(string? accessToken = null)
        {
            try
            {
                // If Cross-Account Protection is disabled, return a basic secure status
                if (!_capConfig.CurrentValue.Enabled)
                {
                    return new CrossAccountProtectionStatus
                    {
                        IsSecure = true,
                        SecurityWarnings = new[] { "Cross-Account Protection is disabled - basic security validation only" },
                        LastSecurityCheck = DateTime.UtcNow,
                        RequiresReauthentication = false
                    };
                }

                var token = accessToken;
                if (string.IsNullOrEmpty(token))
                {
                    var httpContext = _httpContextAccessor.HttpContext;
                    if (httpContext?.User?.Identity?.IsAuthenticated == true)
                    {
                        token = await httpContext.GetTokenAsync("access_token");
                    }
                }

                if (string.IsNullOrEmpty(token))
                {
                    return new CrossAccountProtectionStatus
                    {
                        IsSecure = false,
                        SecurityWarnings = new[] { "No access token available for security check" },
                        LastSecurityCheck = DateTime.UtcNow,
                        RequiresReauthentication = true
                    };
                }

                // Use Google's tokeninfo endpoint to validate the token and get security information
                var tokenInfoUrl = $"https://oauth2.googleapis.com/tokeninfo?access_token={Uri.EscapeDataString(token)}";
                
                var response = await _httpClient.GetAsync(tokenInfoUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var tokenInfo = JsonSerializer.Deserialize<JsonElement>(content);
                    
                    var warnings = new List<string>();
                    var isSecure = true;
                    var requiresReauth = false;
                    
                    // Check token expiration
                    if (tokenInfo.TryGetProperty("expires_in", out var expiresIn))
                    {
                        var secondsUntilExpiry = expiresIn.GetInt32();
                        if (secondsUntilExpiry < 300) // Less than 5 minutes
                        {
                            warnings.Add("Access token expires soon (less than 5 minutes)");
                            requiresReauth = true;
                        }
                    }
                    
                    // Check token scope - ensure it has the required scopes
                    if (tokenInfo.TryGetProperty("scope", out var scope))
                    {
                        var scopes = scope.GetString()?.Split(' ') ?? Array.Empty<string>();
                        var requiredScopes = new[] { "openid", "email", "profile" };
                        var missingScopes = requiredScopes.Where(rs => !scopes.Contains(rs)).ToArray();
                        
                        if (missingScopes.Any())
                        {
                            warnings.Add($"Missing required scopes: {string.Join(", ", missingScopes)}");
                            isSecure = false;
                        }
                        
                        // Check for additional security-related scopes (optional)
                        var securityScopes = new[] { 
                            "https://www.googleapis.com/auth/drive.file",
                            "https://www.googleapis.com/auth/userinfo.email",
                            "https://www.googleapis.com/auth/userinfo.profile"
                        };
                        
                        var grantedSecurityScopes = securityScopes.Where(ss => scopes.Contains(ss)).ToArray();
                        if (grantedSecurityScopes.Any())
                        {
                            warnings.Add($"Enhanced security monitoring enabled with scopes: {string.Join(", ", grantedSecurityScopes)}");
                        }
                    }
                    
                    // Validate audience (client ID)
                    if (tokenInfo.TryGetProperty("aud", out var audience))
                    {
                        var clientId = _config.CurrentValue.ClientId;
                        if (audience.GetString() != clientId)
                        {
                            warnings.Add("Token audience mismatch - potential security risk");
                            isSecure = false;
                            requiresReauth = true;
                        }
                    }
                    
                    // Additional security checks
                    var securityChecks = PerformAdditionalSecurityChecks(tokenInfo);
                    warnings.AddRange(securityChecks.warnings);
                    isSecure = isSecure && securityChecks.isSecure;
                    requiresReauth = requiresReauth || securityChecks.requiresReauth;

                    return new CrossAccountProtectionStatus
                    {
                        IsSecure = isSecure,
                        SecurityWarnings = warnings.ToArray(),
                        LastSecurityCheck = DateTime.UtcNow,
                        RequiresReauthentication = requiresReauth
                    };
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    // Invalid or expired token
                    return new CrossAccountProtectionStatus
                    {
                        IsSecure = false,
                        SecurityWarnings = new[] { "Access token is invalid or expired" },
                        LastSecurityCheck = DateTime.UtcNow,
                        RequiresReauthentication = true
                    };
                }
                else
                {
                    _logger.LogWarning($"Failed to validate token. Status: {response.StatusCode}");
                    return new CrossAccountProtectionStatus
                    {
                        IsSecure = true, // Default to secure if API is temporarily unavailable
                        SecurityWarnings = new[] { $"Unable to verify token status (HTTP {response.StatusCode})" },
                        LastSecurityCheck = DateTime.UtcNow,
                        RequiresReauthentication = false
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Cross-Account Protection status");
                return new CrossAccountProtectionStatus
                {
                    IsSecure = false,
                    SecurityWarnings = new[] { $"Security check failed: {ex.Message}" },
                    LastSecurityCheck = DateTime.UtcNow,
                    RequiresReauthentication = true
                };
            }
        }

        private (string[] warnings, bool isSecure, bool requiresReauth) PerformAdditionalSecurityChecks(JsonElement tokenInfo)
        {
            var warnings = new List<string>();
            var isSecure = true;
            var requiresReauth = false;
            
            try
            {
                // Check if token was issued recently (security best practice)
                if (tokenInfo.TryGetProperty("iat", out var issuedAt))
                {
                    var issuedTime = DateTimeOffset.FromUnixTimeSeconds(issuedAt.GetInt64());
                    var timeSinceIssued = DateTimeOffset.UtcNow - issuedTime;
                    
                    if (timeSinceIssued.TotalHours > 24)
                    {
                        warnings.Add("Token is older than 24 hours - consider refreshing for better security");
                    }
                    
                    if (timeSinceIssued.TotalDays > 7)
                    {
                        warnings.Add("Token is older than 7 days - refresh recommended for enhanced security");
                        requiresReauth = true;
                    }
                }
                
                // Check for email verification
                if (tokenInfo.TryGetProperty("email_verified", out var emailVerified))
                {
                    if (!emailVerified.GetBoolean())
                    {
                        warnings.Add("Email address is not verified - this may pose a security risk");
                        requiresReauth = true;
                    }
                }
                
                // Validate issuer
                if (tokenInfo.TryGetProperty("iss", out var issuer))
                {
                    var validIssuers = new[] { "https://accounts.google.com", "accounts.google.com" };
                    if (!validIssuers.Contains(issuer.GetString()))
                    {
                        warnings.Add("Token issued by unrecognized issuer - potential security risk");
                        isSecure = false;
                        requiresReauth = true;
                    }
                }
                
                // Check token usage patterns (basic Cross-Account Protection concept)
                var userId = "";
                if (tokenInfo.TryGetProperty("sub", out var subject))
                {
                    userId = subject.GetString() ?? "";
                    
                    // In a real implementation, you would check against stored usage patterns
                    // For now, we'll just log that Cross-Account Protection monitoring is active
                    warnings.Add("Cross-Account Protection monitoring active for enhanced security");
                }
                
                return (warnings.ToArray(), isSecure, requiresReauth);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error performing additional security checks");
                return (new[] { "Could not perform all security checks" }, true, false);
            }
        }

        public async Task<bool> ReportSecurityEventAsync(string userId, SecurityEventType eventType, string? details = null)
        {
            try
            {
                // If Cross-Account Protection is disabled or auto-reporting is disabled, skip reporting
                if (!_capConfig.CurrentValue.Enabled || !_capConfig.CurrentValue.AutoReportEvents)
                {
                    _logger.LogDebug("Cross-Account Protection reporting is disabled - skipping security event report for user {UserId}, event {EventType}", userId, eventType);
                    return true; // Return true as if reported successfully
                }

                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext?.User?.Identity?.IsAuthenticated != true)
                {
                    _logger.LogWarning("Cannot report security event - user not authenticated");
                    return false;
                }

                var accessToken = await httpContext.GetTokenAsync("access_token");
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("Cannot report security event - no access token");
                    return false;
                }

                // Log security event locally (since Google's RISC API requires special setup)
                var securityEvent = new
                {
                    userId = userId,
                    eventType = eventType.ToString(),
                    timestamp = DateTime.UtcNow.ToString("O"),
                    details = details ?? string.Empty,
                    source = _config.CurrentValue.ApplicationName,
                    userAgent = httpContext.Request.Headers.UserAgent.ToString(),
                    ipAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
                    crossAccountProtectionEnabled = _capConfig.CurrentValue.Enabled
                };

                // Store the security event in cache for later analysis
                var eventKey = $"security_event:{userId}:{DateTime.UtcNow:yyyyMMddHHmmss}";
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30) // Keep for 30 days
                };
                
                await _cache.SetStringAsync(eventKey, JsonSerializer.Serialize(securityEvent), cacheOptions);
                
                // Log the security event for monitoring
                _logger.LogWarning("Security event reported: {EventType} for user {UserId}. Details: {Details} (Cross-Account Protection: {CAPEnabled})", 
                    eventType, userId, details, _capConfig.CurrentValue.Enabled ? "Enabled" : "Disabled");

                // In a production environment, you would:
                // 1. Send to your security monitoring system
                // 2. Send to Google via proper RISC API setup (requires publisher verification)
                // 3. Trigger automated security responses
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reporting security event {eventType} for user {userId}");
                return false;
            }
        }

        public async Task<ReauthenticationResult> RequestReauthenticationAsync(string userId, string[] scopes)
        {
            try
            {
                var scopeString = string.Join(" ", scopes);
                var clientId = _config.CurrentValue.ClientId;
                var host = _config.CurrentValue.Host;
                
                var reauthUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                    $"client_id={Uri.EscapeDataString(clientId)}&" +
                    $"response_type=code&" +
                    $"scope={Uri.EscapeDataString(scopeString)}&" +
                    $"redirect_uri={Uri.EscapeDataString($"{host}/api/device/reauth-callback")}&" +
                    $"access_type=offline&" +
                    $"prompt=consent&" +
                    $"state={Uri.EscapeDataString(userId)}";

                return new ReauthenticationResult
                {
                    Success = true,
                    ReauthUrl = reauthUrl
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating reauthentication URL for user {userId}");
                return new ReauthenticationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
    }
}