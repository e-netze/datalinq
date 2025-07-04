using System.Threading.Tasks;

namespace E.DataLinq.Core.Services.Abstraction;
public interface IBinaryCache
{
    bool HasData(string key, string @namespace = "");
    Task<byte[]> GetBytes(string key, string @namespace = "");
    Task SetBytes(string key, byte[] bytes, string @namespace = "");
    void Remove(string key, string @namespace = "");

    void Cleanup(string filter);
}
