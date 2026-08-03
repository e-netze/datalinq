using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace E.DataLinq.Web.Razor.Json;

internal static class JsonPathResolver
{
    static public IEnumerable<object> Select(object root, string path)
    {
        root = Unwrap(root);

        if (root is null)
        {
            return Enumerable.Empty<object>();
        }

        var segments = JsonPathParser.Parse(path);

        IEnumerable<object> current = new object[] { root };

        foreach (var segment in segments)
        {
            current = Apply(current, segment);
        }

        return current.Select(Unwrap);
    }

    static public object SelectFirst(object root, string path)
    {
        foreach (var item in Select(root, path))
        {
            return item;
        }

        return null;
    }

    /// <summary>
    /// Converts a System.Text.Json.JsonElement into a plain clr value / dictionary / list.
    /// Values coming from the json api engine can still be boxed JsonElements.
    /// Json null is converted to null, so JsonHas() and default values work as expected.
    /// </summary>
    static internal object Unwrap(object node)
    {
        if (node is not JsonElement element)
        {
            return node;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Number:
                if (element.TryGetInt64(out long longValue))
                {
                    return longValue;
                }

                if (element.TryGetDecimal(out decimal decimalValue))
                {
                    return decimalValue;
                }

                return element.GetDouble();

            case JsonValueKind.Array:
                var list = new List<object>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(Unwrap(item));
                }
                return list;

            case JsonValueKind.Object:
                var dictionary = new Dictionary<string, object>();
                foreach (var property in element.EnumerateObject())
                {
                    dictionary[property.Name] = Unwrap(property.Value);
                }
                return dictionary;

            default:
                return element.ToString();
        }
    }

    static private IEnumerable<object> Apply(IEnumerable<object> nodes, JsonPathSegment segment)
    {
        switch (segment.Type)
        {
            case JsonPathSegmentType.Property:
                return nodes.SelectMany(node => SelectProperty(node, segment.Name));

            case JsonPathSegmentType.Index:
                return nodes.SelectMany(node => SelectIndex(node, segment.Index));

            case JsonPathSegmentType.Slice:
                return nodes.SelectMany(node => SelectSlice(node, segment.SliceStart, segment.SliceEnd));

            case JsonPathSegmentType.Wildcard:
                return nodes.SelectMany(SelectChildren);

            case JsonPathSegmentType.RecursiveDescent:
                return nodes.SelectMany(node => SelectDescendantsAndSelf(node, new HashSet<object>(ReferenceEqualityComparer.Instance)));

            default:
                return Enumerable.Empty<object>();
        }
    }

    static private IEnumerable<object> SelectProperty(object node, string name)
    {
        if (AsObject(node) is IDictionary<string, object> dictionary)
        {
            if (dictionary.TryGetValue(name, out object value))
            {
                yield return value;
                yield break;
            }

            // json keys are case sensitive by spec, but reports are more forgiving
            foreach (var kvp in dictionary)
            {
                if (String.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    yield return kvp.Value;
                    yield break;
                }
            }

            yield break;
        }

        // applying a property to an array maps over its items (convenience, like items.name)
        if (AsArray(node) is IEnumerable array)
        {
            foreach (var item in array)
            {
                foreach (var result in SelectProperty(item, name))
                {
                    yield return result;
                }
            }
        }
    }

    static private IEnumerable<object> SelectIndex(object node, int index)
    {
        var items = AsList(node);

        if (items is null)
        {
            yield break;
        }

        int effectiveIndex = index < 0
            ? items.Count + index
            : index;

        if (effectiveIndex >= 0 && effectiveIndex < items.Count)
        {
            yield return items[effectiveIndex];
        }
    }

    static private IEnumerable<object> SelectSlice(object node, int? start, int? end)
    {
        var items = AsList(node);

        if (items is null)
        {
            yield break;
        }

        int from = Normalize(start ?? 0, items.Count);
        int to = Normalize(end ?? items.Count, items.Count);

        for (int i = from; i < to; i++)
        {
            yield return items[i];
        }
    }

    static private int Normalize(int value, int count)
    {
        if (value < 0)
        {
            value = count + value;
        }

        return Math.Max(0, Math.Min(count, value));
    }

    static private IEnumerable<object> SelectChildren(object node)
    {
        if (AsObject(node) is IDictionary<string, object> dictionary)
        {
            foreach (var kvp in dictionary)
            {
                yield return kvp.Value;
            }

            yield break;
        }

        if (AsArray(node) is IEnumerable array)
        {
            foreach (var item in array)
            {
                yield return item;
            }
        }
    }

    static private IEnumerable<object> SelectDescendantsAndSelf(object node, HashSet<object> visited)
    {
        if (node is null)
        {
            yield break;
        }

        if (!IsScalar(node) && !visited.Add(node))
        {
            yield break;
        }

        yield return node;

        foreach (var child in SelectChildren(node))
        {
            foreach (var descendant in SelectDescendantsAndSelf(child, visited))
            {
                yield return descendant;
            }
        }
    }

    static internal IDictionary<string, object> AsObject(object node)
        => Unwrap(node) as IDictionary<string, object>;

    static private IEnumerable AsArray(object node)
    {
        node = Unwrap(node);

        if (node is string || node is IDictionary<string, object>)
        {
            return null;
        }

        return node as IEnumerable;
    }

    static private IList<object> AsList(object node)
    {
        var array = AsArray(node);

        if (array is null)
        {
            return null;
        }

        return array as IList<object> ?? array.Cast<object>().ToList();
    }

    static private bool IsScalar(object node)
        => node is null
        || node is string
        || node.GetType().IsValueType;

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        static public readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

        private ReferenceEqualityComparer() { }

        new public bool Equals(object x, object y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj)
            => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
