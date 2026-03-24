using E.DataLinq.Web.Models.TokenCache;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services.TokenCache;

internal interface IDataLinqCacheTokenStore
{
    Task<TokenMetadata> CreateAsync(string payload, string dataLinqRoute);
    Task<TokenMetadata> GetAsync(string token);
    Task<bool> RevokeAsync(string token);
}
