using E.DataLinq.Core.Models.Authentication;
using E.DataLinq.Core.Services.Crypto.Abstraction;
using E.DataLinq.Core.Services.Persistance;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace E.DataLinq.Web.Mcp;

public class DataLinqMcpGrant
{
    public DataLinqCodeIdentity Identity { get; set; }
    public DateTime Expires { get; set; }

    // preview tokens only
    public string Route { get; set; }
}

public class DataLinqMcpTokenStore
{
    private const string AgentTokenPrefix = "dla_";
    private const string AgentTokenFileName = "mcp-agent-tokens.blb";

    private readonly DataLinqMcpOptions _options;
    private readonly DataLinqMcpAgentTokenRegistry _registry;

    // Agent tokens are random values. Only their SHA-256 hash (with name, user, identity, expiration)
    // is persisted in an encrypted file (same encryption as the secrets store). Revoke removes the entry.
    // Preview tokens are short-lived and kept in memory.
    private readonly ConcurrentDictionary<string, DataLinqMcpGrant> _previewTokens = new();

    public DataLinqMcpTokenStore(IOptions<DataLinqMcpOptions> options,
                                 ICryptoService crypto,
                                 IOptionsMonitor<PersistanceProviderServiceOptions> persistanceOptions)
    {
        _options = options.Value;

        var storagePath = persistanceOptions.CurrentValue.ConnectionString;
        _registry = new DataLinqMcpAgentTokenRegistry(
            !String.IsNullOrWhiteSpace(_options.AgentTokenRegistryPath)
                ? _options.AgentTokenRegistryPath
                : !String.IsNullOrWhiteSpace(storagePath)
                    ? System.IO.Path.Combine(storagePath, AgentTokenFileName)
                    : null,
            crypto,
            persistanceOptions.CurrentValue.SecureStringEncryptionLevel);
    }

    #region Agent Tokens

    public int MaxAgentTokenLifetimeHours => Math.Max(1, _options.MaxAgentTokenLifetimeHours);

    public (string token, DataLinqMcpAgentTokenInfo info) IssueAgentToken(DataLinqCodeIdentity identity, int lifetimeHours, string name = null)
    {
        if (identity == null || String.IsNullOrEmpty(identity.Name))
        {
            throw new ArgumentException("identity required");
        }

        var hours = Math.Clamp(lifetimeHours, 1, MaxAgentTokenLifetimeHours);
        var token = AgentTokenPrefix + Base64Url(RandomNumberGenerator.GetBytes(32));
        var entry = new DataLinqMcpAgentTokenEntry()
        {
            Id = Base64Url(RandomNumberGenerator.GetBytes(12)),
            Name = String.IsNullOrWhiteSpace(name) ? "Agent token" : name.Trim().Substring(0, Math.Min(name.Trim().Length, 80)),
            User = identity.Name,
            Created = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddHours(hours),
            TokenHash = Hash(token),
            Identity = identity
        };

        _registry.Add(entry);

        return (token, entry.ToInfo());
    }

    public DataLinqMcpAgentTokenInfo[] ListAgentTokens(string user)
        => _registry.ForUser(user);

    public bool RevokeAgentToken(string user, string id)
        => _registry.Remove(user, id);

    public DataLinqMcpGrant ValidateAgentToken(string token)
    {
        if (String.IsNullOrEmpty(token) || !token.StartsWith(AgentTokenPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var entry = _registry.FindActive(Hash(token));
        if (entry == null)
        {
            return null;  // unknown, expired or revoked
        }

        return new DataLinqMcpGrant() { Identity = entry.Identity, Expires = entry.Expires };
    }

    #endregion

    #region Preview Tokens

    public string IssuePreviewToken(DataLinqCodeIdentity identity, string route, int lifetimeSeconds)
    {
        Cleanup();

        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        _previewTokens[Hash(token)] = new DataLinqMcpGrant()
        {
            Identity = identity,
            Route = route,
            Expires = DateTime.UtcNow.AddSeconds(lifetimeSeconds)
        };
        return token;
    }

    public DataLinqMcpGrant ValidatePreviewToken(string token, string route)
    {
        var grant = GetPreviewGrant(token);

        return grant != null && String.Equals(grant.Route, route, StringComparison.OrdinalIgnoreCase) ? grant : null;
    }

    // Consumes a single-use preview link token and returns a new session token for the cookie.
    public (DataLinqMcpGrant grant, string sessionToken) RedeemPreviewLink(string token, string route, int sessionLifetimeSeconds)
    {
        var grant = ValidatePreviewToken(token, route);
        if (grant == null || !_previewTokens.TryRemove(Hash(token), out _))
        {
            return (null, null);
        }

        return (grant, IssuePreviewToken(grant.Identity, grant.Route, sessionLifetimeSeconds));
    }

    public DataLinqMcpGrant GetPreviewGrant(string token)
    {
        if (String.IsNullOrEmpty(token) || !_previewTokens.TryGetValue(Hash(token), out var grant))
        {
            return null;
        }

        if (grant.Expires < DateTime.UtcNow)
        {
            _previewTokens.TryRemove(Hash(token), out _);
            return null;
        }

        return grant;
    }

    #endregion

    #region Helper

    private void Cleanup()
    {
        var now = DateTime.UtcNow;
        foreach (var item in _previewTokens.Where(i => i.Value.Expires < now).ToArray())
        {
            _previewTokens.TryRemove(item.Key, out _);
        }
    }

    static private string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    static private string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    #endregion
}
