using System;

namespace E.DataLinq.Web.Html.Abstractions;

public interface IHtmlParentElementBuilder<T>
{
    T Append(string elementName, Action<IHtmlElementBuilder> action, WriteTags writeTags = WriteTags.OpenClose);
}
