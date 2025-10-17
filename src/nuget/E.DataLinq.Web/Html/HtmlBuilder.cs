using E.DataLinq.Web.Html.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace E.DataLinq.Web.Html;

public class HtmlBuilder : IHtmlBuilder
{
    private List<IHtmlElementBuilder> _htmlElements = new();

    protected HtmlBuilder()
    {
    }

    static public IHtmlBuilder Create() => new HtmlBuilder();

    #region IHtmlBuilder

    public IHtmlBuilder Append(
            string elementName, 
            Action<IHtmlElementBuilder> action, 
            WriteTags writeTags = WriteTags.OpenClose)
    {
        var htmlElement = HtmlElementBuilder.Create(elementName, writeTags);

        _htmlElements.Add(htmlElement);
        action(htmlElement);

        return this;
    }

    public string BuildHtmlString()
    {
        var stream = new HtmlStream();

        foreach (var htmlElement in _htmlElements)
        {
            htmlElement.WriteTo(stream);
        }

        return stream.ToString();
    }

    #endregion

    #region Classes

    private class HtmlStream : IHtmlStream
    {
        private StringBuilder _sb = new();

        public void Write(string value)
        {
            _sb.Append(value);
        }

        public override string ToString()
        {
            return _sb.ToString();
        }
    }

    #endregion
}
