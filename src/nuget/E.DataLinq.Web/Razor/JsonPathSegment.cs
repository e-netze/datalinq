using System;

namespace E.DataLinq.Web.Razor.Json;

internal enum JsonPathSegmentType
{
    Property,
    Index,
    Slice,
    Wildcard,
    RecursiveDescent
}

internal sealed class JsonPathSegment
{
    private JsonPathSegment(JsonPathSegmentType type)
    {
        this.Type = type;
    }

    public JsonPathSegmentType Type { get; private set; }

    public string Name { get; private set; }

    public int Index { get; private set; }

    public int? SliceStart { get; private set; }

    public int? SliceEnd { get; private set; }

    static public JsonPathSegment Property(string name)
        => new JsonPathSegment(JsonPathSegmentType.Property)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name))
        };

    static public JsonPathSegment FromIndex(int index)
        => new JsonPathSegment(JsonPathSegmentType.Index)
        {
            Index = index
        };

    static public JsonPathSegment Slice(int? start, int? end)
        => new JsonPathSegment(JsonPathSegmentType.Slice)
        {
            SliceStart = start,
            SliceEnd = end
        };

    static public JsonPathSegment Wildcard()
        => new JsonPathSegment(JsonPathSegmentType.Wildcard);

    static public JsonPathSegment RecursiveDescent()
        => new JsonPathSegment(JsonPathSegmentType.RecursiveDescent);

    public override string ToString()
        => this.Type switch
        {
            JsonPathSegmentType.Property => this.Name,
            JsonPathSegmentType.Index => $"[{this.Index}]",
            JsonPathSegmentType.Slice => $"[{this.SliceStart}:{this.SliceEnd}]",
            JsonPathSegmentType.Wildcard => "[*]",
            JsonPathSegmentType.RecursiveDescent => "..",
            _ => base.ToString()
        };
}
