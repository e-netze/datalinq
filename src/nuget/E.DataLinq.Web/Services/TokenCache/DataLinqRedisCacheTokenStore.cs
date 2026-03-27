using E.DataLinq.Core.Services.Crypto.Abstraction;
using E.DataLinq.Core.Services.Persistance.Abstraction;
using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Models.TokenCache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services.TokenCache;

internal class DataLinqRedisCacheTokenStore : IDataLinqCacheTokenStore
{
    private readonly IPersistanceProviderService _persistanceProvider;
    private readonly ILogger<DataLinqRedisCacheTokenStore> _logger;
    private readonly DataLinqTokenStoreOptions _tokenOptions;
    private readonly IDatabase _redisDb;
    private readonly ICryptoService _crypto;

    // static: reuse
    private static IConnectionMultiplexer _redisConnection = null;

    public DataLinqRedisCacheTokenStore(
        IPersistanceProviderService persistanceProvider,
        ILogger<DataLinqRedisCacheTokenStore> logger,
        IOptions<DataLinqTokenStoreOptions> tokenOptions,
        ICryptoService crypto
        )
    {
        _tokenOptions = tokenOptions.Value;
        _redisConnection ??= ConnectionMultiplexer.Connect(tokenOptions.Value.RedisConnectionString ?? throw new InvalidOperationException("Redis Connection string missing!"));

        _persistanceProvider = persistanceProvider;
        _logger = logger;

        _redisDb = _redisConnection.GetDatabase();
        _crypto = crypto;
    }

    public async Task<TokenMetadata> CreateAsync(string payload, string dataLinqRoute)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(100));

        var (lifeTime, maxUsage) = await dataLinqRoute.ParseDataLinqRouteAsync(_persistanceProvider, _tokenOptions);

        var tokenMetadata = token.GenerateTokenMetadata(payload, dataLinqRoute, lifeTime, maxUsage, _crypto);

        var setValue = await _redisDb.StringSetAsync(token, JsonSerializer.Serialize(tokenMetadata));

        if (setValue)
        {
            _logger.LogInformation("Token created: {Token}, at {DateTime}", token, DateTime.Now);
            return tokenMetadata;
        }
        else
        {
            _logger.LogInformation("Failed to create token created: {Token}, at {DateTime}", token, DateTime.Now);
            return null;
        }
    }

    public async Task<TokenMetadata> GetAsync(string token)
    {
        var json = await _redisDb.StringGetAsync(token);
        if (!json.HasValue)
            return null;

        var metadata = JsonSerializer.Deserialize<TokenMetadata>(json.ToString()).DecryptTokenMetadata(_crypto);
        if (metadata == null)
            return null;

        if (metadata.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogInformation("Token expired: {Token}", token);
            await RevokeAsync(token);
            return null;
        }

        var usageKey = $"{token}:UsageCount";
        var currentUsage = await _redisDb.StringIncrementAsync(usageKey);

        if (metadata.MaxUsage.HasValue && currentUsage > metadata.MaxUsage.Value)
        {
            _logger.LogInformation("Token exceeded max usage: {Token}", token);
            await RevokeAsync(token);
            return null;
        }

        _logger.LogInformation("Token usage incremented: {Token}, UsageCount: {UsageCount}", token, currentUsage);
        metadata.MaxUsage = (int)currentUsage;
        return metadata;
    }

    public async Task<bool> RevokeAsync(string token)
    {
        var deleteValue = await _redisDb.KeyDeleteAsync(token);
        if (deleteValue)
        {
            _logger.LogInformation("Token revoked: {Token}, at {DateTime}", token, DateTime.Now);
            return deleteValue;
        }
        else
        {
            _logger.LogInformation("Failed to revoke token: {Token}, at {DateTime}", token, DateTime.Now);
            return false;
        }

    }
}