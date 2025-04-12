using E.DataLinq.Web.Html.Abstractions;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;

namespace E.DataLinq.Web.Html.Extensions;

static public class HtmlBuilderExtensions
{
    static public TBuilder AppendDiv<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action, WriteTags writeTags = WriteTags.OpenClose)
        => builder.Append("div", action, writeTags);

    static public TBuilder AppendTable<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action, WriteTags writeTags = WriteTags.OpenClose)
        => builder.Append("table", action, writeTags);

    static public TBuilder AppendTableRow<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action, WriteTags writeTags = WriteTags.OpenClose)
        => builder.Append("tr", action, writeTags);

    static public TBuilder AppendTableCell<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action, WriteTags writeTags = WriteTags.OpenClose)
        => builder.Append("td", action, writeTags);

    static public TBuilder AppendTableHeaderCell<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action, WriteTags writeTags = WriteTags.OpenClose)
        => builder.Append("th", action, writeTags);

    static public TBuilder AppendButton<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action, WriteTags writeTags = WriteTags.OpenClose)
        => builder.Append("button", action, writeTags);

    static public TBuilder AppendInput<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action, WriteTags writeTags = WriteTags.OpenClose)
        => builder.Append("input", action, writeTags);

    static public TBuilder AppendSelect<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action, WriteTags writeTags = WriteTags.OpenClose)
        => builder.Append("select", action, writeTags);

    static public TBuilder AppendSelectOption<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action, WriteTags writeTags = WriteTags.OpenClose)
        => builder.Append("option", action, writeTags);

    static public TBuilder AppendTextarea<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action, WriteTags writeTags = WriteTags.OpenClose)
        => builder.Append("textarea", action, writeTags);

    static public TBuilder AppendBreak<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder)
        => builder.Append("br", b => { }, WriteTags.SelfClose);
    static public TBuilder AppendBreak<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action)
        => builder.Append("br", action, WriteTags.SelfClose);

    static public TBuilder AppendHtmlElement<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action, string htmlElement, WriteTags writeTags = WriteTags.OpenClose)
        => builder.Append(htmlElement, action, writeTags);

    static public TBuilder AppendTextInput<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, string value, Action<IHtmlElementBuilder> action, WriteTags writeTags = WriteTags.OpenClose)
        => builder.Append("input", input =>
        {
            input.AddAttribute("type", "text");
            input.AddAttribute("value", value);
            action(input);
        }, writeTags);

    static public TBuilder AppendCheckbox<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, bool isChecked, Action<IHtmlElementBuilder> action, WriteTags writeTags = WriteTags.OpenClose)
        => builder.Append("input", input =>
        {
            input.AddAttribute("type", "checkbox");
            if (isChecked)
            {
                input.AddAttribute("checked", "checked");
            }
            action(input);
        }, writeTags);

    static public TBuilder AppendLabel<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action, WriteTags writeTags = WriteTags.OpenClose)
        => builder.Append("label", action, writeTags);


    static public TBuilder AppendLabelFor<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, string forId, Action<IHtmlElementBuilder> action, WriteTags writeTags = WriteTags.OpenClose)
        => builder.Append("label", label =>
        {
            label.AddAttribute("for", forId);
            action(label);
        }, writeTags);

    static public TBuilder AppendSpan<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action, WriteTags writeTags = WriteTags.OpenClose)
        => builder.Append("span", action, writeTags);

    static public TBuilder AppendForm<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, Action<IHtmlElementBuilder> action, WriteTags writeTags = WriteTags.OpenClose)
        => builder.Append("form", action, writeTags);

    static public TBuilder AppendJavaScriptBlock<TBuilder>(this IHtmlParentElementBuilder<TBuilder> builder, string scriptBody, WriteTags writeTags = WriteTags.OpenClose)
        => builder.Append("script", script =>
        {
            script
                .AddAttribute("type", "text/javascript")
                .Content(scriptBody);
        }, writeTags);

}
