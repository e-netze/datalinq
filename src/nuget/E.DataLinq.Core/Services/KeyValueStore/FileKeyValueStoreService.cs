using E.DataLinq.Core.Services.Abstraction;
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
    private const string SecretsFilePrefix = "secrets";
    private const string ConstantsFilePrefix = "constants";

    private readonly ICryptoService _crypto;
    private readonly IDataLinqEnvironmentService _environment;
    private readonly string _storagePath;
    private readonly object _syncRoot = new object();

    public FileKeyValueStoreService(
        ICryptoService crypto,
        IOptionsMonitor<PersistanceProviderServiceOptions> optionsMonitor,
        IDataLinqEnvironmentService environment = null)
    {
        _crypto = crypto;
        _environment = environment;
        _storagePath = optionsMonitor.CurrentValue.ConnectionString;
    }

    public IEnumerable<string> GetKeys(KeyValueStoreType store, DataLinqEnvironmentType? environment = null)
    {
        lock (_syncRoot)
        {
            return Read(store, ResolveEnvironment(environment)).Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    public string GetValue(KeyValueStoreType store, string key, DataLinqEnvironmentType? environment = null)
    {
        if (String.IsNullOrEmpty(key))
        {
            return "";
        }

        lock (_syncRoot)
        {
            return Read(store, ResolveEnvironment(environment)).TryGetValue(key, out var value)
                ? value ?? ""
                : "";
        }
    }

    public void SetValue(KeyValueStoreType store, string key, string value, DataLinqEnvironmentType? environment = null)
    {
        if (String.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key must not be empty.", nameof(key));
        }

        lock (_syncRoot)
        {
            var environmentType = ResolveEnvironment(environment);

            var dict = Read(store, environmentType);
            dict[key] = value ?? "";
            Write(store, environmentType, dict);
        }
    }

    public bool DeleteKey(KeyValueStoreType store, string key, DataLinqEnvironmentType? environment = null)
    {
        if (String.IsNullOrEmpty(key))
        {
            return false;
        }

        lock (_syncRoot)
        {
            var environmentType = ResolveEnvironment(environment);

            var dict = Read(store, environmentType);

            if (!dict.Remove(key))
            {
                return false;
            }

            Write(store, environmentType, dict);
            return true;
        }
    }

    #region Helpers

    private DataLinqEnvironmentType ResolveEnvironment(DataLinqEnvironmentType? environment)
        => environment ?? _environment?.CurrentEnvironment ?? DataLinqEnvironmentType.Default;

    private string StoreFilePath(KeyValueStoreType store, DataLinqEnvironmentType environment)
        => Path.Combine(
            _storagePath,
            $"{(store == KeyValueStoreType.Secret ? SecretsFilePrefix : ConstantsFilePrefix)}.{environment.ToString().ToLowerInvariant()}.blb");

    private Dictionary<string, string> Read(KeyValueStoreType store, DataLinqEnvironmentType environment)
    {
        var fi = new FileInfo(StoreFilePath(store, environment));

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

    private void Write(KeyValueStoreType store, DataLinqEnvironmentType environment, Dictionary<string, string> dict)
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

        File.WriteAllText(StoreFilePath(store, environment), json);
    }

    #endregion
}
