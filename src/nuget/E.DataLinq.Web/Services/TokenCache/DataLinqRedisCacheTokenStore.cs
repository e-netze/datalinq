using E.DataLinq.Core.Services.Crypto.Abstraction;
using E.DataLinq.Core.Services.Persistance.Abstraction;
using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Models.TokenCache;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services.TokenCache;

internal class DataLinqRedisCacheTokenStore : IDataLinqCacheTokenStore
{
    private readonly IPersistanceProviderService _persistanceProvider;
    private readonly ILogger<DataLinqRedisCacheTokenStore> _logger;
    private readonly TokenStoreOptions _tokenOptions;
    private readonly IDatabase _redisDb;
    private readonly ICryptoService _crypto;


    public DataLinqRedisCacheTokenStore(
        IPersistanceProviderService persistanceProvider,
        ILogger<DataLinqRedisCacheTokenStore> logger,
        IOptions<TokenStoreOptions> tokenOptions,
        IConnectionMultiplexer redis,
        ICryptoService crypto
        )
    {
        _persistanceProvider = persistanceProvider;
        _logger = logger;
        _tokenOptions = tokenOptions.Value;
        _redisDb = redis.GetDatabase();
        _crypto = crypto;
    }

    public async Task<TokenMetadata> CreateAsync(string payload, string dataLinqRoute)
    {
        var token = Guid.NewGuid().ToString("N");

        var (lifeTime, maxUsage) = await dataLinqRoute.ParseDataLinqRouteAsync(_persistanceProvider, _tokenOptions);

        var tokenMetadata = token.GenerateTokenMetadata(payload,dataLinqRoute,lifeTime, maxUsage);

        var setValue = await _redisDb.StringSetAsync(token,_crypto.EncryptTextDefault(JsonSerializer.Serialize(tokenMetadata)));

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
        var json = _crypto.DecryptTextDefault(await _redisDb.StringGetAsync(token));

        var metadata = JsonSerializer.Deserialize<TokenMetadata>(json);

        if (metadata == null)
            return null;

        if (metadata.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogInformation("Token expired: {Token}", token);
            await RevokeAsync(token);
            return null;
        }

        if (metadata.MaxUsage.HasValue && metadata.UsageCount >= metadata.MaxUsage.Value)
        {
            _logger.LogInformation("Token exceeded max usage: {Token}", token);
            await RevokeAsync(token);
            return null;
        }

        metadata.UsageCount++;

        var updateValue = await _redisDb.StringSetAsync(token, _crypto.EncryptTextDefault(JsonSerializer.Serialize(metadata)));

        if (updateValue)
        {
            _logger.LogInformation("Token updated: {Token}, at {DateTime}", token, DateTime.Now);
            return metadata;
        }
        else
        {
            _logger.LogInformation("Failed to update token: {Token}, at {DateTime}", token, DateTime.Now);
            return null;
        }
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