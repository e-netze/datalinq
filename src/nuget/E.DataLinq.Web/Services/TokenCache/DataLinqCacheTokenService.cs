using E.DataLinq.Web.Models.TokenCache;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services.TokenCache;

internal class DataLinqCacheTokenService : IDataLinqCacheTokenService
{
    private readonly IDataLinqCacheTokenStore _tokenStore;
    private readonly ILogger<DataLinqCacheTokenService> _logger;

    public DataLinqCacheTokenService(IDataLinqCacheTokenStore tokenStore, ILogger<DataLinqCacheTokenService> logger)
    {
        _tokenStore = tokenStore;
        _logger = logger;
    }

    public async Task<TokenCreateResponse> CreateTokenAsync(TokenCreateRequest request)
    {
        try
        {
            var tokenData = await _tokenStore.CreateAsync(request.Payload, request.DataLinqRoute);

            return new TokenCreateResponse 
            { 
                Token = tokenData.Token, 
                ValidFrom = tokenData.CreatedAt, 
                ValidTo = tokenData.ExpiresAt, 
                MaxUsage = tokenData.MaxUsage
            };
        }catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument in token creation");
            throw new InvalidOperationException("Invalid token creation parameters", ex);
        }catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating token");
            throw;
        }
    }

    public async Task<TokenResolveResponse> ResolveTokenAsync(string token)
    {
        var tokenMetadata = await _tokenStore.GetAsync(token);

        if (tokenMetadata == null)
        {
            _logger.LogWarning("Token not found or expired: {Token}", token);
            return new TokenResolveResponse
            {
                Success = false,
                ErrorMessage = "Token not found or expired"
            };
        }

        return new TokenResolveResponse
        {
            Success = true,
            Payload = tokenMetadata.Payload,
            DataLinqRoute = tokenMetadata.DataLinqRoute
        };
    }

    public Task<bool> RevokeTokenAsync(string token)
    {
        throw new NotImplementedException();
    }
}
