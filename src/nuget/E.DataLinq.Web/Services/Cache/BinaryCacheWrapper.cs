using E.DataLinq.Core.Services.Abstraction;
using E.DataLinq.Core.Services.Cache;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;

namespace E.DataLinq.Web.Services.Cache;
internal class BinaryCacheWrapper : IBinaryCache
{
    private readonly IBinaryCache _cache;

    public BinaryCacheWrapper(
            IOptions<DataLinqOptions> dataLinqOptions,
            ILogger<BinaryFileCache> logger
        )
    {
        var options = dataLinqOptions.Value;

        _cache = options switch
        {
            DataLinqOptions o when !String.IsNullOrEmpty(o.TempPath) => new BinaryFileCache(logger, o.TempPath, true),
            _ => new BinaryNullCache()
        };
    }

    public void Cleanup(string filter)
    {
        _cache.Cleanup(filter);
    }

    public byte[] GetBytes(string key, string @namespace = "") => _cache.GetBytes(key, @namespace);

    public bool HasData(string key, string @namespace = "") => _cache.HasData(key, @namespace);

    public void Remove(string key, string @namespace = "") => _cache.Remove(key, @namespace);

    public void SetBytes(string key, byte[] bytes, string @namespace = "") => _cache.SetBytes(key, bytes, @namespace);
}
