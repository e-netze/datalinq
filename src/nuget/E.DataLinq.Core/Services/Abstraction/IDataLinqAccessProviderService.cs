using E.DataLinq.Core.Models;
using System.Threading.Tasks;

namespace E.DataLinq.Core.Services.Abstraction;

public interface IDataLinqAccessProviderService
{
    ValueTask<string[]> GetAccess(DataLinqEndPoint endpoint);
    ValueTask<string[]> GetAccessTokens(DataLinqEndPoint endpoint);

    ValueTask<string[]> GetAccess(DataLinqEndPoint endpoint, DataLinqEndPointQuery query);
    ValueTask<string[]> GetAccessTokens(DataLinqEndPointQuery query);
}
