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
        private readonly IHttpContextAccessor _httpContextAccessor;

        private const string GOOGLE_CAP_API_BASE = "https://risc.googleapis.com/v1beta";

        public CrossAccountProtectionService(
            HttpClient httpClient,
            IDistributedCache cache,
            ILogger<CrossAccountProtectionService> logger,
            IOptionsMonitor<Configuration> config,
            IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
            _config = config;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<bool> IsUserAccountSecureAsync(string userId)
        {
            try
            {
                var status = await CheckSecurityStatusAsync(null); // Will use current user's token
                return status.IsSecure && !status.RequiresReauthentication;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking account security for user {userId}");
                return false; // Assume insecure if we can't check
            }
        }

        public async Task<CrossAccountProtectionStatus> CheckSecurityStatusAsync(string? accessToken = null)
        {
            try
            {
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

                // Use Google's Cross-Account Protection API
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                
                // Check for security events
                var response = await _httpClient.GetAsync($"{GOOGLE_CAP_API_BASE}/securityEvents");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var securityData = JsonSerializer.Deserialize<JsonElement>(content);
                    
                    // Parse security status from response
                    var hasActiveThreats = false;
                    var warnings = new List<string>();
                    
                    if (securityData.TryGetProperty("securityEvents", out var events))
                    {
                        foreach (var eventElement in events.EnumerateArray())
                        {
                            if (eventElement.TryGetProperty("severity", out var severity) && 
                                severity.GetString() == "HIGH")
                            {
                                hasActiveThreats = true;
                                if (eventElement.TryGetProperty("description", out var desc))
                                {
                                    warnings.Add(desc.GetString() ?? "Unknown security threat");
                                }
                            }
                        }
                    }

                    return new CrossAccountProtectionStatus
                    {
                        IsSecure = !hasActiveThreats,
                        SecurityWarnings = warnings.ToArray(),
                        LastSecurityCheck = DateTime.UtcNow,
                        RequiresReauthentication = hasActiveThreats
                    };
                }
                else
                {
                    _logger.LogWarning($"Failed to check security status. Status: {response.StatusCode}");
                    return new CrossAccountProtectionStatus
                    {
                        IsSecure = true, // Default to secure if API is unavailable
                        SecurityWarnings = new[] { "Unable to verify security status with Google" },
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

        public async Task<bool> ReportSecurityEventAsync(string userId, SecurityEventType eventType, string? details = null)
        {
            try
            {
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

                var securityEvent = new
                {
                    userId = userId,
                    eventType = eventType.ToString(),
                    timestamp = DateTime.UtcNow.ToString("O"),
                    details = details ?? string.Empty,
                    source = _config.CurrentValue.ApplicationName
                };

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                
                var jsonContent = JsonSerializer.Serialize(securityEvent);
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{GOOGLE_CAP_API_BASE}/securityEvents", content);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Successfully reported security event {eventType} for user {userId}");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"Failed to report security event. Status: {response.StatusCode}");
                    return false;
                }
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