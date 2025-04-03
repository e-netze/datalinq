using E.DataLinq.Web.Html.Abstractions;
using System;

namespace E.DataLinq.Web.Html.Extensions;

static public class HtmlBuilderExtensions
{
    static public TBuilder AppendDiv<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action)
        => builder.Append("div", action);

    static public TBuilder AppendTable<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action)
        => builder.Append("table", action);

    static public TBuilder AppendTableRow<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action)
        => builder.Append("tr", action);

    static public TBuilder AppendTableCell<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action)
        => builder.Append("td", action);

    static public TBuilder AppendTableHeaderCell<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action)
        => builder.Append("th", action);

    static public TBuilder AppendButton<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action)
        => builder.Append("button", action);

    static public TBuilder AppendInput<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action)
        => builder.Append("input", action);

    static public TBuilder AppendTextInput<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, string value, Action<IHtmlElementBuilder> action)
        => builder.Append("input", input =>
        {
            input.AddAttribute("type", "text");
            input.AddAttribute("value", value);
            action(input);
        });

    static public TBuilder AppendCheckbox<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, bool isChecked, Action<IHtmlElementBuilder> action)
        => builder.Append("input", input =>
        {
            input.AddAttribute("type", "checkbox");
            if (isChecked)
            {
                input.AddAttribute("checked", "checked");
            }
            action(input);
        });

    static public TBuilder AppendLabel<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action)
        => builder.Append("label", action);


    static public TBuilder AppendLabelFor<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, string forId, Action<IHtmlElementBuilder> action)
        => builder.Append("label", label =>
        {
            label.AddAttribute("for", forId);
            action(label);
        });

    static public TBuilder AppendSpan<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action)
        => builder.Append("span", action);

    static public TBuilder AppendForm<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action)
        => builder.Append("form", action);

    static public TBuilder AppendJavaScriptBlick<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, string scriptBody)
        => builder.Append("script", script =>
        {
            script
                .AddAttribute("type", "text/jacascript")
                .Content(scriptBody);
        });

}
