using AlgorandGoogleDriveAccount.Model;
using AlgorandGoogleDriveAccount.Repository;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AlgorandGoogleDriveAccount.BusinessLogic
{
    public class PortfolioValuationService : IPortfolioValuationService
    {
        private readonly GoogleDriveRepository _googleDriveRepository;
        private readonly IDistributedCache _cache;
        private readonly ILogger<PortfolioValuationService> _logger;
        private readonly IOptionsMonitor<Configuration> _config;
        private readonly HttpClient _httpClient;

        // Current EUR prices (in a real implementation, these would come from a price API)
        private readonly decimal _algoEurPrice = 0.20m; // Example: 1 ALGO = 0.20 EUR

        public PortfolioValuationService(
            GoogleDriveRepository googleDriveRepository,
            IDistributedCache cache,
            ILogger<PortfolioValuationService> logger,
            IOptionsMonitor<Configuration> config,
            HttpClient httpClient)
        {
            _googleDriveRepository = googleDriveRepository;
            _cache = cache;
            _logger = logger;
            _config = config;
            _httpClient = httpClient;
        }

        public async Task<decimal> GetPortfolioValueAsync(string email)
        {
            try
            {
                // Try to get cached portfolio value first
                var cacheKey = $"portfolio_value:{email}";
                var cachedValue = await _cache.GetStringAsync(cacheKey);
                
                if (!string.IsNullOrEmpty(cachedValue) && decimal.TryParse(cachedValue, out var cached))
                {
                    return cached;
                }

                // Calculate portfolio value from Algorand accounts
                var totalValue = await CalculatePortfolioValueAsync(email);

                // Cache for 1 hour
                await _cache.SetStringAsync(cacheKey, totalValue.ToString(), new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                });

                return totalValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calculating portfolio value for {email}");
                return 0m; // Default to free tier on error
            }
        }

        public async Task<ServiceTier> GetServiceTierAsync(string email)
        {
            var portfolioValue = await GetPortfolioValueAsync(email);
            return DetermineServiceTier(portfolioValue);
        }

        public async Task<PortfolioSummary> GetPortfolioSummaryAsync(string email)
        {
            try
            {
                var cacheKey = $"portfolio_summary:{email}";
                var cachedSummary = await _cache.GetStringAsync(cacheKey);
                
                if (!string.IsNullOrEmpty(cachedSummary))
                {
                    var cached = JsonSerializer.Deserialize<PortfolioSummary>(cachedSummary);
                    if (cached != null && cached.LastUpdated > DateTime.UtcNow.AddHours(-1))
                    {
                        return cached;
                    }
                }

                // Calculate fresh portfolio summary
                var portfolioValue = await CalculatePortfolioValueAsync(email);
                var algoBalance = await GetAlgorandBalanceAsync(email);
                
                var summary = new PortfolioSummary
                {
                    TotalValueEur = portfolioValue,
                    CurrentTier = DetermineServiceTier(portfolioValue),
                    LastUpdated = DateTime.UtcNow,
                    AccountCount = 1, // For now, assuming 1 account per user
                    AlgorandBalance = algoBalance,
                    AssetValue = portfolioValue
                };

                // Cache for 1 hour
                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(summary), new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                });

                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting portfolio summary for {email}");
                return new PortfolioSummary
                {
                    TotalValueEur = 0m,
                    CurrentTier = ServiceTier.Free,
                    LastUpdated = DateTime.UtcNow,
                    AccountCount = 0,
                    AlgorandBalance = 0m,
                    AssetValue = 0m
                };
            }
        }

        public async Task UpdatePortfolioValuationAsync(string email)
        {
            try
            {
                // Clear cached values to force recalculation
                var portfolioCacheKey = $"portfolio_value:{email}";
                var summaryCacheKey = $"portfolio_summary:{email}";
                
                await _cache.RemoveAsync(portfolioCacheKey);
                await _cache.RemoveAsync(summaryCacheKey);

                // Trigger fresh calculation
                await GetPortfolioSummaryAsync(email);

                _logger.LogInformation($"Portfolio valuation updated for {email}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating portfolio valuation for {email}");
            }
        }

        private async Task<decimal> CalculatePortfolioValueAsync(string email)
        {
            try
            {
                // Load the user's Algorand account
                var account = await _googleDriveRepository.LoadAccount(email, 0);
                
                // In a real implementation, you would:
                // 1. Query the Algorand blockchain for account balance
                // 2. Get current ALGO/EUR exchange rate from a price API
                // 3. Calculate the total value of all assets
                
                // For demonstration, we'll use a mock calculation
                var algoBalance = await GetAlgorandBalanceAsync(email);
                var totalValueEur = algoBalance * _algoEurPrice;

                return totalValueEur;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Could not calculate portfolio value for {email}, returning 0");
                return 0m;
            }
        }

        private async Task<decimal> GetAlgorandBalanceAsync(string email)
        {
            try
            {
                // In a real implementation, this would query the Algorand blockchain
                // For now, we'll return a mock balance based on email hash for demonstration
                var emailHash = email.GetHashCode();
                var mockBalance = Math.Abs(emailHash % 100000); // Mock balance between 0-100,000 ALGO
                
                return mockBalance;
            }
            catch
            {
                return 0m;
            }
        }

        private static ServiceTier DetermineServiceTier(decimal portfolioValueEur)
        {
            return portfolioValueEur switch
            {
                < 10000m => ServiceTier.Free,
                < 1000000m => ServiceTier.Professional,
                _ => ServiceTier.Enterprise
            };
        }
    }
}