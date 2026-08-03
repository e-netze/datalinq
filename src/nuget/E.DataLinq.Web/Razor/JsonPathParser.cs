using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace E.DataLinq.Web.Razor.Json;

internal static class JsonPathParser
{
    static private readonly ConcurrentDictionary<string, JsonPathSegment[]> _cache =
        new ConcurrentDictionary<string, JsonPathSegment[]>(StringComparer.Ordinal);

    static private readonly JsonPathSegment[] _empty = new JsonPathSegment[0];

    static public JsonPathSegment[] Parse(string path)
    {
        if (String.IsNullOrWhiteSpace(path))
        {
            return _empty;
        }

        return _cache.GetOrAdd(path, ParsePath);
    }

    static private JsonPathSegment[] ParsePath(string path)
    {
        var segments = new List<JsonPathSegment>();
        var buffer = new StringBuilder();

        int pos = 0;

        // optional root prefix: $ or $.
        if (path[pos] == '$')
        {
            pos++;
        }

        while (pos < path.Length)
        {
            char c = path[pos];

            switch (c)
            {
                case '.':
                    FlushProperty(buffer, segments);

                    if (pos + 1 < path.Length && path[pos + 1] == '.')
                    {
                        segments.Add(JsonPathSegment.RecursiveDescent());
                        pos += 2;
                    }
                    else
                    {
                        pos++;
                    }
                    break;

                case '[':
                    FlushProperty(buffer, segments);
                    pos = ParseBracket(path, pos, segments);
                    break;

                case ']':
                    throw MalformedPath(path, pos, "unexpected ']'");

                default:
                    buffer.Append(c);
                    pos++;
                    break;
            }
        }

        FlushProperty(buffer, segments);

        return segments.ToArray();
    }

    static private void FlushProperty(StringBuilder buffer, List<JsonPathSegment> segments)
    {
        if (buffer.Length == 0)
        {
            return;
        }

        var name = buffer.ToString();
        buffer.Clear();

        segments.Add(name == "*"
            ? JsonPathSegment.Wildcard()
            : JsonPathSegment.Property(name));
    }

    static private int ParseBracket(string path, int pos, List<JsonPathSegment> segments)
    {
        int start = pos;
        pos++;   // skip '['

        if (pos >= path.Length)
        {
            throw MalformedPath(path, start, "unterminated '['");
        }

        char quote = path[pos];

        if (quote == '\'' || quote == '"')
        {
            pos++;

            var name = new StringBuilder();
            while (pos < path.Length && path[pos] != quote)
            {
                if (path[pos] == '\\' && pos + 1 < path.Length)
                {
                    pos++;   // escaped character
                }

                name.Append(path[pos]);
                pos++;
            }

            if (pos >= path.Length)
            {
                throw MalformedPath(path, start, "unterminated quoted key");
            }

            pos++;   // skip closing quote

            if (pos >= path.Length || path[pos] != ']')
            {
                throw MalformedPath(path, start, "missing ']' after quoted key");
            }

            segments.Add(JsonPathSegment.Property(name.ToString()));

            return pos + 1;
        }

        int close = path.IndexOf(']', pos);
        if (close < 0)
        {
            throw MalformedPath(path, start, "unterminated '['");
        }

        var content = path.Substring(pos, close - pos).Trim();

        if (content.Length == 0)
        {
            throw MalformedPath(path, start, "empty '[]'");
        }

        if (content == "*")
        {
            segments.Add(JsonPathSegment.Wildcard());
        }
        else if (content.Contains(":"))
        {
            var parts = content.Split(':');
            if (parts.Length != 2)
            {
                throw MalformedPath(path, start, "invalid slice");
            }

            segments.Add(JsonPathSegment.Slice(
                ParseOptionalInt(parts[0], path, start),
                ParseOptionalInt(parts[1], path, start)));
        }
        else if (Int32.TryParse(content, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
        {
            segments.Add(JsonPathSegment.FromIndex(index));
        }
        else
        {
            // unquoted key, e.g. [headers]
            segments.Add(JsonPathSegment.Property(content));
        }

        return close + 1;
    }

    static private int? ParseOptionalInt(string value, string path, int pos)
    {
        value = value.Trim();

        if (value.Length == 0)
        {
            return null;
        }

        if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
        {
            throw MalformedPath(path, pos, "invalid slice bound");
        }

        return result;
    }

    static private ArgumentException MalformedPath(string path, int pos, string reason)
        => new ArgumentException($"Malformed json path '{path}' at position {pos}: {reason}.", nameof(path));
}
