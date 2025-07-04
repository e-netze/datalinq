using E.DataLinq.Core.Services.Abstraction;
using System.Threading.Tasks;

namespace E.DataLinq.Core.Services.Cache;
public class BinaryNullCache : IBinaryCache
{
    public void Cleanup(string filter) { }

    public Task<byte[]> GetBytes(string key, string @namespace = "") 
        => Task.FromResult<byte[]>(null);

    public bool HasData(string key, string @namespace = "") => false;

    public void Remove(string key, string @namespace = "") { }

    public Task SetBytes(string key, byte[] bytes, string @namespace = "") 
        => Task.CompletedTask;
}
