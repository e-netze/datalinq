using System.Collections.Generic;

namespace E.DataLinq.Core.Services.KeyValueStore.Abstraction;

public interface IKeyValueStoreService
{
    IEnumerable<string> GetKeys(KeyValueStoreType store, DataLinqEnvironmentType? environment = null);

    string GetValue(KeyValueStoreType store, string key, DataLinqEnvironmentType? environment = null);

    void SetValue(KeyValueStoreType store, string key, string value, DataLinqEnvironmentType? environment = null);

    bool DeleteKey(KeyValueStoreType store, string key, DataLinqEnvironmentType? environment = null);
}
