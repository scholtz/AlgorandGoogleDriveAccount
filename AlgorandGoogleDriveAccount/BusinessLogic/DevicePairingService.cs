using AlgorandGoogleDriveAccount.Model;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace AlgorandGoogleDriveAccount.BusinessLogic
{
    public class DevicePairingService : IDevicePairingService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<DevicePairingService> _logger;
        private const int CacheExpirationDays = 1;

        public DevicePairingService(
            IDistributedCache cache,
            ILogger<DevicePairingService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<string> InitiatePairingAsync(string sessionId, string deviceName)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException("Session ID is required for device pairing", nameof(sessionId));
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

            return tempKey;
        }

        public async Task<DevicePairingResponse> ProcessPairingCallbackAsync(string sessionId, string email, string accessToken, string? refreshToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    return new DevicePairingResponse
                    {
                        Success = false,
                        Message = "Session ID is required"
                    };
                }

                // Get temporary session data
                var tempKey = $"temp_session:{sessionId}";
                var tempDataJson = await _cache.GetStringAsync(tempKey);
                
                if (string.IsNullOrEmpty(tempDataJson))
                {
                    return new DevicePairingResponse
                    {
                        Success = false,
                        Message = "Session not found or expired. Please initiate pairing again."
                    };
                }

                var tempData = JsonSerializer.Deserialize<JsonElement>(tempDataJson);

                if (string.IsNullOrEmpty(email))
                {
                    return new DevicePairingResponse
                    {
                        Success = false,
                        Message = "Email not found in claims. Authentication failed."
                    };
                }

                if (string.IsNullOrEmpty(accessToken))
                {
                    return new DevicePairingResponse
                    {
                        Success = false,
                        Message = "No access token found. Authentication failed."
                    };
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

                return new DevicePairingResponse
                {
                    Success = true,
                    Message = "Device paired successfully",
                    SessionId = sessionId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error pairing device with session ID: {sessionId}");
                return new DevicePairingResponse
                {
                    Success = false,
                    Message = "An error occurred while pairing the device"
                };
            }
        }

        public async Task<string?> GetDeviceAccessTokenAsync(string sessionId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    throw new ArgumentException("Session ID is required", nameof(sessionId));
                }

                var cacheKey = $"device_session:{sessionId}";
                var deviceInfoJson = await _cache.GetStringAsync(cacheKey);

                if (string.IsNullOrEmpty(deviceInfoJson))
                {
                    return null;
                }

                var deviceInfo = JsonSerializer.Deserialize<PairedDeviceInfo>(deviceInfoJson);
                
                if (deviceInfo == null)
                {
                    return null;
                }

                // Check if token is expired
                if (DateTime.UtcNow > deviceInfo.ExpiresAt)
                {
                    await _cache.RemoveAsync(cacheKey);
                    return null;
                }

                return deviceInfo.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving access token for session ID: {sessionId}");
                throw;
            }
        }

        public async Task<PairedDeviceInfo?> GetDeviceInfoAsync(string sessionId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    throw new ArgumentException("Session ID is required", nameof(sessionId));
                }

                var cacheKey = $"device_session:{sessionId}";
                var deviceInfoJson = await _cache.GetStringAsync(cacheKey);

                if (string.IsNullOrEmpty(deviceInfoJson))
                {
                    return null;
                }

                var deviceInfo = JsonSerializer.Deserialize<PairedDeviceInfo>(deviceInfoJson);
                
                if (deviceInfo == null)
                {
                    return null;
                }

                // Check if token is expired
                if (DateTime.UtcNow > deviceInfo.ExpiresAt)
                {
                    await _cache.RemoveAsync(cacheKey);
                    return null;
                }

                // Don't return sensitive tokens in info endpoint
                return new PairedDeviceInfo
                {
                    AccessToken = "***", // Hide for security
                    RefreshToken = "***", // Hide for security
                    Email = deviceInfo.Email,
                    DeviceName = deviceInfo.DeviceName,
                    PairedAt = deviceInfo.PairedAt,
                    ExpiresAt = deviceInfo.ExpiresAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving device info for session ID: {sessionId}");
                throw;
            }
        }

        public async Task<DevicePairingResponse> UnpairDeviceAsync(string sessionId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    return new DevicePairingResponse
                    {
                        Success = false,
                        Message = "Session ID is required"
                    };
                }

                var cacheKey = $"device_session:{sessionId}";
                await _cache.RemoveAsync(cacheKey);

                _logger.LogInformation($"Device unpaired. SessionId: {sessionId}");

                return new DevicePairingResponse
                {
                    Success = true,
                    Message = "Device unpaired successfully",
                    SessionId = sessionId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error unpairing device with session ID: {sessionId}");
                return new DevicePairingResponse
                {
                    Success = false,
                    Message = "An error occurred while unpairing the device"
                };
            }
        }
    }
}