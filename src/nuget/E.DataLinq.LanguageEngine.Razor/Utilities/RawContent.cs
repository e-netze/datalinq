namespace E.DataLinq.LanguageEngine.Razor.Utilities;

public class RawContent
{
    public RawContent(object value)
    {
        Value = value;
    }

    public object Value { get; }

    public override string ToString() => Value?.ToString() ?? "";
}