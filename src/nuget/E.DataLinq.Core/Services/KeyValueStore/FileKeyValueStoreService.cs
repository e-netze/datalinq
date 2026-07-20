using E.DataLinq.Core.Services.Crypto.Abstraction;
using E.DataLinq.Core.Services.KeyValueStore.Abstraction;
using E.DataLinq.Core.Services.Persistance;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace E.DataLinq.Core.Services.KeyValueStore;

public class FileKeyValueStoreService : IKeyValueStoreService
{
    private const string SecretsFileName = "secrets.blb";
    private const string ConstantsFileName = "constants.blb";

    private readonly ICryptoService _crypto;
    private readonly string _storagePath;
    private readonly object _syncRoot = new object();

    public FileKeyValueStoreService(
        ICryptoService crypto,
        IOptionsMonitor<PersistanceProviderServiceOptions> optionsMonitor)
    {
        _crypto = crypto;
        _storagePath = optionsMonitor.CurrentValue.ConnectionString;
    }

    public IEnumerable<string> GetKeys(KeyValueStoreType store)
    {
        lock (_syncRoot)
        {
            return Read(store).Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    public string GetValue(KeyValueStoreType store, string key)
    {
        if (String.IsNullOrEmpty(key))
        {
            return "";
        }

        lock (_syncRoot)
        {
            return Read(store).TryGetValue(key, out var value)
                ? value ?? ""
                : "";
        }
    }

    public void SetValue(KeyValueStoreType store, string key, string value)
    {
        if (String.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key must not be empty.", nameof(key));
        }

        lock (_syncRoot)
        {
            var dict = Read(store);
            dict[key] = value ?? "";
            Write(store, dict);
        }
    }

    public bool DeleteKey(KeyValueStoreType store, string key)
    {
        if (String.IsNullOrEmpty(key))
        {
            return false;
        }

        lock (_syncRoot)
        {
            var dict = Read(store);

            if (!dict.Remove(key))
            {
                return false;
            }

            Write(store, dict);
            return true;
        }
    }

    #region Helpers

    private string StoreFilePath(KeyValueStoreType store)
        => Path.Combine(_storagePath, store == KeyValueStoreType.Secret ? SecretsFileName : ConstantsFileName);

    private Dictionary<string, string> Read(KeyValueStoreType store)
    {
        var fi = new FileInfo(StoreFilePath(store));

        if (!fi.Exists)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var fileContent = File.ReadAllText(fi.FullName);

        if (String.IsNullOrWhiteSpace(fileContent))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            if (store == KeyValueStoreType.Secret)
            {
                fileContent = _crypto.DecryptTextDefault(fileContent);
            }

            var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(fileContent);

            return dict is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Write(KeyValueStoreType store, Dictionary<string, string> dict)
    {
        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }

        var json = JsonConvert.SerializeObject(dict);

        if (store == KeyValueStoreType.Secret)
        {
            json = _crypto.EncryptTextDefault(json);
        }

        File.WriteAllText(StoreFilePath(store), json);
    }

    #endregion
}
