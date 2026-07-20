using System.Collections.Generic;

namespace E.DataLinq.Core.Services.KeyValueStore.Abstraction;

public interface IKeyValueStoreService
{
    IEnumerable<string> GetKeys(KeyValueStoreType store);

    string GetValue(KeyValueStoreType store, string key);

    void SetValue(KeyValueStoreType store, string key, string value);

    bool DeleteKey(KeyValueStoreType store, string key);
}
