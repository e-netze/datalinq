#nullable enable

using E.DataLinq.Web.Html.Abstractions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RazorEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace E.DataLinq.Web.Html;

internal class HtmlElementBuilder : IHtmlElementBuilder
{
    private List<IHtmlElementBuilder>? _htmlElements = null;
    private readonly Dictionary<string, string> _attributes = new();
    private List<string>? _classes;
    private Dictionary<string, string>? _styles;

    private readonly string _elementName;
    private string _content = "";

    private HtmlElementBuilder(string elementName) : base()
    {
        _elementName = elementName;
    }

    #region IHtmlElementBuilder

    static public IHtmlElementBuilder Create(string elementName) => new HtmlElementBuilder(elementName);

    public IHtmlElementBuilder AddAttribute(string key, string value)
    {
        if (key == "class")
        {
            return AddClass(value);
        }

        if (key == "style")
        {
            foreach (var style in value.Split(';'))
            {
                var parts = style.Split(':');

                if (parts.Length == 2
                    && !String.IsNullOrEmpty(parts[0])
                    && !String.IsNullOrEmpty(parts[1]))
                {
                    AddStyle(parts[0], parts[1]);
                }
            }
            return this;
        }

        if(value != null)
        {
            _attributes[key] = value;
        }

        return this;
    }

    public IHtmlElementBuilder WithId(string id)
    {
        AddAttribute("id", id);

        return this;
    }

    public IHtmlElementBuilder WithName(string name)
    {
        AddAttribute("name", name);

        return this;
    }

    public IHtmlElementBuilder AddClass(string classNames)
    {
        if (!String.IsNullOrEmpty(classNames))
        {
            _classes ??= new List<string>();

            foreach (var className in classNames.Split(' '))
            {
                if (!string.IsNullOrEmpty(className) && !_classes.Contains(className))
                {
                    _classes.Add(className);
                }
            }
        }

        return this;
    }

    public IHtmlElementBuilder AddStyle(string style, string value)
    {
        if (!String.IsNullOrEmpty(value))
        {
            _styles ??= new();

            _styles[style] = value;
        }

        return this;
    }

    public IHtmlElementBuilder AddAttributes(object htmlAttributes)
    {
        if (htmlAttributes != null)
        {
            foreach (var htmlAttribute in htmlAttributes.GetType().GetProperties())
            {
                string val = htmlAttribute.GetValue(htmlAttributes)?.ToString()!;

                AddAttribute(htmlAttribute.Name, val);
            }
        }

        return this;
    }

    public IHtmlElementBuilder Append(string elementName, Action<IHtmlElementBuilder> action)
    {
        var htmlElement = HtmlElementBuilder.Create(elementName);

        _htmlElements ??= new List<IHtmlElementBuilder>();
        _htmlElements.Add(htmlElement);
        action(htmlElement);

        return this;
    }

    public IHtmlElementBuilder Content(string content)
    {
        _htmlElements = null;
        _content = content;

        return this;
    }

    public void WriteTo(IHtmlStream stream)
    {
        if (_elementName.Equals("br"))
        {
            stream.Write($"<{_elementName}/>");
        }
        else
        {
            stream.Write($"<{_elementName}");

            if (_classes is not null)
            {
                stream.Write(" class='");
                stream.Write(string.Join(" ", _classes));
                stream.Write("'");
            }

            if (_styles is not null)
            {
                stream.Write(" style=\'");

                if (_styles.Count().Equals(1))
                {
                    stream.Write($"{_styles.First().Key}:{_styles.First().Value}");
                }
                else
                {
                    foreach (var style in _styles)
                    {
                        stream.Write($"{style.Key}:{style.Value};");
                    }
                }

                stream.Write("\'");
            }

            foreach (var attr in _attributes)
            {
                stream.Write($" {attr.Key}=\'{attr.Value.Replace("'", "\"")}\'");
            }

            stream.Write(">");

            if (_htmlElements is not null)
            {
                foreach (var htmlElement in _htmlElements)
                {
                    htmlElement.WriteTo(stream);
                }
            }
            else
            {
                stream.Write(_content);
            }

            stream.Write($"</{_elementName}>");

        }
    }

    #endregion
}
