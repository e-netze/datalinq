using E.DataLinq.Web.Models.TokenCache;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services.TokenCache;

public interface IDataLinqCacheTokenService
{
    Task<TokenCreateResponse> CreateTokenAsync(TokenCreateRequest request);
    Task<TokenResolveResponse> ResolveTokenAsync(string token);
    Task<bool> RevokeTokenAsync(string token);

    string UrlParamterName { get; }
}
