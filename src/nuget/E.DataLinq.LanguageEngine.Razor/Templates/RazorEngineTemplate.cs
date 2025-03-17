using E.DataLinq.LanguageEngine.Razor.Abstractions;
using E.DataLinq.LanguageEngine.Razor.Utilities;
using System.Text;
using System.Web;

namespace E.DataLinq.LanguageEngine.Razor.Templates;

public abstract class RazorEngineTemplate : IRazorEngineTemplate
{
    private readonly StringBuilder _output = new StringBuilder();

    private string? _attributeSuffix = null;

    public dynamic Model { get; set; } = default!;

    public object Raw(object value) => new RawContent(value);

    public Action Breakpoint { get; set; } = () => { };

    public virtual void WriteLiteral(string? literal = null)
    {
        _output.Append(literal);
    }

    public virtual void Write(object? obj = null)
    {
        //if (obj is RawContent raw)
        //{
        //    stringBuilder.Append(raw.Value);
        //    return;
        //}

        _output.Append(obj switch
        {
            RawContent rawContent => rawContent.Value,
            String str => HttpUtility.HtmlEncode(str),
            _ => obj?.ToString()
        });
    }

    public virtual void BeginWriteAttribute(string name, string prefix, int prefixOffset, string suffix, int suffixOffset, int attributeValuesCount)
    {
        _attributeSuffix = suffix;
        _output.Append(prefix);
    }

    public virtual void WriteAttributeValue(string prefix, int prefixOffset, object value, int valueOffset, int valueLength, bool isLiteral)
    {
        _output.Append(prefix);
        _output.Append(value);
    }

    public virtual void EndWriteAttribute()
    {
        _output.Append(_attributeSuffix);
        _attributeSuffix = null;
    }

    public virtual Task ExecuteAsync()
    {
        return Task.CompletedTask;
    }

    public virtual Task<string> ResultAsync()
    {
        return Task.FromResult(_output.ToString());
    }
}

public abstract class RazorEngineTemplate<T> : RazorEngineTemplate
{
    public new T Model { get; set; } = default!;
}
