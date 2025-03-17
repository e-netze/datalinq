using E.DataLinq.Core.Services.Abstraction;

namespace E.DataLinq.Core.Services.Cache;
public class BinaryNullCache : IBinaryCache
{
    public void Cleanup(string filter) { }

    public byte[] GetBytes(string key, string @namespace = "") => null;

    public bool HasData(string key, string @namespace = "") => false;

    public void Remove(string key, string @namespace = "") { }

    public void SetBytes(string key, byte[] bytes, string @namespace = "") { }
}
