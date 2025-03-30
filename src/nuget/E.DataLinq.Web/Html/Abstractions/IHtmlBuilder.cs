namespace E.DataLinq.Web.Html.Abstractions;

public interface IHtmlBuilder : IHtmlParentElementBuilder<IHtmlBuilder>
{
    string BuildHtmlString();
}