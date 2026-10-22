using E.DataLinq.Core.Extensions;
using E.DataLinq.Core.Models.Authentication;
using E.DataLinq.Core.Services.Crypto.Abstraction;
using E.DataLinq.Core.Services.Persistance;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace E.DataLinq.Web.Mcp;

public class DataLinqMcpAgentTokenInfo
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string User { get; set; }
    public DateTime Created { get; set; }
    public DateTime Expires { get; set; }
    public DateTime? LastUsed { get; set; }
}

internal class DataLinqMcpAgentTokenEntry : DataLinqMcpAgentTokenInfo
{
    // SHA-256 of the token; the token itself is never stored
    public string TokenHash { get; set; }
    public DataLinqCodeIdentity Identity { get; set; }

    public DataLinqMcpAgentTokenInfo ToInfo() => new DataLinqMcpAgentTokenInfo()
    {
        Id = Id,
        Name = Name,
        User = User,
        Created = Created,
        Expires = Expires,
        LastUsed = LastUsed
    };
}

// Allow-list of issued agent tokens, persisted as an encrypted json file (same encryption as the secrets store).
// Revoking removes the entry, which invalidates the token immediately.
// The file is reloaded when it changes, so multiple instances sharing the storage stay in sync.
internal class DataLinqMcpAgentTokenRegistry
{
    private readonly string _path;
    private readonly ICryptoService _crypto;
    private readonly EncryptionLevel _encryptionLevel;
    private readonly object _lock = new();

    private Dictionary<string, DataLinqMcpAgentTokenEntry> _items = new();
    private DateTime _loadedFileTime = DateTime.MinValue;

    public DataLinqMcpAgentTokenRegistry(string path, ICryptoService crypto, EncryptionLevel encryptionLevel)
    {
        _path = String.IsNullOrWhiteSpace(path) ? null : path;
        _crypto = crypto;
        _encryptionLevel = encryptionLevel;
    }

    public void Add(DataLinqMcpAgentTokenEntry entry)
    {
        lock (_lock)
        {
            Reload();
            RemoveExpired();
            _items[entry.Id] = entry;
            Save();
        }
    }

    public DataLinqMcpAgentTokenEntry FindActive(string tokenHash)
    {
        if (String.IsNullOrEmpty(tokenHash))
        {
            return null;
        }

        lock (_lock)
        {
            Reload();

            var entry = _items.Values.FirstOrDefault(i => FixedTimeEquals(i.TokenHash, tokenHash));
            if (entry == null || entry.Expires <= DateTime.UtcNow || entry.Identity == null ||
                !String.Equals(entry.User, entry.Identity.Name, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // persist at most every few minutes to avoid a file write per MCP request
            var save = entry.LastUsed == null || entry.LastUsed < DateTime.UtcNow.AddMinutes(-5);
            entry.LastUsed = DateTime.UtcNow;
            if (save) Save();

            return entry;
        }
    }

    public DataLinqMcpAgentTokenInfo[] ForUser(string user)
    {
        lock (_lock)
        {
            Reload();
            RemoveExpired();
            return _items.Values
                .Where(i => String.Equals(i.User, user, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(i => i.Created)
                .Select(i => i.ToInfo())
                .ToArray();
        }
    }

    public bool Remove(string user, string id)
    {
        lock (_lock)
        {
            Reload();
            if (id == null || !_items.TryGetValue(id, out var info) ||
                !String.Equals(info.User, user, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            _items.Remove(id);
            Save();
            return true;
        }
    }

    #region Helper

    private void RemoveExpired()
    {
        var now = DateTime.UtcNow;
        var expired = _items.Values.Where(i => i.Expires <= now).Select(i => i.Id).ToArray();
        foreach (var id in expired)
        {
            _items.Remove(id);
        }
        if (expired.Length > 0) Save();
    }

    private void Reload()
    {
        if (_path == null) return;

        var fi = new FileInfo(_path);
        if (!fi.Exists)
        {
            if (_loadedFileTime != DateTime.MinValue)
            {
                _items = new Dictionary<string, DataLinqMcpAgentTokenEntry>();
                _loadedFileTime = DateTime.MinValue;
            }
            return;
        }

        if (fi.LastWriteTimeUtc == _loadedFileTime)
        {
            return;
        }

        try
        {
            var content = File.ReadAllText(fi.FullName);
            var items = String.IsNullOrWhiteSpace(content)
                ? null
                : JsonSerializer.Deserialize<DataLinqMcpAgentTokenEntry[]>(content.DecryptStringProperty(_crypto));

            _items = (items ?? Array.Empty<DataLinqMcpAgentTokenEntry>())
                .Where(i => !String.IsNullOrEmpty(i?.Id) && !String.IsNullOrEmpty(i.TokenHash))
                .ToDictionary(i => i.Id);
        }
        catch
        {
            // unreadable/undecryptable => no valid tokens (fail closed)
            _items = new Dictionary<string, DataLinqMcpAgentTokenEntry>();
        }

        _loadedFileTime = fi.LastWriteTimeUtc;
    }

    private void Save()
    {
        if (_path == null) return;

        var directory = Path.GetDirectoryName(Path.GetFullPath(_path));
        if (!String.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(_items.Values.ToArray())
                                 .EncryptStringProperty(_crypto, _encryptionLevel);

        var temp = _path + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, _path, true);

        _loadedFileTime = new FileInfo(_path).LastWriteTimeUtc;
    }

    static private bool FixedTimeEquals(string a, string b)
        => a != null && b != null && a.Length == b.Length &&
           CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));

    #endregion
}
