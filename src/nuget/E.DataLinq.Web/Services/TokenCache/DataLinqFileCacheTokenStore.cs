using E.DataLinq.Core.Services.Crypto.Abstraction;
using E.DataLinq.Core.Services.Persistance.Abstraction;
using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Models.TokenCache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services.TokenCache;

internal class DataLinqFileCacheTokenStore : IDataLinqCacheTokenStore
{
    private readonly TokenStoreOptions _tokenOptions;
    private readonly ILogger<DataLinqFileCacheTokenStore> _logger;
    private readonly IPersistanceProviderService _persistanceProvider;
    private readonly ICryptoService _crypto;

    public DataLinqFileCacheTokenStore(
        IOptions<TokenStoreOptions> tokenOptions,
        ILogger<DataLinqFileCacheTokenStore> logger,
        IPersistanceProviderService persistanceProvider,
        ICryptoService crypto

        )
    {
        _tokenOptions = tokenOptions.Value;
        _logger = logger;
        _crypto = crypto;
        _persistanceProvider = persistanceProvider;

        var directory = Path.GetDirectoryName(_tokenOptions.FilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }

    public async Task<TokenMetadata> CreateAsync(string payload, string dataLinqRoute)
    {
        try
        {
            var token = Guid.NewGuid().ToString("N");

            var (lifeTime, maxUsage) = await dataLinqRoute.ParseDataLinqRouteAsync(_persistanceProvider, _tokenOptions);

            var tokenMetadata = token.GenerateTokenMetadata(payload, dataLinqRoute, lifeTime, maxUsage);

            await File.WriteAllTextAsync(GetFilePath(token), _crypto.EncryptTextDefault(JsonSerializer.Serialize(tokenMetadata)));

            _logger.LogInformation("Token created: {Token}, at {DateTime}", token, DateTime.Now);

            return tokenMetadata;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to serialize token metadata");
            throw new InvalidOperationException("Failed to serialize token data", ex);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "File system error while creating token file");
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied while writing token file");
            throw;
        }
    }

    public async Task<TokenMetadata> GetAsync(string token)
    {
        var filePath = GetFilePath(token);

        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath);

        var metadata = JsonSerializer.Deserialize<TokenMetadata>(_crypto.DecryptTextDefault(json));

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

        await File.WriteAllTextAsync(filePath, _crypto.EncryptTextDefault(JsonSerializer.Serialize(metadata)));

        return metadata;
    }

    public async Task<bool> RevokeAsync(string token)
    {
        var filePath = GetFilePath(token);

        return await Task.Run(() =>
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Token revoked: {Token}", token);
                return true;
            }
            return false;
        });
    }

    public async Task<bool> RevokeIfExpired(string token)
    {
        var now = DateTime.UtcNow;

        var filePath = GetFilePath(token);

        if (!File.Exists(filePath))
            return false;

        var json = await File.ReadAllTextAsync(filePath);

        var metadata = JsonSerializer.Deserialize<TokenMetadata>(_crypto.DecryptTextDefault(json));

        if (metadata == null)
            return false;

        if (metadata.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogInformation("Token expired: {Token}", token);
            await RevokeAsync(token);
            return true;
        }

        return false;
    }

    #region Helpers
    private string GetFilePath(string token)
    {
        if(_tokenOptions.FilePath.IsNotEmpty())
            return Path.Combine(_tokenOptions.FilePath, $"{token}.token");

        throw new ArgumentException("DataLinqCachToken FilePath not specified!");
    }
    #endregion
}