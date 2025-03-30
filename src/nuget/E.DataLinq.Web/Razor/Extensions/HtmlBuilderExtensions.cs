using E.DataLinq.Web.Html.Abstractions;
using E.DataLinq.Web.Html.Extensions;

namespace E.DataLinq.Web.Razor.Extensions;

static internal class HtmlBuilderExtensions
{
    static public T AppendRefreshViewClickButtton<T>(this IHtmlParentElementBuilder<T> htmlBuilder, string label = "Aktualisieren", object htmlAttributes = null)
        => htmlBuilder.AppendButton(button =>
                        button.AddClass("datalinq-button apply")
                              .AddAttributes(htmlAttributes)
                              .AddAttribute("onclick", "dataLinq.refresh(this)")
                              .Content(label)
                    );
}
