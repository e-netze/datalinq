using E.DataLinq.Core.Models;
using E.DataLinq.Core.Services.Abstraction;
using System.Threading.Tasks;

namespace E.DataLinq.Core.Services;

public class DataLinqAccessProviderService : IDataLinqAccessProviderService
{
    public ValueTask<string[]> GetAccess(DataLinqEndPoint endpoint) => new ValueTask<string[]>(endpoint.Access);

    public ValueTask<string[]> GetAccess(DataLinqEndPoint endpoint, DataLinqEndPointQuery query) => new ValueTask<string[]>(query.Access);

    public ValueTask<string[]> GetAccessTokens(DataLinqEndPoint endpoint) => new ValueTask<string[]>(endpoint.AccessTokens);

    public ValueTask<string[]> GetAccessTokens(DataLinqEndPointQuery query) => new ValueTask<string[]>(query.AccessTokens);
}
