using E.DataLinq.Core.Services.KeyValueStore.Abstraction;
using System.Text.RegularExpressions;

namespace E.DataLinq.Core.Services.KeyValueStore;

public static class KeyValueStoreServiceExtensions
{
    private static readonly Regex _secretPattern = new Regex(@"\{\$([A-Za-z0-9_\-]+)\}", RegexOptions.Compiled);
    private static readonly Regex _constantPattern = new Regex(@"\{&([A-Za-z0-9_\-]+)\}", RegexOptions.Compiled);

    public static string ResolvePlaceholders(this IKeyValueStoreService keyValueStore, string value)
    {
        if (string.IsNullOrEmpty(value) || keyValueStore == null)
        {
            return value ?? string.Empty;
        }

        value = _secretPattern.Replace(value, m =>
            keyValueStore.GetValue(KeyValueStoreType.Secret, m.Groups[1].Value));

        value = _constantPattern.Replace(value, m =>
            keyValueStore.GetValue(KeyValueStoreType.Constant, m.Groups[1].Value));

        return value;
    }
}
