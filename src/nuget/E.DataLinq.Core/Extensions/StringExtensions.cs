using E.DataLinq.Core.Exceptions;
using E.DataLinq.Core.Services.Crypto.Abstraction;
using E.DataLinq.Core.Services.Persistance;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Dynamic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace E.DataLinq.Core.Extensions;

public static class StringExtensions
{
    public static bool IsValidDataLinqRouteId(this string s)
    {
        if (String.IsNullOrWhiteSpace(s))
        {
            return false;
        }

        if (s.Length < 3 || s.Length > 32)
        {
            return false;
        }

        string pattern = "^[a-z0-9-]+${3,32}";

        var regex = new Regex(pattern);
        bool ret = regex.IsMatch(s);

        if (ret)
        {
            if (s.StartsWith(".") || s.EndsWith(".") ||
                s.StartsWith("_") || s.EndsWith("_") ||
                s.StartsWith("-") || s.EndsWith("-"))
            {
                return false;
            }

            Guid guid;
            if (Guid.TryParse(s, out guid))  // UrlId is Guid!!! not allowed
            {
                return false;
            }
        }

        return ret;
    }

    static public string ToValidDataLinqRouteId(this string routeId)
    {
        routeId = routeId?.ToLower()?.Trim();

        if (String.IsNullOrEmpty(routeId) || routeId.Length < 3 || routeId.Contains("@"))
        {
            throw new ArgumentException($"Invalid route id: {routeId}");
        }

        routeId.Replace("_", "-")
               .Replace("ä", "ae")
               .Replace("ü", "ue")
               .Replace("ö", "oe")
               .Replace("ß", "ss");

        if (routeId.Length > 32)
        {
            throw new ArgumentException($"Invalid route id (to long): {routeId}");
        }

        StringBuilder sb = new StringBuilder();

        foreach (var c in routeId)
        {
            if (c >= 'a' && c <= 'z')
            {
                sb.Append(c);
            }
            else if (c >= '0' && c <= '9')
            {
                sb.Append(c);
            }
            else if (c == '-')
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('-');
            }
        }

        return sb.ToString();
    }

    static public string Username2StorageDirectory(this string username)
    {
        return username.Replace(":", "~").Replace(@"\", "$");
    }

    #region Connection String

    static public string GetPrefix(this string connectionString, char separator = ':')
    {
        if (connectionString == null || !connectionString.Contains(separator))
        {
            return String.Empty;
        }

        return connectionString.Substring(0, connectionString.IndexOf(separator));
    }

    static public string RemovePrefix(this string connectionString, char separator = ':')
    {
        if (connectionString == null || !connectionString.Contains(separator))
        {
            return connectionString;
        }

        return connectionString.Substring(connectionString.IndexOf(separator) + 1);
    }

    static public string ReplacePlaceholders(this string statement,
                                             NameValueCollection arguments,
                                             string placeholderPrefix = "{{",
                                             string placeholderPostFix = "}}")
    {
        if (arguments != null)
        {
            foreach (string key in arguments.Keys)
            {
                statement = statement.Replace($"{placeholderPrefix}{key}{placeholderPostFix}", arguments[key]);
            }
        }

        return statement;
    }

    static public ExpandoObject ToRecord(this string line, string fileExtension)
    {
        bool isCsv = ".csv".Equals(fileExtension, StringComparison.OrdinalIgnoreCase);

        var record = new ExpandoObject();

        if (isCsv)
        {
            var columns = line.Split(';');

            for (int i = 0; i < columns.Length; i++)
            {
                ((IDictionary<string, object>)record).Add($"column{i + 1}", columns[i]);
            }
        }
        else
        {
            ((IDictionary<string, object>)record).Add("line", line);
        }

        return record;
    }

    #endregion

    #region Security

    public static string EncryptStringProperty(this string str, ICryptoService cryptoService, EncryptionLevel encryptionLevel)
    {
        if (str.StartsWith("enc:"))
        {
            return str;
        }

        switch (encryptionLevel)
        {
            case EncryptionLevel.DefaultStaticEncryption:
                return $"enc:static:{cryptoService.StaticEncryptDefault(str)}";
            case EncryptionLevel.RandomSaltedPasswordEncryption:
                return $"enc:{cryptoService.EncryptTextDefault(str)}";
            default:
                return str; // None... No encryption
        }

    }

    public static string DecryptStringProperty(this string str, ICryptoService cryptoService)
    {
        if (str.StartsWith("enc:static:"))
        {
            str = cryptoService.StaticDecryptDefault(str.Substring(11));
        }
        else if (str.StartsWith("enc:"))
        {
            str = cryptoService.DecryptTextDefault(str.Substring(4));
        }

        return str;
    }

    #endregion

    #region Sql Injection

    static public void ParseBlackList(this string term)
    {
        term = term.Trim();
        foreach (char c in "<>='\"?&*".ToCharArray())
        {
            if (term.Contains(c.ToString()))
            {
                throw new InputValidationException("Invalid character (black list)");
            }
        }
    }

    static public void ParseWhiteList(this string term, string whiteList = "^[0-9\\p{L} -_.,%/#]{0,120}$")    // \p{L} .... Unicode Letters
    {
        Regex reWhiteList = new Regex(whiteList);
        if (reWhiteList.IsMatch(term))
        {
            return; // it's ok, proceed to step 2
        }
        else
        {
            throw new InputValidationException("Invalid character (white list)");// it's not ok, inform user they've entered invalid characters and try again
        }
    }

    static public string Parse(string term, string whiteList = "^[0-9\\p{L} -_.,%/#]{0,120}$", bool parse = true)    // \p{L} .... Unicode Letters
    {
        if (parse)
        {
            term.ParseBlackList();
            term.ParseWhiteList(whiteList);
        }
        return term;
    }

    static public string ParsePro(this string term, string whiteList = "^[0-9\\p{L} -_.,%/#]{0,120}$", string ignoreCharacters = "", bool parse = true)
    {
        if (parse)
        {
            string originalTerm = term;

            if ((!String.IsNullOrWhiteSpace(ignoreCharacters) && ignoreCharacters.Contains("'")) ||
                (!String.IsNullOrWhiteSpace(whiteList) && whiteList.Contains("'")))
            {
                originalTerm = originalTerm.Replace("'", "''");
            }

            if (!String.IsNullOrWhiteSpace(ignoreCharacters))
            {
                foreach (char ignoreCharacter in ignoreCharacters)
                {
                    term = term.Replace(ignoreCharacter.ToString(), "");
                }
            }

            Parse(term, whiteList, true);
            return originalTerm;
        }

        return term;
    }

    static public string ParsePro(this string term, char separator, string whiteList = "^[0-9\\p{L} -_.,%/#]{0,120}$", string ignoreCharacters = "", bool parse = true)
    {
        if (String.IsNullOrWhiteSpace(term))
        {
            return String.Empty;
        }

        var terms = term.Split(separator);
        List<string> parsedTerms = new List<string>();

        foreach (var t in terms)
        {
            parsedTerms.Add(t.ParsePro(whiteList, ignoreCharacters, parse));
        }

        return string.Join(separator.ToString(), parsedTerms);
    }

    #endregion

    static public string WildcardToRegex(this string pattern)
    {
        return "^" + Regex.Escape(pattern)
                        .Replace("\\*", ".*")
                        .Replace("\\?", ".") + "$";
    }

    static public string AddPathSeparator(this string path)
    {
        if (path?.EndsWith(Path.DirectorySeparatorChar.ToString()) == true)
        {
            return path;
        }
        return $"{path}{Path.DirectorySeparatorChar}";
    }

    static public bool IsInPath(this string path, string subPath)
    {
        return path.AddPathSeparator()
                   .StartsWith(subPath.AddPathSeparator(),
                            Platform.IsWindows
                            ? StringComparison.OrdinalIgnoreCase
                            : StringComparison.Ordinal);
    }

    static public bool HasFileExtension(this string path, string extension)
    {
        return path.EndsWith(extension,
                            Platform.IsWindows
                                ? StringComparison.OrdinalIgnoreCase
                                : StringComparison.Ordinal);
    }
}
