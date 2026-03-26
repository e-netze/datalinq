using E.DataLinq.Core.Services.Persistance.Abstraction;
using E.DataLinq.Web.Models.TokenCache;
using E.DataLinq.Web.Services.TokenCache;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Extensions;

internal static class StringExtensions
{
    static public NameValueCollection ToKeyValues(this string str, string defaultParameterName = "key")
    {
        var keyValues = new NameValueCollection();
        if (!str.Contains(";"))
        {
            keyValues[defaultParameterName] = str;
        }
        foreach (var keyValue in str.Split(';'))
        {
            if (keyValue.Contains("="))
            {
                var key = keyValue.Substring(0, keyValue.IndexOf("="));
                var value = keyValue.Substring(keyValue.IndexOf("=") + 1);

                keyValues[key] = value;
            }
            else
            {
                keyValues[defaultParameterName] = keyValue.Trim();
            }
        }

        return keyValues;
    }

    static public string[] KeyParameters(this string commandLine, string startingBracket = "[", string endingBracket = "]")
    {
        int pos1 = 0, pos2;
        pos1 = commandLine.IndexOf(startingBracket);
        string parameters = "";

        while (pos1 != -1)
        {
            pos2 = commandLine.IndexOf(endingBracket, pos1);
            if (pos2 == -1)
            {
                break;
            }

            if (parameters != "")
            {
                parameters += ";";
            }

            parameters += commandLine.Substring(pos1 + startingBracket.Length, pos2 - pos1 - endingBracket.Length);
            pos1 = commandLine.IndexOf(startingBracket, pos2);
        }
        if (parameters != "")
        {
            return parameters.Split(';');
        }
        else
        {
            return null;
        }
    }

    static public string ToRazorAssemblyFilename(this string id)
        => $"razor_{id}.dll";

    public static string CleanRazorString(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        // Entferne Razor-Block-Kommentare (@* ... *@)
        input = Regex.Replace(input, @"@\*.*?\*@", "", RegexOptions.Singleline);

        // Entferne C-Style-Block-Kommentare (/* ... */)
        input = Regex.Replace(input, @"/\*.*?\*/", "", RegexOptions.Singleline);

        // Entferne einzeilige Kommentare (// ...)
        input = Regex.Replace(input, @"//.*", "");

        // Entferne überflüssige Leerzeilen
        input = Regex.Replace(input, @"(^\s*\n)|(^\s*$)", "", RegexOptions.Multiline);

        // Entferne doppelte Leerzeichen
        input = Regex.Replace(input, @"\s+", " ").Trim();

        // Setze Leerzeichen korrekt für Klammern und Semikolons
        input = Regex.Replace(input, @"\s*{\s*", " {\n");
        input = Regex.Replace(input, @"\s*}\s*", "\n}\n");
        input = Regex.Replace(input, @"\s*;\s*", ";\n");
        input = Regex.Replace(input, @"\s*\(\s*", "(");
        input = Regex.Replace(input, @"\s*\)\s*", ")");

        // Entferne Leerzeichen um Punkte für Methodenzugriffe
        input = Regex.Replace(input, @"\s*\.\s*", ".");

        // Entferne Leerzeichen um @ für @using, @inject directiven
        input = Regex.Replace(input, @"\s*\@\s*", "@");

        return input;
    }

    public static string DefaultIfNullOrEmpty(this string input, string defaultString)
        => string.IsNullOrEmpty(input)
            ? defaultString
            : input;

    public static string ExtractLanguage(this string input, string language)
    {
        if (string.IsNullOrEmpty(input))
        {
            return String.Empty;
        }

        var reader = new System.IO.StringReader(input);
        var sb = new System.Text.StringBuilder();
        string line, readerLine;
        // be optimistic and set found to true
        // if there is no language tag, we assume the default language
        bool found = true;

        while ((readerLine = reader.ReadLine()) != null)
        {
            line = readerLine; //.Trim();

            var match = Regex.Match(line.Trim(), @"^([a-z]{2}):");
            if (match.Success)
            {
                found = match.Groups[1].Value == language;
                line = line.Trim().Substring(3).Trim();
            }

            if (found)
            {
                sb.Append(line);
                sb.Append(Environment.NewLine);
            }
        }

        return sb.ToString().Trim();
    }

    public static bool IsNotEmpty(this string str)
        => !string.IsNullOrEmpty(str);

    public static TokenMetadata GenerateTokenMetadata(this string token, string payload, string dataLinqRoute, TimeSpan lifeTime, int? maxUsage)
    {
        return new TokenMetadata
        {
            Token = token,
            DataLinqRoute = dataLinqRoute,
            Payload = payload,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(lifeTime),
            MaxUsage = maxUsage,
            UsageCount = 0
        };
    }

    public static async Task<(TimeSpan lifeTime, int? maxUsage)> ParseDataLinqRouteAsync(
        this string dataLinqRoute,
        IPersistanceProviderService persistanceProvider,
        DataLinqTokenStoreOptions options)
    {
        var parts = dataLinqRoute.Split('@');
        var endPointQueryView = await persistanceProvider.GetEndPointQueryView(parts[0], parts[1], parts[2]);

        var combinedTTL = TimeSpan.FromDays(endPointQueryView.CacheTokenTTL_days)
                         + TimeSpan.FromHours(endPointQueryView.CacheTokenTTL_hours)
                         + TimeSpan.FromMinutes(endPointQueryView.CacheTokenTTL_minutes);

        var lifeTime = combinedTTL == TimeSpan.Zero ? options.DefaultTTL : combinedTTL;
        var maxUsage = endPointQueryView.CacheTokenMaxUsage == 0 ? options.DefaultMaxUsage : endPointQueryView.CacheTokenMaxUsage;

        return (lifeTime, maxUsage);
    }

    public static NameValueCollection ParseCacheTokenPayload(this string payload)
    {
        var collection = new NameValueCollection();

        if (string.IsNullOrWhiteSpace(payload))
            return collection;

        var pairs = payload.Split('&');

        foreach (var pair in pairs)
        {
            var parts = pair.Split(new[] { '=' }, 2);

            if (parts.Length != 2)
                continue;

            var key = parts[0].Trim();
            var valuesPart = parts[1].Trim();

            var values = valuesPart.Split(',');

            foreach (var value in values)
            {
                var trimmedValue = value.Trim();
                if (!string.IsNullOrEmpty(trimmedValue))
                {
                    collection.Add(key, trimmedValue);
                }
            }
        }

        return collection;
    }
}