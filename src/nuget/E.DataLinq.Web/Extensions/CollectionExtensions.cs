using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace E.DataLinq.Web.Extensions;

static class CollectionExtensions
{
    static public string ToCSharpConstants(this NameValueCollection constants, string className)
    {
        if (constants == null || constants.Count == 0)
        {
            return $"var {className}=new {{}};"; // String.Empty;
        }

        StringBuilder sb = new StringBuilder();

        sb.Append($"var {className}=new {{");
        sb.Append(Environment.NewLine);
        bool first = true;
        foreach (string name in constants.Keys)
        {
            //sb.Append("public const string " + name + "=\"" + constants[name] + "\";");
            if (!first)
            {
                sb.Append(", ");
            }

            sb.Append($"{name}=\"{constants[name]}\"");
            first = false;
        }
        sb.Append("};");
        sb.Append(Environment.NewLine);

        return sb.ToString();
    }

    static public NameValueCollection ToCollection(this IEnumerable<KeyValuePair<string, StringValues>> collection)
    {
        NameValueCollection result = new NameValueCollection();

        if (collection != null)
        {
            foreach (var pairs in collection)
            {
                result[pairs.Key] = (string)pairs.Value;
            }
        }

        return result;
    }

    static public NameValueCollection Union(this NameValueCollection collection, NameValueCollection collection2, bool overrideExisting)
    {
        var result = collection.Copy();

        foreach (var key in collection2.AllKeys)
        {
            if (result.AllKeys.Contains(key) && overrideExisting == false)
            {
                continue;
            }

            result[key] = collection2[key];
        }

        return result;
    }

    static public NameValueCollection RemoveKeysStartsWith(this NameValueCollection collection, string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return collection;
        }

        var result = new NameValueCollection();

        foreach (var key in collection.AllKeys)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result[key] = collection[key];
        }

        return result;
    }

    public static NameValueCollection Copy(this NameValueCollection collection)
    {
        if (collection == null)
        {
            return new NameValueCollection();
        }

        var copy = new NameValueCollection();

        foreach (var key in collection.AllKeys)
        {
            copy[key] = collection[key];
        }

        return copy;
    }

    public static NameValueCollection Clone(this NameValueCollection collection, IEnumerable<string> excludeKeys)
    {
        if (excludeKeys == null)
        {
            return collection;
        }

        var nvc = new NameValueCollection();

        foreach (string k in collection.Keys)
        {
            if (!excludeKeys.Contains(k))
            {
                nvc[k] = collection[k];
            }
        }

        return nvc;
    }

    public static string ToFilterString(this NameValueCollection collection)
    {
        StringBuilder sb = new StringBuilder();

        //sb.Append("?");
        foreach (string k in collection.Keys)
        {
            if (sb.Length > 1)
            {
                sb.Append("&");
            }

            sb.Append(k);
            sb.Append("=");
            sb.Append(System.Web.HttpUtility.UrlDecode(collection[k]));
        }

        return sb.ToString();
    }
}
