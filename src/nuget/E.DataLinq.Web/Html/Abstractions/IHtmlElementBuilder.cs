namespace E.DataLinq.Web.Html.Abstractions;

public interface IHtmlElementBuilder : IHtmlParentElementBuilder<IHtmlElementBuilder>
{
    IHtmlElementBuilder AddAttribute(string key, string value);

    IHtmlElementBuilder AddAttributes(object htmlAttributes);

    IHtmlElementBuilder WithId(string id);

    IHtmlElementBuilder WithName(string name);

    IHtmlElementBuilder AddClass(string classNames);

    IHtmlElementBuilder AddStyle(string style, string value);

    IHtmlElementBuilder Content(string content);

    void WriteTo(IHtmlStream stream);
}
